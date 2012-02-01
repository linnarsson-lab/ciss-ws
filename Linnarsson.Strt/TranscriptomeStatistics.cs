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
        public static readonly int minMismatchReadCountForSNPDetection = 10;

        StreamWriter nonAnnotWriter = null;
        StreamWriter nonExonWriter = null;

        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private RandomTagFilterByBc randomTagFilter;
        private SnpRndTagVerifier snpRndTagVerifier;
        public SyntReadReporter TestReporter { get; set; }

        Dictionary<string, int[]> TotalHitsByAnnotTypeAndChr; // Separates sense and antisense
        int[,] TotalHitsByAnnotTypeAndBarcode; // Separates sense and antisense
        int[] TotalHitsByAnnotType;            // Separates sense and antisense
        /// <summary>
        /// Number of hits to distinct annotations in each barcode (multireads can get a count for each mapping)
        /// </summary>
        int[] TotalHitsByBarcode;
        double[] labelingEfficiencyByBc;

        AbstractGenomeAnnotations Annotations;
        Barcodes barcodes;
		DnaMotif[] motifs;
        int currentBcIdx = 0;
        string currentMapFilePath;
        public string OutputPathbase;

        /// <summary>
        /// Total number of mapped reads in each barcode
        /// </summary>
        int[] nMappedReadsByBarcode;
        /// <summary>
        /// Total number of mapped reads
        /// </summary>
        int nMappedReads { get { return nMappedReadsByBarcode.Sum(); } }
        /// <summary>
        /// Total number of mapped unique molecules in each barcode (mapped reads when not using random Tags)
        /// </summary>
        int[] nMoleculesByBarcode;
        /// <summary>
        /// Total number of mapped unique molecules
        /// </summary>
        int nMolecules { get { return nMoleculesByBarcode.Sum(); } }
        /// <summary>
        /// Total number of duplicated reads when using random Tags
        /// </summary>
        int nDuplicateReads { get { return nMappedReads - nMolecules; } }
        /// <summary>
        /// Number of mappings with any annotation
        /// </summary>
        int nAnnotatedMappings = 0;
        /// <summary>
        /// Number of reads that map to some exon/splice
        /// </summary>
        int nExonAnnotatedReads = 0;
        /// <summary>
        /// Number of mappings to exons/splices (may be more than number of molecules/reads if redundant mappings occur)
        /// </summary>
        int nExonMappings = 0;
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
            foreach (string chr in Annotations.GetChromosomeIds())
                TotalHitsByAnnotTypeAndChr[chr] = new int[AnnotType.Count];
            TotalHitsByAnnotType = new int[AnnotType.Count];
            nMappedReadsByBarcode = new int[barcodes.Count];
            nMoleculesByBarcode = new int[barcodes.Count];
            labelingEfficiencyByBc = new double[barcodes.Count];
            exonHitGeneNames = new List<string>(100);
            annotationChrId = Annotations.Genome.Annotation;
            randomTagFilter = new RandomTagFilterByBc(barcodes, Annotations.GetChromosomeIds());
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
        public void ProcessMapFiles(List<string> mapFilePaths, int averageReadLen)
        {
            if (mapFilePaths.Count == 0)
                return;
            mapFilePaths.Sort(CompareMapFiles); // Important to have them sorted by barcode
            if (Props.props.AnalyzeSNPs)
            {
                MapFileSnpFinder mfsf = new MapFileSnpFinder(barcodes);
                mfsf.ProcessMapFiles(mapFilePaths);
                int nSNPs = randomTagFilter.SetupSNPCounters(averageReadLen, mfsf.IterSNPLocations(minMismatchReadCountForSNPDetection));
                Console.WriteLine("Registered " + nSNPs + " potential expressed SNPs (positions with >=" + minMismatchReadCountForSNPDetection + " mismatch reads).");
            }
            if (Props.props.SnpRndTagVerification && barcodes.HasRandomBarcodes)
                snpRndTagVerifier = new SnpRndTagVerifier(Props.props, Annotations.Genome);
            List<string> bcMapFilePaths = new List<string>();
            string mapFileName = Path.GetFileName(mapFilePaths[0]);
            currentBcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
            Console.Write("Annotatating " + mapFilePaths.Count + " map files");

            if (Props.props.DebugAnnotation)
            {
                if (!Directory.Exists(Path.GetDirectoryName(OutputPathbase)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPathbase));
                nonAnnotWriter = new StreamWriter(OutputPathbase + "_NONANNOTATED.tab");
                nonExonWriter = new StreamWriter(OutputPathbase + "_NONEXON.tab");
            }

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
                    randomTagFilter.ChangeBcIdx(currentBcIdx);
                }
                bcMapFilePaths.Add(mapFilePath);
            }
            if (bcMapFilePaths.Count > 0)
                ProcessBarcodeMapFiles(bcMapFilePaths);
            Console.WriteLine();

            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter.Close();
                nonExonWriter.Close();
            }
        }

        /// <summary>
        /// Annotate a set of map files that have the same barcode
        /// </summary>
        /// <param name="bcMapFilePaths">Paths to files where all reads have the sanme barcode</param>
        private void ProcessBarcodeMapFiles(List<string> bcMapFilePaths)
        {
            long totalReadLength = 0;
            foreach (string mapFilePath in bcMapFilePaths)
            {
                currentMapFilePath = mapFilePath;
                MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, barcodes);
                if (mapFileReader == null)
                    throw new Exception("Unknown read map file type : " + mapFilePath);
                foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
                {
                    bool someExonHit = false;
                    foreach (MultiReadMapping m in mrm.IterMappings())
                    {
                        if (Annotations.IsTranscript(m.Chr, m.Strand, m.HitMidPos))
                        {
                            someExonHit = true;
                            randomTagFilter.Add(m);
                        }
                    }
                    if (!someExonHit)
                        randomTagFilter.Add(mrm[0]); // If no exon-mapping is found, add the read to the first mapping it happened to get
                    else
                        nExonAnnotatedReads++;
                    if (snpRndTagVerifier != null)
                        snpRndTagVerifier.Add(mrm);
                    totalReadLength += mrm.SeqLen;
                    if ((++nMappedReadsByBarcode[currentBcIdx]) % statsSampleDistPerBarcode == 0)
                        SampleReadStatistics(statsSampleDistPerBarcode);
                    if (mrm.HasAltMappings) nMaxAltMappingsReads++;
                }
            }
            SampleReadStatistics(nMappedReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode);
            MappedTagItem.AverageReadLen = (int)Math.Round((double)totalReadLength / nMappedReadsByBarcode[currentBcIdx]);
            List<string> ctrlChrId = new List<string>();
            if (randomTagFilter.chrTagDatas.ContainsKey("CTRL"))
            {
                ctrlChrId.Add("CTRL");
                foreach (MappedTagItem mtitem in randomTagFilter.IterItems(ctrlChrId, true))
                    Annotate(mtitem);
                double labelingEfficiency = Annotations.GetEfficiencyFromSpikes(currentBcIdx);
                TagItem.LabelingEfficiency = labelingEfficiency;
                labelingEfficiencyByBc[currentBcIdx] = labelingEfficiency;
            }
            foreach (MappedTagItem mtitem in randomTagFilter.IterItems(ctrlChrId, false))
                Annotate(mtitem);
            FinishBarcode();
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
            int molCount = item.MolCount;
            nMoleculesByBarcode[currentBcIdx] += molCount;
            exonHitGeneNames.Clear();
            bool someAnnotationHit = false;
            bool someExonHit = false;
            List<FtInterval> trMatches = Annotations.GetTranscriptMatches(item.chr, item.strand, item.HitMidPos).ToList();
            if (trMatches.Count > 0)
            {
                someExonHit = someAnnotationHit = true;
                MarkStatus markStatus = (trMatches.Count > 1 || item.hasAltMappings) ? MarkStatus.NONUNIQUE_EXON_MAPPING : MarkStatus.UNIQUE_EXON_MAPPING;
                foreach (FtInterval trMatch in trMatches)
                {
                    if (!exonHitGeneNames.Contains(trMatch.Feature.Name))
                    { // If a gene is hit multiple times (internal repeats, hit to both real and splice chr...), we should still annotate it only one time
                        exonHitGeneNames.Add(trMatch.Feature.Name);
                        item.splcToRealChrOffset = 0;
                        MarkResult res = trMatch.Mark(item, trMatch.ExtraData, markStatus);
                        /*annotMatches[res.annotType] = true;*/
                        TotalHitsByAnnotTypeAndBarcode[res.annotType, currentBcIdx] += molCount;
                        TotalHitsByAnnotTypeAndChr[item.chr][res.annotType] += molCount;
                        TotalHitsByAnnotType[res.annotType] += molCount;
                        TotalHitsByBarcode[currentBcIdx] += molCount;
                    }
                }
                if (exonHitGeneNames.Count > 1)
                {
                    exonHitGeneNames.Sort();
                    string combNames = string.Join("#", exonHitGeneNames.ToArray());
                    if (!redundantHits.ContainsKey(combNames))
                        redundantHits[combNames] = molCount;
                    else
                        redundantHits[combNames] += molCount;
                }
            }
            if (!someExonHit && item.chr != annotationChrId)
            { // Annotate all features of molecules that do not map to any transcript
                foreach (FtInterval nonTrMatch in Annotations.GetNonTrMatches(item.chr, item.strand, item.HitMidPos))
                {
                    someAnnotationHit = true;
                    MarkResult res = nonTrMatch.Mark(item, nonTrMatch.ExtraData, MarkStatus.NONEXONIC_MAPPING);
                    /*annotMatches[res.annotType] = true;*/
                    TotalHitsByAnnotTypeAndBarcode[res.annotType, currentBcIdx] += molCount;
                    TotalHitsByAnnotTypeAndChr[item.chr][res.annotType] += molCount;
                    TotalHitsByAnnotType[res.annotType] += molCount;
                    TotalHitsByBarcode[currentBcIdx] += molCount;
                }
            }
            if (item.chr != annotationChrId && !item.hasAltMappings)
            {
                // Add to the motif (base 21 in the motif will be the first base of the read)
                // Subtract one to make it zero-based
                if (DetermineMotifs && someAnnotationHit && Annotations.HasChromosome(item.chr))
                    motifs[currentBcIdx].Add(Annotations.GetChromosome(item.chr), item.hitStartPos - 20 - 1, item.strand);
            }
            if (someAnnotationHit)
            {
                nAnnotatedMappings += molCount;
                if (someExonHit)
                    nExonMappings += molCount;
                /*else if (nonExonWriter != null)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < annotMatches.Length; i++)
                        if (annotMatches[i] == true)
                            sb.Append(" " + AnnotType.GetName(i));
                    nonExonWriter.WriteLine(item.ToString() + " - Annotations: " + sb.ToString());
                }*/
            }
            /*else if (nonAnnotWriter != null)
                nonAnnotWriter.WriteLine(item.ToString());*/
        }

        public void FinishBarcode()
        {
            MakeGeneRndTagProfiles();
            MakeBcWigglePlots();
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
                        continue;
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
            if (!Props.props.GenerateBarcodedWiggle && !Props.props.UseRPKM) return;
            int readLength = MappedTagItem.AverageReadLen;
            WriteBcWiggleStrand(readLength, '+');
            WriteBcWiggleStrand(readLength, '-');
        }

        private void WriteBcWiggleStrand(int readLength, char strand)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            string fileNameHead = string.Format("{0}_{1}_{2}", currentBcIdx, Annotations.Genome.Build, ((strand == '+') ? "_fw" : "_rev"));
            string filePathHead = Path.Combine(Path.GetDirectoryName(currentMapFilePath), fileNameHead);
            string fileByRead = filePathHead + "_byread.wig.gz";
            if (File.Exists(fileByRead)) return;
            StreamWriter writerByRead = fileByRead.OpenWrite();
            writerByRead.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} (+)\" visibility=full", fileNameHead + "_byread", strand);
            StreamWriter writerByMol = null;
            string fileByMol = filePathHead + "_bymolecule.wig.gz";
            if (barcodes.HasRandomBarcodes && !File.Exists(fileByMol))
            {
                writerByMol = fileByMol.OpenWrite();
                writerByMol.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} (+)\" visibility=full", fileNameHead + "_bymolecule", strand);
            }
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
                WriteSNPPositions(fileNameBase);
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
            WriteBarcodeStats(fileNameBase, xmlFile, readCounter);
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
                nReads = nMappedReadsByBarcode[bcIdx];
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </" + tag + ">");
        }

        private void WriteReadStats(ReadCounter readCounter, StreamWriter xmlFile)
        {
            int allBcCount = barcodes.Count;
            int spBcCount = barcodes.GenomeBarcodeIndexes(Annotations.Genome, false).Length;
            xmlFile.WriteLine("  <readfiles>");
            foreach (string path in readCounter.GetReadFiles())
                xmlFile.WriteLine("    <readfile path=\"{0}\" />", path);
            xmlFile.WriteLine("  </readfiles>");
            xmlFile.WriteLine("  <reads>");
            xmlFile.WriteLine("    <title>Read distribution (10^6). [# samples/wells]</title>");
            double totalReads = readCounter.GrandTotal;
            if (totalReads > 0)
            {
                xmlFile.WriteLine("    <point x=\"All reads [{0}] (100%)\" y=\"{1}\" />", allBcCount, totalReads / 1.0E6d);
                int validReads = readCounter.GrandCount(ReadStatus.VALID);
                xmlFile.WriteLine("    <point x=\"Valid STRT reads[{0}] ({1:0%})\" y=\"{2}\" />", allBcCount, validReads / totalReads, validReads / 1.0E6d);
            }
            else
                totalReads = nMappedReads; // Default to nMappedReads if extraction summary files are missing
            xmlFile.WriteLine("    <point x=\"Mapped reads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMappedReads / totalReads, nMappedReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Multireads [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nMaxAltMappingsReads / totalReads, nMaxAltMappingsReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Exon/Splc reads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nExonAnnotatedReads / totalReads, nExonAnnotatedReads / 1.0E6d);
            if (barcodes.HasRandomBarcodes)
                xmlFile.WriteLine("    <point x=\"Duplicate reads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nDuplicateReads / totalReads, nDuplicateReads / 1.0E6d);
            xmlFile.WriteLine("  </reads>");
            xmlFile.WriteLine("  <hits>");
            double dividend = nAnnotatedMappings;
            double reducer = 1.0E6d;
            if (barcodes.HasRandomBarcodes)
            {
                dividend = nMolecules;
                reducer = 1.0E3d;
                xmlFile.WriteLine("    <title>Molecule distribution (10^3). [# samples/wells]</title>");
                xmlFile.WriteLine("    <point x=\"Unique molecules [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMolecules / dividend, nMolecules / reducer);
            }
            else
                xmlFile.WriteLine("    <title>Annotations (10^6) [# samples/wells].\n(N.B.: Multireads give multiple annotations)</title>");
            xmlFile.WriteLine("    <point x=\"Annotated mappings [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nAnnotatedMappings / dividend, nAnnotatedMappings / reducer);
            xmlFile.WriteLine("    <point x=\"Exon mappings [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nExonMappings / dividend, nExonMappings / reducer);
            int nIntronHits = TotalHitsByAnnotType[AnnotType.INTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.AINTR]);
            xmlFile.WriteLine("    <point x=\"Intron hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nIntronHits / dividend, nIntronHits / reducer);
            int nUstrHits = TotalHitsByAnnotType[AnnotType.USTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.AUSTR]);
            xmlFile.WriteLine("    <point x=\"Upstream hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nUstrHits / dividend, nUstrHits / reducer);
            int nDstrHits = TotalHitsByAnnotType[AnnotType.DSTR] + ((Props.props.DirectionalReads) ? 0 : TotalHitsByAnnotType[AnnotType.ADSTR]);
            xmlFile.WriteLine("    <point x=\"Downstream hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nDstrHits / dividend, nDstrHits / reducer);
            if (Props.props.DirectionalReads)
            {
                int numOtherAS = TotalHitsByAnnotType[AnnotType.AUSTR] + TotalHitsByAnnotType[AnnotType.AEXON] + 
                                 TotalHitsByAnnotType[AnnotType.AINTR] + TotalHitsByAnnotType[AnnotType.ADSTR];
                xmlFile.WriteLine("    <point x=\"Loci A-sense [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, numOtherAS / dividend, numOtherAS / reducer);
            }
            xmlFile.WriteLine("    <point x=\"Repeat [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount,
                               TotalHitsByAnnotType[AnnotType.REPT] / dividend, TotalHitsByAnnotType[AnnotType.REPT] / reducer);
            xmlFile.WriteLine("  </hits>");
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
            double nAnnotationsHits = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                nUniqueMolecules += nMoleculesByBarcode[bcIdx];
                nAnnotationsHits += TotalHitsByBarcode[bcIdx];
            }
            xmlFile.WriteLine("  <reads species=\"{0}\">", speciesName);
            string molTitle = (barcodes.HasRandomBarcodes)? "molecule": "read";
            double reducer = (barcodes.HasRandomBarcodes)? 1.0E6d : 1.0E3d;
            xmlFile.WriteLine("    <title>Distribution of {0} hits (10^{3}) by categories in {1} {2} wells</title>",
                              molTitle, speciesBcIndexes.Length, speciesName, (int)Math.Log10(reducer));
            xmlFile.WriteLine("    <point x=\"Mapped {0}s (100%)\" y=\"{1}\" />", molTitle, nUniqueMolecules / reducer);
            xmlFile.WriteLine("    <point x=\"Annotations ({0:0%})\" y=\"{1}\" />", nAnnotationsHits / nUniqueMolecules, nAnnotationsHits / reducer);
            foreach (int annotType in new int[] { AnnotType.EXON, AnnotType.INTR, AnnotType.USTR, AnnotType.DSTR, AnnotType.REPT })
            {
                int numOfType = GetSpeciesAnnotHitCount(speciesBcIndexes, annotType);
                xmlFile.WriteLine("    <point x=\"{0} ({1:0%})\" y=\"{2}\" />", AnnotType.GetName(annotType), numOfType / nUniqueMolecules, numOfType / reducer);
            }
            if (Props.props.DirectionalReads)
            {
                int numAEXON = GetSpeciesAnnotHitCount(speciesBcIndexes, AnnotType.AEXON);
                xmlFile.WriteLine("    <point x=\"AEXON ({0:0%})\" y=\"{1}\" />", numAEXON / nUniqueMolecules, numAEXON / reducer);
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
                if (gf.Name.StartsWith("RNA_SPIKE_") && gf.IsExpressed())
                {
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
            int minValidWellCount = nAnnotatedMappings / genomeBcIndexes.Length / 20;
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
        private void WriteBarcodeStats(string fileNameBase, StreamWriter xmlFile, ReadCounter readCounter)
        {
            StreamWriter barcodeStats = new StreamWriter(fileNameBase + "_barcode_summary.tab");
            string molTitle = (barcodes.HasRandomBarcodes) ? "molecules" : "reads";
            barcodeStats.WriteLine("Total annotated {0}: {1}\n", molTitle, nAnnotatedMappings);
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
            if (readCounter.TotalBarcodeReads.Length == barcodes.Count)
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, readCounter.TotalBarcodeReads,
                                    "READS", "Total reads by barcode", "reads");
            WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, TotalHitsByBarcode,
                                "TOTAL", "Total annotated hits by barcode", "annotated hits");
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
            if (barcodes.HasRandomBarcodes)
            {
                xmlFile.Write("    <barcodestat section=\"labeling efficiency (based on {0} spike mols)\">", Props.props.TotalNumberOfAddedSpikeMolecules);
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    if (genomeBcIndexes.Contains(bcIdx)) xmlFile.Write("    <d>{0:0.###}</d>", labelingEfficiencyByBc[bcIdx]);
                    else xmlFile.Write("    <d>({0:0.###})</d>", labelingEfficiencyByBc[bcIdx]);
                }
                xmlFile.WriteLine("\n    </barcodestat>");
            }
            xmlFile.WriteLine("  </barcodestats>");
        }

        private void WriteTotalByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes,
                                         int[] values, string bCodeLinesTitle, string barcodeStatsTitle, string xmlFileSection)
        {
            bCodeLines.Write(bCodeLinesTitle);
            xmlFile.Write("    <barcodestat section=\"" + xmlFileSection + "\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = values[bcIdx].ToString();
                bCodeLines.Write("\t" + counts[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? counts[bcIdx] : "(" + counts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            bCodeLines.WriteLine();
            barcodeStats.WriteLine("\n" + barcodeStatsTitle + ":\n");
            barcodeStats.WriteLine(MakeDataMatrix(counts, "0"));
        }

        private void WriteFeaturesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            string molTitle = (barcodes.HasRandomBarcodes) ? "molecules" : "reads";
            for (var annotType = 0; annotType < AnnotType.Count; annotType++)
            {
                if (annotType == AnnotType.AREPT) continue;
                string annotName = AnnotType.GetName(annotType);
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
                xmlFile.WriteLine("\n    </barcodestat>");
                bCodeLines.WriteLine();
                barcodeStats.WriteLine("\nTotal {0} mapped to {1}:\n", molTitle, annotName);
                barcodeStats.WriteLine(MakeTotalMatrix(annotType));
                barcodeStats.WriteLine(MakeFracDevStatsMatrix(annotType));
            }
        }

        private void WriteDuplicateMoleculesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            if (!barcodes.HasRandomBarcodes) return;
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
            bCodeLines.WriteLine();
            barcodeStats.WriteLine("\nDuplicated reads filtered away due to same random tag and position, by barcode:\n");
            barcodeStats.WriteLine(MakeDataMatrix(counts, "0"));
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
            return nAnnotatedMappings;
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

        private void WriteSNPPositions(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs.tab").OpenWrite();
            int thres = (int)(SnpAnalyzer.thresholdFractionAltHitsForMixPos * 100);
            int minHitsToTestSNP = (barcodes.HasRandomBarcodes) ? Props.props.MinMoleculesToTestSnp : Props.props.MinReadsToTestSnp;
            string minTxt = (barcodes.HasRandomBarcodes) ? "Molecules" : "Reads";
            snpFile.WriteLine("#(minimum {0} {3}/Pos required to check, limits used heterozygous: {1}-{2}% AltNt and homozygous: >{2}% Alt Nt)",
                              minHitsToTestSNP, thres, 100 - thres, minTxt);
            snpFile.WriteLine("#Gene\tChr\tmRNALeftChrPos\tSNPChrPos\tType\t" + SNPCounter.Header);
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                List<SNPCounter> sumSNPCounters = SnpAnalyzer.GetSnpChrPositions(gf);
                string first = gf.Name + "\t" + gf.Chr + "\t" + gf.Start + "\t";
                foreach (SNPCounter sumCounter in sumSNPCounters)
                {
                    if (sumCounter.nTotal >= minHitsToTestSNP)
                    {
                        int type = SnpAnalyzer.TestSNP(sumCounter);
                        if (type == SnpAnalyzer.REFERENCE) continue;
                        string typeName = (type == SnpAnalyzer.ALTERNATIVE) ? "AltNt" : "MixNt";
                        snpFile.WriteLine(first + sumCounter.posOnChr + "\t" + typeName + "\t" + sumCounter.ToLine());
                        first = "\t\t\t";
                    }
                }
            }
            snpFile.Close();
        }

        private void WriteSnpsByBarcode(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs_by_barcode.tab").OpenWrite();
            SnpAnalyzer.WriteSnpsByBarcode(snpFile, barcodes, Annotations.geneFeatures);
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
            readWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full",
                Path.GetFileNameWithoutExtension(fileNameBase) + "_byread", strand);
            StreamWriter molWriter = null;
            if (barcodes.HasRandomBarcodes)
            {
                molWriter = (fileNameBase + "_" + strandString + "_bymolecule.wig.gz").OpenWrite();
                molWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full",
                    Path.GetFileNameWithoutExtension(fileNameBase) + "_bymolecule", strand);
            }
            int averageReadLength = MappedTagItem.AverageReadLen;
            foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
            {
                string chr = data.Key;
                if (StrtGenome.IsSyntheticChr(chr))
                    continue;
                data.Value.GetWiggle(strand).WriteReadWiggle(readWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr]);
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
                if (Annotations.HasTrOrRepeatMatches(chr, strand, i))
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
