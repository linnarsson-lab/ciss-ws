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

        private static readonly int maxHotspotCount = 50;
        private static readonly int minHotspotDistance = 5;

        private int minMismatchReadCountForSNPDetection;

        StreamWriter nonAnnotWriter = null;
        StreamWriter nonExonWriter = null;

        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private int[] SelectedBcWiggleAnnotations;

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

        int[] nNonAnnotatedItemsByBc;
        int[] nNonAnnotatedMolsByBc;

        /// <summary>
        /// Return the total number of hits (may be read or molecules depending on settings)
        /// </summary>
        int TotalHits { get { return TotalHitsByAnnotType.Sum(); } }

        LabelingEfficiencyEstimator labelingEfficiencyEstimator;

        GenomeAnnotations Annotations;
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
        /// Counts number of reads that were ignored due to too many alternative mappings
        /// </summary>
        int nTooMultiMappingReads = 0;

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
        // For non-rndTagged samples the following two will be identical:
        Dictionary<int, List<int>> sampledUniqueMoleculesByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledUniqueHitPositionsByBcIdx = new Dictionary<int, List<int>>();

        private UpstreamAnalyzer upstreamAnalyzer;
        private PerLaneStats perLaneStats;

        private StreamWriter rndTagProfileByGeneWriter;
        private List<GeneFeature> readsPerMoleculeHistogramGenes;
        private readonly static int maxNReadsInPerMoleculeHistograms = 999;

        Dictionary<string, int> overlappingGeneFeatures = new Dictionary<string, int>();
        List<IFeature> exonHitFeatures;
        private string spliceChrId;
        private static int nMaxMappings;
        private List<GeneFeature> gfsForRndTagProfile = new List<GeneFeature>();

        public TranscriptomeStatistics(GenomeAnnotations annotations, Props props, string outputPathbase)
		{
            SelectedBcWiggleAnnotations = props.SelectedBcWiggleAnnotations;
            if (props.SelectedBcWiggleAnnotations != null && props.SelectedBcWiggleAnnotations.Length == 0)
                SelectedBcWiggleAnnotations = null;
            if (props.GenerateBarcodedWiggle && SelectedBcWiggleAnnotations != null)
                Console.WriteLine("Selected annotation types for barcoded wiggle plots: "
                                  + string.Join(",", Array.ConvertAll(SelectedBcWiggleAnnotations, t => AnnotType.GetName(t))));
            this.OutputPathbase = outputPathbase;
            AnnotType.DirectionalReads = props.DirectionalReads;
            barcodes = props.Barcodes;
            Annotations = annotations;
            AnalyzeAllGeneVariants = !Annotations.noGeneVariants;
            SetupMotifs(props);
            TotalHitsByBarcode = new int[barcodes.Count];
            nNonAnnotatedItemsByBc = new int[barcodes.Count];
            nNonAnnotatedMolsByBc = new int[barcodes.Count];
            TotalTranscriptMolsByBarcode = new int[barcodes.Count];
            TotalHitsByAnnotTypeAndBarcode = new int[AnnotType.Count, barcodes.Count];
            TotalHitsByAnnotTypeAndChr = new Dictionary<string, int[]>();
            foreach (string chr in Annotations.GetChromosomeIds())
                TotalHitsByAnnotTypeAndChr[chr] = new int[AnnotType.Count];
            TotalHitsByAnnotType = new int[AnnotType.Count];
            nMappedReadsByBarcode = new int[barcodes.Count];
            nMappingsByBarcode = new int[barcodes.Count];
            exonHitFeatures = new List<IFeature>(100);
            spliceChrId = Annotations.Genome.Annotation;
            randomTagFilter = new RandomTagFilterByBc(barcodes, Annotations.GetChromosomeIds());
            TagItem.SetRndTagMutationFilter(props);
            mappingAdder = new MappingAdder(annotations, randomTagFilter, barcodes);
            statsSampleDistPerBarcode = sampleDistForAccuStats / barcodes.Count;
            if (props.AnalyzeSeqUpstreamTSSite && barcodes.Count > 1)
                upstreamAnalyzer = new UpstreamAnalyzer(Annotations, barcodes);
            perLaneStats = new PerLaneStats(barcodes);
            minMismatchReadCountForSNPDetection = props.MinAltNtsReadCountForSNPDetection;
            nMaxMappings = props.MaxAlternativeMappings - 1;
            SetupGfsForRndTagProfile();
            labelingEfficiencyEstimator = new LabelingEfficiencyEstimator(barcodes, PathHandler.GetCTRLConcPath(), props.TotalNumberOfAddedSpikeMolecules);
            MappedTagItem.labelingEfficiencyEstimator = labelingEfficiencyEstimator;
        }

        private void SetupMotifs(Props props)
        {
            DetermineMotifs = props.DetermineMotifs;
            motifs = new DnaMotif[barcodes.Count];
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i] = new DnaMotif(40);
            }
        }

        private void SetupGfsForRndTagProfile()
        {
            if (Props.props.GenesToShowRndTagProfile != null && barcodes.HasUMIs)
            {
                foreach (string geneName in Props.props.GenesToShowRndTagProfile)
                {
                    foreach (GeneFeature gf in IterMatchingGeneFeatures(geneName))
                    {
                        gfsForRndTagProfile.Add(gf);
                    }
                }
            }
        }

        private IEnumerable<GeneFeature> IterMatchingGeneFeatures(string geneName)
        {
            string upperName = geneName.ToUpper();
            string variantName = geneName + "_v";
            string upperVariantName = upperName + "_v";
            foreach (KeyValuePair<string, GeneFeature> gfp in
                Annotations.geneFeatures.Where(kvp => (kvp.Key == geneName || kvp.Key == upperName ||
                                                kvp.Key.StartsWith(variantName) || kvp.Key.StartsWith(upperVariantName))))
            {
                yield return gfp.Value;
            }
        }

        private string AssertOutputPathbase()
        {
            if (!Directory.Exists(Path.GetDirectoryName(OutputPathbase)))
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPathbase));
            return OutputPathbase;
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
        /// <param name="averageReadLen">average length of the reads in map files. Used during SNP analysis</param>
        public void ProcessMapFiles(List<string> mapFilePaths, int averageReadLen)
        {
            nTooMultiMappingReads = 0;
            trSampleDepth = (barcodes.HasUMIs) ? libraryDepthSampleMolsCountPerBc : libraryDepthSampleReadCountPerBc;
            if (mapFilePaths.Count == 0)
                return;
            if (Props.props.AnalyzeSNPs)
                RegisterPotentialSNPs(mapFilePaths, averageReadLen);
            if (Props.props.SnpRndTagVerification && barcodes.HasUMIs)
                snpRndTagVerifier = new SnpRndTagVerifier(Props.props, Annotations.Genome);
            Console.WriteLine("Annotatating {0} map files ignoring reads with > {1} alternative mappings.", mapFilePaths.Count, nMaxMappings);

            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter = new StreamWriter(AssertOutputPathbase() + "_NONANNOTATED.tab");
                nonExonWriter = new StreamWriter(OutputPathbase + "_NONEXON.tab");
            }

            foreach (Pair<int, List<string>> bcIdxAndMapFilePaths in IterMapFilesGroupedByBarcode(mapFilePaths))
            {
                ProcessBarcodeMapFiles(bcIdxAndMapFilePaths);
            }
            Console.WriteLine("\nIgnored {0} reads with > {1} alternative mappings.", nTooMultiMappingReads, Props.props.MaxAlternativeMappings);

            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter.Close(); nonAnnotWriter.Dispose();
                nonExonWriter.Close(); nonExonWriter.Dispose();
            }
        }

        private IEnumerable<Pair<int, List<string>>> IterMapFilesGroupedByBarcode(List<string> mapFilePaths)
        {
            mapFilePaths.Sort(CompareMapFiles); // Important to have them sorted by barcode
            HashSet<int> usedBcIdxs = new HashSet<int>();
            List<string> bcMapFilePaths = new List<string>();
            string mapFileName = Path.GetFileName(mapFilePaths[0]);
            int currentBcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
            foreach (string mapFilePath in mapFilePaths)
            {
                mapFileName = Path.GetFileName(mapFilePath);
                int bcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
                if (bcIdx != currentBcIdx)
                {
                    yield return new Pair<int, List<string>>(currentBcIdx, bcMapFilePaths);
                    bcMapFilePaths.Clear();
                    if (usedBcIdxs.Contains(bcIdx))
                        throw new Exception("Program or map file naming error: Revisiting an already analyzed barcode (" + bcIdx + ") is not allowed.");
                    usedBcIdxs.Add(bcIdx);
                    currentBcIdx = bcIdx;
                }
                bcMapFilePaths.Add(mapFilePath);
            }
            if (bcMapFilePaths.Count > 0)
                yield return new Pair<int, List<string>>(currentBcIdx, bcMapFilePaths);
        }

        private void RegisterPotentialSNPs(List<string> mapFilePaths, int averageReadLen)
        {
            MapFileSnpFinder mfsf = new MapFileSnpFinder(barcodes);
            mfsf.ProcessMapFiles(mapFilePaths);
            int nSNPs = randomTagFilter.SetupSNPCounters(averageReadLen, mfsf.IterSNPLocations(minMismatchReadCountForSNPDetection));
            Console.WriteLine("Registered {0} potential expressed SNPs (positions with >= {1} mismatch reads).", nSNPs, minMismatchReadCountForSNPDetection);
        }

        /// <summary>
        /// Annotate the set of all map files that have the same barcode
        /// </summary>
        /// <param name="bcIdxAndMapFilePaths">Barcode index and paths to all files with mapped reads of that barcode</param>
        private void ProcessBarcodeMapFiles(Pair<int, List<string>> bcIdxAndMapFilePaths)
        {
            currentBcIdx = bcIdxAndMapFilePaths.First;
            foreach (string mapFilePath in bcIdxAndMapFilePaths.Second)
            {
                Console.Write(".");
                if (File.Exists(mapFilePath))
                    AddReadMappingsToTagItems(mapFilePath);
            }
            SampleReadStatistics(nMappedReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode);
            AnnotateFeaturesFromTagItems();
            if (Props.props.MakeGeneReadsPerMoleculeHistograms && barcodes.HasUMIs)
                AddToGeneReadsPerMoleculeHistograms();
            MakeGeneRndTagProfiles();
            MakeBcWigglePlots();
            if (Props.props.LogMode)
                Console.WriteLine("Calling TranscriptomeStatistics.FinishBarcode() Bc={0} #TagItems={1}", currentBcIdx, randomTagFilter.TagItemCount());
            randomTagFilter.FinishBarcode();
            //Console.WriteLine("#SPLC: {0} #ASPLC: {1}", TotalHitsByAnnotType[AnnotType.SPLC], TotalHitsByAnnotType[AnnotType.ASPLC]);
        }

        private void AddReadMappingsToTagItems(string mapFilePath)
        {
            currentMapFilePath = mapFilePath;
            MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, barcodes);
            if (mapFileReader == null)
                throw new Exception("Unknown read map file type : " + mapFilePath);
            int nMappedReadsByFile = 0;
            perLaneStats.BeforeFile(currentBcIdx, nMappedReadsByBarcode[currentBcIdx], mappingAdder.NUniqueReadSignatures(currentBcIdx),
                                    randomTagFilter.GetNumDistinctMappings());
            foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
            {
                if (mrm.NMappings >= nMaxMappings)
                {
                    nTooMultiMappingReads++;
                    continue;
                }
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

        private void AnnotateFeaturesFromTagItems()
        {
            List<string> ctrlChrId = new List<string>();
            if (randomTagFilter.chrTagDatas.ContainsKey("CTRL"))
            { // First process CTRL chromosome to get the labeling efficiency
                //TagItem.LabelingEfficiency = 1.0; // Needed to avoid overflow in real mol estimate calculator
                ctrlChrId.Add("CTRL");
                foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, true))
                    Annotate(mtitem);
                labelingEfficiencyEstimator.CalcEfficiencyFromSpikes(Annotations.geneFeatures.Values, currentBcIdx);
                //double labelingEfficiency = Annotations.GetEfficiencyFromSpikes(currentBcIdx);
                //TagItem.LabelingEfficiency = labelingEfficiency;
                //labelingEfficiencyByBc[currentBcIdx] = labelingEfficiency;
            }
            foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, false))
                Annotate(mtitem);
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
            exonHitFeatures.Clear();
            foreach (FtInterval trMatch in Annotations.IterTranscriptMatches(item.chr, item.strand, item.HitMidPos))
            {
                someExonHit = someAnnotationHit = true;
                MarkStatus markStatus = (IterTranscriptMatchers.HasVariants || item.hasAltMappings) ? MarkStatus.NONUNIQUE_EXON_MAPPING : MarkStatus.UNIQUE_EXON_MAPPING;
                if (!exonHitFeatures.Contains(trMatch.Feature))
                { // If a gene is hit multiple times (happens if two diff. splices have same seq.), we should annotate it only once
                    exonHitFeatures.Add(trMatch.Feature);
                    item.splcToRealChrOffset = 0;
                    int annotType = trMatch.Mark(item, trMatch.ExtraData, markStatus);
                    TotalHitsByAnnotTypeAndBarcode[annotType, currentBcIdx] += molCount;
                    TotalHitsByAnnotTypeAndChr[item.chr][annotType] += molCount;
                    TotalHitsByAnnotType[annotType] += molCount;
                    TotalHitsByBarcode[currentBcIdx] += molCount;
                    item.SetTypeOfAnnotation(annotType);
                }
            }
            if (exonHitFeatures.Count > 1)
                RegisterOverlappingGeneFeatures(molCount);
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
                    item.SetTypeOfAnnotation(annotType);
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
            else
            {
                nNonAnnotatedItemsByBc[currentBcIdx]++;
                nNonAnnotatedMolsByBc[currentBcIdx] += item.MolCount;
            }
            int t = nMappingsByBarcode[currentBcIdx] - trSampleDepth;
            if (t > 0 && t <= molCount) // Sample if we just passed the sampling point with current MappedTagItem
                sampledExpressedTranscripts.Add(Annotations.GetNumExpressedTranscripts(currentBcIdx));
        }

        /// <summary>
        /// Called when a read hits several genes at the same mapping position. Some genes may overlap, but
        /// should usually only happen if two alt. splices match the read and point to the same true genomic position.
        /// </summary>
        /// <param name="molCount"></param>
        private void RegisterOverlappingGeneFeatures(int molCount)
        {
            exonHitFeatures.Sort();
            string combNames = string.Join("#", exonHitFeatures.ConvertAll<string>(v => v.Name).ToArray());
            if (!overlappingGeneFeatures.ContainsKey(combNames))
                overlappingGeneFeatures[combNames] = molCount;
            else
                overlappingGeneFeatures[combNames] += molCount;
        }

        /// <summary>
        /// Add data from current barcode of selected chr-positions to the read count-per-molecule histograms
        /// </summary>
        private void AddToGeneReadsPerMoleculeHistograms()
        {
            if (readsPerMoleculeHistogramGenes == null)
                SelectGenesForReadsPerMoleculeHistograms();
            foreach (GeneFeature gf in readsPerMoleculeHistogramGenes)
            {
                foreach (KeyValuePair<int, ushort[]> d in gf.readsPerMoleculeData)
                {
                    int estMolCount;
                    ushort[] profile;
                    int chrPos = d.Key;
                    ushort[] histo = d.Value;
                    randomTagFilter.GetReadCountProfile(gf.Chr, chrPos, gf.Strand, out estMolCount, out profile);
                    if (profile != null)
                        foreach (int nReads in profile.Where(v => v > 0))
                            histo[Math.Min(maxNReadsInPerMoleculeHistograms, nReads)]++;
                }
            }
        }

        /// <summary>
        /// Determine which genes and positions should be used for the read count-per-molecule histograms.
        /// Select 10 genes from each of five expression level intervals, and only the positions where any
        /// hits have been found within current (i.e. first) barcode
        /// </summary>
        private void SelectGenesForReadsPerMoleculeHistograms()
        {
            readsPerMoleculeHistogramGenes = new List<GeneFeature>();
            foreach (int minLevel in new int[] { 150, 100, 60, 30, 10 })
            {
                int nGfs = 0;
                foreach (GeneFeature gf in Annotations.geneFeatures.Values)
                {
                    if (gf.GetTranscriptHits() > minLevel && !readsPerMoleculeHistogramGenes.Contains(gf))
                    {
                        readsPerMoleculeHistogramGenes.Add(gf);
                        gf.readsPerMoleculeData = new Dictionary<int, ushort[]>();
                        ushort[] d = CompactGenePainter.GetTranscriptProfile(gf);
                        for (int trPos = 0; trPos < d.Length; trPos++)
                            if (d[trPos] > 0)
                                gf.readsPerMoleculeData[gf.GetChrPos(trPos)] = new ushort[maxNReadsInPerMoleculeHistograms + 1];
                        if (nGfs++ == 10) break;
                    }
                }
            }
        }

        private void WriteGeneReadsPerMoleculeHistograms()
        {
            string file = OutputPathbase + "_ReadsPerMolHistograms.tab";
            using (StreamWriter writer = file.OpenWrite())
            {
                writer.WriteLine("#Gene\tTrPos\tNReadsPerMolDistribution");
                foreach (GeneFeature gf in readsPerMoleculeHistogramGenes)
                {
                    foreach (KeyValuePair<int, ushort[]> d in gf.readsPerMoleculeData)
                    {
                        int chrPos = d.Key;
                        ushort[] histo = d.Value;
                        writer.Write("{0}\t{1}", gf.Name, chrPos);
                        foreach (uint count in histo)
                            writer.Write("\t{0}", count);
                        writer.WriteLine();
                    }
                }
            }
        }

        private void MakeGeneRndTagProfiles()
        {
            foreach (GeneFeature gf in gfsForRndTagProfile)
            {
                int estMolCount;
                ushort[] profile;
                int trPos = (gf.Strand == '+') ? 1 : gf.Length;
                int trDir = (gf.Strand == '+') ? 1 : -1;
                foreach (int chrPos in gf.IterExonPositionsInChrDir())
                {
                    randomTagFilter.GetReadCountProfile(gf.Chr, chrPos, gf.Strand, out estMolCount, out profile);
                    if (profile != null)
                    {
                        if (rndTagProfileByGeneWriter == null)
                        {
                            string file = AssertOutputPathbase() + "_rnd_tag_profiles.tab";
                            rndTagProfileByGeneWriter = file.OpenWrite();
                            rndTagProfileByGeneWriter.WriteLine("#Gene\tBarcode\tChr\tStrand\tChrPos\tTrPos(>=1)\tEstMolCount\tReadCountsByRndTagIdx");
                        }
                        rndTagProfileByGeneWriter.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", 
                            gf.Name, barcodes.Seqs[currentBcIdx], gf.Chr, gf.Strand, chrPos, trPos, estMolCount);
                        foreach (int count in profile)
                            rndTagProfileByGeneWriter.Write("\t{0}", count);
                        rndTagProfileByGeneWriter.WriteLine();
                    }
                    trPos += trDir;
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
            string selAnnots = (SelectedBcWiggleAnnotations == null)? 
                                "" : "_" + string.Join(".", Array.ConvertAll(SelectedBcWiggleAnnotations, t => AnnotType.GetName(t)));
            string bcWiggleSubfolder = AssertOutputPathbase() + "_wiggle_by_bc" + selAnnots;
            if (!Directory.Exists(bcWiggleSubfolder))
                Directory.CreateDirectory(bcWiggleSubfolder);
            string fileNameHead = string.Format("{0}_{1}", currentBcIdx, ((strand == '+') ? "fw" : "rev"));
            string filePathHead = Path.Combine(bcWiggleSubfolder, fileNameHead);
            string fileByRead = filePathHead + "_byread.wig.gz";
            string fileByMol = filePathHead + "_bymolecule.wig.gz";
            if (File.Exists(fileByRead)) return;
            using (StreamWriter writerByRead = fileByRead.OpenWrite())
            using (StreamWriter writerByMol = (barcodes.HasUMIs && !File.Exists(fileByMol)? fileByMol.OpenWrite() : null))
            {
                writerByRead.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} {2} ({1})\" visibility=full alwaysZero=on",
                                       fileNameHead + "_Read", strand, DateTime.Now.ToString("yyMMdd"));
                if (writerByMol != null)
                    writerByMol.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} {2} ({1})\" visibility=full alwaysZero=on",
                                          fileNameHead + "_Mol", strand, DateTime.Now.ToString("yyMMdd"));
                foreach (KeyValuePair<string, ChrTagData> tagDataPair in randomTagFilter.chrTagDatas)
                {
                    string chr = tagDataPair.Key;
                    if (!StrtGenome.IsSyntheticChr(chr))
                    {
                        int chrLen = Annotations.ChromosomeLengths[chr];
                        int[] positions, molsAtEachPos, readsAtEachPos;
                        tagDataPair.Value.GetDistinctPositionsAndCounts(strand, SelectedBcWiggleAnnotations, 
                                                                        out positions, out molsAtEachPos, out readsAtEachPos);
                        if (writerByMol != null)
                        {
                            int[] posCopy = (int[])positions.Clone();
                            Array.Sort(posCopy, molsAtEachPos);
                            Wiggle.WriteToWigFile(writerByMol, chr, readLength, strand, chrLen, posCopy, molsAtEachPos);
                        }
                        Array.Sort(positions, readsAtEachPos);
                        Wiggle.WriteToWigFile(writerByRead, chr, readLength, strand, chrLen, positions, readsAtEachPos);
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
            PaintReadIntervals();
            if (upstreamAnalyzer != null)
                upstreamAnalyzer.WriteUpstreamStats(OutputPathbase);
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            if (barcodes.HasUMIs)
            {
                WriteReadCountDistroByRndTagCount();
                if (readsPerMoleculeHistogramGenes != null)
                    WriteGeneReadsPerMoleculeHistograms();
            }
            WriteSpikeEfficiencies();
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
            if (Props.props.GenerateBed)
                WriteBed();
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
                                     (barcodes.HasUMIs) ? "molecules" : "reads", sampledLibraryDepths.Count,
                                     sampledUniqueMolecules.Count, sampledExpressedTranscripts.Count);
                WriteReadsBySpecies(xmlFile);
                WriteFeatureStats(xmlFile);
                WriteSenseAntisenseStats(xmlFile);
                WriteHitsByChromosome(xmlFile);
                WriteMappingDepth(xmlFile);
                WritePerLaneStats(xmlFile);
                AddSpikes(xmlFile);
                WriteSpikeDetection(xmlFile);
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

        public void WriteSpikeDetection(StreamWriter xmlFile)
        {
            StringBuilder sbt = new StringBuilder();
            StringBuilder sbf = new StringBuilder();
            foreach (GeneFeature gf in Annotations.IterTranscripts(true))
            {
                if (!gf.IsExpressed())
                    continue;
                int total = 0;
                int detected = 0;
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    int bcHits = gf.TranscriptHitsByBarcode[bcIdx];
                    total += bcHits;
                    if (bcHits > 0) detected++;
                }
                double fraction = (detected / (double)barcodes.Count);
                string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                sbt.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0}\" />\n", spikeId, total));
                sbf.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0.###}\" />\n", spikeId, fraction));
            }
            if (sbt.Length > 0)
            {
                xmlFile.WriteLine("  <spikedetection>");
                xmlFile.WriteLine("    <title>Detection of spikes across all {0} wells</title>", barcodes.Count);
                xmlFile.WriteLine("    <curve legend=\"total reads\" yaxis=\"right\" color=\"black\">");
                xmlFile.Write(sbt.ToString());
                xmlFile.WriteLine("    </curve>");
                xmlFile.WriteLine("    <curve legend=\"frac. of wells\" yaxis=\"left\" color=\"blue\">");
                xmlFile.Write(sbf.ToString());
                xmlFile.WriteLine("    </curve>");
                xmlFile.WriteLine("  </spikedetection>");
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
            if (Props.props.AnalyzeSNPs && barcodes.HasUMIs)
                xmlFile.WriteLine("    <MinMoleculesToTestSnp>{0}</MinMoleculesToTestSnp>", Props.props.MinMoleculesToTestSnp);
            if (Props.props.AnalyzeSNPs && !barcodes.HasUMIs)
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
            int nBc = sampledUniqueHitPositionsByBcIdx.Keys.Count;
            if (nBc > 24)
            {
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings per barcode vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, 0, nBc / 2);
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings per barcode vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, nBc / 2, nBc);
            }
            else
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings per barcode vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, 0, nBc);
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (!barcodes.HasUMIs) return;
            xmlFile.WriteLine("  <randomtagfrequence>");
            xmlFile.WriteLine("    <title>Number of reads detected in each random tag</title>");
            xmlFile.WriteLine("    <xtitle>Random tag index (AAAA...TTTT)</xtitle>");
            for (int i = 0; i < randomTagFilter.nReadsByRandomTag.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", barcodes.MakeUMISeq(i), randomTagFilter.nReadsByRandomTag[i]);
            xmlFile.WriteLine("  </randomtagfrequence>");
            xmlFile.WriteLine("  <nuniqueateachrandomtagcoverage>");
            xmlFile.WriteLine("    <title>Unique alignmentposition-barcodes vs. # random tags they occur in</title>");
            xmlFile.WriteLine("    <xtitle>Number of different random tags</xtitle>");
            for (int i = 1; i < randomTagFilter.nCasesPerRandomTagCount.Length; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.nCasesPerRandomTagCount[i]);
            xmlFile.WriteLine("  </nuniqueateachrandomtagcoverage>");
            WriteAccuMoleculesByBc(xmlFile, "moleculedepthbybc", "Distinct detected molecules per barcode vs. mapped reads processed",
                                   sampledUniqueMoleculesByBcIdx, 0, sampledUniqueMoleculesByBcIdx.Keys.Count);
            xmlFile.WriteLine("  <moleculereadscountshistogram>");
            xmlFile.WriteLine("    <title>Distribution of number of times every unique molecule has been observed</title>");
            xmlFile.WriteLine("    <xtitle>Number of observations (reads)</xtitle>");
            for (int i = 1; i < randomTagFilter.moleculeReadCountsHistogram.Length; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.moleculeReadCountsHistogram[i]);
            xmlFile.WriteLine("  </moleculereadscountshistogram>");
        }

        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data, int start, int stop)
        {
            xmlFile.WriteLine("  <{0}>", tag);
            xmlFile.WriteLine("    <title>{0}</title>", title);
            xmlFile.WriteLine("    <xtitle>Millions of mapped reads</xtitle>");
            int[] bcIndices = data.Keys.ToArray();
            Array.Sort(bcIndices);
            for (int bII = start; bII < stop; bII++)
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
            if (!double.IsNaN(meanFrac0))
            {
                WritePerLaneStatsSection(xmlFile, "low", 0.0, meanFrac0);
                WritePerLaneStatsSection(xmlFile, "high", meanFrac0, 1.0);
            }
        }

        private void WritePerLaneStatsSection(StreamWriter xmlFile, string sectionTitle, double minF, double maxF)
        {
            xmlFile.WriteLine("  <fracuniqueperlane>");
            string type = barcodes.HasUMIs ? "molecules" : "mappings";
            xmlFile.WriteLine("    <title>Fraction ({0}) distinct {2} among first {1} mapped reads in each lane</title>",
                              sectionTitle, PerLaneStats.nMappedReadsPerFileAtSample, type);
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                List<Pair<string, double>> data = perLaneStats.GetComplexityIndex(bcIdx);
                if (data == null || data[0].Second < minF || data[0].Second >= maxF)
                    continue;
                string legend = string.Format("{0} [{1}]", barcodes.Seqs[bcIdx], barcodes.GetWellId(bcIdx));
                xmlFile.WriteLine("    <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  legend, (bcIdx * 47) % 255, (bcIdx * 21) % 255, (255 - (60 * bcIdx % 255)));
                foreach (Pair<string, double> laneAndFrac in data)
                    xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1:0.0000000}\" />", laneAndFrac.First, laneAndFrac.Second);
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
            if (barcodes.HasUMIs)
                xmlFile.WriteLine("    <point x=\"Duplicates [{0}] ({1:0%})\" y=\"{2}\" />", 
                                  spBcCount, mappingAdder.TotalNDuplicateReads / speciesReads, mappingAdder.TotalNDuplicateReads / 1.0E6d);
            xmlFile.WriteLine("  </reads>");
            xmlFile.WriteLine("  <hits>");
            int nAllHits = TotalHitsByAnnotType.Sum();
            double dividend = nAllHits;
            double reducer = 1.0E6d;
            string mr = "Multireads" + (Props.props.UseMaxAltMappings? string.Format(" (&lt; {0}-fold)", Props.props.MaxAlternativeMappings) : "");
            string mrHead = mr + ((Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping) ?
                            " are assigned only to their most 5' transcript" : " are assigned to all their transcript hits");
            if (barcodes.HasUMIs)
            {
                dividend = nMappings;
                reducer = 1.0E3d;
                xmlFile.WriteLine("    <title>Molecule mappings distribution (10^3). [#samples].\n[{0}]</title>", mrHead);
            }
            else
                xmlFile.WriteLine("    <title>Read mappings distribution (10^6) [#samples].\n[{0}]</title>", mrHead);
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
            string molTitle = (barcodes.HasUMIs)? "molecule": "read";
            double reducer = (barcodes.HasUMIs)? 1.0E6d : 1.0E3d;
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
            double nTotalHits = TotalHits;
            foreach (string chr in sortedkeys)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chr))
                    continue;
                string c = chr; // (chr.Length > 5) ? chr.Substring(0, 2) + ".." + chr.Substring(chr.Length - 2) : chr;
                double nSenseHits = TotalHitsByAnnotTypeAndChr[chr][AnnotType.EXON];
                double nAsenseHits = TotalHitsByAnnotTypeAndChr[chr][AnnotType.AEXON];
                string ratio = (nAsenseHits == 0)? "1:0" : string.Format("{0:0}", nSenseHits / (double)nAsenseHits);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  c, ratio, 100.0d*(nSenseHits / nTotalHits), 100.0d*(nAsenseHits / nTotalHits));
            }
            xmlFile.WriteLine("  </senseantisensebychr>");
        }

        private void WriteFeatureStats(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <features>");
            xmlFile.WriteLine("    <title>Overall detection of features</title>");
            if (!Annotations.noGeneVariants)
                xmlFile.WriteLine("    <point x=\"Detected tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedTranscripts());
            xmlFile.WriteLine("    <point x=\"Detected main tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedMainTranscriptVariants());
            int[] bcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            int sumExprTr = 0;
            foreach (int bcIdx in bcIndexes)
                sumExprTr += Annotations.geneFeatures.Values.Count(gf => (!gf.IsSpike() && gf.IsExpressed(bcIdx)));
            xmlFile.WriteLine("    <point x=\"Mean per species well ({0})\" y=\"{1}\" />", bcIndexes.Length, (int)(sumExprTr / bcIndexes.Count()));
            xmlFile.WriteLine("    <point x=\"Detected spikes\" y=\"{0}\" />", Annotations.GetNumExpressedSpikes());
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
            int minHitsPerGene = (barcodes.HasUMIs) ? 50 : nSections * 10;
            int maxHitsPerGene = (barcodes.HasUMIs) ? (int)(0.7 * barcodes.UMICount * barcodes.Count) : int.MaxValue;
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
            int[] totalsByBarcode = new int[barcodes.Count];
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.IsSpike())
                    continue;
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
            List<double> CVs = new List<double>(nGenes);
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
                double cv = DescriptiveStatistics.CV(normedBcValues.ToArray());
                if (!double.IsNaN(cv) && !double.IsInfinity(cv))
                    CVs.Add(cv);
            }
            return (CVs.Count > 2)? CVs.ToArray() : null;
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
            using (StreamWriter barcodeStats = new StreamWriter(OutputPathbase + "_barcode_summary.tab"))
            using (StreamWriter bCodeLines = new StreamWriter(OutputPathbase + "_barcode_oneliners.tab"))
            {
                string molT = (barcodes.HasUMIs) ? "molecules" : "reads";
                barcodeStats.WriteLine("Total annotated {0}: {1}\n", molT, nAnnotatedMappings);
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
                if (readCounter.ValidReadsByBarcode.Count == barcodes.Count)
                {
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, readCounter.ValidReadsByBarcode.ToArray(),
                                        "BARCODEDREADS", "Total barcoded reads by barcode", "barcoded reads");
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, readCounter.ValidReadsByBarcode.ToArray(),
                                        "VALIDSTRTREADS", "Total valid STRT reads by barcode", "valid STRT reads");
                }
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, nMappedReadsByBarcode,
                                    "MAPPEDREADS", "Total mapped reads by barcode", "mapped reads");
                if (barcodes.HasUMIs)
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, mappingAdder.NDuplicateReadsByBc(),
                                    "DUPLICATE_READS", "Duplicate reads of molecules (same UMI/position/strand) by barcode", "redundant reads (PCR)");
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, nNonAnnotatedItemsByBc,
                                    "NON_ANNOTATED_POS_STRANDS", "Non-annotated strand-positions by barcode", "non-annotated strand-positions");
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, nNonAnnotatedMolsByBc,
                                    "NON_ANNOTATED_" + molT.ToUpper(), "Non-annotated " + molT + " by barcode", "non-annotated " + molT);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, TotalHitsByBarcode,
                                    "HITS", "Total annotated hits by barcode", "annotated hits");
                WriteFeaturesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, TotalTranscriptMolsByBarcode,
                                    "TRNSR_DETECTING_" + molT.ToUpper(), "Transcript detecting " + molT + " by barcode", "tr. detecting " + molT);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes, Annotations.GetByBcNumExpressedTranscripts(),
                                    "TRANSCRIPTS", "Detected transcripts by barcode", "detected transcripts");
            }
            if (barcodes.HasUMIs)
            {
                xmlFile.Write("    <barcodestat section=\"labeling efficiency ({0} spike mols)\">", Props.props.TotalNumberOfAddedSpikeMolecules);
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    if (genomeBcIndexes.Contains(bcIdx)) xmlFile.Write("    <d>{0:0.###}</d>", labelingEfficiencyEstimator.LabelingEfficiencyByBc[bcIdx]);
                    else xmlFile.Write("    <d>({0:0.###})</d>", labelingEfficiencyEstimator.LabelingEfficiencyByBc[bcIdx]);
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
            string molTitle = (barcodes.HasUMIs) ? "molecules" : "reads";
            for (var annotType = 0; annotType < AnnotType.Count; annotType++)
            {
                if (annotType == AnnotType.AREPT) continue;
                string bCodeLinesTitle = AnnotType.GetName(annotType);
                bCodeLines.Write(bCodeLinesTitle);
                xmlFile.Write("    <barcodestat section=\"{0} hits\">", bCodeLinesTitle);
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
                barcodeStats.WriteLine("\nTotal {0} mapped to {1}:\n", molTitle, bCodeLinesTitle);
                barcodeStats.WriteLine(MakeTotalMatrix(annotType));
                barcodeStats.WriteLine(MakeFracDevStatsMatrix(annotType));
            }
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

        private void WriteSpikeEfficiencies()
        {
            using (StreamWriter effFile = new StreamWriter(OutputPathbase + "_spike_efficiencies.tab"))
            {
                effFile.Write("Spike\tLength\tAdded#");
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    effFile.Write("\t" + barcodes.Seqs[bcIdx]);
                effFile.WriteLine();
                foreach (string spikeName in labelingEfficiencyEstimator.efficiencyBySpike.Keys)
                {
                    double expected = labelingEfficiencyEstimator.AddedCount(spikeName);
                    effFile.Write("{0}\t{1}\t{2}", spikeName, Annotations.geneFeatures[spikeName].GetTranscriptLength(), (int)Math.Round(expected));
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                        effFile.Write("\t{0}", labelingEfficiencyEstimator.efficiencyBySpike[spikeName][bcIdx]);
                    effFile.WriteLine();
                }
            }
        }

        private void WriteASExonDistributionHistogram()
        {
            int[] histo;
            double firstBinStart, binWidth, median;
            MakeExonAntisenseHistogram(out histo, out median, out firstBinStart, out binWidth);
            if (histo.Length == 1) return;
            using (StreamWriter ASHistFile = new StreamWriter(OutputPathbase + "_ASRPM_Histo.tab"))
            {
                ASHistFile.WriteLine("#Distribution of Antisense RPM/bp transcript among all genes with any Antisense hit.");
                ASHistFile.WriteLine("#BinStart\tCount");
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
            if (overlappingGeneFeatures.Count == 0)
                return;
            Dictionary<string, List<string>> byGene = new Dictionary<string, List<string>>();
            using (StreamWriter redFile = new StreamWriter(OutputPathbase + "_shared_hits.tab"))
            {
                redFile.WriteLine("#Reads\tGenomically overlapping transcripts competing for these reads, that all include them in their MaxExonHits.");
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
                sharedFile.WriteLine("#Transcript\tMinHits\tMaxHits\tNon-unique hits in the difference, that also have been annotated to other overlapping transcripts/variants");
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
                int minHitsToTestSNP = (barcodes.HasUMIs) ? Props.props.MinMoleculesToTestSnp : Props.props.MinReadsToTestSnp;
                string minTxt = (barcodes.HasUMIs) ? "Molecules" : "Reads";
                snpFile.WriteLine("SNP positions found in " + genomeBarcodes.Length + " barcodes belonging to species.");
                snpFile.WriteLine("#(minimum {0} {3}/Pos required to check, limits used heterozygous: {1}-{2}% AltNt and homozygous: >{2}% Alt Nt)",
                                  minHitsToTestSNP, thres, 100 - thres, minTxt);
                snpFile.WriteLine("#Gene\tChr\tmRNALeftChrPos\tSNPChrPos\tType\tRefNt\tTotal\tMut-A\tMut-C\tMut-G\tMut-T");
                foreach (GeneFeature gf in Annotations.geneFeatures.Values)
                {
                    if (gf.bcSNPCountsByRealChrPos == null)
                        continue;
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
                                sb.Append('\t');
                                int c; bool overflow;
                                bcCounts.SummarizeNt(nt, genomeBarcodes, out c, out overflow);
                                if (c >= SNPCountsByBarcode.MaxCount)
                                    sb.Append(">=");
                                if (c > 0) sb.Append(c);
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
                readWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full alwaysZero=on",
                    Path.GetFileNameWithoutExtension(OutputPathbase) + "Read" + DateTime.Now.ToString("yyMMdd"), strand);
                foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                {
                    string chr = data.Key;
                    if (!StrtGenome.IsSyntheticChr(chr) && Annotations.ChromosomeLengths.ContainsKey(chr))
                        data.Value.GetWiggle(strand).WriteWiggle(readWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr], true);
                }
            }
            if (barcodes.HasUMIs)
            {
                using (StreamWriter molWriter = (OutputPathbase + "_" + strandString + "_bymolecule.wig.gz").OpenWrite())
                {
                    molWriter.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} ({1})\" visibility=full alwaysZero=on",
                        Path.GetFileNameWithoutExtension(OutputPathbase) + "Mol" + DateTime.Now.ToString("yyMMdd"), strand);
                    foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                    {
                        string chr = data.Key;
                        if (!StrtGenome.IsSyntheticChr(chr) && Annotations.ChromosomeLengths.ContainsKey(chr))
                            data.Value.GetWiggle(strand).WriteWiggle(molWriter, chr, strand, averageReadLength, Annotations.ChromosomeLengths[chr], false);
                    }
                }
            }
        }

        private void WriteBed()
        {
            int averageReadLength = MappedTagItem.AverageReadLen;
            using (StreamWriter readWriter = (OutputPathbase + "_byread.bed.gz").OpenWrite())
            using (StreamWriter molWriter = (barcodes.HasUMIs)? (OutputPathbase + "_bymol.bed.gz").OpenWrite() : null)
            {
                foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                {
                    string chr = data.Key;
                    if (!StrtGenome.IsSyntheticChr(chr) && Annotations.ChromosomeLengths.ContainsKey(chr))
                    {
                        data.Value.GetWiggle('+').WriteBed(readWriter, chr, '+', averageReadLength, true);
                        data.Value.GetWiggle('-').WriteBed(readWriter, chr, '-', averageReadLength, true);
                        if (molWriter != null)
                        {
                            data.Value.GetWiggle('+').WriteBed(molWriter, chr, '+', averageReadLength, false);
                            data.Value.GetWiggle('-').WriteBed(molWriter, chr, '-', averageReadLength, false);
                        }
                    }
                }
            }

        }

        private void PaintReadIntervals()
        {
            if (Props.props.GenePaintIntervals == null || Props.props.GenePaintIntervals.Length == 0)
                return;
            StreamWriter paintWriter = null;
            string type = (barcodes.HasUMIs) ? "Molecule" : "Read";
            foreach (string paintIvl in Props.props.GenePaintIntervals)
            {
                string[] parts = paintIvl.Split(',');
                if (parts.Length < 3)
                    continue;
                string geneName = parts[0];
                int ivlStart, ivlEnd;
                if (!int.TryParse(parts[1], out ivlStart) || !int.TryParse(parts[2], out ivlEnd) || ivlEnd <= ivlStart)
                    continue;
                foreach (GeneFeature gf in IterMatchingGeneFeatures(geneName))
                {
                    if (ivlStart > gf.End || ivlEnd < gf.Start)
                        continue;
                    if (paintWriter == null)
                    {
                        paintWriter = (OutputPathbase + "_PaintedIntervals.tab").OpenWrite();
                        paintWriter.WriteLine("Gene\tChr\tIvlStartPos\tStrand\tBcIdx\t{0} coverage at consecutive position in interval", type);
                    }
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    {
                        int[] profile = CompactGenePainter.PaintHitsInInterval(gf,
                                                ivlStart, ivlEnd, bcIdx, MappedTagItem.AverageReadLen);
                        paintWriter.Write("{0}\t{1}\t{2}\t{3}\t{4}", gf.Name, gf.Chr, ivlStart, gf.Strand, bcIdx);
                        foreach (int v in profile)
                            paintWriter.Write("\t{0}", v);
                        paintWriter.WriteLine();
                    }
                }
            }
            if (paintWriter != null)
                paintWriter.Close();
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
                    data.Value.GetWiggle('+').GetPositionsAndCounts(out positions, out counts, true);
                    FindHotspots(writer, chr, '+', positions, counts);
                    data.Value.GetWiggle('-').GetPositionsAndCounts(out positions, out counts, true);
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

        private void WriteReadCountDistroByRndTagCount()
        {
            using (StreamWriter writer = new StreamWriter(OutputPathbase + "_ReadCountDistr_by_UMICount.tab"))
            {
                int maxNReads = randomTagFilter.readDistributionByMolCount.GetLength(1) - 1;
                bool nonZeroFound = false;
                while (maxNReads > 0 &&  !nonZeroFound)
                {
                    for (int rndTagCount = 1; rndTagCount <= barcodes.UMICount; rndTagCount++)
                        if (randomTagFilter.readDistributionByMolCount[rndTagCount, maxNReads] > 0)
                        {
                            nonZeroFound = true;
                            break;
                        }
                    maxNReads--;
                }
                writer.Write("#DetectedUMIs\tCasesOf1Read");
                for (int nReads = 2; nReads <= maxNReads; nReads++)
                    writer.Write("\tCasesOf{0}Reads", nReads);
                writer.WriteLine();
                for (int rndTagCount = 1; rndTagCount <= barcodes.UMICount; rndTagCount++)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(rndTagCount);
                    for (int nReads = 1; nReads < maxNReads; nReads++)
                    {
                        sb.Append('\t');
                        sb.Append(randomTagFilter.readDistributionByMolCount[rndTagCount, nReads]);
                    }
                    writer.WriteLine(sb);
                }
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
                int minHitsPerGene = (barcodes.HasUMIs) ? 50 : nSections * 10;
                int maxHitsPerGene = (barcodes.HasUMIs) ? (int)(0.7 * barcodes.UMICount * barcodes.Count) : int.MaxValue;
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
