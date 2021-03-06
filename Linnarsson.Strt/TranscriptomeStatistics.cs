﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.C1;

using System.Diagnostics;

namespace Linnarsson.Strt
{
	public class TranscriptomeStatistics
	{
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
        /// Total exon/splc mappings per barcode. Molecules are singleton filtered and UMI collision compensated.
        /// </summary>
        int[] TotalRealTrMappingsByBarcode;
        int[] TotalSpikeMappingsByBarcode;

        private TotalHitCounter totalHitCounter;

        private GeneExpressionSummary geneExpressionSummary = null;

        int[] nNonAnnotatedItemsByBc;
        int[] nNonAnnotatedMolsByBc;

        LabelingEfficiencyEstimator labelingEfficiencyEstimator;

        GenomeAnnotations Annotations;
		DnaMotif[] motifs;
        int currentBcIdx = 0;
        private string plateId;
        private string resultFolder;
        private string resultFileprefix = null;
        private string OutputPathbase { get { return Path.Combine(resultFolder, resultFileprefix); } }
        private Dictionary<string, int> cellIdByWell;

        /// <summary>
        /// Total number of species mapped reads in each barcode (excluding spikes)
        /// </summary>
        int[] nRealChrMappedReadsByBarcode;
        int[] nSpikeMappedReadsByBarcode;

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
        /// Total number of molecule mappings after filtering false/mutated UMIs, or read mappings when not using UMIs.
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
        // For non-UMI samples the following two will be identical:
        Dictionary<int, List<int>> sampledUniqueMoleculesByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledUniqueHitPositionsByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledFilteredExonMolsByBcIdx = new Dictionary<int, List<int>>();

        // Samplings by barcode:
        bool analyzeMappingsBySpikeReads = false;
        private List<int>[] mappingsBySpikeReadsSamples;

        private UpstreamAnalyzer upstreamAnalyzer;
        private GCAnalyzer gcAnalyzer;
        private PerLaneStats perLaneStats;
        private OverOccupiedUMICounter occupiedUMICounter;

        private StreamWriter UMIProfileByGeneWriter;
        private List<GeneFeature> readsPerMoleculeHistogramGenes;
        private readonly static int maxNReadsInPerMoleculeHistograms = 999;

        Dictionary<string, int> overlappingGeneFeatures = new Dictionary<string, int>();
        List<IFeature> exonHitFeatures;
        private string spliceChrId;
        private static int nMaxMappings;
        private List<GeneFeature> gfsForUMIProfile = new List<GeneFeature>();

        public TranscriptomeStatistics(GenomeAnnotations annotations, string resultFolder, string resultFilePrefix, string plateId)
		{
            this.plateId = plateId;
            this.resultFolder = resultFolder;
            this.resultFileprefix = resultFilePrefix;
            SelectedBcWiggleAnnotations = Props.props.SelectedBcWiggleAnnotations;
            if (Props.props.SelectedBcWiggleAnnotations != null && Props.props.SelectedBcWiggleAnnotations.Length == 0)
                SelectedBcWiggleAnnotations = null;
            if (Props.props.GenerateBarcodedWiggle && SelectedBcWiggleAnnotations != null)
                Console.WriteLine("{0}: Selected annotation types for barcoded wiggle plots: {1}",
                            DateTime.Now, string.Join(",", Array.ConvertAll(SelectedBcWiggleAnnotations, t => AnnotType.GetName(t))));
            Annotations = annotations;
            AnalyzeAllGeneVariants = !Annotations.noGeneVariants;
            SetupMotifs(Props.props);
            nNonAnnotatedItemsByBc = new int[Props.props.Barcodes.Count];
            nNonAnnotatedMolsByBc = new int[Props.props.Barcodes.Count];
            TotalRealTrMappingsByBarcode = new int[Props.props.Barcodes.Count];
            TotalSpikeMappingsByBarcode = new int[Props.props.Barcodes.Count];
            totalHitCounter = new TotalHitCounter(annotations);
            if (Props.props.AnalyzeAllGeneVariants)
                geneExpressionSummary = new GeneExpressionSummary(annotations);
            nRealChrMappedReadsByBarcode = new int[Props.props.Barcodes.Count];
            nSpikeMappedReadsByBarcode = new int[Props.props.Barcodes.Count];
            nMappingsByBarcode = new int[Props.props.Barcodes.Count];
            exonHitFeatures = new List<IFeature>(100);
            spliceChrId = Annotations.Genome.Annotation;
            TagItem.InitTagItemType();
            randomTagFilter = new RandomTagFilterByBc(Props.props.Barcodes, Annotations.GetChromosomeIds());
            mappingAdder = new MappingAdder(annotations, randomTagFilter, Props.props.Barcodes);
            statsSampleDistPerBarcode = Props.props.SampleDistPerBcForAccuStats;
            if (Props.props.AnalyzeSeqUpstreamTSSite && Props.props.Barcodes.Count > 1)
                upstreamAnalyzer = new UpstreamAnalyzer(Annotations, Props.props.Barcodes);
            if (Props.props.AnalyzeGCContent)
                gcAnalyzer = new GCAnalyzer(Props.props.Barcodes.Count);
            if (Props.props.Barcodes.HasUMIs)
                occupiedUMICounter = new OverOccupiedUMICounter(Props.props.Barcodes);
            perLaneStats = new PerLaneStats(Props.props.Barcodes);
            minMismatchReadCountForSNPDetection = Props.props.MinAltNtsReadCountForSNPDetection;
            nMaxMappings = Props.props.MaxAlternativeMappings - 1;
            SetupGfsForUMIProfile();
            labelingEfficiencyEstimator = new LabelingEfficiencyEstimator(Props.props.Barcodes, PathHandler.GetCTRLConcPath(), Props.props.TotalNumberOfAddedSpikeMolecules);
            MappedTagItem.labelingEfficiencyEstimator = labelingEfficiencyEstimator;
            if (Props.props.MappingsBySpikeReadsSampleDist > 0)
                InitMappingsBySpikeReadsSampling();
            if (Props.props.InsertCellDBData)
                cellIdByWell = DBFactory.GetProjectDB().GetWell2CellIdMapping(plateId);
        }

        private void InitMappingsBySpikeReadsSampling()
        {
            analyzeMappingsBySpikeReads = true;
            mappingsBySpikeReadsSamples = new List<int>[Props.props.Barcodes.Count];
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                mappingsBySpikeReadsSamples[bcIdx] = new List<int>();
        }

        private void SetupMotifs(Props props)
        {
            DetermineMotifs = props.DetermineMotifs;
            motifs = new DnaMotif[Props.props.Barcodes.Count];
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i] = new DnaMotif(40);
            }
        }

        private void SetupGfsForUMIProfile()
        {
            if (Props.props.GenesToShowRndTagProfile != null && Props.props.Barcodes.HasUMIs)
            {
                foreach (string geneName in Props.props.GenesToShowRndTagProfile)
                {
                    foreach (GeneFeature gf in IterMatchingGeneFeatures(geneName))
                    {
                        gfsForUMIProfile.Add(gf);
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
            if (!Directory.Exists(resultFolder))
                Directory.CreateDirectory(resultFolder);
            return OutputPathbase;
        }

        public void SetSyntReadReporter(string syntLevelFile)
        {
            TestReporter = new SyntReadReporter(syntLevelFile, Annotations.Genome.GeneVariants, OutputPathbase, Annotations.geneFeatures);
        }

        /// <summary>
        /// Annotate the complete set of map files from a study.
        /// </summary>
        /// <param name="mapFilePaths"></param>
        /// <param name="averageReadLen">average length of the reads in map files. Used during SNP analysis</param>
        public void ProcessMapFiles(List<string> mapFilePaths, int averageReadLen)
        {
            nTooMultiMappingReads = 0;
            trSampleDepth = (Props.props.Barcodes.HasUMIs) ? libraryDepthSampleMolsCountPerBc : libraryDepthSampleReadCountPerBc;
            if (mapFilePaths.Count == 0)
                return;
            if (Props.props.AnalyzeSNPs)
                RegisterPotentialSNPs(mapFilePaths, averageReadLen);
            if (Props.props.SnpRndTagVerification && Props.props.Barcodes.HasUMIs)
                snpRndTagVerifier = new SnpRndTagVerifier(Props.props, Annotations.Genome);
            Console.WriteLine("{0}: Annotatating {1} map files ignoring reads with > {2} alternative mappings.", DateTime.Now, mapFilePaths.Count, nMaxMappings);
            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter = new StreamWriter(AssertOutputPathbase() + "_NONANNOTATED.tab");
                nonExonWriter = new StreamWriter(OutputPathbase + "_NONEXON.tab");
            }

            foreach (Pair<int, List<string>> bcIdxAndMapFilePaths in IterMapFilesGroupedByBcIdx(mapFilePaths))
            {
                ProcessBarcodeMapFiles(bcIdxAndMapFilePaths);
            }
            Console.WriteLine("\n{0}: Ignored {1} reads with >= {2} alternative mappings.", DateTime.Now, nTooMultiMappingReads, Props.props.MaxAlternativeMappings);
            if (Props.props.DebugAnnotation)
            {
                nonAnnotWriter.Close(); nonAnnotWriter.Dispose();
                nonExonWriter.Close(); nonExonWriter.Dispose();
            }
        }

        /// <summary>
        /// Iterate the mapFiles ordered by the barcode index. Take into account remappings of samples
        /// defined in the "Merge" column of the plate layout file.
        /// </summary>
        /// <param name="mapFilePaths"></param>
        /// <returns></returns>
        private IEnumerable<Pair<int, List<string>>> IterMapFilesGroupedByBcIdx(List<string> mapFilePaths)
        {
            Dictionary<int, List<string>> filesByBcIdx = new Dictionary<int, List<string>>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string mapFileName = Path.GetFileName(mapFilePath);
                int bcIdx = int.Parse(mapFileName.Substring(0, mapFileName.IndexOf('_')));
                bcIdx = Props.props.Barcodes.GetWellIdxFromBcIdx(bcIdx);
                if (!filesByBcIdx.ContainsKey(bcIdx))
                    filesByBcIdx[bcIdx] = new List<string>();
                filesByBcIdx[bcIdx].Add(mapFilePath);
            }
            int[] bcIndexes = filesByBcIdx.Keys.ToArray();
            Array.Sort(bcIndexes);
            foreach (int bcIdx in bcIndexes)
                yield return new Pair<int, List<string>>(bcIdx, filesByBcIdx[bcIdx]);
        }

        private void RegisterPotentialSNPs(List<string> mapFilePaths, int averageReadLen)
        {
            MapFileSnpFinder mfsf = new MapFileSnpFinder(Props.props.Barcodes);
            mfsf.ProcessMapFiles(mapFilePaths);
            int nSNPs = randomTagFilter.SetupSNPCounters(averageReadLen, mfsf.IterSNPLocations(minMismatchReadCountForSNPDetection));
            Console.WriteLine("{0}: Registered {1} potential expressed SNPs (positions with >= {2} mismatch reads).", 
                                DateTime.Now, nSNPs, minMismatchReadCountForSNPDetection);
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
                if (!Props.props.LogMode) Console.Write(".");
                if (File.Exists(mapFilePath))
                    AddReadMappingsToTagItems(mapFilePath);
            }
            SampleReadStatistics();
            AnnotateFeaturesFromTagItems();
            WriteCurrentBcWiggle();
            GenerateReadsPerUMIRelatedData();
            randomTagFilter.FinishBarcode();
            labelingEfficiencyEstimator.FinishBarcode(currentBcIdx);
        }

        private void AddReadMappingsToTagItems(string mapFilePath)
        {
            int nTotalMappedBcReads = nRealChrMappedReadsByBarcode[currentBcIdx] + nSpikeMappedReadsByBarcode[currentBcIdx];
            MapFile mapFileReader = MapFile.GetMapFile(mapFilePath);
            if (mapFileReader == null)
                throw new Exception("Unknown read map file type : " + mapFilePath);
            int sampleMappedReadsByFileCounter = PerLaneStats.nMappedReadsPerFileAtSample;
            int sampleSpikeReadsPerMolCounter = Props.props.MappingsBySpikeReadsSampleDist;
            perLaneStats.BeforeFile(currentBcIdx, nTotalMappedBcReads, mappingAdder.NUniqueReadSignatures(currentBcIdx),
                                    randomTagFilter.GetNumDistinctMappings());
            foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
            {
                if (mrm.NMappings > nMaxMappings)
                {
                    nTooMultiMappingReads++;
                    continue;
                }
                mrm.BcIdx = currentBcIdx; // Needs to be set, and we may use layout 'Merge' column to combine one sample loaded in different barcodes into one well/barcode
                bool mapsToCtrl = mrm[0].Chr == Props.props.ChrCTRLId;
                if (mappingAdder.Add(mrm))
                {
                    nExonAnnotatedReads++;
                    if (Props.props.AnalyzeGCContent && Annotations.HasChrSeq(mrm[0].Chr))
                        gcAnalyzer.Add(currentBcIdx, Annotations.GetChrSeq(mrm[0].Chr).SubSequence(mrm[0].Position, mrm[0].SeqLen));
                    if (analyzeMappingsBySpikeReads && mapsToCtrl)
                    {
                        if (--sampleSpikeReadsPerMolCounter == 0)
                        {
                            mappingsBySpikeReadsSamples[currentBcIdx].Add(randomTagFilter.GetNumDistinctMappings());
                            sampleSpikeReadsPerMolCounter = Props.props.MappingsBySpikeReadsSampleDist;
                        }
                    }
                }
                if (snpRndTagVerifier != null)
                    snpRndTagVerifier.Add(mrm);
                nTotalMappedBcReads++;
                if (mapsToCtrl)
                    nSpikeMappedReadsByBarcode[currentBcIdx]++;
                else
                    nRealChrMappedReadsByBarcode[currentBcIdx]++;
                if (nTotalMappedBcReads % statsSampleDistPerBarcode == 0)
                    SampleReadStatistics();
                if (nTotalMappedBcReads == libraryDepthSampleReadCountPerBc)
                {
                    sampledLibraryDepths.Add(randomTagFilter.GetNumDistinctMappings());
                    sampledUniqueMolecules.Add(mappingAdder.NUniqueReadSignatures(currentBcIdx));
                }
                if (--sampleMappedReadsByFileCounter == 0)
                {
                    perLaneStats.AfterFile(mapFilePath, nTotalMappedBcReads, mappingAdder.NUniqueReadSignatures(currentBcIdx),
                                                        randomTagFilter.GetNumDistinctMappings());
                    sampleMappedReadsByFileCounter = PerLaneStats.nMappedReadsPerFileAtSample;
                }
                if (mrm.HasMultipleMappings) nMultiReads++;
            }
        }

        private void AnnotateFeaturesFromTagItems()
        {
            List<string> ctrlChrId = new List<string>();
            if (randomTagFilter.chrTagDatas.ContainsKey(Props.props.ChrCTRLId))
            { // First process CTRL chromosome to get the labeling efficiency
				labelingEfficiencyEstimator.ResetEfficiency();
                ctrlChrId.Add(Props.props.ChrCTRLId);
                foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, true))
                    Annotate(mtitem);
                double efficiency = labelingEfficiencyEstimator.CalcEfficiencyFromSpikes(Annotations.geneFeatures.Values, currentBcIdx);
				foreach (KeyValuePair<string, GeneFeature> p in Annotations.geneFeatures.Where(kvp => ctrlChrId.Contains(kvp.Value.Chr))) {
					p.Value.ScaleTrueCounts(currentBcIdx, efficiency);
				}
            }
            foreach (MappedTagItem mtitem in randomTagFilter.IterItems(currentBcIdx, ctrlChrId, false))
                Annotate(mtitem);
        }

        public void SampleReadStatistics()
        {
            if (!sampledUniqueHitPositionsByBcIdx.ContainsKey(currentBcIdx))
            {
                sampledUniqueMoleculesByBcIdx[currentBcIdx] = new List<int>();
                sampledUniqueHitPositionsByBcIdx[currentBcIdx] = new List<int>();
                if (Props.props.SampleAccuFilteredExonMols)
                    sampledFilteredExonMolsByBcIdx[currentBcIdx] = new List<int>();
            }
            sampledUniqueMoleculesByBcIdx[currentBcIdx].Add(mappingAdder.NUniqueReadSignatures(currentBcIdx));
            sampledUniqueHitPositionsByBcIdx[currentBcIdx].Add(randomTagFilter.GetNumDistinctMappings());
            if (Props.props.SampleAccuFilteredExonMols)
                sampledFilteredExonMolsByBcIdx[currentBcIdx].Add(randomTagFilter.GetCurrentNumFilteredExonMols());
        }

        public void Annotate(MappedTagItem item)
        {
            MarkStatus markStatus;
            bool someAnnotationHit = false;
            bool someTrHit = false;
            try
            {
                int molCount = item.MolCount;
                nMappingsByBarcode[currentBcIdx] += molCount;
                exonHitFeatures.Clear();
                foreach (FtInterval trMatch in Annotations.IterTranscriptMatches(item.chr, item.SequencedStrand, item.HitMidPos))
                {
                    someTrHit = someAnnotationHit = true;
                    markStatus = (IterTranscriptMatchers.HasVariants || item.HasAltMappings) ? MarkStatus.NONUNIQUE_EXON_MAPPING : MarkStatus.UNIQUE_EXON_MAPPING;
                    if (!exonHitFeatures.Contains(trMatch.Feature))
                    { // If a gene is hit multiple times (happens if two diff. splices have same seq.), we should annotate it only once
                        exonHitFeatures.Add(trMatch.Feature);
                        item.splcToRealChrOffset = 0;
                        int annotType = trMatch.Mark(item, trMatch.ExtraData, markStatus);
                        if (item.chr == Props.props.ChrCTRLId)
                            totalHitCounter.Add(AnnotType.CTRL, item);
                        else
                            totalHitCounter.Add(annotType, item);
                        item.SetTypeOfAnnotation(annotType);
                    }
                }
                if (someTrHit)
                {
                    if (item.chr == Props.props.ChrCTRLId)
                        TotalSpikeMappingsByBarcode[currentBcIdx] += molCount;
                    else
                        TotalRealTrMappingsByBarcode[currentBcIdx] += molCount;
                    if (geneExpressionSummary != null)
                        geneExpressionSummary.Summarize(item, exonHitFeatures);
                    if (upstreamAnalyzer != null)
                        upstreamAnalyzer.CheckSeqUpstreamTSSite(item, currentBcIdx);
                    if (exonHitFeatures.Count > 1)
                        RegisterOverlappingGeneFeatures(molCount);
                    if (occupiedUMICounter != null)
                        occupiedUMICounter.Check(item, exonHitFeatures);
                }
                else if (item.chr != spliceChrId)
                { // Annotate all features of molecules that do not map to any transcript
                    foreach (FtInterval nonTrMatch in Annotations.IterNonTrMatches(item.chr, item.SequencedStrand, item.HitMidPos))
                    {
                        someAnnotationHit = true;
                        int annotType = nonTrMatch.Mark(item, nonTrMatch.ExtraData, MarkStatus.NONEXONIC_MAPPING);
                        totalHitCounter.Add(annotType, item);
                        item.SetTypeOfAnnotation(annotType);
                    }
                }
                if (someAnnotationHit)
                {
                    nAnnotatedMappings += molCount;
                    if (DetermineMotifs && item.chr != spliceChrId && !item.HasAltMappings && Annotations.HasChrSeq(item.chr))
                        motifs[currentBcIdx].Add(Annotations.GetChrSeq(item.chr), item.hitStartPos - 20 - 1, item.DetectedStrand);
                }
                else
                {
                    nNonAnnotatedItemsByBc[currentBcIdx]++;
                    nNonAnnotatedMolsByBc[currentBcIdx] += molCount;
                }
                int t = nMappingsByBarcode[currentBcIdx] - trSampleDepth;
                if (t > 0 && t <= molCount) // Sample if we just passed the sampling point with current MappedTagItem
                    sampledExpressedTranscripts.Add(Annotations.GetNumExpressedTranscripts(currentBcIdx));
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in TranscriptomeStatistics.Annotate(TagItem item): " + e);
                Console.WriteLine("\nsomeAnnotHit=" + someAnnotationHit);
                Console.WriteLine(" someTrHit=" + someTrHit);
                Console.WriteLine(" Bc=" + item.bcIdx);
                Console.WriteLine(" Chr=" + item.chr);
                Console.WriteLine(" MidPos=" + item.HitMidPos);
                Console.WriteLine(" hitStartPos=" + item.hitStartPos);
                Console.WriteLine(" DetextedStrand=" + item.DetectedStrand);
                Console.WriteLine(" HasAltMappings=" + item.HasAltMappings);
                Console.WriteLine(" SequencedStrand=" + item.SequencedStrand);
                Console.WriteLine(" MolCount=" + item.MolCount);
                Console.WriteLine(" spliceToRealChrOffset=" + item.splcToRealChrOffset);
                Console.WriteLine(" exonHitFeatres=" + exonHitFeatures);
                Console.WriteLine(" sampledExpreseedTrs=" + sampledExpressedTranscripts);
                Console.WriteLine(" nMappingsByBarcode=" + nMappingsByBarcode);
                Console.WriteLine(" nNonAnnotationedItemsByBc=" + nNonAnnotatedItemsByBc);
                Console.WriteLine(" nNonAnnotMolsByBC=" + nNonAnnotatedMolsByBc);
                Console.WriteLine(" Annotations=" + Annotations);
                Console.WriteLine(" DetermineMotifs=" + DetermineMotifs);
                Console.WriteLine(" spliceChrId=" + spliceChrId);
                Console.WriteLine(" TotalRealTrMolsByBc=" + TotalRealTrMappingsByBarcode);
                Console.WriteLine(" TotalSpikeMolsByBc=" + TotalSpikeMappingsByBarcode);
                Console.WriteLine(" totalHitCounter=" + totalHitCounter);
            }
        }

        /// <summary>
        /// Called when a read hits several genes at the same mapping position. Some genes may overlap, but
        /// should in 'single' mode only happen if two alt. splices match the read and point to the same true genomic position.
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

        private void GenerateReadsPerUMIRelatedData()
        {
            if (!Props.props.Barcodes.HasUMIs || !TagItem.CountsReadsPerUMI) return;
            if (Props.props.MakeGeneReadsPerMoleculeHistograms)
                AddToGeneReadsPerMoleculeHistograms();
            MakeGeneUMIProfiles();
            if (Props.props.GenerateReadCountsByUMI)
                WriteReadCountsByUMI();
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

        private void MakeGeneUMIProfiles()
        {
            foreach (GeneFeature gf in gfsForUMIProfile)
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
                        if (UMIProfileByGeneWriter == null)
                        {
                            string file = AssertOutputPathbase() + "_UMI_profiles.tab";
                            UMIProfileByGeneWriter = file.OpenWrite();
                            UMIProfileByGeneWriter.WriteLine("#Gene\tBarcode\tChr\tStrand\tChrPos\tTrPos(>=1)\tEstMolCount\tReadCountsByUMIIdx");
                        }
                        UMIProfileByGeneWriter.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", 
                            gf.Name, Props.props.Barcodes.Seqs[currentBcIdx], gf.Chr, gf.Strand, chrPos, trPos, estMolCount);
                        foreach (int count in profile)
                            UMIProfileByGeneWriter.Write("\t{0}", count);
                        UMIProfileByGeneWriter.WriteLine();
                    }
                    trPos += trDir;
                }
            }
        }

        private void WriteReadCountsByUMI()
        {
            string readsByUMISubfolder = AssertOutputPathbase() + "_read_counts_by_UMI";
            if (!Directory.Exists(readsByUMISubfolder))
                Directory.CreateDirectory(readsByUMISubfolder);
            string filename = Path.Combine(readsByUMISubfolder, Props.props.Barcodes.GetWellId(currentBcIdx) + "_reads.txt");
            if (File.Exists(filename)) return;
            using (StreamWriter writer = filename.OpenWrite())
            {
                foreach (KeyValuePair<string, ChrTagData> tagDataPair in randomTagFilter.chrTagDatas)
                {
                    string chr = tagDataPair.Key;
                    foreach (MappedTagItem t in tagDataPair.Value.IterItems(currentBcIdx, chr))
                    {
                        ushort[] d = t.tagItem.GetReadCountsByUMI();
                        for (int UMIIdx = 0; UMIIdx < d.Length; UMIIdx++)
                        {
                            if (d[UMIIdx] > 0)
                                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", chr, t.SequencedStrand, t.hitStartPos, UMIIdx, d[UMIIdx]);
                        }
                    }
                }
            }
        }

        /// <summary>
		///  Write all output files
		/// </summary>
        /// <param name="readCounter">Holder of types of reads in input</param>
        /// <param name="resultDescr"></param>
		public void SaveResult(ReadCounter readCounter, ResultDescription resultDescr)
		{
            Annotations.SaveResult(OutputPathbase, plateId, MappedTagItem.AverageReadLen);
            WriteSummary(readCounter, resultDescr);
            AddQC(readCounter, resultDescr);
            WriteWiggleAndBed();
            if (Props.props.OutputLevel <= 1) return;

            if (geneExpressionSummary != null)
                geneExpressionSummary.WriteOutput(OutputPathbase);
            if (Props.props.OutputLevel <= 2) return;

            if (Props.props.WriteHotspots)
                WriteHotspots();
            if (occupiedUMICounter != null)
                occupiedUMICounter.WriteOutput(OutputPathbase);
            PaintReadIntervals();
            if (upstreamAnalyzer != null)
                upstreamAnalyzer.WriteUpstreamStats(OutputPathbase);
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            WriteRedundantExonHits();
            WriteMappingsBySpikeReads();
            WriteSpikeEfficiencies();
            WriteHitProfilesByBarcode();
            WriteASExonDistributionHistogram();
            if (Props.props.Barcodes.HasUMIs)
            {
                if (TagItem.CountsReadsPerUMI)
                    WriteReadCountDistroByUMICount();
                if (readsPerMoleculeHistogramGenes != null)
                    WriteGeneReadsPerMoleculeHistograms();
            }
            if (UMIProfileByGeneWriter != null)
            {
                UMIProfileByGeneWriter.Close();
                UMIProfileByGeneWriter.Dispose();
            }
            if (snpRndTagVerifier != null)
                snpRndTagVerifier.Verify(OutputPathbase);
            if (Props.props.AnalyzeSNPs)
                WriteSnps();
            if (DetermineMotifs)
                WriteSequenceLogos();
        }

        private void WriteMappingsBySpikeReads()
        {
            if (Props.props.MappingsBySpikeReadsSampleDist == 0)
                return;
            using (StreamWriter mbsFile = new StreamWriter(OutputPathbase + "_mappings_by_processed_spike_reads.tab"))
            {
                mbsFile.WriteLine("Development of number of detected unique exon mappings as function of number of spike reads processed in each well.");
                mbsFile.Write("Well\\#Spikes");
                int maxNSamples = mappingsBySpikeReadsSamples.Max(v => v.Count);
                for (int i = 1; i <= maxNSamples; i++)
                    mbsFile.Write("\t{0}", i * Props.props.MappingsBySpikeReadsSampleDist);
                mbsFile.WriteLine();
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                {
                    mbsFile.Write(Props.props.Barcodes.GetWellId(bcIdx));
                    foreach (int v in mappingsBySpikeReadsSamples[bcIdx])
                        mbsFile.Write("\t{0}", v);
                    mbsFile.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write sequence logo data for each barcode
        /// </summary>
        private void WriteSequenceLogos()
        {
            string logoDir = Directory.CreateDirectory(OutputPathbase + "_sequence_logos").FullName;
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i].Save(Path.Combine(logoDir, Props.props.Barcodes.Seqs[i] + "_motif.txt"));
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
                xmlFile.WriteLine("<strtSummary project=\"{0}\">", resultFileprefix);
                WriteSettings(xmlFile, resultDescr);
                WriteReadStats(readCounter, xmlFile);
                WriteHitStats(xmlFile);
                xmlFile.WriteLine("  <librarycomplexity>\n" +
                                  "    <title>Median values from [indicated number of] barcodes</title>\n" +
                                  "    <point x=\"Unique molecules [{6}] after {0} reads\" y=\"{2}\" />\n" +
                                  "    <point x=\"Distinct mappings [{7}] after {0} reads\" y=\"{1}\" />\n" +
                                  "    <point x=\"Expressed transcripts [{8}] after {3} {5}\" y=\"{4}\" />\n" +
                                  "  </librarycomplexity>", libraryDepthSampleReadCountPerBc,
                                     DefaultMedian(sampledLibraryDepths, "N/A"), DefaultMedian(sampledUniqueMolecules, "N/A"),
                                     trSampleDepth, DefaultMedian(sampledExpressedTranscripts, "N/A"),
                                     (Props.props.Barcodes.HasUMIs) ? "molecules" : "reads", sampledLibraryDepths.Count,
                                     sampledUniqueMolecules.Count, sampledExpressedTranscripts.Count);
                WriteHitsBySpecies(xmlFile);
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
            foreach (GeneFeature gf in Annotations.IterOrderedGeneFeatures(true, false))
            {
                if (!gf.IsExpressed())
                    continue;
                int total = 0;
                int detected = 0;
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                {
                    int bcHits = gf.TrHits(bcIdx);
                    total += bcHits;
                    if (bcHits > 0) detected++;
                }
                double fraction = (detected / (double)Props.props.Barcodes.Count);
                string spikeId = gf.Name.Replace("RNA_SPIKE_", "Old").Replace("ERCC-00", "");
                sbt.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0}\" />\n", spikeId, total));
                sbf.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0.###}\" />\n", spikeId, fraction));
            }
            if (sbt.Length > 0)
            {
                xmlFile.WriteLine("  <spikedetection>");
                xmlFile.WriteLine("    <title>Detection of spikes across all {0} wells</title>", Props.props.Barcodes.Count);
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
            xmlFile.WriteLine("    <BarcodeSet>{0}</BarcodeSet>", Props.props.BarcodesName);
            xmlFile.WriteLine("    <GenomeBuild>{0}</GenomeBuild>", Annotations.Genome.BuildVarAnnot);
            xmlFile.WriteLine("    <AlignerIndexVersion>{0}</AlignerIndexVersion>", resultDescr.splcIndexVersion);
            xmlFile.WriteLine("    <Aligner>{0}</Aligner>", Props.props.Aligner);
            xmlFile.WriteLine("    <DirectionalReads>{0}</DirectionalReads>", Props.props.DirectionalReads);
            xmlFile.WriteLine("    <UMIMutationFilter>{0}/{1}</UMIMutationFilter>", Props.props.RndTagMutationFilter, Props.props.RndTagMutationFilterParam);
            xmlFile.WriteLine("    <MaxAlternativeAlignments>{0}</MaxAlternativeAlignments>", Props.props.MaxAlternativeMappings);
            xmlFile.WriteLine("    <MultireadMappingMode>{0}</MultireadMappingMode>", Props.props.MultireadMappingMode);
            xmlFile.WriteLine("    <UseRPKM>{0}</UseRPKM>", Props.props.UseRPKM);
            xmlFile.WriteLine("    <MaxFeatureLength>{0}</MaxFeatureLength>", Props.props.MaxFeatureLength);
            xmlFile.WriteLine("    <GeneFeature5PrimeExtension>{0}</GeneFeature5PrimeExtension>", Props.props.GeneFeature5PrimeExtension);
            xmlFile.WriteLine("    <LocusFlankLength>{0}</LocusFlankLength>", Props.props.LocusFlankLength);
            if (Props.props.AnalyzeSNPs && Props.props.Barcodes.HasUMIs)
                xmlFile.WriteLine("    <MinMoleculesToTestSnp>{0}</MinMoleculesToTestSnp>", Props.props.MinMoleculesToTestSnp);
            if (Props.props.AnalyzeSNPs && !Props.props.Barcodes.HasUMIs)
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
            int nKeys = sampledUniqueHitPositionsByBcIdx.Keys.Count;
            int[] orderedBcIndexes = new int[nKeys];
            int[] orderedMaxCounts = new int[nKeys];
            int i = 0;
            foreach (KeyValuePair<int, List<int>> p in sampledUniqueHitPositionsByBcIdx)
            {
                orderedBcIndexes[i] = p.Key;
                orderedMaxCounts[i++] = p.Value[p.Value.Count - 1];
            }
            Sort.QuickSort(orderedMaxCounts, orderedBcIndexes);
            if (nKeys > 24)
            {
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mapping positions (low end) vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, orderedBcIndexes.Take(nKeys / 2));
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mapping positions (high end) vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, orderedBcIndexes.Reverse().Take(nKeys / 2));
            }
            else
                WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mapping positions vs. mapped reads processed",
                                       sampledUniqueHitPositionsByBcIdx, orderedBcIndexes.Reverse());
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (!Props.props.Barcodes.HasUMIs) return;
            xmlFile.WriteLine("  <randomtagfrequence>");
            xmlFile.WriteLine("    <title>Number of reads detected in each UMI</title>");
            xmlFile.WriteLine("    <xtitle>UMI index (AA...-TT...)</xtitle>");
            for (int i = 0; i < randomTagFilter.nReadsByUMI.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", Props.props.Barcodes.MakeUMISeq(i), randomTagFilter.nReadsByUMI[i]);
            xmlFile.WriteLine("  </randomtagfrequence>");
            xmlFile.WriteLine("  <nuniqueateachrandomtagcoverage>");
            xmlFile.WriteLine("    <title>Unique alignmentposition-barcodes vs. # UMIs they occur in</title>");
            xmlFile.WriteLine("    <xtitle>Number of different UMIs</xtitle>");
            for (int i = 1; i < randomTagFilter.nCasesPerUMICount.Length; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.nCasesPerUMICount[i]);
            xmlFile.WriteLine("  </nuniqueateachrandomtagcoverage>");
            WriteAccuMoleculesByBc(xmlFile, "moleculedepthbybc", "Distinct detected molecules (all feature types) vs. mapped reads processed",
                                   sampledUniqueMoleculesByBcIdx);
            if (Props.props.SampleAccuFilteredExonMols)
                WriteAccuMoleculesByBc(xmlFile, "filteredmoleculesbybc", "EXON molecules (mutation filtered, not UMI collision compensated) vs. mapped reads processed",
                                   sampledFilteredExonMolsByBcIdx);
            if (TagItem.CountsReadsPerUMI)
                WriteReadsPerMolDistros(xmlFile);
        }

        private void WriteReadsPerMolDistros(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <moleculereadscountshistogram>");
            xmlFile.WriteLine("    <title>Distro of reads/molecule (all features, after mutated UMI removal) TotMols={0}(before)/{1}(after removal)</title>",
                              randomTagFilter.totalMolecules, randomTagFilter.totalFilteredMolecules);
            xmlFile.WriteLine("    <xtitle>Number of observations (reads)</xtitle>");
            int lastNonZeroIdx = Array.FindLastIndex(randomTagFilter.readsPerMolHistogram, v => v > 0);
            for (int i = 1; i <= lastNonZeroIdx; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.readsPerMolHistogram[i]);
            xmlFile.WriteLine("  </moleculereadscountshistogram>");
            xmlFile.WriteLine("  <readspertrmolhistogram>");
            xmlFile.WriteLine("    <title>Distro of reads/molecule (EXON, after mutated UMI removal) TotExonMols={0}(before)/{1}(after removal)</title>",
                              randomTagFilter.totalTrMolecules, randomTagFilter.totalFilteredTrMolecules);
            xmlFile.WriteLine("    <xtitle>Number of observations (reads)</xtitle>");
            lastNonZeroIdx = Array.FindLastIndex(randomTagFilter.readsPerTrMolHistogram, v => v > 0);
            for (int i = 1; i <= lastNonZeroIdx; i++)
                xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.readsPerTrMolHistogram[i]);
            xmlFile.WriteLine("  </readspertrmolhistogram>");
        }

        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data)
        {
            WriteAccuMoleculesByBc(xmlFile, tag, title, data, data.Keys);
        }
        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title,
                                            Dictionary<int, List<int>> data, IEnumerable<int> orderedSelectedBcIndexes)
        {
            bool anyData = false;
            foreach (int bcIdx in orderedSelectedBcIndexes)
            {
                if (!data.ContainsKey(bcIdx) || data[bcIdx].Count < 2)
                    continue;
                List<int> curve = data[bcIdx];
                if (!anyData)
                {
                    xmlFile.WriteLine("  <{0}>", tag);
                    xmlFile.WriteLine("    <title>{0}</title>", title);
                    xmlFile.WriteLine("    <xtitle>Millions of mapped reads</xtitle>");
                    anyData = true;
                }
                string legend = string.Format("{0}[Max={1}]", Props.props.Barcodes.GetWellId(bcIdx), curve[curve.Count - 1]);
                xmlFile.WriteLine("      <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  legend, (bcIdx * 47) % 255, (bcIdx * 21) % 255, (255 - (60 * bcIdx % 255)));
                int nReads = 0;
                int i = 0;
                for (; i < curve.Count - 1; i++)
                {
                    nReads += statsSampleDistPerBarcode;
                    xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                }
                nReads = nRealChrMappedReadsByBarcode[bcIdx] + nSpikeMappedReadsByBarcode[bcIdx];
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                xmlFile.WriteLine("    </curve>");
            }
            if (anyData)
                xmlFile.WriteLine("  </{0}>", tag);
        }

        private void WritePerLaneStats(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <fracuniqueperlane>");
            string type = Props.props.Barcodes.HasUMIs ? "molecules" : "mappings";
            xmlFile.WriteLine("    <title>Fraction distinct {0} among first {1} mapped reads in each lane</title>",
                              type, PerLaneStats.nMappedReadsPerFileAtSample);
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
            {
                List<Pair<string, double>> data = perLaneStats.GetComplexityIndex(bcIdx);
                string legend = Props.props.Barcodes.GetWellId(bcIdx);
                xmlFile.WriteLine("    <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  legend, (bcIdx * 47) % 255, (bcIdx * 21) % 255, (255 - (60 * bcIdx % 255)));
                foreach (Pair<string, double> laneAndFrac in data)
                {
                    if (laneAndFrac.Second > 0.0)
                        xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1:0.0000000}\" />", laneAndFrac.First, laneAndFrac.Second);
                }
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </fracuniqueperlane>");
        }

        private void AddQC(ReadCounter readCounter, ResultDescription resultDescr)
        {
            double totReads = readCounter.TotalAnalyzedReads;
            double unknownBcFrac = readCounter.ReadCount(ReadStatus.UNKNOWN_BC) / totReads;
            resultDescr.setStat(ResultDescription.ResultStats.UnknownBc, unknownBcFrac);
            double tn5Frac = (readCounter.ReadCount(ReadStatus.NO_BC_TN5) + readCounter.ReadCount(ReadStatus.NO_TSSEQ_TN5)) / totReads;
            resultDescr.setStat(ResultDescription.ResultStats.Tn5, tn5Frac);
            List<int> bcTrCounts = new List<int>();
            int[] onlyGenomeBcIndexes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
			if (onlyGenomeBcIndexes.Length == 0)
				onlyGenomeBcIndexes = Enumerable.Range(0, Props.props.Barcodes.Count).ToArray();
            foreach (int bcIdx in onlyGenomeBcIndexes)
                bcTrCounts.Add(TotalRealTrMappingsByBarcode[bcIdx]);
            double trMolsPerBc = bcTrCounts.Average();
            resultDescr.setStat(ResultDescription.ResultStats.AverageBcMolCount, trMolsPerBc);
        }


        private void WriteReadStats(ReadCounter readCounter, StreamWriter xmlFile)
        {
            int[] speciesBarcodes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            int spBcCount = speciesBarcodes.Length;
            xmlFile.WriteLine("  <readfiles>");
            foreach (FileReads fr in readCounter.GetReadFiles())
                xmlFile.WriteLine("    <readfile path=\"{0}\" totalReads=\"{1}\" validExtractedReads=\"{2}\" averageValidReadLen=\"{3:0.000}\"/>", 
                                   fr.path, ((fr.readCount==0)? "---" : fr.readCount.ToString()),
                                            ((fr.readCount==0)? "---" : fr.validReadCount.ToString()), fr.AverageValidReadLen);
            xmlFile.WriteLine("  </readfiles>");
            double allBcReads = readCounter.TotalAnalyzedReads;
            if (allBcReads > 0)
            {
                xmlFile.WriteLine("  <readstatus>");
                xmlFile.WriteLine("    <title>Reads (10^6) sorted by errors and artefacts.</title>");
                for (int readStatus = 0; readStatus < ReadStatus.Count; readStatus++)
                {
                    int c = readCounter.ReadCount(readStatus);
                    if (c > 0)
                        xmlFile.WriteLine("    <point x=\"{0} ({1:0.00%})\" y=\"{2}\" />",
                                             ReadStatus.GetName(readStatus), c / allBcReads, c / 1.0E6d);
                }
                xmlFile.WriteLine("  </readstatus>");
            }
            xmlFile.WriteLine("  <reads>");
            xmlFile.WriteLine("    <title>Read distribution (10^6) [#wells in brackets]</title>");
            double totalReads = readCounter.TotalReads();
            double speciesReads = readCounter.TotalReads(speciesBarcodes);
            double speciesRealChrMappedReads = SumAtIndices(speciesBarcodes, nRealChrMappedReadsByBarcode);
            double speciesSpikeMappedReads = SumAtIndices(speciesBarcodes, nSpikeMappedReadsByBarcode);
            if (speciesReads > 0 && allBcReads > 0)
            {
                if (readCounter.LimiterExcludedReads > 0)
                    xmlFile.WriteLine("    <point x=\"Limiter excluded\" y=\"{0}\" />", readCounter.LimiterExcludedReads / 1.0E6d);
                //xmlFile.WriteLine("    <point x=\"Processed reads\" y=\"{0}\" />", allBcReads / 1.0E6d);
                //xmlFile.WriteLine("    <point x=\"PF reads\" y=\"{0}\" />", readCounter.PassedIlluminaFilter / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Total reads [{0}] \" y=\"{1}\" />", Props.props.Barcodes.Count, totalReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"In {0} wells [{1}] (100%)\" y=\"{2}\" />",
                                             Annotations.Genome.Abbrev, spBcCount, speciesReads / 1.0E6d);
                int validReads = readCounter.ValidReads(speciesBarcodes);
                xmlFile.WriteLine("    <point x=\"Valid [{0}] ({1:0.0%})\" y=\"{2}\" />",
                                             spBcCount, validReads / speciesReads, validReads / 1.0E6d);
            }
            else if (allBcReads > 0)
            { // Old versions of extraction summary files without the total-reads-per-barcode data
                speciesReads = allBcReads;
                xmlFile.WriteLine("    <point x=\"All PF reads (100%)\" y=\"{0}\" />", allBcReads / 1.0E6d);
                int validReads = readCounter.ValidReads(speciesBarcodes);
                xmlFile.WriteLine("    <point x=\"Valid [{0}] ({1:0.0%})\" y=\"{2}\" />",
                                            spBcCount, validReads / speciesReads, validReads / 1.0E6d);
            }
            else
                speciesReads = speciesRealChrMappedReads; // Default to nMappedReads if extraction summary files are missing
            double mapped = speciesRealChrMappedReads + speciesSpikeMappedReads;
            xmlFile.WriteLine("    <point x=\"Mapped total [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, mapped / speciesReads, mapped / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"> transcript [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nExonAnnotatedReads / speciesReads, nExonAnnotatedReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"> spike [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, speciesSpikeMappedReads / speciesReads, speciesSpikeMappedReads / 1.0E6d);
            //xmlFile.WriteLine("    <point x=\"Multireads [{0}] ({1:0.0%})\" y=\"{2}\" />", spBcCount, nMultiReads / speciesReads, nMultiReads / 1.0E6d);
            //if (Props.props.Barcodes.HasUMIs)
            //    xmlFile.WriteLine("    <point x=\"PCR amplified [{0}] ({1:0.0%})\" y=\"{2}\" />", 
            //                      spBcCount, mappingAdder.TotalNDuplicateReads / speciesReads, mappingAdder.TotalNDuplicateReads / 1.0E6d);
            xmlFile.WriteLine("  </reads>");
        }

        private static int SumAtIndices(int[] indices, int[] data)
        {
            int sum = 0;
            foreach (int idx in indices)
                sum += data[idx];
            return sum;
        }

        private void WriteHitStats(StreamWriter xmlFile)
        {
            int nAllHits = totalHitCounter.TotalHits;
            double dividend = Props.props.Barcodes.HasUMIs? nMappings : nAllHits;
            double reducer = Props.props.Barcodes.HasUMIs? 1.0E3d : 1.0E6d;
            string mrHead = GetMultireadSubHeader();
            string molTitle = (Props.props.Barcodes.HasUMIs) ? "Molecule" : "Read";
            xmlFile.WriteLine("  <hits>");
            xmlFile.WriteLine("    <title>{0} mappings distribution (10^{1})\n[{2}]</title>", molTitle, (int)Math.Log10(reducer), mrHead);
            xmlFile.WriteLine("    <point x=\"Total mappings ({0:0%})\" y=\"{1}\" />", nMappings / dividend, nMappings / reducer);
            xmlFile.WriteLine("    <point x=\"mappings at feature(s) ({0:0.0%})\" y=\"{1}\" />", nAnnotatedMappings / dividend, nAnnotatedMappings / reducer);
            xmlFile.WriteLine("    <point x=\"Total feature hits ({0:0.0%})\" y=\"{1}\" />", nAllHits / dividend, nAllHits / reducer);
            int nExonMappings = TotalRealTrMappingsByBarcode.Sum();
            xmlFile.WriteLine("    <point x=\"> {0} trnscrpt ({1:0.0%})\" y=\"{2}\" />", Annotations.Genome.Name.ToLower(), nExonMappings / dividend, nExonMappings / reducer);
            int nSpikeMappings = TotalSpikeMappingsByBarcode.Sum();
            xmlFile.WriteLine("    <point x=\"> spike ({0:0.0%})\" y=\"{1}\" />", nSpikeMappings / dividend, nSpikeMappings / reducer);
            int nIntronHits = totalHitCounter.GetTotalAnnotHits(AnnotType.INTR);
            xmlFile.WriteLine("    <point x=\"> intron ({0:0.0%})\" y=\"{1}\" />", nIntronHits / dividend, nIntronHits / reducer);
            int nUstrHits = totalHitCounter.GetTotalAnnotHits(AnnotType.USTR);
            xmlFile.WriteLine("    <point x=\"> upstream ({0:0.0%})\" y=\"{1}\" />", nUstrHits / dividend, nUstrHits / reducer);
            int nDstrHits = totalHitCounter.GetTotalAnnotHits(AnnotType.DSTR);
            xmlFile.WriteLine("    <point x=\"> downstream ({0:0.0%})\" y=\"{1}\" />", nDstrHits / dividend, nDstrHits / reducer);
            if (Props.props.DirectionalReads)
            {
                int numOtherAS = totalHitCounter.GetAnnotHits(AnnotType.AUSTR) + totalHitCounter.GetAnnotHits(AnnotType.AEXON) +
                                 totalHitCounter.GetAnnotHits(AnnotType.AINTR) + totalHitCounter.GetAnnotHits(AnnotType.ADSTR);
                xmlFile.WriteLine("    <point x=\"> anti exon+intr+up+dn ({0:0.0%})\" y=\"{1}\" />", numOtherAS / dividend, numOtherAS / reducer);
            }
            xmlFile.WriteLine("    <point x=\"> repeat ({0:0.0%})\" y=\"{1}\" />",
                               totalHitCounter.GetAnnotHits(AnnotType.REPT) / dividend, totalHitCounter.GetAnnotHits(AnnotType.REPT) / reducer);
            xmlFile.WriteLine("  </hits>");
        }

        private static string GetMultireadSubHeader()
        {
            string mr = "Multireads" + (Props.props.UseMaxAltMappings ? string.Format(" (&lt; {0}-fold)", Props.props.MaxAlternativeMappings) : "");
            string mrMode = (Props.props.MultireadMappingMode == MultiReadMappingType.Random) ? "to one random transcript mapping" :
                (Props.props.MultireadMappingMode == MultiReadMappingType.Most5Prime) ? "to their most 5' transcript mapping" : "multiply to every transcript mapping";
            string mrHead = mr + " are assigned " + mrMode
                + ",\nbut are assigned repeat if some non-transcript repeat mapping exists."
                + "\nOne random mapping is chosen if no transcript mapping exists";
            return mrHead;
        }

        private void WriteHitsBySpecies(StreamWriter xmlFile)
        {
            if (!Props.props.Barcodes.HasSampleLayout() || !Props.props.DirectionalReads) return;
            int[] speciesBcIndexes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            if (speciesBcIndexes.Length < Props.props.Barcodes.Count)
                WriteSpeciesHitSection(xmlFile, speciesBcIndexes, Annotations.Genome.Name);
            int[] emptyBcIndexes = Props.props.Barcodes.EmptyBarcodeIndexes();
            if (emptyBcIndexes.Length > 0)
                WriteSpeciesHitSection(xmlFile, emptyBcIndexes, "empty");
        }

        private void WriteSpeciesHitSection(StreamWriter xmlFile, int[] speciesBcIndexes, string speciesName)
        {
            double nUniqueMolecules = 0;
            double nFeaturesHits = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                nUniqueMolecules += nMappingsByBarcode[bcIdx];
                nFeaturesHits += totalHitCounter.GetBcHits(bcIdx);
            }
            string hitSubHeader = "A mapping to exonic sequence shared by two different genes is counted as two hits.";
            double reducer = (Props.props.Barcodes.HasUMIs)? 1.0E3d : 1.0E6d;
            xmlFile.WriteLine("  <reads species=\"{0}\">", speciesName);
            xmlFile.WriteLine("    <title>Hit counts to feature categories (10^{0}) IN {1} {2} WELLS\n[{3}\n{4}]</title>",
                              (int)Math.Log10(reducer), speciesBcIndexes.Length, speciesName.ToUpper(), GetMultireadSubHeader(), hitSubHeader);
            xmlFile.WriteLine("    <point x=\"Total mappings\" y=\"{0}\" />", nUniqueMolecules / reducer);
            xmlFile.WriteLine("    <point x=\"Total feature hits (100%)\" y=\"{0}\" />", nFeaturesHits / reducer);
            foreach (int annotType in AnnotType.GetTypes())
            {
                int numOfType = totalHitCounter.GetTotalBcsAnnotHits(speciesBcIndexes, annotType);
                xmlFile.WriteLine("    <point x=\"> {0} ({1:0%})\" y=\"{2}\" />", AnnotType.GetName(annotType), numOfType / nFeaturesHits, numOfType / reducer);
            }
            xmlFile.WriteLine("  </reads>");
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
                long totLen = Annotations.GetTotalAnnotLength(at, true);
                if (totASense > 0)
                    ratio = string.Format("{0:0.0}:1", totSense / (double)totASense);
                double sensePerKb = 1000.0d * (totSense / (double)totLen);
                double antiPerKb = 1000.0d * (totASense / (double)totLen);
                if (double.IsNaN(sensePerKb) || double.IsNaN(antiPerKb))
                    sensePerKb = antiPerKb = 0.0;
                xmlFile.WriteLine("   <point x=\"{0}#br#{1}\" y=\"{2:0.##}\" y2=\"{3:0.##}\" />", AnnotType.GetName(t),
                                  ratio, sensePerKb, antiPerKb);
            }
            long reptLen = Annotations.GetTotalAnnotLength(AnnotType.REPT);
            int reptCount = totalHitCounter.GetAnnotHits(AnnotType.REPT);
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
            foreach (string key in totalHitCounter.ChrIds())
                if (int.TryParse(key, out v)) numkeys.Add(v);
                else alphakeys.Add(key);
            numkeys.Sort();
            alphakeys.Sort();
            List<string> sortedkeys = numkeys.ConvertAll((n) => n.ToString());
            sortedkeys.AddRange(alphakeys);
            double nTotalHits = totalHitCounter.TotalHits;
            foreach (string chr in sortedkeys)
            {
                if (StrtGenome.IsASpliceAnnotation(chr))
                    continue;
                double nSenseHits = totalHitCounter.GetChrAnnotHits(chr, AnnotType.EXON);
                double nAsenseHits = totalHitCounter.GetChrAnnotHits(chr, AnnotType.AEXON);
                string ratio = (nAsenseHits == 0)? "1:0" : string.Format("{0:0}", nSenseHits / (double)nAsenseHits);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  chr, ratio, 100.0d*(nSenseHits / nTotalHits), 100.0d*(nAsenseHits / nTotalHits));
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
			string sp = " " + Annotations.Genome.Name;
            int[] bcIndexes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
			if (bcIndexes.Length == 0) {
				bcIndexes = Enumerable.Range (0, Props.props.Barcodes.Count).ToArray();
				sp = "";
			}
            int sumExprTr = 0;
            foreach (int bcIdx in bcIndexes)
                sumExprTr += Annotations.geneFeatures.Values.Count(gf => (!gf.IsSpike() && gf.IsExpressed(bcIdx)));
			xmlFile.WriteLine("    <point x=\"Mean per{2} well ({0})\" y=\"{1}\" />", bcIndexes.Length, (int)(sumExprTr / bcIndexes.Count()), sp);
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
                    foreach (int bcIdx in Props.props.Barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome))
                    {
                        int c = totalHitCounter.GetTotalBcAnnotHits(bcIdx, AnnotType.EXON);
                        if (c > 0)
                        {
                            double RPM = gf.TrNCHits(bcIdx) * 1.0E+6 / (double)c;
                            ds.Add(RPM);
                        }
                    }
                    if (ds.Count == 0)
                        continue;
                    double mean = Math.Max(0.001, ds.Mean());
                    string spikeId = gf.Name.Replace("RNA_SPIKE_", "Old").Replace("ERCC-00", "");
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
            xmlFile.WriteLine("  <hitprofile>");
            xmlFile.WriteLine("  <title>5'->3' read distr. Red=Transcripts/Blue=Spikes</title>");
            xmlFile.WriteLine("	 <xtitle>Relative pos within transcript</xtitle>");
            int trLenBinSize = 800;
            int trLenBinCount = 5;
            int nSections = 20;
            DescriptiveStatistics[,] binnedEfficiencies;
            int[] geneCounts;
            int colorStep = -(0xFF00 / trLenBinCount);
            int spikeColor = 0x00FFFF;
            GetBinnedHitProfiles(trLenBinSize, trLenBinCount, nSections, true, out binnedEfficiencies, out geneCounts);
            WriteBinnedHitProfile(xmlFile, trLenBinSize, spikeColor, colorStep, binnedEfficiencies);
            int transcriptColor = 0xFFFF00;
            GetBinnedHitProfiles(trLenBinSize, trLenBinCount, nSections, false, out binnedEfficiencies, out geneCounts);
            WriteBinnedHitProfile(xmlFile, trLenBinSize, transcriptColor, colorStep, binnedEfficiencies);
            xmlFile.WriteLine("  </hitprofile>");
        }

        private static void WriteBinnedHitProfile(StreamWriter xmlFile, int trLenBinSize,
                                                 int geneColor, int geneColorStep, DescriptiveStatistics[,] binnedEfficiencies)
        {
            int trLenBinCount = binnedEfficiencies.GetLength(0);
            int nSections = binnedEfficiencies.GetLength(1);
            for (int trLenBin = 0; trLenBin < trLenBinCount; trLenBin++)
            {
                int lenBinStart = trLenBin * trLenBinSize;
                string curve = "";
                curve += string.Format("    <curve legend=\"{0}-{1}bp\" color=\"#{2:X6}\">\n", lenBinStart, lenBinStart + trLenBinSize - 1, geneColor);
                geneColor += geneColorStep;
                bool anyPoints = false;
                for (int section = 0; section < nSections; section++)
                {
                    if (binnedEfficiencies[trLenBin, section].Count == 0)
                        continue;
                    anyPoints = true;
                    double eff = binnedEfficiencies[trLenBin, section].Mean();
                    double fracPos = (section + 0.5D) / (double)nSections;
                    curve += string.Format("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />\n", fracPos, eff);
                }
                curve += string.Format("    </curve>\n");
                if (anyPoints)
                    xmlFile.Write(curve);
            }
        }

        private void GetBinnedHitProfiles(int trLenBinSize, int trLenBinCount, int nSections, bool selectSpikes,
                                          out DescriptiveStatistics[,] binnedEfficiencies, out int[] trLenBinGeneCount)
        {
            binnedEfficiencies = new DescriptiveStatistics[trLenBinCount, nSections];
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[trLenBinIdx, section] = new DescriptiveStatistics();
            }
            trLenBinGeneCount = new int[trLenBinCount];
            int maxTrLen = trLenBinCount * trLenBinSize;
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.IsSpike() != selectSpikes || gf.IsPseudogeneType()) continue;
                int trLen = gf.GetTranscriptLength();
                if (trLen > maxTrLen) continue;
                ushort[] trHits = CompactGenePainter.GetTranscriptProfile(gf);
                double trHitSum = trHits.Sum(v => (int)v);
                if (trHitSum == 0.0) continue;
                int firstHitIdx = selectSpikes? 0 : Array.FindIndex(trHits, v => v > 0); // If not spike, count from first nonzero position
                int effectiveTrLen = trLen - firstHitIdx;
                int trLenBin = effectiveTrLen / trLenBinSize;
                if (trLenBin >= trLenBinCount) continue;
                double sectionDist = effectiveTrLen / (double)nSections;
                for (int hitIdx = firstHitIdx; hitIdx < trLen; hitIdx++)
                {
                    int section = (int)Math.Floor((hitIdx - firstHitIdx) / sectionDist);
                    binnedEfficiencies[trLenBin, section].Add(trHits[hitIdx] / trHitSum);
                }
                trLenBinGeneCount[trLenBin]++;
            }
        }

        private void AddCVHistogram(StreamWriter xmlFile)
        {
            int[] genomeBcIndexes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            if (genomeBcIndexes.Length < 3)
                return;
            int nPairs = 1000;
            int nBins = 40;
            List<double[]> validBcCountsByGene = new List<double[]>();
            List<int> totalCountsByGene = new List<int>();
            int[] totalsByBarcode = new int[Props.props.Barcodes.Count];
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.IsSpike())
                    continue;
                double[] gfValidBcCounts = new double[Props.props.Barcodes.Count];
                int gfTotal = 0;
                foreach (int bcIdx in genomeBcIndexes)
                {
                    int c = gf.TrNCHits(bcIdx);
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
            if (validBcCountsByGene.Count == 0 || totalCountsByGene.Count == 0)
                return null;
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
        /// <param name="xmlFile"></param>
        /// <param name="readCounter"></param>
        private void WriteBarcodeStats(StreamWriter xmlFile, ReadCounter readCounter)
        {
            string sp = Annotations.Genome.Name;
            int[] onlyGenomeBcIndexes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            using (StreamWriter barcodeStats = new StreamWriter(OutputPathbase + "_barcode_summary.tab"))
            using (StreamWriter bCodeLines = new StreamWriter(OutputPathbase + "_barcode_oneliners.tab"))
            {
                string molT = (Props.props.Barcodes.HasUMIs) ? "molecules" : "reads";
                barcodeStats.WriteLine("Total annotated {0}: {1}\n", molT, nAnnotatedMappings);
                xmlFile.WriteLine("  <barcodestats>");
                xmlFile.Write("    <barcodestat section=\"wellids\">");
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                {
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    xmlFile.Write("    <d>{0}</d>", Props.props.Barcodes.GetWellSymbolFromBcIdx(bcIdx));
                }
                xmlFile.WriteLine("\n    </barcodestat>");
                WriteBarcodes(xmlFile, barcodeStats, bCodeLines);
                if (Props.props.Barcodes.SpeciesByWell != null)
                    WriteSpeciesByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes);
                string posinpatchTitle = Props.props.Barcodes.TestAnnotation("PosInPatch");
                if (posinpatchTitle != null)
                {
                    xmlFile.Write("    <barcodestat section=\"posinpatch\">");
                    for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                    {
                        if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                        xmlFile.Write("    <d>{0}</d>", Props.props.Barcodes.GetAnnotation(posinpatchTitle, bcIdx));
                    }
                    xmlFile.WriteLine("\n    </barcodestat>");
                }
                if (readCounter.ValidReadsByBarcode.Length == Props.props.Barcodes.Count)
                {
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, readCounter.TotalReadsByBarcode.ToArray(),
                                        "BARCODEDREADS", "Total barcoded reads by barcode", "barcoded reads");
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, readCounter.ValidReadsByBarcode.ToArray(),
                                        "VALIDSTRTREADS", "Total valid STRT reads by barcode", "valid STRT reads");
                }
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, nRealChrMappedReadsByBarcode,
                                    "MAPPEDREADS", "Total " + sp + " mapped reads by barcode", sp + " mapped reads");
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, nSpikeMappedReadsByBarcode,
                                    "SPIKEREADS", "Total spike mapped reads by barcode", "spike mapped reads");
                if (Props.props.Barcodes.HasUMIs)
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, mappingAdder.NDuplicateReadsByBc(),
                                    "DUPLICATE_READS", "Duplicate reads of molecules (same UMI/position/strand) by barcode", "redundant reads (PCR)");
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, nNonAnnotatedItemsByBc,
                                    "NON_ANNOTATED_POS_STRANDS", "Non-annotated strand-positions by barcode", "non-annotated strand-positions");
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, nNonAnnotatedMolsByBc,
                                    "NON_ANNOTATED_" + molT.ToUpper(), "Non-annotated " + molT + " by barcode", "non-annotated " + molT);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, totalHitCounter.GetTotalHitsByBarcode(),
                                    "HITS", "Total annotated hits by barcode", "annotated hits");
                if (Props.props.Barcodes.HasUMIs)
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, labelingEfficiencyEstimator.maxOccupiedUMIsByBc,
                                    "MAX_OCCUPIED_UMIS", "Max detected UMIs (after singleton/mutation filter) by barcode", "max detected UMIs (after mutation filter)");
                if (Props.props.AnalyzeGCContent)
                    WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, gcAnalyzer.GetPercentGCByBarcode(),
                                        "PERCENT_READ_GC_CONTENT", "Percent GC in transcript mapping reads", "transcript reads percent GC");
                WriteFeaturesByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, TotalRealTrMappingsByBarcode,
                                    "TRNSR_DETECTING_" + molT.ToUpper(), sp + " transcript detecting " + molT + " by barcode", sp + " tr. detecting " + molT);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, TotalSpikeMappingsByBarcode,
                                    "SPIKE_DETECTING_" + molT.ToUpper(), "Spike detecting " + molT + " by barcode", "spike detecting " + molT);
                WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, onlyGenomeBcIndexes, Annotations.GetByBcNumExpressedTranscripts(),
                                    "TRANSCRIPTS", "Detected " + sp + " genes by barcode",
                                    "detected " + sp + " genes");
            }
            if (Props.props.Barcodes.HasUMIs && Props.props.TotalNumberOfAddedSpikeMolecules > 0)
            {
                xmlFile.Write("    <barcodestat section=\"labeling efficiency ({0} spike mols)\">", Props.props.TotalNumberOfAddedSpikeMolecules);
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                {
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    double e = labelingEfficiencyEstimator.LabelingEfficiencyByBc[bcIdx];
                    string es = (e < 0.0005) ? "0" : string.Format("{0:0.000}", e);
                    if (onlyGenomeBcIndexes.Contains(bcIdx)) xmlFile.Write("    <d>{0}</d>", es);
                    else xmlFile.Write("    <d>({0})</d>", es);
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
            string[] counts = new string[Props.props.Barcodes.Count];
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
            string molTitle = (Props.props.Barcodes.HasUMIs) ? "molecules" : "reads";
            for (var annotType = 0; annotType < AnnotType.Count; annotType++)
            {
                if (annotType == AnnotType.AREPT || annotType == AnnotType.ASPLC) continue;
                string bCodeLinesTitle = AnnotType.GetName(annotType);
                bCodeLines.Write(bCodeLinesTitle);
                xmlFile.Write("    <barcodestat section=\"{0} hits\">", bCodeLinesTitle);
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                {
                    string annotHits = totalHitCounter.GetBcAnnotHits(bcIdx, annotType).ToString();
                    bCodeLines.Write("\t{0}", annotHits);
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    string d = genomeBcIndexes.Contains(bcIdx) ? annotHits : "(" + annotHits + ")";
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
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                bCodeLines.Write("\t{0}", Props.props.Barcodes.GetWellId(bcIdx));
            bCodeLines.WriteLine();
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
            {
                bCodeLines.Write("\t{0}", Props.props.Barcodes.Seqs[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", Props.props.Barcodes.Seqs[bcIdx]);
            }
            bCodeLines.WriteLine();
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine("WellIds:\n");
            barcodeStats.WriteLine(MakeDataMatrix(Props.props.Barcodes.WellIds, "-"));
            barcodeStats.WriteLine("Barcode by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(Props.props.Barcodes.Seqs, "-"));
        }

        private void WriteSpeciesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            foreach (string species in Props.props.Barcodes.SpeciesByWell)
                bCodeLines.Write("\t{0}", species);
            bCodeLines.WriteLine();
            barcodeStats.WriteLine("Species by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(Props.props.Barcodes.SpeciesByWell, "empty"));
            xmlFile.Write("    <barcodestat section=\"species\">");
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string species = genomeBcIndexes.Contains(bcIdx) ? Props.props.Barcodes.SpeciesByWell[bcIdx] : string.Format("({0})", Props.props.Barcodes.SpeciesByWell[bcIdx]);
                xmlFile.Write("    <d>{0}</d>", species);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
        }

        private string MakeTotalMatrix(int annotType)
        {
            string[] counts = new string[Props.props.Barcodes.Count];
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                counts[bcIdx] = totalHitCounter.GetBcAnnotHits(bcIdx, annotType).ToString();
            return MakeDataMatrix(counts, "0");
        }

        private string MakeFracDevStatsMatrix(int annotType)
        {
            double fracSum = 0.0;
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                fracSum += (double)totalHitCounter.GetBcAnnotHits(bcIdx, annotType) / totalHitCounter.GetBcHits(bcIdx);
            double meanFrac = fracSum / Props.props.Barcodes.Count;
            string[] values = new string[Props.props.Barcodes.Count];
            for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
            {
                double ownFrac = (double)totalHitCounter.GetBcAnnotHits(bcIdx, annotType) / totalHitCounter.GetBcHits(bcIdx);
                values[bcIdx] = (meanFrac == 0)? "NoData" : string.Format("{0:0.000}", ownFrac / meanFrac);
            }
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
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                    effFile.Write("\t" + Props.props.Barcodes.Seqs[bcIdx]);
                effFile.WriteLine();
                foreach (string spikeName in labelingEfficiencyEstimator.efficiencyBySpike.Keys)
                {
                    if (!Annotations.geneFeatures.ContainsKey(spikeName))
                        continue;
                    double expected = labelingEfficiencyEstimator.AddedCount(spikeName);
                    effFile.Write("{0}\t{1}\t{2}", spikeName, Annotations.geneFeatures[spikeName].GetTranscriptLength(), expected);
                    for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
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
            double totHits = totalHitCounter.GetAnnotHits(AnnotType.EXON) + totalHitCounter.GetAnnotHits(AnnotType.AEXON);
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
                    string altGenes = string.Join("\t", byGene[gene].ToArray());
                    sharedFile.WriteLine("{0}\t{1}\t{2}\t{3}", gene, gf.TrNCHitSum(), gf.TrHitSum(), altGenes);
                }
            }
        }

        private void WriteSNPPositions(int[] genomeBarcodes)
        {
            using (StreamWriter snpFile = new StreamWriter(OutputPathbase + "_SNPs.tab"))
            {
                char[] nts = new char[] { '0', 'A', 'C', 'G', 'T' };
                int thres = (int)(SnpAnalyzer.thresholdFractionAltHitsForMixPos * 100);
                int minHitsToTestSNP = (Props.props.Barcodes.HasUMIs) ? Props.props.MinMoleculesToTestSnp : Props.props.MinReadsToTestSnp;
                string minTxt = (Props.props.Barcodes.HasUMIs) ? "Molecules" : "Reads";
                snpFile.WriteLine("SNP positions found in " + genomeBarcodes.Length + " barcodes belonging to species.");
                snpFile.WriteLine("#(minimum {0} {3}/Pos required to check, limits used heterozygous: {1}-{2}% AltNt and homozygous: >{2}% Alt Nt)",
                                  minHitsToTestSNP, thres, 100 - thres, minTxt);
                snpFile.WriteLine("#Gene\tChr\tmRNALeftChrPos\tSNPChrPos\tType\tRefNt\tTotal\tMut-A\tMut-C\tMut-G\tMut-T");
                foreach (GeneFeature gf in Annotations.IterOrderedGeneFeatures(true, true))
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
            int[] genomeBarcodes = Props.props.Barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            string snpPath = OutputPathbase + "_SNPs_by_barcode.tab";
            SnpAnalyzer.WriteSnpsByBarcode(snpPath, Props.props.Barcodes, genomeBarcodes, Annotations.geneFeatures);
            WriteSNPPositions(genomeBarcodes);
        }


        private void PaintReadIntervals()
        {
            if (Props.props.GenePaintIntervals == null || Props.props.GenePaintIntervals.Length == 0)
                return;
            StreamWriter paintWriter = null;
            string type = (Props.props.Barcodes.HasUMIs) ? "Molecule" : "Read";
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
                    for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
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

        private void WriteWiggleAndBed()
        {
            WriteWiggleStrand('+', resultFolder, true);
            WriteWiggleStrand('-', resultFolder, true);
        }

        private void WriteCurrentBcWiggle()
        {
            if (!Props.props.GenerateBarcodedWiggle) return;
            string selAnnots = (SelectedBcWiggleAnnotations == null) ?
                                "" : "_" + string.Join(".", Array.ConvertAll(SelectedBcWiggleAnnotations, t => AnnotType.GetName(t)));
            string bcWiggleSubfolder = AssertOutputPathbase() + "_wiggle_by_bc" + selAnnots;
            if (!Directory.Exists(bcWiggleSubfolder))
                Directory.CreateDirectory(bcWiggleSubfolder);
            WriteWiggleStrand('+', bcWiggleSubfolder, false);
            WriteWiggleStrand('-', bcWiggleSubfolder, false);
        }

        private void WriteWiggleStrand(char strand, string outputFolder, bool allBarcodes)
        {
            bool makeBed = Props.props.GenerateBed && Props.props.OutputLevel >= 2;
            string projwell = resultFileprefix + (allBarcodes ? "" : ("_" + Props.props.Barcodes.GetWellId(currentBcIdx)));
            string fileNameHead = projwell + "_" + ((strand == '+') ? "fw" : "rev");
            string filePathHead = Path.Combine(outputFolder, fileNameHead);
            string fileByRead = filePathHead + "_byread.wig.gz";
            string fileByMol = filePathHead + "_bymol.wig.gz";
            int readLength = MappedTagItem.AverageReadLen;
            using (StreamWriter writerByRead = fileByRead.OpenWrite())
            using (StreamWriter writerByMol = (Props.props.Barcodes.HasUMIs && !File.Exists(fileByMol) ? fileByMol.OpenWrite() : null))
            {
                string readTrackName = projwell + "R" + strand;
                string readTrackDescr = fileNameHead + "_Read " + DateTime.Now.ToString("yyMMdd");
                writerByRead.WriteLine("track type=wiggle_0 name=\"{0}\" description=\"{1}\" visibility=full alwaysZero=on", readTrackName, readTrackDescr);
                if (writerByMol != null)
                {
                    string molTrackName = projwell + "M" + strand;
                    string molTrackDescr = fileNameHead + "_Mol " + DateTime.Now.ToString("yyMMdd");
                    writerByMol.WriteLine("track type=wiggle_0 name=\"{0}\" description=\"{1}\" visibility=full alwaysZero=on", molTrackName, molTrackDescr);
                }
                StreamWriter bedWriterByRead = makeBed ? (filePathHead + "_byread.bed.gz").OpenWrite() : null;
                StreamWriter bedWriterByMol = (makeBed && Props.props.Barcodes.HasUMIs) ? (filePathHead + "_bymol.bed.gz").OpenWrite() : null;
                foreach (KeyValuePair<string, ChrTagData> tagDataPair in randomTagFilter.chrTagDatas)
                {
                    string chr = tagDataPair.Key;
                    if (!StrtGenome.IsSyntheticChr(chr) && Annotations.HasChrLen(chr))
                    {
                        int chrLen = Annotations.GetChrLen(chr);
                        int[] positions, molsAtEachPos, readsAtEachPos;
                        tagDataPair.Value.GetDistinctPositionsAndCounts(strand, SelectedBcWiggleAnnotations,
                                                        out positions, out molsAtEachPos, out readsAtEachPos, allBarcodes);
                        if (writerByMol != null)
                        {
                            int[] posCopy = (int[])positions.Clone();
                            Array.Sort(posCopy, molsAtEachPos);
                            Wiggle.WriteToWigFile(writerByMol, chr, readLength, strand, chrLen, posCopy, molsAtEachPos);
                            if (!allBarcodes)
                                InsertWiggleIntoDb(chr, readLength, strand, chrLen, posCopy, molsAtEachPos);
                            if (bedWriterByMol != null)
                                Wiggle.WriteToBedFile(bedWriterByMol, chr, readLength, strand, posCopy, molsAtEachPos);
                        }
                        Array.Sort(positions, readsAtEachPos);
                        Wiggle.WriteToWigFile(writerByRead, chr, readLength, strand, chrLen, positions, readsAtEachPos);
                        if (bedWriterByRead != null)
                            Wiggle.WriteToBedFile(bedWriterByRead, chr, readLength, strand, positions, readsAtEachPos);
                    }
                }
            }
        }

        private void InsertWiggleIntoDb(string chr, int readLength, char strand, int chrLength, int[] sortedPositions, int[] countAtEachPosition)
        {
            if (!Props.props.InsertCellDBData || !Props.props.InsertBcWigToDB) return;
            string well = Props.props.Barcodes.GetWellId(currentBcIdx);
            int cellID = cellIdByWell[well];
            int transcriptomeID = Annotations.dbTranscriptome.TranscriptomeID.Value;
            IExpressionDB edb = DBFactory.GetExpressionDB();
            IEnumerator<Pair<int, int>> wiggle = Wiggle.IterWiggle(readLength, chrLength, sortedPositions, countAtEachPosition).GetEnumerator();
            edb.InsertChrWiggle(wiggle, cellID, transcriptomeID, chr, strand);
        }

        private void WriteHotspots()
        {
            using (StreamWriter writer = new StreamWriter(OutputPathbase + "_hotspots.tab"))
            {
                writer.WriteLine("#Positions with local maximal read counts that lack gene or repeat annotation. Samples < 5 bp apart not shown.");
                writer.WriteLine("#Chr\tPosition\tStrand\tCoverage");
                int[] positions, molsAtEachPos, readsAtEachPos;
                foreach (KeyValuePair<string, ChrTagData> data in randomTagFilter.chrTagDatas)
                {
                    string chr = data.Key;
                    if (!StrtGenome.IsSyntheticChr(chr) && Annotations.HasChrLen(chr))
                    {
                        data.Value.GetDistinctPositionsAndCounts('+', null, out positions, out molsAtEachPos, out readsAtEachPos, true);
                        FindHotspots(writer, chr, '+', positions, readsAtEachPos);
                        data.Value.GetDistinctPositionsAndCounts('-', null, out positions, out molsAtEachPos, out readsAtEachPos, true);
                        FindHotspots(writer, chr, '-', positions, readsAtEachPos);
                    }
                }
            }
        }

        private void FindHotspots(StreamWriter writer, string chr, char strand, int[] positions, int[] counts)
        {
            int averageReadLength = MappedTagItem.AverageReadLen;
            int chrLength = Annotations.GetChrLen(chr);
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

        private void WriteReadCountDistroByUMICount()
        {
            using (StreamWriter writer = new StreamWriter(OutputPathbase + "_ReadCountDistr_by_UMICount.tab"))
            {
                int maxNReads = randomTagFilter.readDistributionByMolCount.GetLength(1) - 1;
                bool nonZeroFound = false;
                while (maxNReads > 0 && !nonZeroFound)
                {
                    for (int UMICount = 1; UMICount <= Props.props.Barcodes.UMICount; UMICount++)
                        if (randomTagFilter.readDistributionByMolCount[UMICount, maxNReads] > 0)
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
                for (int UMICount = 1; UMICount <= Props.props.Barcodes.UMICount; UMICount++)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append(UMICount);
                    for (int nReads = 1; nReads < maxNReads; nReads++)
                    {
                        sb.Append('\t');
                        sb.Append(randomTagFilter.readDistributionByMolCount[UMICount, nReads]);
                    }
                    writer.WriteLine(sb);
                }
            }
        }

        private void WriteHitProfilesByBarcode()
        {
            int trLenBinSize = 400;
            int trLenBinCount = 10;
            int maxTrLen = trLenBinSize * trLenBinCount;
            int nSections = 20;
            using (StreamWriter profileFile = new StreamWriter(OutputPathbase + "_5to3_profiles_by_barcode.tab"))
            {
                profileFile.WriteLine("5'->3' read distributions by barcode.");
                profileFile.WriteLine("\t\t\tRelative position within transcript, counting from first position with a hit.");
                profileFile.Write("\t\t");
                for (int section = 0; section < nSections; section++)
                    profileFile.Write("\t{0}", (section + 0.5D) / (double)nSections);
                profileFile.WriteLine("\nBarcode\tTrLenFrom\tTrLenTo\tFraction within interval of all reads.");
                int minHitsPerGene = (Props.props.Barcodes.HasUMIs) ? 50 : nSections * 10;
                int maxHitsPerGene = (Props.props.Barcodes.HasUMIs) ? (int)(0.7 * Props.props.Barcodes.UMICount * Props.props.Barcodes.Count) : int.MaxValue;
                for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
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
                        if (gf.IsSpike() || gf.IsPseudogeneType() ||
                            gf.TrHits(bcIdx) < minHitsPerGene || gf.TrHits(bcIdx) > maxHitsPerGene)
                            continue;
                        int trLen = gf.GetTranscriptLength();
                        if (trLen > maxTrLen) continue;
                        ushort[] trHits = CompactGenePainter.GetTranscriptProfile(gf, bcIdx);
                        double trHitSum = trHits.Sum(v => (int)v);
                        if (trHitSum == 0.0) continue;
                        int firstHitIdx = Array.FindIndex(trHits, v => v > 0);
                        int effectiveTrLen = trLen - firstHitIdx;
                        int trLenBin = effectiveTrLen / trLenBinSize;
                        if (trLenBin >= trLenBinCount) continue;
                        double sectionDist = effectiveTrLen / (double)nSections;
                        for (int hitIdx = firstHitIdx; hitIdx < trLen; hitIdx++)
                        {
                            int section = (int)Math.Floor((hitIdx - firstHitIdx) / sectionDist);
                            binnedEfficiencies[trLenBin, section].Add(trHits[hitIdx] / trHitSum);
                        }
                        geneCounts[trLenBin]++;
                    }
                    for (int trLenBin = 0; trLenBin < trLenBinCount; trLenBin++)
                    {
                        int binStart = trLenBinSize * trLenBin;
                        profileFile.Write("{0}\t{1}\t{2}", bcIdx, binStart, binStart + trLenBinSize - 1);
                        for (int section = 0; section < nSections; section++)
                        {
                            double eff = (binnedEfficiencies[trLenBin, section].Count == 0) ? 0.0 : binnedEfficiencies[trLenBin, section].Mean();
                            profileFile.Write("\t{0}", eff);
                        }
                        profileFile.WriteLine();
                    }
                }
            }
        }
    }

}
