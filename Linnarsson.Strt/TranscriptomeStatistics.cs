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
        private static readonly int libraryDepthSampleReadCountPerBc = 200000;
        private static readonly int libraryDepthSampleMolsCountPerBc = 20000;
        private int trSampleDepth;

        public static readonly int maxHotspotCount = 50;
        public static readonly int minHotspotDistance = 5;
        public static readonly int minMismatchReadCountForSNPDetection = 10;

        StreamWriter nonAnnotWriter = null;
        StreamWriter nonExonWriter = null;

        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private RandomTagFilterByBc randomTagFilter;
        private MappingAdder mappingAdder;

        private SnpRndTagVerifier snpRndTagVerifier;
        private SyntReadReporter TestReporter;

        /// <summary>
        /// Per chromosome hits of each type, separates sense and antisense
        /// </summary>
        Dictionary<string, int[]> TotalHitsByAnnotTypeAndChr;
        /// <summary>
        /// Total exon/splc matching molecules (or reads when no rnd labels are used) per barcode, each molecule/read is counted exactly once
        /// </summary>
        int[] TotalTranscriptMolsByBarcode;
        /// <summary>
        /// Hit counts sorted by type and barcode. Multireads hitting transcripts may be counted several times. Separates sense and antisense matches
        /// </summary>
        int[,] TotalHitsByAnnotTypeAndBarcode;
        /// <summary>
        /// Total hits of each type. Multireads hitting transcripts may be counted several times. Separates sense and antisense matches
        /// </summary>
        int[] TotalHitsByAnnotType;
        /// <summary>
        /// Number of hits to distinct annotations in each barcode (multireads can get a count for each mapping)
        /// </summary>
        int[] TotalHitsByBarcode;
        /// <summary>
        /// Estimates of labelling efficiency based on the known number of spike molecules added to the samples.
        /// </summary>
        double[] labelingEfficiencyByBc;

        AbstractGenomeAnnotations Annotations;
        Barcodes barcodes;
		DnaMotif[] motifs;
        int currentBcIdx = 0;
        string currentMapFilePath;
        private string OutputPathbase;

        /// <summary>
        /// Total number of mapped reads in each barcode
        /// </summary>
        int[] nMappedReadsByBarcode;

        /// <summary>
        /// Total number of mapped reads
        /// </summary>
        int TotalNMappedReads { get { return nMappedReadsByBarcode.Sum(); } }

        /// <summary>
        /// Number of reads that map to some exon/splice
        /// </summary>
        int nExonAnnotatedReads = 0;

        /// <summary>
        /// Number of reads that have alternative genomic mapping positions
        /// </summary>
        int nMultiReads = 0;

        /// <summary>
        /// Per-barcode molecule mappings after filtering mutated rndTags, or read mappings when not using rndTags.
        /// When all alternative exon mappings are annotated, the same molecule/read is counted more than once
        /// </summary>
        int[] nMappingsByBarcode;

        /// <summary>
        /// Total number of molecule mappings after filtering mutated rndTags, or read mappings when not using rndTags.
        /// When all alternative exon mappings are annotated, the same molecule/read is counted more than once
        /// </summary>
        int nMappings { get { return nMappingsByBarcode.Sum(); } }

        /// <summary>
        /// Number of mappings with any annotation
        /// </summary>
        int nAnnotatedMappings = 0;

        private int statsSampleDistPerBarcode;
        List<double> sampledLibraryDepths = new List<double>();
        List<double> sampledUniqueMolecules = new List<double>();
        List<double> sampledExpressedTranscripts = new List<double>();
        Dictionary<int, List<int>> sampledDetectedTranscriptsByBcIdx = new Dictionary<int, List<int>>();
        // For non-rndTagged samples the following to will be identical:
        Dictionary<int, List<int>> sampledUniqueMoleculesByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledUniqueHitPositionsByBcIdx = new Dictionary<int, List<int>>();

        private UpstreamAnalyzer upstreamAnalyzer;
        private PerLaneStats perLaneStats;

        private StreamWriter rndTagProfileByGeneWriter;

        Dictionary<string, int> overlappingGeneFeatures = new Dictionary<string, int>();
        List<string> exonHitGeneNames;
        private string spliceChrId;

        public TranscriptomeStatistics(AbstractGenomeAnnotations annotations, Props props, string outputPathbase)
		{
            this.OutputPathbase = outputPathbase;
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
            TotalTranscriptMolsByBarcode = new int[barcodes.Count];
            TotalHitsByAnnotTypeAndBarcode = new int[AnnotType.Count, barcodes.Count];
            TotalHitsByAnnotTypeAndChr = new Dictionary<string, int[]>();
            foreach (string chr in Annotations.GetChromosomeIds())
                TotalHitsByAnnotTypeAndChr[chr] = new int[AnnotType.Count];
            TotalHitsByAnnotType = new int[AnnotType.Count];
            nMappedReadsByBarcode = new int[barcodes.Count];
            nMappingsByBarcode = new int[barcodes.Count];
            labelingEfficiencyByBc = new double[barcodes.Count];
            exonHitGeneNames = new List<string>(100);
            spliceChrId = Annotations.Genome.Annotation;
            randomTagFilter = new RandomTagFilterByBc(barcodes, Annotations.GetChromosomeIds());
            mappingAdder = new MappingAdder(annotations, randomTagFilter, barcodes);
            statsSampleDistPerBarcode = sampleDistForAccuStats / barcodes.Count;
            if (props.AnalyzeSeqUpstreamTSSite)
                upstreamAnalyzer = new UpstreamAnalyzer(Annotations, barcodes);
            perLaneStats = new PerLaneStats(barcodes);
        }

        public void SetSyntReadReporter(string syntLevelFile)
        {
            TestReporter = new SyntReadReporter(syntLevelFile, Annotations.Genome.GeneVariants, OutputPathbase, Annotations.geneFeatures);
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
            trSampleDepth = (barcodes.HasRandomBarcodes) ? libraryDepthSampleMolsCountPerBc : libraryDepthSampleReadCountPerBc;
            if (mapFilePaths.Count == 0)
                return;
            mapFilePaths.Sort(CompareMapFiles); // Important to have them sorted by barcode
            if (Props.props.AnalyzeSNPs)
                RegisterPotentialSNPs(mapFilePaths, averageReadLen);
            if (Props.props.SnpRndTagVerification && barcodes.HasRandomBarcodes)
                snpRndTagVerifier = new SnpRndTagVerifier(Props.props, Annotations.Genome);
            string mapFileName = Path.GetFileName(mapFilePaths[0]);
            currentBcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
            Console.Write("Annotatating {0} map files", mapFilePaths.Count);

            if (Props.props.DebugAnnotation)
            {
                if (!Directory.Exists(Path.GetDirectoryName(OutputPathbase)))
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPathbase));
                nonAnnotWriter = new StreamWriter(OutputPathbase + "_NONANNOTATED.tab");
                nonExonWriter = new StreamWriter(OutputPathbase + "_NONEXON.tab");
            }

            HashSet<int> usedBcIdxs = new HashSet<int>();
            List<string> bcMapFilePaths = new List<string>();
            foreach (string mapFilePath in mapFilePaths)
            {
                Console.Write(".");
                mapFileName = Path.GetFileName(mapFilePath);
                int bcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
                if (bcIdx != currentBcIdx)
                {
                    ProcessBarcodeMapFiles(bcMapFilePaths);
                    bcMapFilePaths.Clear();
                    if (usedBcIdxs.Contains(bcIdx))
                        throw new Exception("Program or map file naming error: Revisiting an already analyzed barcode (" + bcIdx + ") is not allowed.");
                    usedBcIdxs.Add(bcIdx);
                    currentBcIdx = bcIdx;
                }
                bcMapFilePaths.Add(mapFilePath);
            }
            if (bcMapFilePaths.Count > 0)
                ProcessBarcodeMapFiles(bcMapFilePaths);
            Console.WriteLine();

            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter.Close(); nonAnnotWriter.Dispose();
                nonExonWriter.Close(); nonExonWriter.Dispose();
            }
        }

        private void RegisterPotentialSNPs(List<string> mapFilePaths, int averageReadLen)
        {
            MapFileSnpFinder mfsf = new MapFileSnpFinder(barcodes);
            mfsf.ProcessMapFiles(mapFilePaths);
            int nSNPs = randomTagFilter.SetupSNPCounters(averageReadLen, mfsf.IterSNPLocations(minMismatchReadCountForSNPDetection));
            Console.WriteLine("Registered {0} potential expressed SNPs (positions with >= {1} mismatch reads).", nSNPs, minMismatchReadCountForSNPDetection);
        }

        /// <summary>
        /// Annotate a set of map files that have the same barcode
        /// </summary>
        /// <param name="bcMapFilePaths">Paths to files where all reads have the same barcode</param>
        private void ProcessBarcodeMapFiles(List<string> bcMapFilePaths)
        {
            foreach (string mapFilePath in bcMapFilePaths)
            {
                currentMapFilePath = mapFilePath;
                if (!File.Exists(mapFilePath))
                    continue;
                MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, barcodes);
                if (mapFileReader == null)
                    throw new Exception("Unknown read map file type : " + mapFilePath);
                int nMappedReadsByFile = 0;
                perLaneStats.BeforeFile(currentBcIdx, nMappedReadsByBarcode[currentBcIdx], mappingAdder.NUniqueReadSignatures(currentBcIdx),
                                        randomTagFilter.GetNumDistinctMappings());
                foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
                {
                    if (mappingAdder.Add(mrm))
                        nExonAnnotatedReads++;
                    if (snpRndTagVerifier != null)
                        snpRndTagVerifier.Add(mrm);
                    if ((++nMappedReadsByBarcode[currentBcIdx]) % statsSampleDistPerBarcode == 0)
                        SampleReadStatistics(statsSampleDistPerBarcode);
                    if ((nMappedReadsByBarcode[currentBcIdx]) == libraryDepthSampleReadCountPerBc)
                    {
                        sampledLibraryDepths.Add(randomTagFilter.GetNumDistinctMappings());
                        sampledUniqueMolecules.Add(mappingAdder.NUniqueReadSignatures(currentBcIdx));
                    }
                    if (++nMappedReadsByFile == PerLaneStats.nMappedReadsPerFileAtSample)
                        perLaneStats.AfterFile(mapFilePath, nMappedReadsByBarcode[currentBcIdx], mappingAdder.NUniqueReadSignatures(currentBcIdx),
                                                            randomTagFilter.GetNumDistinctMappings());
                    if (mrm.HasAltMappings) nMultiReads++;
                    //else if (upstreamAnalyzer != null)
                    //    upstreamAnalyzer.CheckSeqUpstreamTSSite(mrm[0], currentBcIdx); // Analysis on raw read bases
                }
            }
            SampleReadStatistics(nMappedReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode);
            List<string> ctrlChrId = new List<string>();
            if (randomTagFilter.chrTagDatas.ContainsKey("CTRL"))
            { // First process CTRL chromosome to get the labeling efficiency
                ctrlChrId.Add("CTRL");
                foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, true))
                    Annotate(mtitem);
                double labelingEfficiency = Annotations.GetEfficiencyFromSpikes(currentBcIdx);
                TagItem.LabelingEfficiency = labelingEfficiency;
                labelingEfficiencyByBc[currentBcIdx] = labelingEfficiency;
            }
            foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, false))
                Annotate(mtitem);
            FinishBarcode();
        }

        private void FinishBarcode()
        {
            MakeGeneRndTagProfiles();
            MakeBcWigglePlots();
            randomTagFilter.FinishBarcode();
        }

        public void SampleReadStatistics(int numReadsInBin)
        {
            if (!sampledUniqueHitPositionsByBcIdx.ContainsKey(currentBcIdx))
            {
                sampledUniqueMoleculesByBcIdx[currentBcIdx] = new List<int>();
                sampledUniqueHitPositionsByBcIdx[currentBcIdx] = new List<int>();
            }
            sampledUniqueMoleculesByBcIdx[currentBcIdx].Add(mappingAdder.NUniqueReadSignatures(currentBcIdx));
            sampledUniqueHitPositionsByBcIdx[currentBcIdx].Add(randomTagFilter.GetNumDistinctMappings());
        }

        public void Annotate(MappedTagItem item)
        {
            int molCount = item.MolCount;
            nMappingsByBarcode[currentBcIdx] += molCount;
            bool someAnnotationHit = false;
            bool someExonHit = false;
            exonHitGeneNames.Clear();
            foreach (FtInterval trMatch in Annotations.IterTranscriptMatches(item.chr, item.strand, item.HitMidPos))
            {
                someExonHit = someAnnotationHit = true;
                MarkStatus markStatus = (IterTranscriptMatchers.HasVariants || item.hasAltMappings) ? MarkStatus.NONUNIQUE_EXON_MAPPING : MarkStatus.UNIQUE_EXON_MAPPING;
                    if (!exonHitGeneNames.Contains(trMatch.Feature.Name))
                    { // If a gene is hit multiple times (happens if two diff. splices have same seq.), we should annotate it only once
                        exonHitGeneNames.Add(trMatch.Feature.Name);
                        item.splcToRealChrOffset = 0;
                        int annotType = trMatch.Mark(item, trMatch.ExtraData, markStatus);
                        TotalHitsByAnnotTypeAndBarcode[annotType, currentBcIdx] += molCount;
                        TotalHitsByAnnotTypeAndChr[item.chr][annotType] += molCount;
                        TotalHitsByAnnotType[annotType] += molCount;
                        TotalHitsByBarcode[currentBcIdx] += molCount;
                    }
            }
            if (exonHitGeneNames.Count > 1)
            {
                exonHitGeneNames.Sort();
                string combNames = string.Join("#", exonHitGeneNames.ToArray());
                if (!overlappingGeneFeatures.ContainsKey(combNames))
                    overlappingGeneFeatures[combNames] = molCount;
                else
                    overlappingGeneFeatures[combNames] += molCount;
            }
            if (!someExonHit && item.chr != spliceChrId)
            { // Annotate all features of molecules that do not map to any transcript
                foreach (FtInterval nonTrMatch in Annotations.IterNonTrMatches(item.chr, item.strand, item.HitMidPos))
                {
                    someAnnotationHit = true;
                    int annotType = nonTrMatch.Mark(item, nonTrMatch.ExtraData, MarkStatus.NONEXONIC_MAPPING);
                    TotalHitsByAnnotTypeAndBarcode[annotType, currentBcIdx] += molCount;
                    TotalHitsByAnnotTypeAndChr[item.chr][annotType] += molCount;
                    TotalHitsByAnnotType[annotType] += molCount;
                    TotalHitsByBarcode[currentBcIdx] += molCount;
                }
            }
            if (item.chr != spliceChrId && !item.hasAltMappings)
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
                {
                    TotalTranscriptMolsByBarcode[currentBcIdx] += molCount;
                    if (upstreamAnalyzer != null)
                        upstreamAnalyzer.CheckSeqUpstreamTSSite(item, currentBcIdx);
                }
            }
            int t = nMappingsByBarcode[currentBcIdx] - trSampleDepth;
            if (t > 0 && t <= molCount) // Sample if we just passed the sampling point with current MappedTagItem
                sampledExpressedTranscripts.Add(Annotations.GetNumExpressedGenes(currentBcIdx));
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
                                rndTagProfileByGeneWriter.Write("\t{0}", count);
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
            string fileNameHead = string.Format("{0}_{1}", currentBcIdx, ((strand == '+') ? "fw" : "rev"));
            string filePathHead = Path.Combine(Path.GetDirectoryName(currentMapFilePath), fileNameHead);
            string fileByRead = filePathHead + "_byread.wig.gz";
            string fileByMol = filePathHead + "_bymolecule.wig.gz";
            if (File.Exists(fileByRead)) return;
            using (StreamWriter writerByRead = fileByRead.OpenWrite())
            using (StreamWriter writerByMol = (barcodes.HasRandomBarcodes && !File.Exists(fileByMol)? fileByMol.OpenWrite() : null))
            {
                writerByRead.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full", fileNameHead + "_byread", strand);
                if (writerByMol != null)
                    writerByMol.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full", fileNameHead + "_bymolecule", strand);
                int strandSign = (strand == '+') ? 1 : -1;
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
            }
        }

		/// <summary>
		///  Save all the statistics to a set of files
		/// </summary>
        /// <param name="readCounter">Holder of types of reads in input</param>
		/// <param name="OutputPathbase">A path and a filename prefix that will used to create all output files, e.g. "/data/Sample12_"</param>
		public void SaveResult(ReadCounter readCounter, ResultDescription resultDescr)
		{
            if (upstreamAnalyzer != null)
                upstreamAnalyzer.WriteUpstreamStats(OutputPathbase);
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            WriteHitProfilesByBarcode();
            WriteRedundantExonHits();
            WriteASExonDistributionHistogram();
            WriteSummary(readCounter, resultDescr);
            int averageReadLen = MappedTagItem.AverageReadLen;
            Annotations.SaveResult(OutputPathbase, averageReadLen);
            if (snpRndTagVerifier != null)
                snpRndTagVerifier.Verify(OutputPathbase);
            if (Props.props.AnalyzeSNPs)
                WriteSnps();
            if (DetermineMotifs)
                WriteSequenceLogos();
            if (Props.props.GenerateWiggle)
            {
                WriteWriggle();
                WriteHotspots();
            }
            if (rndTagProfileByGeneWriter != null)
            {
                rndTagProfileByGeneWriter.Close();
                rndTagProfileByGeneWriter.Dispose();
            }
            //WriteLogStats(fileNameBase); // Only for debugging
        }

        /// <summary>
        /// Debugging output for counting of reads & molecules
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteLogStats()
        {
            string xmlPath = OutputPathbase + "_stats.log";
            using (StreamWriter xmlFile = new StreamWriter(xmlPath))
            {
                for (int i = 0; i < AnnotType.Count; i++)
                    xmlFile.WriteLine("TotalHitsByAnnotType[{0}]={1}", AnnotType.GetName(i), TotalHitsByAnnotType[i]);
                int[] nmh = new int[AnnotType.Count];
                int[] h = new int[AnnotType.Count];
                for (int i = 0; i < AnnotType.Count; i++)
                {
                    foreach (GeneFeature gr in Annotations.geneFeatures.Values)
                    {
                        h[i] += gr.HitsByAnnotType[i];
                        nmh[i] += gr.NonMaskedHitsByAnnotType[i];
                    }
                }
                for (int i = 0; i < AnnotType.Count; i++)
                    xmlFile.WriteLine("sum NonMaskedHitsByAnnotType[{0}]={1}", AnnotType.GetName(i), nmh[i]);
                for (int i = 0; i < AnnotType.Count; i++)
                    xmlFile.WriteLine("sum HitsByAnnotType[{0}]={1}", AnnotType.GetName(i), h[i]);
            }
        }

        /// <summary>
        /// Write sequence logo data for each barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteSequenceLogos()
        {
            string logoDir = Directory.CreateDirectory(OutputPathbase + "_sequence_logos").FullName;
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i].Save(Path.Combine(logoDir, barcodes.Seqs[i] + "_motif.txt"));
            }
        }

        /// <summary>
        /// Write the "summary.xml" file
        /// </summary>
        /// <param name="readCounter"></param>
        /// <param name="resultDescr"></param>
        private void WriteSummary(ReadCounter readCounter, ResultDescription resultDescr)
        {
            string xmlPath = OutputPathbase + "_summary.xml";
            using (StreamWriter xmlFile = new StreamWriter(xmlPath))
            {
                xmlFile.WriteLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
                xmlFile.WriteLine("<strtSummary project=\"{0}\">", Path.GetDirectoryName(OutputPathbase));
                WriteSettings(xmlFile, resultDescr);
                WriteReadStats(readCounter, xmlFile);
                xmlFile.WriteLine("  <librarycomplexity>\n" +
                                  "    <title>Median values from [indicated number of] barcodes</title>\n" +
                                  "    <point x=\"Unique molecules [{6}] after {0} reads\" y=\"{2}\" />\n" +
                                  "    <point x=\"Distinct mappings [{7}] after {0} reads\" y=\"{1}\" />\n" +
                                  "    <point x=\"Expressed transcripts [{8}] after {3} {5}\" y=\"{4}\" />\n" +
                                  "  </librarycomplexity>", libraryDepthSampleReadCountPerBc,
                                     DefaultMedian(sampledLibraryDepths, "N/A"), DefaultMedian(sampledUniqueMolecules, "N/A"),
                                     trSampleDepth, DefaultMedian(sampledExpressedTranscripts, "N/A"),
                                     (barcodes.HasRandomBarcodes) ? "molecules" : "reads", sampledLibraryDepths.Count,
                                     sampledUniqueMolecules.Count, sampledExpressedTranscripts.Count);
                WriteReadsBySpecies(xmlFile);
                WriteFeatureStats(xmlFile);
                WriteSenseAntisenseStats(xmlFile);
                WriteHitsByChromosome(xmlFile);
                WriteMappingDepth(xmlFile);
                WritePerLaneStats(xmlFile);
                AddSpikes(xmlFile);
                Annotations.WriteSpikeDetection(xmlFile);
                Add5To3PrimeHitProfile(xmlFile);
                AddCVHistogram(xmlFile);
                WriteBarcodeStats(xmlFile, readCounter);
                WriteRandomFilterStats(xmlFile);
                xmlFile.WriteLine("</strtSummary>");
            }
            if (!Environment.OSVersion.VersionString.Contains("Microsoft"))
            {
                CmdCaller.Run("php", "make_html_summary.php " + xmlPath);
            }
        }

        private void WriteSettings(StreamWriter xmlFile, ResultDescription resultDescr)
        {
            xmlFile.WriteLine("  <settings>");
            xmlFile.WriteLine("    <BowtieIndexVersion>{0}</BowtieIndexVersion>", resultDescr.bowtieIndexVersion);
            xmlFile.WriteLine("    <DirectionalReads>{0}</DirectionalReads>", Props.props.DirectionalReads);
            xmlFile.WriteLine("    <UseRPKM>{0}</UseRPKM>", Props.props.UseRPKM);
            xmlFile.WriteLine("    <MaxFeatureLength>{0}</MaxFeatureLength>", Props.props.MaxFeatureLength);
            xmlFile.WriteLine("    <GeneFeature5PrimeExtension>{0}</GeneFeature5PrimeExtension>", Props.props.GeneFeature5PrimeExtension);
            xmlFile.WriteLine("    <LocusFlankLength>{0}</LocusFlankLength>", Props.props.LocusFlankLength);
            xmlFile.WriteLine("    <UseMost5PrimeExonMapping>{0}</UseMost5PrimeExonMapping>", Props.props.UseMost5PrimeExonMapping);
            if (Props.props.AnalyzeSNPs && barcodes.HasRandomBarcodes)
                xmlFile.WriteLine("    <MinMoleculesToTestSnp>{0}</MinMoleculesToTestSnp>", Props.props.MinMoleculesToTestSnp);
            if (Props.props.AnalyzeSNPs && !barcodes.HasRandomBarcodes)
                xmlFile.WriteLine("    <MinReadsToTestSnp>{0}</MinReadsToTestSnp>", Props.props.MinReadsToTestSnp);
            xmlFile.WriteLine("  </settings>");
        }

        /// <summary>
        /// Calc. median of values, or return defaultValue on any error
        /// </summary>
        /// <param name="values"></param>
        /// <param name="defaultValue"></param>
        /// <returns>Median as a string, or defaultValue on error</returns>
        private string DefaultMedian(List<double> values, string defaultValue)
        {
            try
            {
                return string.Format("{0}", DescriptiveStatistics.Median(values));
            }
            catch
            { }
            return defaultValue;
        }

        private void WriteMappingDepth(StreamWriter xmlFile)
        {
            if (barcodes.Count > 24)
            {
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings in odd-numbered barcode as fn. of reads processed",
                                       sampledUniqueHitPositionsByBcIdx, 0, 2);
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings in even-numbered barcode as fn. of reads processed",
                                       sampledUniqueHitPositionsByBcIdx, 1, 2);
            }
            else
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings in each barcode as fn. of reads processed",
                                       sampledUniqueHitPositionsByBcIdx, 0, 1);
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (!barcodes.HasRandomBarcodes) return;
            xmlFile.WriteLine("  <randomtagfrequence>");
            xmlFile.WriteLine("    <title>Number of reads detected in each random tag</title>");
            xmlFile.WriteLine("    <xtitle>Random tag index (AAAA...TTTT)</xtitle>");
            for (int i = 0; i < randomTagFilter.nReadsByRandomTag.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", barcodes.MakeRandomTag(i), randomTagFilter.nReadsByRandomTag[i]);
            xmlFile.WriteLine("  </randomtagfrequence>");
            xmlFile.WriteLine("  <nuniqueateachrandomtagcoverage>");
            xmlFile.WriteLine("    <title>Unique alignmentposition-barcodes as fn. of # random tags they occur in</title>");
            xmlFile.WriteLine("    <xtitle>Number of different random tags</xtitle>");
            for (int i = 0; i < randomTagFilter.nCasesPerRandomTagCount.Length; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.nCasesPerRandomTagCount[i]);
            xmlFile.WriteLine("  </nuniqueateachrandomtagcoverage>");
            WriteAccuMoleculesByBc(xmlFile, "moleculedepthbybc", "Distinct detected molecules in each barcode as fn. of reads processed",
                                   sampledUniqueMoleculesByBcIdx, 0, 1);
            xmlFile.WriteLine("  <moleculereadscountshistogram>");
            xmlFile.WriteLine("    <title>Distribution of number of times every unique molecule has been observed</title>");
            xmlFile.WriteLine("    <xtitle>Number of observations (reads)</xtitle>");
            for (int i = 1; i < randomTagFilter.moleculeReadCountsHistogram.Length; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.moleculeReadCountsHistogram[i]);
            xmlFile.WriteLine("  </moleculereadscountshistogram>");
        }

        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data, int start, int step)
        {
            xmlFile.WriteLine("  <{0}>", tag);
            xmlFile.WriteLine("    <title>{0}</title>", title);
            xmlFile.WriteLine("    <xtitle>Millions of reads processed</xtitle>");
            int[] bcIndices = data.Keys.ToArray();
            Array.Sort(bcIndices);
            for (int bII = start; bII < bcIndices.Length; bII += step)
            {
                int bcIdx = bcIndices[bII];
                List<int> curve = data[bcIdx];
                string legend = string.Format("{0}[{1}]", barcodes.Seqs[bcIdx], barcodes.GetWellId(bcIdx));
                xmlFile.WriteLine("      <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  legend, (bcIdx * 47) % 255, (bcIdx * 21) % 255, (255 - (60 * bcIdx % 255)));
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
            xmlFile.WriteLine("  </{0}>", tag);
        }

        private void WritePerLaneStats(StreamWriter xmlFile)
        {
            double meanFrac0 = perLaneStats.GetMeanOfLaneFracMeans();
            WritePerLaneStatsSection(xmlFile, "low", 0.0, meanFrac0);
            WritePerLaneStatsSection(xmlFile, "high", meanFrac0, 10.0);
        }

        private void WritePerLaneStatsSection(StreamWriter xmlFile, string sectionTitle, double minF, double maxF)
        {
            xmlFile.WriteLine("  <fracuniqueperlane>");
            string type = barcodes.HasRandomBarcodes ? "molecules" : "mappings";
            xmlFile.WriteLine("    <title>Fraction ({0}) distinct {2} among first {1} mapped reads in each lane</title>",
                              sectionTitle, PerLaneStats.nMappedReadsPerFileAtSample, type);
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                List<Pair<string, double>> data = (barcodes.HasRandomBarcodes)?
                                                        perLaneStats.GetUniqueMolsPerMappedReads(bcIdx) : 
                                                        perLaneStats.GetDistinctMappingsPerMappedReads(bcIdx);
                if (data[0].Second < minF || data[0].Second >= maxF)
                    continue;
                string legend = string.Format("{0} [{1}]", barcodes.Seqs[bcIdx], barcodes.GetWellId(bcIdx));
                xmlFile.WriteLine("    <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  legend, (bcIdx * 47) % 255, (bcIdx * 21) % 255, (255 - (60 * bcIdx % 255)));
                foreach (Pair<string, double> laneAndFrac in data)
                    xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1:0.0000}\" />", laneAndFrac.First, laneAndFrac.Second);
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </fracuniqueperlane>");
        }

        private void WriteReadStats(ReadCounter readCounter, StreamWriter xmlFile)
        {
            int allBcCount = barcodes.Count;
            int[] speciesBarcodes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            int spBcCount = speciesBarcodes.Length;
            xmlFile.WriteLine("  <readfiles>");
            foreach (string path in readCounter.GetReadFiles())
                xmlFile.WriteLine("    <readfile path=\"{0}\" />", path);
            xmlFile.WriteLine("  </readfiles>");
            double allBcReads = readCounter.GrandTotal;
            if (allBcReads > 0)
            {
                xmlFile.WriteLine("  <readstatus>");
                xmlFile.WriteLine("    <title>Reads sorted by errors and artefacts.</title>");
                for (int status = 0; status < ReadStatus.Length; status++)
                    xmlFile.WriteLine("    <point x=\"{0} ({1:0.00%})\" y=\"{2}\" />", ReadStatus.categories[status],
                                      (readCounter.GrandCount(status) / allBcReads), readCounter.GrandCount(status));
                xmlFile.WriteLine("  </readstatus>");
            }
            xmlFile.WriteLine("  <reads>");
            xmlFile.WriteLine("    <title>Read distribution (10^6). [#samples]</title>");
            double speciesReads = readCounter.TotalReads(speciesBarcodes);
            if (speciesReads > 0 && allBcReads > 0)
            {
                xmlFile.WriteLine("    <point x=\"All PF reads [{0}]\" y=\"{1}\" />", allBcCount, allBcReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Barcoded as {0} [{1}] (100%)\" y=\"{2}\" />", Annotations.Genome.Abbrev, spBcCount, speciesReads / 1.0E6d);
                int validReads = readCounter.ValidReads(speciesBarcodes);
                xmlFile.WriteLine("    <point x=\"Valid STRT [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, validReads / speciesReads, validReads / 1.0E6d);
            }
            else if (allBcReads > 0)
            { // Old versions of extraction summary files without the total-reads-per-barcode data
                speciesReads = allBcReads;
                xmlFile.WriteLine("    <point x=\"All PF reads [{0}] (100%)\" y=\"{1}\" />", allBcCount, allBcReads / 1.0E6d);
                int validReads = readCounter.ValidReads(speciesBarcodes);
                xmlFile.WriteLine("    <point x=\"Valid STRT [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, validReads / speciesReads, validReads / 1.0E6d);
            }
            else
                speciesReads = TotalNMappedReads; // Default to nMappedReads if extraction summary files are missing
            xmlFile.WriteLine("    <point x=\"Mapped [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, TotalNMappedReads / speciesReads, TotalNMappedReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Multireads [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMultiReads / speciesReads, nMultiReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Exon/Splc [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nExonAnnotatedReads / speciesReads, nExonAnnotatedReads / 1.0E6d);
            if (barcodes.HasRandomBarcodes)
                xmlFile.WriteLine("    <point x=\"Duplicates [{0}] ({1:0%})\" y=\"{2}\" />", 
                                  spBcCount, mappingAdder.TotalNDuplicateReads / speciesReads, mappingAdder.TotalNDuplicateReads / 1.0E6d);
            xmlFile.WriteLine("  </reads>");
            xmlFile.WriteLine("  <hits>");
            int nAllHits = TotalHitsByAnnotType.Sum();
            double dividend = nAllHits;
            double reducer = 1.0E6d;
            string multiReadMethod = (Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping) ?
                "[Multireads are mapped only with their most 5' transcript]" : "[Multireads are mapped with all their transcript hits]";
            if (barcodes.HasRandomBarcodes)
            {
                dividend = nMappings;
                reducer = 1.0E3d;
                xmlFile.WriteLine("    <title>Molecule mappings distribution (10^3). [#samples].\n{0}</title>", multiReadMethod);
            }
            else
                xmlFile.WriteLine("    <title>Read mappings distribution (10^6) [#samples].\n{0}</title>", multiReadMethod);
            xmlFile.WriteLine("    <point x=\"Mappings [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, nMappings / dividend, nMappings / reducer);
            xmlFile.WriteLine("    <point x=\"Annotated [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nAnnotatedMappings / dividend, nAnnotatedMappings / reducer);
            xmlFile.WriteLine("    <point x=\"Feature hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nAllHits / dividend, nAllHits / reducer);
            int nExonMappings = TotalTranscriptMolsByBarcode.Sum();
            xmlFile.WriteLine("    <point x=\"Transcript hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nExonMappings / dividend, nExonMappings / reducer);
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
                xmlFile.WriteLine("    <point x=\"Loci A-sense hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, numOtherAS / dividend, numOtherAS / reducer);
            }
            xmlFile.WriteLine("    <point x=\"Repeat hits [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount,
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
                nUniqueMolecules += nMappingsByBarcode[bcIdx];
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
            xmlFile.WriteLine("    <title>Sense and Antisense hits per kb feature length</title>");
            foreach (int t in AnnotType.GetSenseTypes())
            {
                int totSense = Annotations.GetTotalAnnotCounts(t, true);
                int at = AnnotType.MakeAntisense(t);
                int totASense = Annotations.GetTotalAnnotCounts(at, true);
                string ratio = "1:0";
                int totLen = Annotations.GetTotalAnnotLength(at, true);
                if (totASense > 0)
                    ratio = string.Format("{0:0.0}:1", totSense / (double)totASense);
                double sensePerKb = 1000.0d * (totSense / (double)totLen);
                double antiPerKb = 1000.0d * (totASense / (double)totLen);
                if (double.IsNaN(sensePerKb) || double.IsNaN(antiPerKb))
                    sensePerKb = antiPerKb = 0.0;
                xmlFile.WriteLine("   <point x=\"{0}#br#{1}\" y=\"{2:0.##}\" y2=\"{3:0.##}\" />", AnnotType.GetName(t),
                                  ratio, sensePerKb, antiPerKb);
            }
            int reptLen = Annotations.GetTotalAnnotLength(AnnotType.REPT);
            int reptCount = TotalHitsByAnnotType[AnnotType.REPT];
            double reptPerKb = reptCount / (double)reptLen;
            if (double.IsNaN(reptPerKb))
                reptPerKb = 0.0;
            xmlFile.WriteLine("   <point x=\"REPT#br#1:1\" y=\"{0:0.###}\" y2=\"{0:0.####}\" />", reptPerKb);
            xmlFile.WriteLine("  </senseantisense>");
        }

        private void WriteHitsByChromosome(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <senseantisensebychr>");
            xmlFile.WriteLine("    <title>% of hits to Sense/Antisense exons [ratio below] by chromosome</title>");
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
                string c = (chr.Length > 5)? chr.Substring(0,2) + ".." + chr.Substring(chr.Length - 2) : chr;
                double nSense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.EXON];
                double nAsense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.AEXON];
                string ratio = (nAsense == 0)? "1:0" : string.Format("{0:0}", nSense / (double)nAsense);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  c, ratio, 100.0d *(nSense / (double)TotalNMappedReads), 100.0d * (nAsense / (double)TotalNMappedReads));
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
                if (gf.IsSpike() && gf.IsExpressed())
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
                    string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                    if (ds.Count > 0)
                        sb.Append(string.Format("    <point x=\"#{0}\" y=\"{1:0.###}\" error=\"{2:0.###}\" />\n", spikeId, mean, ds.StandardDeviation()));
                    else
                        sb.Append(string.Format("    <point x=\"#{0}\" y=\"0.0\" error=\"0.0\" />\n", spikeId));
                }
            }
            sb.Append("  </spikes>");
            if (anySpikeDetected)
                xmlFile.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Write 5'->3' hit profiles for spikes and transcripts to "summary.xml" file
        /// </summary>
        /// <param name="xmlFile"></param>
        private void Add5To3PrimeHitProfile(StreamWriter xmlFile)
        {
            int averageReadLen = MappedTagItem.AverageReadLen;
            xmlFile.WriteLine("  <hitprofile>");
            xmlFile.WriteLine("  <title>5'->3' read distr. Red=Transcripts/Blue=Spikes</title>");
            xmlFile.WriteLine("	 <xtitle>Relative pos within transcript</xtitle>");
            int trLenBinSize = 500;
            int trLenBinHalfWidth = trLenBinSize / 2;
            int trLenBinStep = 1500;
            int trLen1stBinMid = 500;
            int trLen1stBinStart = trLen1stBinMid - trLenBinHalfWidth;
            int trLenBinCount = 4;
            int nSections = 20;
            int minHitsPerGene = (barcodes.HasRandomBarcodes) ? 50 : nSections * 10;
            int maxHitsPerGene = (barcodes.HasRandomBarcodes) ? (int)(0.7 * barcodes.RandomBarcodeCount * barcodes.Count) : int.MaxValue;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[trLenBinCount, nSections];
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[trLenBinIdx, section] = new DescriptiveStatistics();
            }
            int[] geneCounts = new int[trLenBinCount];
            int nMaxShownSpikes = 12;
            int spikeColor = 0x00FFFF;
            int spikeColorStep = -(0xFF00 / nMaxShownSpikes);
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.GetTranscriptHits() < minHitsPerGene || gf.GetTranscriptHits() > maxHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double sectionSize = (trLen - averageReadLen) / (double)nSections;
                int[] trSectionCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, sectionSize, Props.props.DirectionalReads, averageReadLen);
                if (trSectionCounts.Length == 0) continue;
                double trTotalCounts = trSectionCounts.Sum();
                if (trTotalCounts == 0.0) continue;
                if (!gf.IsSpike())
                {
                    if (trLen < trLen1stBinStart || (trLen - trLen1stBinStart) % trLenBinStep > trLenBinSize)
                        continue;
                    int trLenBin = (trLen - trLen1stBinStart) / trLenBinStep;
                    if (trLenBin >= trLenBinCount) continue;
                    for (int section = 0; section < nSections; section++)
                        binnedEfficiencies[trLenBin, section].Add(trSectionCounts[section] / trTotalCounts);
                    geneCounts[trLenBin]++;
                }
                else
                {
                    if (--nMaxShownSpikes <= 0) continue;
                    string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                    xmlFile.WriteLine("    <curve legend=\"#{0} {1}bp\" color=\"#{2:X6}\">", spikeId, trLen, spikeColor);
                    spikeColor += spikeColorStep;
                    for (int section = 0; section < nSections; section++)
                    {
                        double eff = trSectionCounts[section] / trTotalCounts;
                        double fracPos = (section + 0.5D) / (double)nSections;
                        xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", fracPos, eff);
                    }
                    xmlFile.WriteLine("    </curve>");
                }
            }
            int geneColor = 0xFFFF00;
            int geneColorStep = -(0xFF00 / trLenBinCount);
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
                Console.WriteLine("ERROR: CVs.Count={0} minCV= {1} maxCV= {2}", CVs.Length, minCV, maxCV);
        }

        /// <summary>
        /// Write plate layout formatted statistics for hits by barcodes.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteBarcodeStats(StreamWriter xmlFile, ReadCounter readCounter)
        {
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome);
            string[] trCounts = Array.ConvertAll<int, string>(Annotations.SampleBarcodeExpressedGenes(), (x => x.ToString()));
            using (StreamWriter barcodeStats = new StreamWriter(OutputPathbase + "_barcode_summary.tab"))
            using (StreamWriter bCodeLines = new StreamWriter(OutputPathbase + "_barcode_oneliners.tab"))
            {
                string molTitle = (barcodes.HasRandomBarcodes) ? "molecules" : "reads";
                barcodeStats.WriteLine("Total annotated {0}: {1}\n", molTitle, nAnnotatedMappings);
                xmlFile.WriteLine("  <barcodestats>");
                xmlFile.Write("    <barcodestat section=\"wellids\">");
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    xmlFile.Write("    <d>{0}</d>", barcodes.GetWellId(bcIdx));
                }
                xmlFile.WriteLine("\n    </barcodestat>");
                WriteBarcodes(xmlFile, barcodeStats, bCodeLines);
                if (barcodes.SpeciesByWell != null)
                    WriteSpeciesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
                if (readCounter.ValidReadsByBarcode.Length == barcodes.Count)
                {
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, readCounter.ValidReadsByBarcode,
                                        "BARCODEDREADS", "Total barcoded reads by barcode", "barcoded reads");
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, readCounter.ValidReadsByBarcode,
                                        "VALIDSTRTREADS", "Total valid STRT reads by barcode", "valid STRT reads");
                }
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, TotalHitsByBarcode,
                                    "HITS", "Total annotated hits by barcode", "annotated hits");
                WriteDuplicateMoleculesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
                WriteFeaturesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
                barcodeStats.WriteLine("Transcripts detected in each barcode:");
                barcodeStats.WriteLine(MakeDataMatrix(trCounts, "0"));
            }
            xmlFile.Write("    <barcodestat section=\"transcripts\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? trCounts[bcIdx] : string.Format("({0})", trCounts[bcIdx]);
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            xmlFile.Write("    <barcodestat section=\"transcript detecting {0}\">", (barcodes.HasRandomBarcodes)? "molecules" : "reads");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", TotalTranscriptMolsByBarcode[bcIdx]);
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
            xmlFile.Write("    <barcodestat section=\"{0}\">", xmlFileSection);
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = values[bcIdx].ToString();
                bCodeLines.Write("\t{0}", counts[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? counts[bcIdx] : string.Format("({0})", counts[bcIdx]);
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            bCodeLines.WriteLine();
            barcodeStats.WriteLine("\n{0}:\n", barcodeStatsTitle);
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
                    bCodeLines.Write("\t{0}", annotHits);
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    string d = genomeBcIndexes.Contains(bcIdx) ? annotHits : string.Format("({0})", annotHits);
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
            bCodeLines.Write("DUPLICATE_READS");
            xmlFile.Write("    <barcodestat section=\"duplicated molecules (by position-random tag)\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = mappingAdder.NDuplicateReads(bcIdx).ToString();
                bCodeLines.Write("\t{0}", counts[bcIdx]);
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
                bCodeLines.Write("\t{0}", barcodes.Seqs[bcIdx]);
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
            foreach (string species in barcodes.SpeciesByWell) bCodeLines.Write("\t{0}", species);
            barcodeStats.WriteLine("Species by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(barcodes.SpeciesByWell, "empty"));
            xmlFile.Write("    <barcodestat section=\"species\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string species = genomeBcIndexes.Contains(bcIdx) ? barcodes.SpeciesByWell[bcIdx] : string.Format("({0})", barcodes.SpeciesByWell[bcIdx]);
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

        private void WriteASExonDistributionHistogram()
        {
            using (StreamWriter ASHistFile = new StreamWriter(OutputPathbase + "_ASRPM_Histo.tab"))
            {
                ASHistFile.WriteLine("#Distribution of Antisense RPM/bp transcript among all genes with any Antisense hit.");
                ASHistFile.WriteLine("#BinStart\tCount");
                int[] histo;
                double firstBinStart, binWidth, median;
                MakeExonAntisenseHistogram(out histo, out median, out firstBinStart, out binWidth);
                for (int bin = 0; bin < histo.Length; bin++)
                {
                    ASHistFile.WriteLine("{0}\t{1}", firstBinStart + bin * binWidth, histo[bin]);
                }
                ASHistFile.WriteLine("\n\nMedian:\t{0}", median);
            }
        }

        private void MakeExonAntisenseHistogram(out int[] histo, out double median, out double firstBinStart, out double binWidth)
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

        private void WriteRedundantExonHits()
        {
            Dictionary<string, List<string>> byGene = new Dictionary<string, List<string>>();
            using (StreamWriter redFile = new StreamWriter(OutputPathbase + "_shared_hits.tab"))
            {
                redFile.WriteLine("#Reads\tGenomically overlapping transcripts competing for these reads");
                foreach (string combName in overlappingGeneFeatures.Keys)
                {
                    int sharedHits = overlappingGeneFeatures[combName];
                    string[] names = combName.Split('#');
                    foreach (string n in names)
                    {
                        string group = string.Format("{0}({1})",
                                         string.Join("/", names.Where((on) => (on != n)).ToArray()), sharedHits);
                        if (!byGene.ContainsKey(n))
                            byGene[n] = new List<string>();
                        byGene[n].Add(group);
                    }
                    string tabbedNames = string.Join("\t", names);
                    redFile.WriteLine("{0}\t{1}", sharedHits, tabbedNames);
                }
            }
            using (StreamWriter sharedFile = new StreamWriter(OutputPathbase + "_shared_hits_by_gene.tab"))
            {
                sharedFile.WriteLine("#Transcript\tMinHits\tMaxHits\tNon-unique hits in the difference, that also map to other overlapping transcripts/variants");
                foreach (string gene in byGene.Keys)
                {
                    GeneFeature gf = Annotations.geneFeatures[gene];
                    int ncHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                    int allHits = gf.TranscriptHitsByBarcode.Sum();
                    string altGenes = string.Join("\t", byGene[gene].ToArray());
                    sharedFile.WriteLine("{0}\t{1}\t{2}\t{3}", gene, ncHits, allHits, altGenes);
                }
            }
        }

        private void WriteSNPPositions(int[] genomeBarcodes)
        {
            using (StreamWriter snpFile = new StreamWriter(OutputPathbase + "_SNPs.tab"))
            {
                char[] nts = new char[] { '0', 'A', 'C', 'G', 'T' };
                int thres = (int)(SnpAnalyzer.thresholdFractionAltHitsForMixPos * 100);
                int minHitsToTestSNP = (barcodes.HasRandomBarcodes) ? Props.props.MinMoleculesToTestSnp : Props.props.MinReadsToTestSnp;
                string minTxt = (barcodes.HasRandomBarcodes) ? "Molecules" : "Reads";
                snpFile.WriteLine("SNP positions found in " + genomeBarcodes.Length + " barcodes belonging to species.");
                snpFile.WriteLine("#(minimum {0} {3}/Pos required to check, limits used heterozygous: {1}-{2}% AltNt and homozygous: >{2}% Alt Nt)",
                                  minHitsToTestSNP, thres, 100 - thres, minTxt);
                snpFile.WriteLine("#Gene\tChr\tmRNALeftChrPos\tSNPChrPos\tType\tRefNt\tTotal\tMut-A\tMut-C\tMut-G\tMut-T");
                foreach (GeneFeature gf in Annotations.geneFeatures.Values)
                {
                    string first = string.Format("{0}\t{1}\t{2}\t", gf.Name, gf.Chr, gf.Start);
                    foreach (KeyValuePair<int, SNPCountsByBarcode> chrPosAndBcCounts in gf.bcSNPCountsByRealChrPos)
                    {
                        int nTotal, nAlt;
                        SNPCountsByBarcode bcCounts = chrPosAndBcCounts.Value;
                        bcCounts.GetTotals(genomeBarcodes, out nTotal, out nAlt);
                        if (nTotal >= minHitsToTestSNP)
                        {
                            int type = SnpAnalyzer.TestSNP(nTotal, nAlt);
                            if (type == SnpAnalyzer.REFERENCE) continue;
                            int posOnChr = chrPosAndBcCounts.Key;
                            string typeName = (type == SnpAnalyzer.ALTERNATIVE) ? "AltNt" : "MixNt";
                            StringBuilder sb = new StringBuilder();
                            sb.Append(bcCounts.refNt);
                            foreach (char nt in nts)
                            {
                                int c; bool overflow;
                                bcCounts.SummarizeNt(nt, genomeBarcodes, out c, out overflow);
                                if (c >= SNPCountsByBarcode.MaxCount)
                                    sb.Append(">=");
                                if (c > 0) sb.Append(c);
                                sb.Append('\t');
                            }
                            snpFile.WriteLine("{0}{1}\t{2}\t{3}", first, posOnChr, typeName, sb);
                            first = "\t\t\t";
                        }
                    }
                }
            }
        }

        private void WriteSnps()
        {
            int[] genomeBarcodes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            string snpPath = OutputPathbase + "_SNPs_by_barcode.tab";
            SnpAnalyzer.WriteSnpsByBarcode(snpPath, barcodes, genomeBarcodes, Annotations.geneFeatures);
            WriteSNPPositions(genomeBarcodes);
        }

        public void WriteWriggle()
        {
            WriteWiggleStrand('+');
            WriteWiggleStrand('-');
        }

        private void WriteWiggleStrand(char strand)
        {
            int averageReadLength = MappedTagItem.AverageReadLen;
            string strandString = (strand == '+') ? "fw" : "rev";
            using (StreamWriter readWriter = (OutputPathbase + "_" + strandString + "_byread.wig.gz").OpenWrite())
            {
                readWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full",
                    Path.GetFileNameWithoutExtension(OutputPathbase) + "_byread", strand);
                foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                {
                    string chr = data.Key;
                    if (!StrtGenome.IsSyntheticChr(chr) && Annotations.ChromosomeLengths.ContainsKey(chr))
                        data.Value.GetWiggle(strand).WriteReadWiggle(readWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr]);
                }
            }
            if (barcodes.HasRandomBarcodes)
            {
                using (StreamWriter molWriter = (OutputPathbase + "_" + strandString + "_bymolecule.wig.gz").OpenWrite())
                {
                    molWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full",
                        Path.GetFileNameWithoutExtension(OutputPathbase) + "_bymolecule", strand);
                    foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                    {
                        string chr = data.Key;
                        if (!StrtGenome.IsSyntheticChr(chr) && Annotations.ChromosomeLengths.ContainsKey(chr))
                            data.Value.GetWiggle(strand).WriteMolWiggle(molWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr]);
                    }
                }
            }
        }

        private void WriteHotspots()
        {
            using (StreamWriter writer = new StreamWriter(OutputPathbase + "_hotspots.tab"))
            {
                writer.WriteLine("#Positions with local maximal read counts that lack gene or repeat annotation. Samples < 5 bp apart not shown.");
                writer.WriteLine("#Chr\tPosition\tStrand\tCoverage");
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
            }
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
                writer.WriteLine("{0}\t{1}\t{2}\t{3}", chr, start + averageReadLength / 2, strand, topCounts[cI]);
            }
        }

        private void WriteHitProfilesByBarcode()
        {
            int trLenBinSize = 500;
            int trLenBinHalfWidth = trLenBinSize / 2;
            int trLenBinStep = 1500;
            int trLen1stBinMid = 500;
            int trLen1stBinStart = trLen1stBinMid - trLenBinHalfWidth;
            int trLenBinCount = 4;
            int nSections = 20;
            int averageReadLen = MappedTagItem.AverageReadLen;
            using (StreamWriter profileFile = new StreamWriter(OutputPathbase + "_5to3_profiles_by_barcode.tab"))
            {
                profileFile.WriteLine("5'->3' read distributions by barcode.");
                profileFile.WriteLine("\t\t\tRelative position within transcript.");
                profileFile.Write("\t\t");
                for (int section = 0; section < nSections; section++)
                    profileFile.Write("\t{0}", (section + 0.5D) / (double)nSections);
                profileFile.WriteLine("\nBarcode\tTrLenFrom\tTrLenTo\tFraction within interval of all reads.");
                int minHitsPerGene = (barcodes.HasRandomBarcodes) ? 50 : nSections * 10;
                int maxHitsPerGene = (barcodes.HasRandomBarcodes) ? (int)(0.7 * barcodes.RandomBarcodeCount * barcodes.Count) : int.MaxValue;
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[trLenBinCount, nSections];
                    for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
                    {
                        for (int section = 0; section < nSections; section++)
                            binnedEfficiencies[trLenBinIdx, section] = new DescriptiveStatistics();
                    }
                    int[] geneCounts = new int[trLenBinCount];
                    foreach (GeneFeature gf in Annotations.geneFeatures.Values)
                    {
                        if (gf.IsSpike() || gf.TranscriptHitsByBarcode[bcIdx] < minHitsPerGene || gf.TranscriptHitsByBarcode[bcIdx] > maxHitsPerGene)
                            continue;
                        int trLen = gf.GetTranscriptLength();
                        double sectionSize = (trLen - averageReadLen) / (double)nSections;
                        int[] trSectionCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, sectionSize, Props.props.DirectionalReads, averageReadLen);
                        if (trSectionCounts.Length == 0) continue;
                        double trTotalCounts = trSectionCounts.Sum();
                        if (trTotalCounts == 0.0) continue;
                        if (trLen < trLen1stBinStart || (trLen - trLen1stBinStart) % trLenBinStep > trLenBinSize)
                            continue;
                        int trLenBin = (trLen - trLen1stBinStart) / trLenBinStep;
                        if (trLenBin >= trLenBinCount) continue;
                        for (int section = 0; section < nSections; section++)
                            binnedEfficiencies[trLenBin, section].Add(trSectionCounts[section] / trTotalCounts);
                        geneCounts[trLenBin]++;
                    }
                    for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
                    {
                        int midLen = (trLenBinIdx * trLenBinStep) + trLen1stBinMid;
                        profileFile.Write("{0}\t{1}\t{2}", bcIdx, midLen - trLenBinHalfWidth, midLen + trLenBinHalfWidth);
                        for (int section = 0; section < nSections; section++)
                        {
                            double eff = (geneCounts[trLenBinIdx] < 10) ? 0.0 : binnedEfficiencies[trLenBinIdx, section].Mean();
                            profileFile.Write("\t{0}", eff);
                        }
                        profileFile.WriteLine();
                    }
                }
            }
        }
    }

}
