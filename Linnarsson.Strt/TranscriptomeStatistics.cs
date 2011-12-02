using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
	public class TranscriptomeStatistics
	{
        private static readonly int sampleDistForAccuStats = 5000000;

        public static readonly int maxHotspotCount = 50;
        public static readonly int minHotspotDistance = 5;

        StreamWriter nonAnnotWriter;
        StreamWriter nonExonWriter;

        public bool GenerateWiggle { get; set; }
        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private RandomTagFilterByBc randomTagFilter;
        private SnpRndTagVerifier snpRndTagVerifier;
        public SyntReadReporter TestReporter { get; set; }

        Dictionary<string, int[]> TotalHitsByAnnotTypeAndChr; // Separates sense and antisense
        int[,] TotalHitsByAnnotTypeAndBarcode; // Separates sense and antisense
        int[] TotalHitsByAnnotType;            // Separates sense and antisense
        /// <summary>
        /// Number of hits to distinct annotations in each barcode
        /// </summary>
        int[] TotalHitsByBarcode;

        AbstractGenomeAnnotations Annotations;
        Barcodes barcodes;
		DnaMotif[] motifs;
        int currentBcIdx = 0;
        string currentMapFilePath;
        public string OutputPathbase;

        /// <summary>
        /// Total number of mapped reads in each barcode
        /// </summary>
        int[] numReadsByBarcode;
        /// <summary>
        /// Total number of mapped reads
        /// </summary>
        int nMappedReads { get { return numReadsByBarcode.Sum(); } }
        /// <summary>
        /// Total number of unique molecules in each barcode
        /// </summary>
        int[] numMoleculesByBarcode;
        /// <summary>
        /// Total number of unique molecules
        /// </summary>
        int numMolecules { get { return numMoleculesByBarcode.Sum(); } }
        /// <summary>
        /// Total number of duplicated reads when using random Tags
        /// </summary>
        int numDuplicateReads { get { return nMappedReads - numMolecules; } }
        /// <summary>
        /// Number of molecules (reads when rndTags missing) that map to some annotation
        /// </summary>
        int numAnnotatedMols = 0;
        /// <summary>
        /// Number of molecules (reads when rndTags missing) that map to (one or more) exons
        /// </summary>
        int numExonAnnotatedMols = 0;
        /// <summary>
        /// Number of molecules (reads when rndTags missing) that hit the limit of alternative genomic mapping positions in bowtie
        /// </summary>
        int nMaxAltMappingsReads = 0;

        private int statsSampleDistPerBarcode;

        Dictionary<int, List<int>> sampledDetectedTranscriptsByBcIdx = new Dictionary<int, List<int>>();
        // For non-rndTagged samples the following to will be identical:
        Dictionary<int, List<int>> sampledUniqueMoleculesByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledUniqueHitPositionsByBcIdx = new Dictionary<int, List<int>>();

        private StreamWriter rndTagProfileByGeneWriter;

        Dictionary<string, int> redundantHits = new Dictionary<string, int>();
        List<FtInterval> exonsToMark;
        Dictionary<string, Pair<MultiReadMapping, FtInterval>> geneToExonToMark = new Dictionary<string,Pair<MultiReadMapping,FtInterval>>();
        List<string> exonHitGeneNames;
        private string annotationChrId;

        public TranscriptomeStatistics(AbstractGenomeAnnotations annotations, Props props)
		{
            AnnotType.DirectionalReads = props.DirectionalReads;
            barcodes = props.Barcodes;
            Annotations = annotations;
            DetermineMotifs = props.DetermineMotifs;
            AnalyzeAllGeneVariants = !Annotations.noGeneVariants;
			motifs = new DnaMotif[barcodes.Count];
			for(int i = 0; i < motifs.Length; i++)
			{
				motifs[i] = new DnaMotif(40);
			}
            TotalHitsByBarcode = new int[barcodes.Count];
            TotalHitsByAnnotTypeAndBarcode = new int[AnnotType.Count, barcodes.Count];
            TotalHitsByAnnotTypeAndChr = new Dictionary<string, int[]>();
            foreach (string chr in Annotations.GetChromosomeNames())
                TotalHitsByAnnotTypeAndChr[chr] = new int[AnnotType.Count];
            TotalHitsByAnnotType = new int[AnnotType.Count];
            numReadsByBarcode = new int[barcodes.Count];
            numMoleculesByBarcode = new int[barcodes.Count];
            exonsToMark = new List<FtInterval>(100);
            exonHitGeneNames = new List<string>(100);
            annotationChrId = Annotations.Genome.Annotation;
            string tagMappingFile = PathHandler.GetTagMappingPath(Annotations.Genome);
            randomTagFilter = new RandomTagFilterByBc(barcodes, Annotations.GetChromosomeNames(), tagMappingFile);
            statsSampleDistPerBarcode = sampleDistForAccuStats / barcodes.Count;
        }

        /// <summary>
        /// Order by the barcodeIndex. Filenames expected to start with barcodeIdx followed by "_"
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <returns>1, 0, or -1</returns>
        private static int CompareMapFiles(string path1, string path2)
        {
            string name1 = Path.GetFileName(path1);
            string name2 = Path.GetFileName(path2);
            int bc1 = int.Parse(name1.Substring(0, name1.IndexOf('_')));
            int bc2 = int.Parse(name2.Substring(0, name2.IndexOf('_')));
            return bc1.CompareTo(bc2);
        }
        /// <summary>
        /// Annotate the complete set of map files from a study.
        /// </summary>
        /// <param name="mapFilePaths"></param>
        public void ProcessMapFiles(List<string> mapFilePaths)
        {
            if (mapFilePaths.Count == 0)
                return;
            mapFilePaths.Sort(CompareMapFiles); // Important to have them sorted by barcode
            if (Props.props.AnalyzeSNPs)
            {
                MapFileSnpFinder mfsf = new MapFileSnpFinder(barcodes);
                mfsf.ProcessMapFiles(mapFilePaths);
                int nSNPs = randomTagFilter.SetupSNPCounters(mfsf.GetAverageReadLength(), mfsf.IterSNPLocations());
                Console.WriteLine("Registered " + nSNPs + " potential SNP positions.");
                if (Props.props.SnpRndTagVerification)
                    snpRndTagVerifier = new SnpRndTagVerifier(Props.props, mfsf);
            }
            List<string> bcMapFilePaths = new List<string>();
            string mapFileName = Path.GetFileName(mapFilePaths[0]);
            currentBcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
            Console.Write("Annotatating " + mapFilePaths.Count + " map files");

            if (!Directory.Exists(Path.GetDirectoryName(OutputPathbase)))
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPathbase));
            nonAnnotWriter = new StreamWriter(OutputPathbase + "_NONANNOTATED.tab");
            nonExonWriter = new StreamWriter(OutputPathbase + "_NONEXON.tab");

            foreach (string mapFilePath in mapFilePaths)
            {
                Console.Write(".");
                mapFileName = Path.GetFileName(mapFilePath);
                int bcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
                if (bcIdx != currentBcIdx)
                {
                    ProcessBarcodeMapFiles(bcMapFilePaths);
                    bcMapFilePaths.Clear();
                    currentBcIdx = bcIdx;
                }
                bcMapFilePaths.Add(mapFilePath);
            }
            if (bcMapFilePaths.Count > 0)
                ProcessBarcodeMapFiles(bcMapFilePaths);
            Console.WriteLine();

            nonAnnotWriter.Close();
            nonExonWriter.Close();
        }

        private void ProcessBarcodeMapFiles(List<string> bcMapFilePaths)
        {
            long totalReadLength = 0;
            foreach (string mapFilePath in bcMapFilePaths)
            {
                currentMapFilePath = mapFilePath;
                MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, 1, barcodes);
                if (mapFileReader == null)
                    throw new Exception("Unknown read map file type : " + mapFilePath);
                foreach (MultiReadMappings mrm in mapFileReader.SingleMappings(mapFilePath))
                {
                    randomTagFilter.Add(mrm);
                    if (snpRndTagVerifier != null)
                        snpRndTagVerifier.Add(mrm);
                    totalReadLength += mrm.SeqLen;
                    if ((++numReadsByBarcode[currentBcIdx]) % statsSampleDistPerBarcode == 0)
                        SampleReadStatistics(statsSampleDistPerBarcode);
                    if (mrm.HasAltMappings) nMaxAltMappingsReads++;
                }
            }
            SampleReadStatistics(numReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode);
            MappedTagItem.AverageReadLen = (int)Math.Round((double)totalReadLength / numReadsByBarcode[currentBcIdx]);
            foreach (MappedTagItem item in randomTagFilter.IterItems())
                Annotate(item);
            FinishBarcode();
        }

        public void FinishBarcode()
        {
            MakeGeneRndTagProfiles();
            MakeBcWigglePlots();
        }

        public void SampleReadStatistics(int numReadsInBin)
        {
            if (!sampledUniqueHitPositionsByBcIdx.ContainsKey(currentBcIdx))
            {
                sampledUniqueMoleculesByBcIdx[currentBcIdx] = new List<int>();
                sampledUniqueHitPositionsByBcIdx[currentBcIdx] = new List<int>();
            }
            sampledUniqueMoleculesByBcIdx[currentBcIdx].Add(randomTagFilter.nUniqueByBarcode[currentBcIdx]);
            sampledUniqueHitPositionsByBcIdx[currentBcIdx].Add(randomTagFilter.GetNumDistinctMappings());
        }

        public void Annotate(MappedTagItem item)
        {
            numMoleculesByBarcode[currentBcIdx] += item.MolCount;
            exonsToMark.Clear();
            exonHitGeneNames.Clear();
            bool someAnnotationHit = false;
            bool someExonHit = false;
            // Exonic pre-mapped multireads (i.e. the same TagItem will pass here several times, with different locations) should only be annotated at the exons:
            MarkStatus markType = (item.tagItem.hasAltMappings) ? MarkStatus.TEST_EXON_SKIP_OTHER : MarkStatus.TEST_EXON_MARK_OTHER;
            foreach (FtInterval ivl in Annotations.GetMatching(item.chr, item.HitMidPos))
            {
                item.splcToRealChrOffset = 0;
                MarkResult res = ivl.Mark(item, ivl.ExtraData, markType);
                //if (res.annotType == AnnotType.NOHIT)
                //    continue; // Happens if trying to Mark a repeat when some other feature is already annotated
                someAnnotationHit = true;
                if (AnnotType.IsTranscript(res.annotType))
                {
                    if (!exonHitGeneNames.Contains(res.feature.Name))
                    {
                        someExonHit = true;
                        exonHitGeneNames.Add(res.feature.Name);
                        exonsToMark.Add(ivl);
                    }
                }
                else // hit is not to EXON or SPLC (neither AEXON/ASPLC for non-directional samples)
                {
                    if (markType == MarkStatus.TEST_EXON_MARK_OTHER)
                    {
                        TotalHitsByAnnotTypeAndBarcode[res.annotType, currentBcIdx] += item.MolCount;
                        TotalHitsByAnnotTypeAndChr[item.chr][res.annotType] += item.MolCount;
                        TotalHitsByAnnotType[res.annotType] += item.MolCount;
                        TotalHitsByBarcode[currentBcIdx] += item.MolCount;
                        markType = MarkStatus.TEST_EXON_SKIP_OTHER;
                    }
                }
            }
            if (item.chr != annotationChrId && !item.tagItem.hasAltMappings)
            {
                // Add to the motif (base 21 in the motif will be the first base of the read)
                // Subtract one to make it zero-based
                if (DetermineMotifs && someAnnotationHit && Annotations.HasChromosome(item.chr))
                    motifs[currentBcIdx].Add(Annotations.GetChromosome(item.chr), item.hitStartPos - 20 - 1, item.strand);
            }
            // Now when the best alignments have been selected, mark these transcript hits
            MarkStatus markStatus = (exonsToMark.Count > 1 || item.tagItem.hasAltMappings) ? MarkStatus.NONUNIQUE_EXON_MAPPING : MarkStatus.UNIQUE_EXON_MAPPING;
            foreach (FtInterval ivl in exonsToMark)
            {
                item.splcToRealChrOffset = 0;
                MarkResult res = ivl.Mark(item, ivl.ExtraData, markStatus);
                TotalHitsByAnnotTypeAndBarcode[res.annotType, currentBcIdx] += item.MolCount;
                TotalHitsByAnnotTypeAndChr[item.chr][res.annotType] += item.MolCount;
                TotalHitsByAnnotType[res.annotType] += item.MolCount;
                TotalHitsByBarcode[currentBcIdx] += item.MolCount;
            }
            if (someAnnotationHit)
            {
                numAnnotatedMols += item.MolCount;
                if (someExonHit) numExonAnnotatedMols += item.MolCount;
                else
                    nonExonWriter.WriteLine(item.ToString());
            }
            else
                nonAnnotWriter.WriteLine(item.ToString());
            if (exonHitGeneNames.Count > 1)
            {
                exonHitGeneNames.Sort();
                string combNames = string.Join("#", exonHitGeneNames.ToArray());
                if (!redundantHits.ContainsKey(combNames))
                    redundantHits[combNames] = item.MolCount;
                else
                    redundantHits[combNames]+= item.MolCount;
            }
            //if (TestReporter != null)
            //    TestReporter.ReportHit(exonHitGeneNames, mappings, exonsToMark);
        }

        private void MakeGeneRndTagProfiles()
        {
            if (Props.props.GenesToShowRndTagProfile != null && barcodes.HasRandomBarcodes)
            {
                foreach (string geneName in Props.props.GenesToShowRndTagProfile)
                {
                    GeneFeature gf;
                    if (Annotations.geneFeatures.ContainsKey(geneName))
                        gf = Annotations.geneFeatures[geneName];
                    else if (Annotations.geneFeatures.ContainsKey(geneName.ToUpper()))
                        gf = Annotations.geneFeatures[geneName.ToUpper()];
                    else
                    {
                        Console.WriteLine("Can not locate a gene named " + geneName + " for rndTag profile writing.");
                        continue;
                    }
                    for (int trPosInChrDir = 0; trPosInChrDir < gf.Length; trPosInChrDir++)
                    {
                        int chrPos = gf.GetChrPosFromTrPosInChrDir(trPosInChrDir);
                        int estMolCount;
                        ushort[] profile;
                        randomTagFilter.GetReadCountProfile(gf.Chr, chrPos, gf.Strand, out estMolCount, out profile);
                        if (profile != null)
                        {
                            if (rndTagProfileByGeneWriter == null)
                            {
                                string file = OutputPathbase + "_rnd_tag_profiles.tab";
                                if (!Directory.Exists(Path.GetDirectoryName(file)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                                rndTagProfileByGeneWriter = file.OpenWrite();
                                rndTagProfileByGeneWriter.WriteLine("Gene\tBarcode\tTrPos\tEstMolCount\tReadCountsByRndTagIdx");
                            }
                            int trPos = (gf.Strand == '+') ? 1 + trPosInChrDir : 1 + gf.Length - trPosInChrDir;
                            rndTagProfileByGeneWriter.Write("{0}\t{1}\t{2}\t{3}", gf.Name, barcodes.Seqs[currentBcIdx], trPos, estMolCount);
                            foreach (int count in profile)
                                rndTagProfileByGeneWriter.Write("\t" + count);
                            rndTagProfileByGeneWriter.WriteLine();
                        }
                    }
                }
            }
        }

        private void MakeBcWigglePlots()
        {
            if (!Props.props.GenerateBarcodedWiggle) return;
            int readLength = MappedTagItem.AverageReadLen;
            WriteBcWiggleStrand(readLength, '+');
            WriteBcWiggleStrand(readLength, '-');
        }

        private void WriteBcWiggleStrand(int readLength, char strand)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            string fileNameHead = string.Format("{0}_{1}_{2}", currentBcIdx, Annotations.Genome.Build, ((strand == '+') ? "_fw" : "_rev"));
            string filePathHead = Path.Combine(Path.GetDirectoryName(currentMapFilePath), fileNameHead);
            StreamWriter writerByMol = null, writerByRead = null;
            if (barcodes.HasRandomBarcodes)
            {
                string fileByMol = filePathHead + "_bymolecule.wig.gz";
                if (File.Exists(fileByMol)) return;
                writerByMol = fileByMol.OpenWrite();
                writerByMol.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} (+)\" visibility=full", fileNameHead + "_bymolecule", strand);
            }
            string fileByRead = filePathHead + "_byread.wig.gz";
            if (File.Exists(fileByRead)) return;
            writerByRead = fileByRead.OpenWrite();
            writerByRead.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} (+)\" visibility=full", fileNameHead + "_byread", strand);
            foreach (KeyValuePair<string, ChrTagData> tagDataPair in randomTagFilter.chrTagDatas)
            {
                string chr = tagDataPair.Key;
                if (!StrtGenome.IsSyntheticChr(chr))
                {
                    int chrLen = Annotations.ChromosomeLengths[chr];
                    int[] positions, molsAtEachPos, readsAtEachPos;
                    tagDataPair.Value.GetDistinctPositionsAndCounts(strand, out positions, out molsAtEachPos, out readsAtEachPos);
                    Wiggle.WriteToWigFile(writerByRead, chr, readLength, strandSign, chrLen, positions, readsAtEachPos);
                    if (writerByMol != null)
                        Wiggle.WriteToWigFile(writerByMol, chr, readLength, strandSign, chrLen, positions, molsAtEachPos);
                }
            }
            if (writerByMol != null)
                writerByMol.Close();
            writerByRead.Close();
        }

        public void SaveResult(ReadCounter readCounter)
        {
            SaveResult(readCounter, OutputPathbase);
        }
		/// <summary>
		///  Save all the statistics to a set of files
		/// </summary>
        /// <param name="extractionSummaryPath">Full path to Lxxx_extraction_summary.txt file</param>
		/// <param name="fileNameBase">A path and a filename prefix that will used to create all output files, e.g. "/data/Sample12_"</param>
		public void SaveResult(ReadCounter readCounter, string fileNameBase)
		{
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            WriteRedundantExonHits(fileNameBase);
            WriteASExonDistributionHistogram(fileNameBase);
            WriteSummary(fileNameBase, Annotations.GetSummaryLines(), readCounter);
            int averageReadLen = MappedTagItem.AverageReadLen;
            Annotations.SaveResult(fileNameBase, averageReadLen);
            if (snpRndTagVerifier != null)
                snpRndTagVerifier.Verify(fileNameBase);
            if (Props.props.AnalyzeSNPs)
                WriteSnps(fileNameBase);
            if (DetermineMotifs)
                WriteSequenceLogos(fileNameBase);
            WriteWriggle(fileNameBase);
            WriteHotspots(fileNameBase);
            if (rndTagProfileByGeneWriter != null)
                rndTagProfileByGeneWriter.Close();
        }

        /// <summary>
        /// Write sequence logo data for each barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteSequenceLogos(string fileNameBase)
        {
            string logoDir = Directory.CreateDirectory(fileNameBase + "_sequence_logos").FullName;
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i].Save(Path.Combine(logoDir, barcodes.Seqs[i] + "_motif.txt"));
            }
        }

        private void WriteSummary(string fileNameBase, List<string> summaryLines, ReadCounter readCounter)
        {
            string xmlPath = fileNameBase + "_summary.xml";
            var xmlFile = xmlPath.OpenWrite();
            xmlFile.WriteLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            xmlFile.WriteLine("<strtSummary project=\"{0}\">", Path.GetDirectoryName(fileNameBase));
            WriteReadStats(readCounter, xmlFile);
            WriteReadsBySpecies(xmlFile);
            WriteFeatureStats(xmlFile);
            WriteSenseAntisenseStats(xmlFile);
            WriteHitsByChromosome(xmlFile);
            WriteAccuBarcodedDetection(xmlFile);
            WriteMappingDepth(xmlFile);
            AddSpikes(xmlFile);
            Annotations.WriteSpikeDetection(xmlFile);
            AddHitProfile(xmlFile);
            //AddSampledCVs(xmlFile); Difficult when running barcode-by-barcode
            AddCVHistogram(xmlFile);
            WriteBarcodeStats(fileNameBase, xmlFile);
            WriteRandomFilterStats(xmlFile);
            xmlFile.WriteLine("</strtSummary>");
            xmlFile.Close();
            if (!Environment.OSVersion.VersionString.Contains("Microsoft"))
            {
                CmdCaller.Run("php", "make_html_summary.php " + xmlPath);
            }
        }

        private void WriteAccuBarcodedDetection(StreamWriter xmlFile)
        {
            //WriteAccuMolecules(xmlFile, "transcriptdepth", "Distinct (10^6) detected transcripts as fn. of reads processed",
            //                       sampledDetectedTranscriptsByBcIdx);
            //WriteAccuMoleculesByBc(xmlFile, "transcriptdepthbybc", "Distinct detected transcripts in each barcode as fn. of reads processed", 
            //                       sampledDetectedTranscriptsByBcIdx);
        }

        private void WriteMappingDepth(StreamWriter xmlFile)
        {
            //WriteAccuMolecules(xmlFile, "librarydepth", "Distinct (10^6) mappings as fn. of reads processed",
            //                       sampledUniqueHitPositionsByBcIdx);
            WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings in each barcode as fn. of reads processed",
                                   sampledUniqueHitPositionsByBcIdx);
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (!barcodes.HasRandomBarcodes) return;
            xmlFile.WriteLine("    <randomtagfrequence>");
            xmlFile.WriteLine("<title>Number of reads detected in each random barcode</title>");
            xmlFile.WriteLine("<xtitle>Random tag index (AAAA...TTTT)</xtitle>");
            for (int i = 0; i < randomTagFilter.nReadsByRandomTag.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", barcodes.MakeRandomTag(i), randomTagFilter.nReadsByRandomTag[i]);
            xmlFile.WriteLine("    </randomtagfrequence>");
            xmlFile.WriteLine("    <nuniqueateachrandomtagcoverage>");
            xmlFile.WriteLine("<title>Unique alignmentposition-barcodes as fn of # random tags they occur in</title>");
            xmlFile.WriteLine("<xtitle>Number of different random tags</xtitle>");
            for (int i = 0; i < randomTagFilter.nCasesPerRandomTagCount.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.nCasesPerRandomTagCount[i]);
            xmlFile.WriteLine("    </nuniqueateachrandomtagcoverage>");
            //WriteAccuMolecules(xmlFile, "moleculedepth", "Distinct (10^6) detected molecules as fn. of reads processed",
            //                       sampledUniqueMoleculesByBcIdx);
            WriteAccuMoleculesByBc(xmlFile, "moleculedepthbybc", "Distinct detected molecules in each barcode as fn. of reads processed",
                                   sampledUniqueMoleculesByBcIdx);
            xmlFile.WriteLine("    <moleculereadscountshistogram>");
            xmlFile.WriteLine("<title>Distribution of number of times every unique molecule has been observed</title>");
            xmlFile.WriteLine("<xtitle>Number of observations (reads)</xtitle>");
            for (int i = 1; i < randomTagFilter.moleculeReadCountsHistogram.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.moleculeReadCountsHistogram[i]);
            xmlFile.WriteLine("    </moleculereadscountshistogram>");
        }

        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data)
        {
            xmlFile.WriteLine("  <" + tag + ">");
            xmlFile.WriteLine("    <title>" + title + "</title>");
            xmlFile.WriteLine("    <xtitle>Millions of reads processed</xtitle>");
            foreach (int bcIdx in data.Keys)
            {
                List<int> curve = data[bcIdx];
                xmlFile.WriteLine("      <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  barcodes.Seqs[bcIdx], (bcIdx * 17) % 255, (bcIdx * 11) % 255, (255 - (43 * bcIdx % 255)));
                int nReads = 0;
                int i = 0;
                for (; i < curve.Count - 1; i++)
                {
                    nReads += statsSampleDistPerBarcode;
                    xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                }
                nReads = numReadsByBarcode[bcIdx];
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </" + tag + ">");
        }

        private void WriteReadStats(ReadCounter readCounter, StreamWriter xmlFile)
        {
            int allBcCount = barcodes.Count;
            int spBcCount = barcodes.GenomeBarcodeIndexes(Annotations.Genome, false).Length;
            double totalReads = (readCounter.GrandTotal > 0)? readCounter.GrandTotal : nMappedReads; // Default to numReads if extraction summary files are missing
            xmlFile.WriteLine("  <readfiles>");
            foreach (string path in readCounter.GetReadFiles())
                xmlFile.WriteLine("    <readfile path=\"{0}\" />", path); 
            xmlFile.WriteLine("  </readfiles>");
            xmlFile.WriteLine("  <reads>");
            xmlFile.WriteLine("    <title>Read distribution (10^6). [# samples/wells]</title>");
            xmlFile.WriteLine("    <point x=\"All reads [{0}] (100%)\" y=\"{1}\" />", allBcCount, totalReads / 1.0E6d);
            int validReads = readCounter.GrandCount(ReadStatus.VALID);
            xmlFile.WriteLine("    <point x=\"Valid STRT reads[{0}] ({1:0%})\" y=\"{2}\" />", allBcCount, validReads / totalReads, validReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Multireads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMaxAltMappingsReads / totalReads, nMaxAltMappingsReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Mapped reads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMappedReads / totalReads, nMappedReads / 1.0E6d);
            double dividend = totalReads;
            double reducer = 1.0E6d;
            if (barcodes.HasRandomBarcodes)
            {
                xmlFile.WriteLine("    <point x=\"Duplicate reads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numDuplicateReads / totalReads, numDuplicateReads / 1.0E6d);
                dividend = numMolecules;
                reducer = 1.0E3d;
                xmlFile.WriteLine("  </reads>");
                xmlFile.WriteLine("  <molecules>");
                xmlFile.WriteLine("    <title>Molecule distribution (10^3). [# samples/wells]</title>");
                xmlFile.WriteLine("    <point x=\"Unique molecules [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numMolecules / dividend, numMolecules / reducer);
            }
            xmlFile.WriteLine("    <point x=\"Annotated [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numAnnotatedMols / dividend, numAnnotatedMols / reducer);
            xmlFile.WriteLine("    <point x=\"Exon [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numExonAnnotatedMols / dividend, numExonAnnotatedMols / reducer);
            int numIntronHits = TotalHitsByAnnotType[AnnotType.INTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.AINTR]);
            xmlFile.WriteLine("    <point x=\"Intron [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numIntronHits / dividend, numIntronHits / reducer);
            int numUstrHits = TotalHitsByAnnotType[AnnotType.USTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.AUSTR]);
            xmlFile.WriteLine("    <point x=\"Upstream [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numUstrHits / dividend, numUstrHits / reducer);
            int numDstrHits = TotalHitsByAnnotType[AnnotType.DSTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.ADSTR]);
            xmlFile.WriteLine("    <point x=\"Downstream [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numDstrHits / dividend, numDstrHits / reducer);
            if (Props.props.DirectionalReads)
            {
                int numOtherAS = TotalHitsByAnnotType[AnnotType.AUSTR] + TotalHitsByAnnotType[AnnotType.AEXON] + 
                                 TotalHitsByAnnotType[AnnotType.AINTR] + TotalHitsByAnnotType[AnnotType.ADSTR];
                xmlFile.WriteLine("    <point x=\"Loci A-sense [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numOtherAS / dividend, numOtherAS / reducer);
            }
            xmlFile.WriteLine("    <point x=\"Repeat [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount,
                               TotalHitsByAnnotType[AnnotType.REPT] / dividend, TotalHitsByAnnotType[AnnotType.REPT] / reducer);
            if (barcodes.HasRandomBarcodes)
                xmlFile.WriteLine("  </molecules>");
            else
                xmlFile.WriteLine("  </reads>");
        }

        private void WriteReadsBySpecies(StreamWriter xmlFile)
        {
            if (!barcodes.HasSampleLayout() || !Props.props.DirectionalReads) return;
            int[] genomeBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            WriteSpeciesReadSection(xmlFile, genomeBcIndexes, Annotations.Genome.Name);
            int[] emptyBcIndexes = barcodes.EmptyBarcodeIndexes();
            if (emptyBcIndexes.Length > 0)
                WriteSpeciesReadSection(xmlFile, emptyBcIndexes, "empty");
        }

        private void WriteSpeciesReadSection(StreamWriter xmlFile, int[] speciesBcIndexes, string speciesName)
        {
            double nUniqueMolecules = 0;
            double nAnnotationsHit = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                nUniqueMolecules += numMoleculesByBarcode[bcIdx];
                nAnnotationsHit += TotalHitsByBarcode[bcIdx];
            }
            xmlFile.WriteLine("  <reads species=\"{0}\">", speciesName);
            string molTitle = (barcodes.HasRandomBarcodes)? "molecule": "read";
            xmlFile.WriteLine("    <title>Distribution of {0} hits (10^6) by categories in {1} {2} wells</title>", molTitle, speciesBcIndexes.Length, speciesName);
            xmlFile.WriteLine("    <point x=\"Mapped {0}s ({100%})\" y=\"{1}\" />", molTitle, nUniqueMolecules / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Annotations hit ({0:0%})\" y=\"{1}\" />", nAnnotationsHit / nUniqueMolecules, nAnnotationsHit / 1.0E6d);
            foreach (int annotType in new int[] { AnnotType.EXON, AnnotType.INTR, AnnotType.USTR, AnnotType.DSTR, AnnotType.REPT })
            {
                int numOfType = GetSpeciesAnnotHitCount(speciesBcIndexes, annotType);
                xmlFile.WriteLine("    <point x=\"{0} ({1:0%})\" y=\"{2}\" />", AnnotType.GetName(annotType), numOfType / nUniqueMolecules, numOfType / 1.0E6d);
            }
            if (Props.props.DirectionalReads)
            {
                int numAEXON = GetSpeciesAnnotHitCount(speciesBcIndexes, AnnotType.AEXON);
                xmlFile.WriteLine("    <point x=\"AEXON ({0:0%})\" y=\"{1}\" />", numAEXON / nUniqueMolecules, numAEXON / 1.0E6d);
            }
            xmlFile.WriteLine("  </reads>");
        }

        private int GetSpeciesAnnotHitCount(int[] speciesBcIndexes, int annotType)
        {
            int annotType2 = -1;
            if (!Props.props.DirectionalReads && annotType != AnnotType.REPT) 
                annotType2 = AnnotType.MakeAntisense(annotType);
            int numOfType = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                numOfType += TotalHitsByAnnotTypeAndBarcode[annotType, bcIdx];
                if (annotType2 >= 0)
                    numOfType += TotalHitsByAnnotTypeAndBarcode[annotType2, bcIdx];
            }
            return numOfType;
        }
        
        private void WriteSenseAntisenseStats(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <senseantisense>");
            xmlFile.WriteLine("    <title>Sense and Antisense reads per kb feature length</title>");
            foreach (int t in AnnotType.GetSenseTypes())
            {
                int totSense = Annotations.GetTotalAnnotCounts(t, true);
                int at = AnnotType.MakeAntisense(t);
                int totASense = Annotations.GetTotalAnnotCounts(at, true);
                string ratio = "Inf";
                int totLen = Annotations.GetTotalAnnotLength(at, true);
                if (totASense > 0)
                    ratio = string.Format("{0:0.0}", totSense / (double)totASense);
                xmlFile.WriteLine("   <point x=\"{0}#br#{1}:1\" y=\"{2:0.##}\" y2=\"{3:0.##}\" />", AnnotType.GetName(t),
                                  ratio, 1000.0d * (totSense / (double)totLen), 1000.0d * (totASense / (double)totLen));
            }
            int reptLen = Annotations.GetTotalAnnotLength(AnnotType.REPT);
            int reptCount = TotalHitsByAnnotType[AnnotType.REPT];
            xmlFile.WriteLine("   <point x=\"REPT#br#1:1\" y=\"{0:0.###}\" y2=\"{0:0.####}\" />", reptCount / (double)reptLen);
            xmlFile.WriteLine("  </senseantisense>");
        }

        private void WriteHitsByChromosome(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <senseantisensebychr>");
            xmlFile.WriteLine("    <title>% of reads mapped to Sense/Antisense exons [ratio below] by chromosome</title>");
            List<int> numkeys = new List<int>();
            List<string> alphakeys = new List<string>();
            int v;
            foreach (string key in TotalHitsByAnnotTypeAndChr.Keys)
                if (int.TryParse(key, out v)) numkeys.Add(v);
                else alphakeys.Add(key);
            numkeys.Sort();
            alphakeys.Sort();
            List<string> sortedkeys = numkeys.ConvertAll((n) => n.ToString());
            sortedkeys.AddRange(alphakeys);
            string[] chrs = TotalHitsByAnnotTypeAndChr.Keys.ToArray();
            foreach (string chr in sortedkeys)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chr))
                    continue;
                double nSense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.EXON];
                double nAsense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.AEXON];
                string ratio = (nAsense == 0)? "1:0" : string.Format("{0:0}", nSense / (double)nAsense);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  chr, ratio, 100.0d *(nSense / (double)nMappedReads), 100.0d * (nAsense / (double)nMappedReads));
            }
            xmlFile.WriteLine("  </senseantisensebychr>");
        }

        private void WriteFeatureStats(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <features>");
            xmlFile.WriteLine("    <title>Overall detection of features</title>");
            if (!Annotations.noGeneVariants)
                xmlFile.WriteLine("    <point x=\"Detected tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedGenes());
            xmlFile.WriteLine("    <point x=\"Detected main tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedMainGeneVariants());
            int[] bcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            int nGenesInAllBarcodes = 0;
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                foreach (int bcIdx in bcIndexes)
                    if (gf.TranscriptHitsByBarcode[bcIdx] == 0) goto EXIT;
                nGenesInAllBarcodes++;
            EXIT: ;
            }
            xmlFile.WriteLine("    <point x=\"in every species well ({0})\" y=\"{1}\" />", bcIndexes.Length, nGenesInAllBarcodes);
            xmlFile.WriteLine("    <point x=\"Detected repeat classes\" y=\"{0}\" />", Annotations.GetNumExpressedRepeats());
            xmlFile.WriteLine("  </features>");
        }

        private void AddSpikes(StreamWriter xmlFile)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("  <spikes>\n");
            sb.Append("    <title>Normalized spike means and standard deviations</title>\n");
            bool anySpikeDetected = false;
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.Name.StartsWith("RNA_SPIKE_"))
                {
                    if (gf.IsExpressed())
                        anySpikeDetected = true;
                    DescriptiveStatistics ds = new DescriptiveStatistics();
                    foreach (int bcIdx in barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome))
                    {
                        int c = TotalHitsByAnnotTypeAndBarcode[AnnotType.EXON, bcIdx];
                        if (!AnnotType.DirectionalReads) c += TotalHitsByAnnotTypeAndBarcode[AnnotType.AEXON, bcIdx];
                        if (c > 0)
                        {
                            double RPM = gf.NonConflictingTranscriptHitsByBarcode[bcIdx] * 1.0E+6 / (double)c;
                            ds.Add(RPM);
                        }
                    }
                    if (ds.Count == 0)
                        continue;
                    double mean = Math.Max(0.001, ds.Mean());
                    string spikeId = "#" + gf.Name.Replace("RNA_SPIKE_", "");
                    if (ds.Count > 0)
                        sb.Append(string.Format("    <point x=\"{0}\" y=\"{1:0.###}\" error=\"{2:0.###}\" />\n", spikeId, mean, ds.StandardDeviation()));
                    else
                        sb.Append(string.Format("    <point x=\"{0}\" y=\"0.0\" error=\"0.0\" />\n", spikeId));
                }
            }
            sb.Append("  </spikes>");
            if (anySpikeDetected)
                xmlFile.WriteLine(sb.ToString());
        }

        private void AddHitProfile(StreamWriter xmlFile)
        {
            int averageReadLen = MappedTagItem.AverageReadLen;
            xmlFile.WriteLine("  <hitprofile>");
            xmlFile.WriteLine("  <title>5'->3' read distr. Green=Transcripts/Blue=Spikes</title>");
            xmlFile.WriteLine("	 <xtitle>Relative pos within transcript</xtitle>");
            int trLenBinSize = 500;
            int trLenBinHalfWidth = trLenBinSize / 2;
            int trLenBinStep = 1500;
            int trLen1stBinMid = 500;
            int trLen1stBinStart = trLen1stBinMid - trLenBinHalfWidth;
            int trLenBinCount = 4;
            int nSections = 20;
            int minHitsPerGene = nSections * 10;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[trLenBinCount, nSections];
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[trLenBinIdx, section] = new DescriptiveStatistics();
            }
            int[] geneCounts = new int[trLenBinCount];
            int spikeColor = 0x200040;
            int spikeColorStep = ((0xFF - 0x41) / 8);
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                bool isSpike = gf.Name.StartsWith("RNA_SPIKE_");
                if (isSpike && gf.GetTotalHits(true) < 50)
                    continue;
                if (!isSpike && gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double sectionSize = (trLen - averageReadLen) / (double)nSections;
                int[] trSectionCounts = CompactGenePainter.GetBinnedTranscriptHitsRelEnd(gf, sectionSize, Props.props.DirectionalReads, averageReadLen);
                if (trSectionCounts.Length == 0) continue;
                double trTotalCounts = 0.0;
                foreach (int c in trSectionCounts) trTotalCounts += c;
                if (trTotalCounts == 0.0) continue;
                if (!isSpike)
                {
                    if (trLen < trLen1stBinStart || (trLen - trLen1stBinStart) % trLenBinStep > trLenBinSize)
                        continue;
                    //if (Math.Abs((trLen - trLen1stBinMid) % trLenBinSize) > 250) continue;
                    int trLenBin = (trLen - trLen1stBinStart) / trLenBinStep;
                    if (trLenBin >= trLenBinCount) continue;
                    for (int section = 0; section < nSections; section++)
                        binnedEfficiencies[trLenBin, section].Add(trSectionCounts[nSections - 1 - section] / trTotalCounts);
                    geneCounts[trLenBin]++;
                }
                else
                {
                    string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                    xmlFile.WriteLine("    <curve legend=\"#{0} {1}bp\" color=\"#{2:X6}\">", spikeId, trLen, spikeColor);
                    spikeColor += spikeColorStep;
                    for (int section = 0; section < nSections; section++)
                    {
                        double eff = trSectionCounts[nSections - 1 - section] / trTotalCounts;
                        double fracPos = (section + 0.5D) / (double)nSections;
                        xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", fracPos, eff);
                    }
                    xmlFile.WriteLine("    </curve>");
                }
            }
            int geneColor = 0x304030;
            int geneColorStep = ((0xFF - 0x41) / trLenBinCount) * 0x0100;
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                if (geneCounts[trLenBinIdx] < 10) continue;
                int midLen = (trLenBinIdx * trLenBinStep) + trLen1stBinMid;
                xmlFile.WriteLine("    <curve legend=\"{0}-{1}bp\" color=\"#{2:X6}\">",
                                  midLen - trLenBinHalfWidth, midLen + trLenBinHalfWidth, geneColor);
                geneColor += geneColorStep;
                for (int section = 0; section < nSections; section++)
                {
                    double eff = binnedEfficiencies[trLenBinIdx, section].Mean();
                    double fracPos = (section + 0.5D) / (double)nSections;
                    xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", fracPos, eff);
                }
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </hitprofile>");
        }

        private void AddCVHistogram(StreamWriter xmlFile)
        {
            int[] genomeBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            if (genomeBcIndexes.Length < 3)
                return;
            int nPairs = 1000;
            int nBins = 40;
            List<double[]> validBcCountsByGene = new List<double[]>();
            List<int> totalCountsByGene = new List<int>();
            List<int> spikeIndices = new List<int>();
            int[] totalsByBarcode = new int[barcodes.Count];
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                double[] gfValidBcCounts = new double[barcodes.Count];
                int gfTotal = 0;
                foreach (int bcIdx in genomeBcIndexes)
                {
                    int c = gf.NonConflictingTranscriptHitsByBarcode[bcIdx];
                    gfValidBcCounts[bcIdx] = c;
                    gfTotal += c;
                    totalsByBarcode[bcIdx] += c;
                }
                if (gfTotal > 0)
                {
                    totalCountsByGene.Add(gfTotal);
                    validBcCountsByGene.Add(gfValidBcCounts);
                }
            }
            double[] CVs = CalcCVs(genomeBcIndexes, nPairs, validBcCountsByGene, totalCountsByGene, totalsByBarcode);
            if (CVs != null)
                OutputCVBars(xmlFile, nPairs, nBins, CVs);
        }

        private double[] CalcCVs(int[] genomeBcIndexes, int nPairs, List<double[]> validBcCountsByGene, List<int> totalCountsByGene, int[] totalsByBarcode)
        {
            Linnarsson.Mathematics.Sort.HeapSort(totalCountsByGene, validBcCountsByGene);
            validBcCountsByGene.Reverse(); // Get indices of genes in order of most->least expressed
            int nGenes = Math.Min(validBcCountsByGene.Count - 1, nPairs);
            double[] CVs = new double[nGenes];
            int minValidWellCount = numAnnotatedMols / genomeBcIndexes.Length / 20;
            int[] usefulBcIndexes = genomeBcIndexes.Where(bcIdx => totalsByBarcode[bcIdx] > minValidWellCount).ToArray();
            if (usefulBcIndexes.Length < 3)
                return null;
            for (int geneIdx = 0; geneIdx < nGenes; geneIdx++)
            {
                double[] bcodeCounts = validBcCountsByGene[geneIdx];
                List<double> normedBcValues = new List<double>();
                foreach (int bcIdx in usefulBcIndexes)
                    normedBcValues.Add(bcodeCounts[bcIdx] / (double)totalsByBarcode[bcIdx]); // Normalized value
                CVs[geneIdx] = DescriptiveStatistics.CV(normedBcValues.ToArray());
            }
            return CVs;
        }

        private static void OutputCVBars(StreamWriter xmlFile, int nPairs, int nBins, double[] CVs)
        {
            double minCV = CVs.Min();
            double maxCV = CVs.Max() + 0.1;
            if (!double.IsNaN(minCV) && !double.IsNaN(maxCV))
            {
                double[] geneCVHisto = new double[nBins];
                foreach (double cv in CVs)
                    geneCVHisto[(int)(nBins * (cv - minCV) / (maxCV - minCV))]++;
                xmlFile.WriteLine("  <cvhistogram>");
                xmlFile.WriteLine("    <title>CV distribution of normalized counts of top {0} genes</title>", nPairs);
                xmlFile.WriteLine("    <xtitle>CV distribution</xtitle>");
                double binStep = (maxCV - minCV) / nBins;
                for (int bin = 0; bin < nBins; bin++)
                    xmlFile.WriteLine("    <point x=\"{0:0.###}\" y=\"{1}\" />", minCV + bin * binStep, geneCVHisto[bin]);
                xmlFile.WriteLine("  </cvhistogram>");
            }
            else
                Console.WriteLine("ERROR: CVs.Count=" + CVs.Length + " minCV=" + minCV + " maxCV= " + maxCV);
        }

        /// <summary>
        /// Write plate layout formatted statistics for hits by barcodes.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteBarcodeStats(string fileNameBase, StreamWriter xmlFile)
        {
            StreamWriter barcodeStats = new StreamWriter(fileNameBase + "_barcode_summary.tab");
            string molTitle = (barcodes.HasRandomBarcodes) ? "molecules" : "reads";
            barcodeStats.WriteLine("Total annotated {0}: {1}\n", molTitle, numAnnotatedMols);
            StreamWriter bCodeLines = new StreamWriter(fileNameBase + "_barcode_oneliners.tab");
            xmlFile.WriteLine("  <barcodestats>");
            xmlFile.Write("    <barcodestat section=\"wellids\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", barcodes.GetWellId(bcIdx));
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            WriteBarcodes(xmlFile, barcodeStats, bCodeLines);
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome);
            if (barcodes.SpeciesByWell != null)
                WriteSpeciesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            WriteDuplicateMoleculesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            WriteFeaturesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            barcodeStats.WriteLine("Transcripts detected in each barcode:");
            string[] trCounts = Array.ConvertAll<int, string>(Annotations.SampleBarcodeExpressedGenes(), 
                                                              (x => x.ToString()));
            barcodeStats.WriteLine(MakeDataMatrix(trCounts, "0"));
            barcodeStats.Close();
            bCodeLines.Close();
            xmlFile.Write("    <barcodestat section=\"transcripts\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? trCounts[bcIdx] : "(" + trCounts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            xmlFile.WriteLine("  </barcodestats>");
        }

        private void WriteFeaturesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            string molTitle = (barcodes.HasRandomBarcodes) ? "molecules" : "reads";
            for (var annotType = 0; annotType < AnnotType.Count; annotType++)
            {
                if (annotType == AnnotType.AREPT) continue;
                string annotName = AnnotType.GetName(annotType);
                barcodeStats.WriteLine("\nTotal {0} mapped to {1}:\n", molTitle, annotName);
                bCodeLines.Write(annotName);
                xmlFile.Write("    <barcodestat section=\"{0}\">", annotName);
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    string annotHits = TotalHitsByAnnotTypeAndBarcode[annotType, bcIdx].ToString();
                    bCodeLines.Write("\t" + annotHits);
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    string d = genomeBcIndexes.Contains(bcIdx) ? annotHits : "(" + annotHits.ToString() + ")";
                    xmlFile.Write("    <d>{0}</d>", d);
                }
                bCodeLines.WriteLine();
                xmlFile.WriteLine("\n    </barcodestat>");
                barcodeStats.WriteLine(MakeTotalMatrix(annotType));
                barcodeStats.WriteLine(MakeFracDevStatsMatrix(annotType));
            }
        }

        private void WriteTotalByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            barcodeStats.WriteLine("\nTotal annotated hits by barcode:\n");
            bCodeLines.Write("TOTAL");
            xmlFile.Write("    <barcodestat section=\"annotated hits\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = TotalHitsByBarcode[bcIdx].ToString();
                bCodeLines.Write("\t" + counts[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? counts[bcIdx] : "(" + counts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine(MakeDataMatrix(counts, "0"));
            bCodeLines.WriteLine();
        }

        private void WriteDuplicateMoleculesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            if (!barcodes.HasRandomBarcodes) return;
            barcodeStats.WriteLine("\nDuplicated reads filtered away due to same random tag and position, by barcode:\n");
            bCodeLines.Write("TOTAL");
            xmlFile.Write("    <barcodestat section=\"duplicated molecules (by position-random tag)\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = randomTagFilter.nDuplicatesByBarcode[bcIdx].ToString();
                bCodeLines.Write("\t" + counts[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? counts[bcIdx] : "(" + counts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine(MakeDataMatrix(counts, "0"));
            bCodeLines.WriteLine();
        }

        private void WriteBarcodes(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines)
        {
            xmlFile.Write("    <barcodestat section=\"barcodes\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                bCodeLines.Write("\t" + barcodes.Seqs[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", barcodes.Seqs[bcIdx]);
            }
            bCodeLines.WriteLine();
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine("Barcode by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(barcodes.Seqs, "-"));
        }

        private void WriteSpeciesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            foreach (string species in barcodes.SpeciesByWell) bCodeLines.Write("\t" + species);
            barcodeStats.WriteLine("Species by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(barcodes.SpeciesByWell, "empty"));
            xmlFile.Write("    <barcodestat section=\"species\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string species = genomeBcIndexes.Contains(bcIdx) ? barcodes.SpeciesByWell[bcIdx] : "(" + barcodes.SpeciesByWell[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", species);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
        }

        private string MakeTotalMatrix(int annotType)
        {
            string[] counts = new string[barcodes.Count];
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
                counts[bcodeIdx] = TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx].ToString();
            return MakeDataMatrix(counts, "0");
        }

        private string MakeFracDevStatsMatrix(int annotType)
        {
            double fracSum = 0.0;
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
                fracSum += (double)TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx]
                                         / TotalHitsByBarcode[bcodeIdx];
            double meanFrac = fracSum / barcodes.Count;
            string[] values = new string[barcodes.Count];
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
            {
                double ownFrac = (double)TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx]
                                                    / TotalHitsByBarcode[bcodeIdx];
                values[bcodeIdx] = (meanFrac == 0)? "NoData" : string.Format("{0:0.000}", ownFrac / meanFrac);
            }
            string annotName = AnnotType.GetName(annotType);
            string result = string.Format("relative to average fraction {0:0.0000}:\n", meanFrac);
            result += MakeDataMatrix(values, "NoData");
            return result;
        }

        private string MakeDataMatrix(string[] values, string defaultValue)
        {
            StringBuilder result = new StringBuilder();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 12; col++)
                {
                    int bcodeIdx = row + 8 * col;
                    if (bcodeIdx < values.Length)
                        result.Append(values[bcodeIdx].ToString());
                    else
                        result.Append(defaultValue.ToString());
                    if (col < 11) result.Append("\t");
                }
                result.Append("\n");
            }
            return result.ToString();
        }

        public int GetNumMappedReads()
        {
            return numAnnotatedMols;
        }

        private void WriteASExonDistributionHistogram(string fileNameBase)
        {
            var ASHistFile = (fileNameBase + "_ASRPM_Histo.tab").OpenWrite();
            ASHistFile.WriteLine("Distribution of Antisense RPM/bp transcript among all genes with any Antisense hit.");
            ASHistFile.WriteLine("BinStart\tCount");
            int[] histo;
            double firstBinStart, binWidth, median;
            MakeExonAntisenseHistogram(out histo, out median, out firstBinStart, out binWidth);
            for (int bin = 0; bin < histo.Length; bin++)
            {
                ASHistFile.WriteLine("{0}\t{1}", firstBinStart + bin * binWidth, histo[bin]);
            }
            ASHistFile.WriteLine("\n\nMedian:\t{0}", median);
            ASHistFile.Close();
        }

        private void MakeExonAntisenseHistogram(out int[] histo, out double median,
                       out double firstBinStart, out double binWidth)
        {
            int nGenesPerBin = 30;
            double totHits = TotalHitsByAnnotType[AnnotType.EXON] + TotalHitsByAnnotType[AnnotType.AEXON];
            if (totHits == 0)
            {
                histo = new int[1]; median = 0.0; firstBinStart = 0.0; binWidth = 1.0; return;
            }
            double normer = 1.0E+6 / totHits;
            double lastBinEnd = 0.0;
            int nGenesWithASHit = 0;
            firstBinStart = double.MaxValue;
            List<double> ASPBPM = new List<double>();
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                int antiHits = gf.NonMaskedHitsByAnnotType[AnnotType.AEXON];
                if (antiHits == 0) continue;
                nGenesWithASHit++;
                double antiHitsPerBasePerM = (double)antiHits / (double)gf.GetNonMaskedTranscriptLength() / normer;
                ASPBPM.Add(antiHitsPerBasePerM);
                lastBinEnd = Math.Ceiling(Math.Max(lastBinEnd, antiHitsPerBasePerM));
                firstBinStart = Math.Floor(Math.Min(firstBinStart, antiHitsPerBasePerM));
            }
            double valueRange = lastBinEnd - firstBinStart;
            int nBins = (int)((double)nGenesWithASHit / (double)nGenesPerBin);
            histo = new int[nBins + 1];
            binWidth = valueRange / nBins;
            foreach (double antiHitsPerBasePerM in ASPBPM)
            {
                int bin = (int)Math.Ceiling((antiHitsPerBasePerM - firstBinStart) / binWidth);
                histo[bin]++;
            }
            if (ASPBPM.Count == 0) 
                median = double.NaN;
            else
                median = DescriptiveStatistics.Median(ASPBPM);
        }

        private void WriteRedundantExonHits(string fileNameBase)
        {
            Dictionary<string, List<string>> byGene = new Dictionary<string, List<string>>();
            StreamWriter redFile = (fileNameBase + "_shared_hits.tab").OpenWrite();
            redFile.WriteLine("#Reads\tGene loci competing for these reads (not all gene variants spanning the same hit site are shown)");
            foreach (string combName in redundantHits.Keys)
            {
                int sharedHits = redundantHits[combName];
                string[] names = combName.Split('#');
                foreach (string n in names)
                {
                    string group = string.Format("{0}({1})", 
                                     string.Join("/", names.Where( (on) => (on != n)).ToArray()), sharedHits);
                    if (!byGene.ContainsKey(n))
                        byGene[n] = new List<string>();
                    byGene[n].Add(group);
                }
                string tabbedNames = string.Join("\t", names);
                redFile.WriteLine("{0}\t{1}", sharedHits, tabbedNames);
            }
            redFile.Close();
            StreamWriter sharedFile = (fileNameBase + "_shared_hits_by_gene.tab").OpenWrite();
            sharedFile.WriteLine("Gene\tMinHits\tMaxHits\tNon-unique hits in the difference, that also map to other known genes");
            foreach (string gene in byGene.Keys)
            {
                GeneFeature gf = Annotations.geneFeatures[gene];
                int ncHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                int allHits = gf.TranscriptHitsByBarcode.Sum();
                string altGenes = string.Join("\t", byGene[gene].ToArray());
                sharedFile.WriteLine("{0}\t{1}\t{2}\t{3}", gene, ncHits, allHits, altGenes);
            }
            sharedFile.Close();
        }

        private void WriteSnps(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs.tab").OpenWrite();
            snpFile.WriteLine("#Gene\tChr\tmRNAStartChrPosition\tHetero_eSNPChrPositions\tAlt_eSNPChrPositions");
            int thres = (int)(SnpAnalyzer.thresholdFractionAltHitsForMixPos * 100);
            snpFile.WriteLine("#(>={0} AltNtReads/Pos required)\t\t\t({1}-{2}% AltNt)\t(>{2}% Alt Nt)", SnpAnalyzer.minAltHitsToTestSnpPos, thres, 100 - thres);
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                List<int> mixChrPos, altChrPos;
                SnpAnalyzer.GetSnpChrPositions(gf, out mixChrPos, out altChrPos);
                int nNeededOutputLines = Math.Max(mixChrPos.Count, altChrPos.Count);
                if (nNeededOutputLines == 0) continue;
                string first = gf.Name + "\t" + gf.Chr + "\t" + gf.Start + "\t";
                for (int i = 0; i < nNeededOutputLines; i++)
                {
                    snpFile.Write(first);
                    if (i < mixChrPos.Count) snpFile.Write(mixChrPos[i]);
                    if (i < altChrPos.Count) snpFile.Write("\t" + altChrPos[i].ToString());
                    snpFile.WriteLine();
                    first = "\t\t\t";
                }
            }
            snpFile.Close();
        }

        private void WriteSnpsByBarcode(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs_by_barcode.tab").OpenWrite();
            SnpAnalyzer sa = new SnpAnalyzer();
            int averageHitLength = MappedTagItem.AverageReadLen;
            sa.WriteSnpsByBarcode(snpFile, barcodes, Annotations.geneFeatures, averageHitLength);
            snpFile.Close();
        }

        public void WriteWriggle(string fileNameBase)
        {
            WriteWiggleStrand(fileNameBase, '+');
            WriteWiggleStrand(fileNameBase, '-');
        }

        private void WriteWiggleStrand(string fileNameBase, char strand)
        {
            string strandString = (strand == '+') ? "fw" : "rev";
            StreamWriter readWriter = (fileNameBase + "_" + strandString + "_byread.wig.gz").OpenWrite();
            readWriter.WriteLine("track type=wiggle_0 name=\"{0} (+)\" description=\"{0} (+)\" visibility=full",
                Path.GetFileNameWithoutExtension(fileNameBase) + "_byread");
            StreamWriter molWriter = null;
            if (barcodes.HasRandomBarcodes)
            {
                molWriter = (fileNameBase + "_" + strandString + "_bymolecule.wig.gz").OpenWrite();
                molWriter.WriteLine("track type=wiggle_0 name=\"{0} (+)\" description=\"{0} (+)\" visibility=full",
                    Path.GetFileNameWithoutExtension(fileNameBase) + "_bymolecule");
            }
            int averageReadLength = MappedTagItem.AverageReadLen;
            foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
            {
                string chr = data.Key;
                if (StrtGenome.IsSyntheticChr(chr))
                    continue;
                data.Value.GetWiggle(strand).WriteReadWiggle(molWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr]);
                if (molWriter != null)
                {
                    data.Value.GetWiggle(strand).WriteMolWiggle(molWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr]);
                }
            }
            readWriter.Close();
            if (molWriter != null)
                molWriter.Close();
        }

        private void WriteHotspots(string file)
        {
            var writer = (file + "_hotspots.tab").OpenWrite();
            writer.WriteLine("Positions with local maximal read counts that lack gene or repeat annotation. Samples < 5 bp apart not shown.");
            writer.WriteLine("Chr\tPosition\tStrand\tCoverage");
            foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
            {
                string chr = data.Key;
                if (StrtGenome.IsSyntheticChr(chr))
                    continue;
                int[] positions, counts;
                data.Value.GetWiggle('+').GetReadPositionsAndCounts(out positions, out counts);
                FindHotspots(writer, chr, '+', positions, counts);
                data.Value.GetWiggle('-').GetReadPositionsAndCounts(out positions, out counts);
                FindHotspots(writer, chr, '-', positions, counts);
            }
            writer.Close();
        }

        private void FindHotspots(StreamWriter writer, string chr, char strand, int[] positions, int[] counts)
        {
            int averageReadLength = MappedTagItem.AverageReadLen;
            int chrLength = Annotations.ChromosomeLengths[chr];
            HotspotFinder hFinder = new HotspotFinder(maxHotspotCount);
            Queue<int> stops = new Queue<int>();
            int lastHit = 0;
            int hitIdx = 0;
            int i = 0;
            while (i < chrLength && hitIdx < positions.Length)
            {
                int c = counts[hitIdx];
                i = positions[hitIdx++];
                if (Annotations.GetMatching(chr, i).Count() == 0)
                {
                    for (int cc = 0; cc < c; cc++)
                        stops.Enqueue(i + averageReadLength);
                }
                while (i < chrLength && stops.Count > 0)
                {
                    while (hitIdx < positions.Length && positions[hitIdx] == i)
                    {
                        hitIdx++;
                        stops.Enqueue(i + averageReadLength);
                    }
                    i++;
                    if (stops.Count > 0 && i == stops.Peek())
                    {
                        if (i - lastHit >= minHotspotDistance)
                        {
                            lastHit = i;
                            hFinder.Add(stops.Count, i - (averageReadLength / 2));
                        }
                        while (stops.Count > 0 && i == stops.Peek()) stops.Dequeue();
                    }
                }
            }
            int[] topCounts, locations;
            hFinder.GetTop(out topCounts, out locations);
            for (int cI = 0; cI < topCounts.Length; cI++)
            {
                int start = locations[cI];
                writer.WriteLine("{0}\t{1}\t{2}\t{3}",
                                 chr, start + averageReadLength / 2, strand, topCounts[cI]);
            }
        }

    }

}
