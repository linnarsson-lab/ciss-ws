using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Imaging;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using C1;

namespace Linnarsson.Strt
{
    public class GenomeAnnotations
    {
        public static int annotationBinSize = 30000;

        /// <summary>
        /// The actual chromosome sequences - only those needed for stat calc.
        /// </summary>
        private Dictionary<string, DnaSequence> ChromosomeSequences { get; set; }
        private Dictionary<string, int> ChromosomeLengths { get; set; }

        public bool HasChrSeq(string chr)
        {
            return ChromosomeSequences.ContainsKey(chr);
        }
        public DnaSequence GetChrSeq(string chr)
        {
            return ChromosomeSequences[chr];
        }
        public bool HasChrLen(string chr)
        {
            return ChromosomeLengths.ContainsKey(chr);
        }
        public int GetChrLen(string chr)
        {
            return ChromosomeLengths[chr];
        }

        /// <summary>
        /// Map of all chrIds to their sequence files.
        /// </summary>
        private Dictionary<string, string> ChrIdToFileMap;

        /// <summary>
        /// Keeps number of overlapping exons for counter-orientation overlapping genes.
        /// The Keys follow the pattern "GeneName1#GeneName2"
        /// </summary>
        protected Dictionary<string, int> antisensePairExons = new Dictionary<string, int>();

        /// <summary>
        /// A dictionary of annotations indexed by chromosome Id
        /// </summary>
        protected Dictionary<string, QuickAnnotationMap> ExonAnnotations { get; set; }
        protected Dictionary<string, QuickAnnotationMap> NonExonAnnotations { get; set; }

        public IterTranscriptMatcher IterTranscriptMatches;

        public Dictionary<string, GeneFeature> geneFeatures;
        public Dictionary<string, RepeatFeature> repeatFeatures;

        public Transcriptome dbTranscriptome;
        protected StrtGenome genome;
        public StrtGenome Genome { get { return genome; } }
        public bool GenesSetupFromC1DB { get; private set; }

        protected Barcodes barcodes;
        public Barcodes Barcodes { get { return barcodes; } }
        protected Props props;
        protected bool needChromosomeSequences;
        protected bool needChromosomeLengths;
        public bool noGeneVariants;

        public GenomeAnnotations(StrtGenome genome)
		{
            this.props = Props.props;
            this.genome = genome;
            this.barcodes = props.Barcodes;
            needChromosomeSequences = props.DetermineMotifs || props.AnalyzeSeqUpstreamTSSite || props.AnalyzeGCContent;
            needChromosomeLengths = props.GenerateWiggle || props.GenerateBarcodedWiggle || props.GeneFeature5PrimeExtension > 0;
            noGeneVariants = !genome.GeneVariants;
            GeneFeature.LocusFlankLength = props.LocusFlankLength;
            ChromosomeSequences = new Dictionary<string, DnaSequence>();
            ChromosomeLengths = new Dictionary<string, int>();
            ExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            NonExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            geneFeatures = new Dictionary<string, GeneFeature>(50000);
            repeatFeatures = new Dictionary<string, RepeatFeature>(1500);
            IterTranscriptMatches = new IterTranscriptMatchers(ExonAnnotations).GetMatcher();
        }

        #region LoadData
        public void Load()
        {
            SetupChromsomes();
            SetupGenes();
            SetupIntervals();
            SetupRepeats();
        }

        /// <summary>
        /// Read the chrId-to-fastafile mapping. The sequences and/or lengths will be read if needed by config
        /// </summary>
        public void SetupChromsomes()
        {
            ChrIdToFileMap = genome.GetStrtChrFilesMap();
            foreach (string chrId in ChrIdToFileMap.Keys)
            {
                ExonAnnotations[chrId] = new QuickAnnotationMap(annotationBinSize);
                NonExonAnnotations[chrId] = new QuickAnnotationMap(annotationBinSize);
            }
            if (needChromosomeSequences || needChromosomeLengths)
                ReadChromsomeSequences(ChrIdToFileMap);
        }

        private void ReadChromsomeSequences(Dictionary<string, string> chrIdToFileMap)
        {
            string[] selectedChrIds = props.SeqStatsChrIds;
            if (selectedChrIds == null || selectedChrIds[0] == "")
                selectedChrIds = chrIdToFileMap.Keys.ToArray();
            Console.Write("Reading {0} chromosomes...", selectedChrIds.Length);
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                Console.Write(".{0}", chrId);
                if (StrtGenome.IsASpliceAnnotation(chrId)) continue;
                try
                {
                    int chrLen;
                    if (needChromosomeSequences && Array.IndexOf(selectedChrIds, chrId) >= 0)
                    {
                        DnaSequence chrSeq = DnaSequence.FromFile(chrIdToFileMap[chrId]);
                        chrLen = (int)chrSeq.Count;
                        ChromosomeSequences.Add(chrId, chrSeq);
                    }
                    else
                    {
                        double fileLen = new FileInfo(chrIdToFileMap[chrId]).Length;
                        chrLen = (int)(fileLen * 80.0 / 81.0); // Get an approximate length by removing \n:s
                    }
                    ChromosomeLengths.Add(chrId, chrLen);
                }
                catch (Exception e)
                {
                    Console.WriteLine("\nERROR: Could not read chromosome {0} - {1}", chrId, e.Message);
                }
                if (Background.CancellationPending) return;
            }
            Console.WriteLine();
        }

        public void SetupGenes()
        {
            string strtAnnotPath = genome.AssertAStrtAnnotPath();
            bool genesSetupFromC1DB = (props.InsertCells10Data && SetupGenesFromC1DB(strtAnnotPath));
            if (!genesSetupFromC1DB)
                SetupGenesFromStrtAnnotFile(strtAnnotPath);
            int trLen = geneFeatures.Sum(gf => gf.Value.GetTranscriptLength());
            Console.WriteLine("Total length of all transcript models (including overlaps): {0} bp.", trLen);
        }

        /// <summary>
        /// For C1 samples the transcript models are read from the cells10k database, and the (refFlat style) Annotation file
        /// in STRT genome dir is only use to get the splice junctions.
        /// Note that any 5' extensions are already stored in the cells10k database and will not be made here.
        /// </summary>
        /// <param name="STRTAnnotPath"></param>
        /// <returns></returns>
        private bool SetupGenesFromC1DB(string STRTAnnotPath)
        {
            C1DB db = new C1DB();
            dbTranscriptome = db.GetTranscriptome(genome.BuildVarAnnot);
            if (dbTranscriptome == null) return false;
            int nModels = 0, nSpliceModels = 0, nExons = 0;
            foreach (Transcript tt in db.IterTranscriptsFromDB(dbTranscriptome.TranscriptomeID.Value))
            {
                LocusFeature gf = AnnotationReader.GeneFeatureFromDBTranscript(tt);
                int nParts = RegisterGeneFeature(gf);
                if (nParts > 0) { nModels++; nExons += nParts; }
                else nSpliceModels -= nParts;
            }
            Console.WriteLine("Read {0} transcript models totalling {1} exons from database {2}.", nModels, nExons, dbTranscriptome.Name);
            ModifyGeneFeatures(new GeneFeatureOverlapMarkUpModifier());
            foreach (LocusFeature spliceGf in AnnotationReader.IterSTRTAnnotFile(STRTAnnotPath))
                if (spliceGf.Chr == genome.Annotation)
                {
                    int nParts = RegisterGeneFeature(spliceGf);
                    if (nParts > 0) { nModels++; nExons += nParts; }
                    else nSpliceModels -= nParts;
                }
            Console.WriteLine("Added splice junctions for {0} transcript models from {1}.", nSpliceModels, STRTAnnotPath);
            return true;
        }

        private void SetupGenesFromStrtAnnotFile(string STRTAnnotPath)
        {
            int nModels = 0, nSpliceModels = 0, nExons = 0;
            foreach (LocusFeature gf in AnnotationReader.IterSTRTAnnotFile(STRTAnnotPath))
            {
                int nParts = RegisterGeneFeature(gf);
                if (nParts > 0) { nModels++; nExons += nParts; }
                else nSpliceModels -= nParts;
            }
            Console.WriteLine("Read {0}:\n{1} transcript models totalling {2} exons, {3} with splices.",
                               STRTAnnotPath, nModels, nExons, nSpliceModels);
            ModifyGeneFeatures(new GeneFeature5PrimeAndOverlapMarkUpModifier());
        }

        private void ModifyGeneFeatures(GeneFeatureModifiers m)
        {
            foreach (string chrId in GetChromosomeIds())
            {
                if (!StrtGenome.IsSyntheticChr(chrId))
                {
                    m.Process(geneFeatures.Values.Where(gf => gf.Chr == chrId).ToList());
                }
            }
            Console.WriteLine(m.GetStatsOutput());
        }

        /// <summary>
        /// Generate all intervals to match alignments with, corresponding to exon, intron, upstream, downstream, splice...
        /// </summary>
        private void SetupIntervals()
        {
            foreach (GeneFeature gf in geneFeatures.Values)
                AddGeneIntervals((GeneFeature)gf);
        }

        /// <summary>
        /// Generate intervals to match alignments with, for the repeat regions.
        /// </summary>
        private void SetupRepeats()
        {
            Dictionary<string, int> repeatToTrIdMap = new Dictionary<string, int>();
            if (props.InsertCells10Data)
                repeatToTrIdMap = new C1DB().GetRepeatNamesToTranscriptIdsMap(genome.BuildVarAnnot);
            string[] rmskFiles = PathHandler.GetRepeatMaskFiles(genome);
            Console.Write("Reading {0} masking files..", rmskFiles.Length);
            foreach (string rmskFile in rmskFiles)
            {
                Console.Write(".");
                LoadRepeatMaskFile(rmskFile, repeatToTrIdMap);
            }
            Console.WriteLine("{0} annotated repeat types.", repeatFeatures.Count);
        }

        private void LoadRepeatMaskFile(string rmskPath, Dictionary<string, int> repeatToTrIdMap)
        {
            RepeatFeature reptFeature;
            QuickAnnotationMap annotMap;
            foreach (RmskData rd in RmskData.IterRmskFile(rmskPath))
            {
                if (NonExonAnnotations.TryGetValue(rd.Chr, out annotMap))
                {
                    if (!repeatFeatures.TryGetValue(rd.Name, out reptFeature))
                    {
                        repeatFeatures[rd.Name] = new RepeatFeature(rd.Name);
                        int trID;
                        if (!repeatToTrIdMap.TryGetValue(rd.Name, out trID)) trID = -1;
                        repeatFeatures[rd.Name].C1DBTranscriptID = trID;
                        reptFeature = repeatFeatures[rd.Name];
                    }
                    reptFeature.AddRegion(rd);
                    annotMap.Add(new FtInterval(rd.Start, rd.End, reptFeature.MarkHit, 0, reptFeature, AnnotType.REPT, '0'));
                }
            }
        }

        /// <summary>
        /// Adds a normal gene or a splice gene to the set of features
        /// </summary>
        /// <param name="gf"></param>
        /// <returns># exons if gf represents a new gene, -1 if gf represents a series of splices on the junction chr.</returns>
        protected int RegisterGeneFeature(LocusFeature gf)
        {
            if (genome.Annotation == gf.Chr) // I.e., we are on the splice chromosome
            { // Requires that real loci are registered before artificial splice loci.
                if (!geneFeatures.ContainsKey(gf.Name))
                {
                    Console.WriteLine("WARNING: Junctions defined in annotation file but gene is missing: {0}", gf.Name);
                    return 0;
                }
                // Link from artificial splice chromosome to real locus
                ((SplicedGeneFeature)gf).BindToRealFeature(geneFeatures[gf.Name]);
                AddGeneIntervals((SplicedGeneFeature)gf);
                return -1;
            }
            if (geneFeatures.ContainsKey(gf.Name))
            {
                Console.WriteLine("WARNING: Duplicated gene name in annotation file: {0}", gf.Name);
                return 0;
            }
            geneFeatures[gf.Name] = (GeneFeature)gf;
            return ((GeneFeature)gf).ExonCount;
        }


        public string[] GetChromosomeIds()
        {
            return ExonAnnotations.Keys.ToArray();
        }

        /// <summary>
        /// Return the total hit (read or molecule) count to the specified annotation type
        /// </summary>
        /// <param name="annotType">Selected annotation type</param>
        /// <param name="excludeMasked">True => count only hits to regions without overlap with other genes</param>
        /// <returns></returns>
        public int GetTotalAnnotCounts(int annotType, bool excludeMasked)
        {
            int totCounts = 0;
            if (annotType == AnnotType.REPT || annotType == AnnotType.AREPT)
                foreach (RepeatFeature rf in repeatFeatures.Values)
                    totCounts += rf.GetTotalHits();
            else
                foreach (GeneFeature gf in geneFeatures.Values)
                    totCounts += gf.GetAnnotCounts(annotType, excludeMasked);
            return totCounts;
        }

        public long GetTotalAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.REPT || annotType == AnnotType.AREPT)
                return GetTotalRepeatLength();
            long totLen = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
                totLen += gf.GetAnnotLength(annotType, excludeMasked);
            return totLen;
        }
        public long GetTotalAnnotLength(int annotType)
        {
            return GetTotalAnnotLength(annotType, false);
        }

        public long GetTotalRepeatLength()
        {
            long totLen = 0;
            foreach (RepeatFeature rf in repeatFeatures.Values)
                totLen += rf.GetLocusLength();
            return totLen;
        }

        public List<DnaSequence> GetExonSeqsInChrDir(GeneFeature gf)
        {
            List<DnaSequence> exonSeqsInChrDir = new List<DnaSequence>(gf.ExonCount);
            int trLen = 0;
            for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
            {
                int exonLen = 1 + gf.ExonEnds[exonIdx] - gf.ExonStarts[exonIdx];
                trLen += exonLen;
                exonSeqsInChrDir.Add(ChromosomeSequences[gf.Chr].SubSequence(gf.ExonStarts[exonIdx], exonLen));
            }
            return exonSeqsInChrDir;
        }

        /// <summary>
        /// Finds the matching annotated intervals that do NOT correspond to (forward strand for directional reads) transcripts,
        /// like USTR, INTR, DSTR and REPT.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="strand">Strand of hit (for directional reads)</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> IterNonTrMatches(string chr, char strand, int hitMidPos)
        {
            if (Props.props.DirectionalReads)
            { // First check for antisense hits
                foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
                    if (!ivl.IsTrDetectingStrand(strand)) yield return ivl;
            }
            if (!NonExonAnnotations.ContainsKey(chr))
                yield break;
            else
                foreach (FtInterval ivl in NonExonAnnotations[chr].IterItems(hitMidPos))
                    yield return ivl;
        }

        /// <summary>
        /// Checks if there is a matching interval annotated as transcript or repeat
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="hitMidPos"></param>
        /// <returns>True if any matching interval defines an (forward) exon or repeat</returns>
        public bool HasTrOrRepeatMatches(string chr, char strand, int hitMidPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
                if (ivl.IsTrDetectingStrand(strand)) return true;
            if (NonExonAnnotations.ContainsKey(chr))
                foreach (FtInterval ivl in NonExonAnnotations[chr].IterItems(hitMidPos))
                    if (ivl.annotType == AnnotType.REPT) return true;
            return false;
        }

        /// <summary>
        /// Checks if an alignment is within a repeat region
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="hitMidPos"></param>
        /// <returns></returns>
        public bool IsARepeat(string chr, int hitMidPos)
        {
            if (NonExonAnnotations.ContainsKey(chr))
                foreach (FtInterval ivl in NonExonAnnotations[chr].IterItems(hitMidPos))
                    if (ivl.annotType == AnnotType.REPT) return true;
            return false;
        }

        /// <summary>
        /// Check if there is a matching exonic interval (and that it is forward for directional reads)
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="hitMidPos"></param>
        /// <returns>True if an annotation says exon (sense)</returns>
        public bool IsTranscript(string chr, char strand, int hitMidPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
                if (ivl.IsTrDetectingStrand(strand)) return true;
            return false;
        }

        public IEnumerable<FtInterval> IterExonAnnotations(string chr, char strand, int hitMidPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.IsTrDetectingStrand(strand)) yield return ivl;
            }
        }

        public List<FtInterval> GetExonAnnotations(string chr, char strand, int hitMidPos)
        {
            List<FtInterval> ftIvls = new List<FtInterval>();
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.IsTrDetectingStrand(strand)) ftIvls.Add(ivl);
            }
            return ftIvls;
        }

        /// <summary>
        /// Note that intervals corresponding to transcript derived reads are stored separate from other feature's intervals,
        /// to later allow the identification and different handling of multimappings of each kind
        /// </summary>
        /// <param name="ft"></param>
        protected void AddGeneIntervals(LocusFeature ft)
        {
            foreach (FtInterval ivl in ft.IterIntervals())
            {
                if (AnnotType.IsTranscript(ivl.annotType))
                    AddGeneInterval(ft.Chr, ivl, ExonAnnotations);
                else
                    AddGeneInterval(ft.Chr, ivl, NonExonAnnotations);
            }
        }

        private void AddGeneInterval(string chr, FtInterval ivl, Dictionary<string, QuickAnnotationMap> annotMaps)
        {
            QuickAnnotationMap qMap;
            if (!annotMaps.TryGetValue(chr, out qMap))
            {
                qMap = new QuickAnnotationMap(annotationBinSize);
                annotMaps[chr] = qMap;
            }
            qMap.Add(ivl);
        }

        public int GetNumExpressedSpikes()
        {
            return geneFeatures.Values.Count(g => (g.IsSpike() && g.IsExpressed()));
        }
        /// <summary>
        /// Exclude spikes
        /// </summary>
        /// <returns># of non-spike transcript models that have any exon/splice hit in any barcode</returns>
        public int GetNumExpressedTranscripts()
        {
            return geneFeatures.Values.Count(g => (!g.IsSpike() && g.IsExpressed()));
        }
        /// <summary>
        /// Exclude spikes
        /// </summary>
        /// <returns># of non-spike transcript models that have any exon/splice hit in specified barcode</returns>
        public int GetNumExpressedTranscripts(int bcIdx)
        {
            return geneFeatures.Values.Count(g => (!g.IsSpike() && g.IsExpressed(bcIdx)));
        }
        /// <summary>
        /// Exclude spikes
        /// </summary>
        /// <returns># of non-spike main transcript models that have any exon/splice hit in any barcode</returns>
        public int GetNumExpressedMainTranscriptVariants()
        {
            return geneFeatures.Values.Count(g => (!g.IsSpike() && g.IsMainVariant() && g.IsExpressed()));
        }
        public int GetNumExpressedRepeats()
        {
            return repeatFeatures.Values.Count(r => r.GetTotalHits() > 0);
        }

        public int NumLocusHitEntries
        {
            get
            {
                int n = 0;
                foreach (GeneFeature gf in geneFeatures.Values)
                    n += gf.NumLocusHitEntries;
                return n;
            }
        }

        /// <summary>
        /// Find largest number of exons for any transcript model
        /// </summary>
        /// <returns></returns>
        public int MaxExonCount()
        {
            return geneFeatures.Values.Max(gf => gf.ExonCount);
        }

        /// <summary>
        /// Generate by-barcode array of # of expressed non-spike transcripts
        /// </summary>
        /// <returns></returns>
        public int[] GetByBcNumExpressedTranscripts()
        {
            int[] nBarcodeExpressedGenes = new int[barcodes.Count];
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.IsSpike())
                    continue;
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    if (gf.IsExpressed(bcIdx)) nBarcodeExpressedGenes[bcIdx]++;
            }
            return nBarcodeExpressedGenes;
        }

        /// <summary>
        /// Iterate spikes and/or transcripts ordered by 1) start position, 2) length,
        /// with common chrs first, then other in alphabetical order
        /// </summary>
        /// <param name="inclSpikes">include spikes (chrs with CTRL or SPIKE in the name)</param>
        /// <param name="inclNonSpikes">include the non-spike chromsomes (EXTRA and the rest)</param>
        /// <returns></returns>
        public IEnumerable<GeneFeature> IterOrderedGeneFeatures(bool inclSpikes, bool inclNonSpikes)
        {
            List<string> orderedChrIds = new List<string>();
            if (inclSpikes)
                orderedChrIds.AddRange(Props.props.CommonChrIds.Where(c => c.Contains("CTRL") || c.Contains("SPIKE")));
            if (inclNonSpikes)
            {
                orderedChrIds.AddRange(Props.props.CommonChrIds.Where(c => !(c.Contains("CTRL") || c.Contains("SPIKE") )));
                List<string> nonCommonChrIds = ExonAnnotations.Keys.Where(id => !Props.props.CommonChrIds.Contains(id)).ToList();
                nonCommonChrIds.Sort();
                orderedChrIds.AddRange(nonCommonChrIds);
            }
            foreach (string chrId in orderedChrIds)
            {
                List<GeneFeature> chrGfs = geneFeatures.Values.Where(gf => gf.Chr == chrId).ToList();
                chrGfs.Sort((gf1, gf2) => (gf1.Start != gf2.Start)? (gf1.Start - gf2.Start) : (gf2.Length - gf1.Length));
                foreach (GeneFeature gf in chrGfs)
                    yield return gf;
            }
        }

        /// <summary>
        /// Summarize the total exon/splice hit count that stem from transcripts or spikes within each barcode.
        /// Gene main variants contribute their Max count and secondary variants their Min count.
        /// </summary>
        /// <param name="selectSpikes">false to count only non-spikes, true for only spikes</param>
        /// <returns>Array of per-barcode total hit counts</returns>
        public int[] GetTotalTranscriptCountsByBarcode(bool selectSpikes)
        {
            int[] UniqueGeneCountsByBarcode = new int[barcodes.Count];
            foreach (GeneFeature gf in geneFeatures.Values)
                if (gf.IsSpike() == selectSpikes)
                {
                    if (gf.IsMainVariant())
                    {
                        for (int bcIdx = 0; bcIdx < UniqueGeneCountsByBarcode.Length; bcIdx++)
                            UniqueGeneCountsByBarcode[bcIdx] += gf.TrHits(bcIdx);
                    } else {
                        for (int bcIdx = 0; bcIdx < UniqueGeneCountsByBarcode.Length; bcIdx++)
                            UniqueGeneCountsByBarcode[bcIdx] += gf.TrNCHits(bcIdx);
                    }
                }
            return UniqueGeneCountsByBarcode;
        }

        #endregion

        #region SaveResults
        public string ProjectName { get; private set; }

        /// <summary>
        /// Saves all gene centers tables as tab-delimited files
        /// </summary>
        /// <param name="fileNameBase">full path and projectName prefix where file should be saved ( /X/Y/PROJNAME )</param>
        /// <param name="averageReadLen"></param>
        public void SaveResult(string fileNameBase, int averageReadLen)
        {
            ProjectName = Path.GetDirectoryName(fileNameBase);
            if (barcodes.HasUMIs)
            {
                WriteTrueMolsTable(fileNameBase);
                WriteReadsTable(fileNameBase);
                WriteMaxOccupiedUMIsByEXONTable(fileNameBase);
            }
            string expressionFile = WriteExpressionTable(fileNameBase);
            WriteMinExpressionTable(fileNameBase);
            WriteExportTables(fileNameBase);
            string rpmFile = WriteNormalizedExpression(fileNameBase);
            if (props.ShowTranscriptSharingGenes)
                WriteSharedGenes(fileNameBase);
            WriteIntronCounts(fileNameBase);
            if (props.GenerateGeneProfilesByBarcode)
            {
                WriteExonCountsPerBarcode(fileNameBase);
                WriteIntronCountsPerBarcode(fileNameBase);
            }
            if (props.AnalyzeGCContent)
                WriteGCContentByTranscript(fileNameBase);
            if (props.GenerateTranscriptProfiles)
            {
                WriteTranscriptProfiles(fileNameBase);
                WriteTranscriptHistograms(fileNameBase, averageReadLen);
            }
            WriteSplicesByGeneLocus(fileNameBase);
            if (props.AnalyzeSpliceHitsByBarcode)
                WriteSplicesByGeneLocusAndBc(fileNameBase);
            if (props.DirectionalReads)
            {
                if (props.WriteCAPRegionHits)
                    WriteCAPRegionHitsTable(fileNameBase);
                WriteExpressedAntisenseGenes(fileNameBase);
                WriteUniquehits(fileNameBase);
            }
            if (props.GenerateGeneLocusProfiles)
                WriteLocusHitsByGeneLocus(fileNameBase);
            if (props.GenesToPaint != null && props.GenesToPaint.Length > 0)
            {
                PaintSelectedGeneLocusImages(fileNameBase);
                PaintSelectedGeneTranscriptImages(fileNameBase);
            }
            WriteAnnotTypeAndExonCounts(fileNameBase);
            WritePotentialErronousAnnotations(fileNameBase);
            WriteElongationEfficiency(fileNameBase, averageReadLen);
            WriteSpikeProfilesByBc(fileNameBase);
        }

        private void WriteExpressedAntisenseGenes(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_expressed_antisense.tab"))
            {
                file.WriteLine("Expressed gene pairs transcribed in opposite direction that have overlapping exons. " +
                               "Numbers of overlapping exons and the expression from the non-overlapping exons is shown.");
                file.WriteLine("Chr\tGeneA\tGeneB\t#OverlappingExons\tCountA\tCountB\t" +
                               "OverlappingExonsA\tNonoverlappingCountA\tOverlappingExonsB\tNonoverlappingCountB");
                int nPairs = 0;
                foreach (KeyValuePair<string, int> gfPair in antisensePairExons)
                {
                    string[] names = gfPair.Key.Split('#');
                    GeneFeature gfA = geneFeatures[names[0]];
                    GeneFeature gfB = geneFeatures[names[1]];
                    if (gfA.Chr != gfB.Chr || gfA.Strand == gfB.Strand)
                        throw new Exception("Internal error in sense-antisense genes: " + names[0] + "-" + names[1]);
                    int aHits = gfA.GetTranscriptHits();
                    int bHits = gfB.GetTranscriptHits();
                    if (aHits > 0 && bHits > 0)
                    {
                        List<int> freeExonsA = GetNonOverlappingExons(gfA, gfB);
                        int freeHitsA = gfA.GetExpressionFromExons(freeExonsA);
                        List<int> freeExonsB = GetNonOverlappingExons(gfB, gfA);
                        int freeHitsB = gfB.GetExpressionFromExons(freeExonsB);
                        int nCommonExons = gfPair.Value;
                        string freeExonsAList = MakeExonNumbersAsCommaSepString(freeExonsA);
                        string freeExonsBList = MakeExonNumbersAsCommaSepString(freeExonsB);
                        file.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", gfA.Chr, names[0], names[1], nCommonExons,
                                       aHits, bHits, freeExonsAList, freeHitsA, freeExonsBList, freeHitsB);
                        nPairs++;
                    }
                }
            }
        }

        private string MakeExonNumbersAsCommaSepString(List<int> exonIdxs)
        {
            if (exonIdxs.Count == 0)
                return "-";
            StringBuilder sb = new StringBuilder();
            sb.Append((exonIdxs[0] + 1));
            for (int i = 1; i < exonIdxs.Count; i++)
            {
                sb.Append(',');
                sb.Append(exonIdxs[i] + 1);
            }
            return sb.ToString();
        }

        private List<int> GetNonOverlappingExons(GeneFeature gf, GeneFeature maskGf)
        {
            List<int> freeExons = new List<int>();
            for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
            {
                if (!maskGf.ExonsWithin(gf.ExonStarts[exonIdx], gf.ExonEnds[exonIdx], 0))
                    freeExons.Add(exonIdx);
            }
            return freeExons;
        }

        private void WritePotentialErronousAnnotations(string fileNameBase)
        {
            string warnFilename = fileNameBase + "_annot_errors_" + genome.GetMainIndexName() + ".tab";
            int nErr = 0;
            using (StreamWriter warnFile = new StreamWriter(warnFilename))
            {
                warnFile.WriteLine("Feature\tExonHits\tPart\tPartHits\tPartLocation\tNewLeftExonStart\tNewRightExonStart");
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (gf.GetTotalHits(true) == 0 || StrtGenome.IsACommonChrId(gf.Chr)
                        || gf.Name.EndsWith(GeneFeature.nonUTRExtendedIndicator))
                        continue;
                    if (gf.Strand == '+')
                    {
                        nErr += TestErrorAnnotType(warnFile, gf, gf.LeftMatchStart, gf.Start - 1, AnnotType.USTR);
                        nErr += TestErrorAnnotType(warnFile, gf, gf.End, gf.RightMatchEnd, AnnotType.DSTR);
                    }
                    else
                    {
                        nErr += TestErrorAnnotType(warnFile, gf, gf.LeftMatchStart, gf.Start - 1, AnnotType.DSTR);
                        nErr += TestErrorAnnotType(warnFile, gf, gf.End, gf.RightMatchEnd, AnnotType.USTR);
                    }
                }
            }
            if (nErr == 0)
                File.Delete(warnFilename);
        }

        private int TestErrorAnnotType(StreamWriter warnFile, GeneFeature gf, int start, int end, int annotType)
        {
            int flankHits = gf.HitsByAnnotType[annotType];
            int exonHits = gf.HitsByAnnotType[AnnotType.EXON];
            if (flankHits > 5 && flankHits > exonHits)
            {
                string newLeftExonStart = "", newRightExonStart = "";
                if (annotType == AnnotType.USTR)
                {
                    int newExonStart = CompactGenePainter.FindUpstreamHotspotStart(gf, flankHits);
                    if (newExonStart >= 0)
                    {
                        if (gf.Strand == '+') newLeftExonStart = newExonStart.ToString();
                        else newRightExonStart = newExonStart.ToString();
                    }
                }
                warnFile.WriteLine("{0}\t{1}\t{2}\t{3}\tChr{4}{5}:{6}-{7}\t{8}\t{9}",
                            gf.Name, exonHits, AnnotType.GetName(annotType),
                            flankHits, gf.Chr, gf.Strand, start, end,
                            newLeftExonStart, newRightExonStart);
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Iterate expression values for the cells that belong to the genome. Skip wells marked "Empty" by layout file.
        /// Also iterate repeats.
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        public IEnumerable<Expression> IterC1DBExpressions(Dictionary<string, int> cellIdByPlateWell)
        {
            Expression exprHolder = new Expression();
            foreach (int bcIdx in barcodes.GenomeBarcodeIndexes(genome, true))
            {
                exprHolder.CellID = cellIdByPlateWell[barcodes.GetWellId(bcIdx)].ToString();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    exprHolder.TranscriptID = gf.TranscriptID;
                    exprHolder.UniqueMolecules = gf.TrNCHits(bcIdx);
                    exprHolder.Molecules = gf.TrHits(bcIdx);
                    exprHolder.UniqueReads = gf.NonConflictingTrReadsByBc[bcIdx];
                    exprHolder.Reads = gf.TrReadsByBc[bcIdx];
                    yield return exprHolder;
                }
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    if (rf.C1DBTranscriptID == -1) continue;
                    exprHolder.TranscriptID = rf.C1DBTranscriptID;
                    exprHolder.UniqueMolecules = 0;
                    exprHolder.UniqueReads = 0;
                    exprHolder.Molecules = rf.Hits(bcIdx);
                    exprHolder.Reads = rf.TotalReadsByBc[bcIdx];
                    yield return exprHolder;
                }
            }
        }

        /// <summary>
        /// Iterate expression data of non-empty wells as Blobs for DB insertion.
        /// Note that the ExprBlob object is re-used at each cycle.
        /// </summary>
        /// <param name="cellIdByPlateWell"></param>
        /// <returns></returns>
        public IEnumerable<ExprBlob> IterC1DBExprBlobs(Dictionary<string, int> cellIdByPlateWell)
        {
            int nValues = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
                nValues = Math.Max(nValues, gf.ExprBlobIdx + 1);
            ExprBlob exprBlob = new ExprBlob(nValues);
            exprBlob.TranscriptomeID = dbTranscriptome.TranscriptomeID.Value;
            foreach (int bcIdx in barcodes.GenomeBarcodeIndexes(genome, true))
            {
                exprBlob.ClearBlob();
                exprBlob.CellID = cellIdByPlateWell[barcodes.GetWellId(bcIdx)].ToString();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    exprBlob.SetBlobValue(gf.ExprBlobIdx, gf.TrHits(bcIdx));
                }
                yield return exprBlob;
            }
        }

        private void WriteReadsTable(string fileNameBase)
        {
            string readFile = fileNameBase + "_reads.tab";
            string header = MakeFirstHeader(true, "#{0} {1} unfiltered read counts and sense+antisense reads counts for repeat types.{3}");
            WriteBasicDataTable(readFile, header, GeneFeature.IterTrReads);
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            using (StreamWriter outFile = new StreamWriter(readFile, true))
            {
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (int bcIdx in speciesBcIndexes)
                    {
                        sb.Append("\t");
                        sb.Append(rf.TotalReadsByBc[bcIdx]);
                    }
                    outFile.WriteLine("{0}\t\t\t\t{1}\t{2}{3}", rf.Name, rf.GetLocusLength(), rf.TotalReadsByBc.Sum(), sb);
                }
            }
        }

        private void WriteMaxOccupiedUMIsByEXONTable(string fileNameBase)
        {
            string header = "#Maximal occupied UMIs (after mutated UMI filtering) in each barcode of each transcript.";
            WriteBasicDataTable(fileNameBase + "_EXON_UMI_usage.tab", header, GeneFeature.IterMaxOccupiedUMIsByEXON);
        }

        private void WriteTrueMolsTable(string fileNameBase)
        {
            string header = MakeFirstHeader(true, "#{0} {1} estimated true molecule counts.{3}");
            WriteBasicDataTable(fileNameBase + "_true_counts.tab", header, GeneFeature.IterTrEstTrueMolCounts);
        }

        private void WriteBasicDataTable(string fileName, string header, HitIterator hitIterator)
        {
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            using (StreamWriter outFile = new StreamWriter(fileName))
            {
                outFile.WriteLine(header);
                WriteSampleAnnotationLines(outFile, 5, true, speciesBcIndexes);
                outFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tAllBcSum");
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    StringBuilder sbDatarow = new StringBuilder();
                    int total = 0;
                    foreach (int c in hitIterator(gf,  speciesBcIndexes))
                    {
                        total += c;
                        sbDatarow.Append("\t");
                        sbDatarow.Append(c);
                    }
                    outFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}{6}",
                                   gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), total, sbDatarow);

                }
            }
        }

        /// <summary>
        /// Construct a suitable header for an expression output file
        /// </summary>
        /// <param name="withMultireads">The count table will contain multireads, and not only counts from use of single reads</param>
        /// <param name="pattern">formatting pattern, see below</param>
        /// <returns></returns>
        private string MakeFirstHeader(bool withMultireads, string pattern)
        {
            string dataType = withMultireads ? string.Format("Max (using also <={0}x mapping multireads)", props.MaxAlternativeMappings)
                                             : "Min (using only uniquely mapping reads)";
            string dirType = props.DirectionalReads ? "sense only" : "sense+antisense";
            string valueType = barcodes.HasUMIs ? "molecule" : "read";
            string multireadTxt = "";
            if (!withMultireads)
                multireadTxt = " Multireads have been assigned " +
                    (props.UseMost5PrimeExonMapping ? "to their most 5' mapping." : "multiply to all their alternative mappings.");
            string firstHeader = string.Format(pattern, dataType, dirType, valueType, multireadTxt);
            return firstHeader;
        }

        /// <summary>
        /// For each feature, write the total (for genes, transcript) hit count for every barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        /// <returns>Path to output file</returns>
        private string WriteExpressionTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_expression.tab";
            return WriteExtendedDataTable(exprPath, true, GeneFeature.IterTrMaxHits);
        }
        private string WriteMinExpressionTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_expression_singlereads.tab";
            return WriteExtendedDataTable(exprPath, false, GeneFeature.IterTrNCHits);
        }
        private string WriteExtendedDataTable(string fileName, bool withMultireads, HitIterator hitIterator)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                string firstHeader = MakeFirstHeader(withMultireads,
                                       "#{0} {1} {2} counts for transcripts, and sense+antisense {2} counts for repeat types.{3}");
                writer.WriteLine(firstHeader);
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                WriteSampleAnnotationLines(writer, 9, true, speciesBcIndexes);
                writer.WriteLine("Feature\tType\tTrNames\tChr\tPos\tStrand\tTrLen\tClose{0}\tMinExonHits\tExonHits",
                                 string.Join("/", props.CAPCloseSiteSearchCutters));
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    string[] fields = (gf.GeneMetadata + GeneFeature.metadataDelim).Split(GeneFeature.metadataDelim);
                    string trName = fields[0];
                    string cutSites = fields[1];
                    string safeName = ExcelRescueGeneName(gf.Name);
                    writer.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}",
                               safeName, gf.GeneType, trName, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), cutSites,
                               gf.TrNCHitSum(), gf.TrHitSum());
                    foreach (int c in hitIterator(gf, speciesBcIndexes))
                        writer.Write("\t{0}", c);
                    writer.WriteLine();
                }
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    writer.Write("{0}\trepeat\t\t\t\t\t{1}\t\t{2}\t{2}", rf.Name, rf.GetLocusLength(), rf.Hits());
                    foreach (int bcIdx in speciesBcIndexes)
                        writer.Write("\t{0}", rf.Hits(bcIdx));
                    writer.WriteLine();
                }
            }
            return fileName;
        }

        /// <summary>
        /// Changes names of genes that horrible Excel would otherwise change to dates
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static string ExcelRescueGeneName(string name)
        {
            if (name.Length < 4) return name;
            if (Regex.IsMatch(name.ToLower(), "^(jan|feb|mar|apr|jun|jul|aug|sep|oct|nov|dec)[0-9]+$"))
                return "'" + name + "'";
            if (Regex.IsMatch(name.ToLower(), "^(janu|febr|marc|apri|juni|july|augu|sept|octo|nove|dece)[0-9]+$"))
                return "'" + name + "'";
            if (Regex.IsMatch(name.ToLower(), "^(janua|febru|march|april|augus|septe|octob|novem|decem)[0-9]+$"))
                return "'" + name + "'";
            return name;
        }

        private delegate IEnumerable<int> HitIterator(GeneFeature gf, int[] bcIndexes);

        private void WriteExportTables(string fileNameBase)
        {
            WriteMatlabTables(fileNameBase, GeneFeature.IterTrMaxHits);
            WriteRTable(fileNameBase, GeneFeature.IterTrMaxHits);
            WriteQlucoreTable(fileNameBase);
        }

        private void WriteRTable(string fileNameBase, HitIterator hitIterator)
        {
            string fileName = fileNameBase + "_expression_for_R.tab";
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                foreach (int idx in speciesBcIndexes)
                    writer.Write("\t{0}", barcodes.GetWellId(idx));
                writer.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    writer.Write(gf.Name);
                    foreach (int c in hitIterator(gf, speciesBcIndexes))
                        writer.Write("\t{0}", c);
                    writer.WriteLine();
                }
            }
        }

        private string WriteMatlabTables(string fileNameBase, HitIterator hitIterator)
        {
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            using (StreamWriter file = new StreamWriter(fileNameBase + "_MATLAB_annotations.tab"))
                WriteSampleAnnotationLines(file, 0, false, speciesBcIndexes);
            string fileName = fileNameBase + "_MATLAB_expression.tab";
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.Write("Feature\tChr\tPos\tStrand");
                foreach (int idx in speciesBcIndexes)
                    writer.Write("\t{0}", barcodes.GetWellId(idx));
                writer.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    writer.Write("{0}\t{1}\t{2}\t{3}", gf.Name, gf.Chr, gf.Start, gf.Strand);
                    foreach (int c in hitIterator(gf, speciesBcIndexes))
                        writer.Write("\t{0}", c);
                    writer.WriteLine();
                }
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    writer.Write("{0}\t\t\t", rf.Name);
                    foreach (int bcIdx in speciesBcIndexes)
                        writer.Write("\t{0}", rf.Hits(bcIdx));
                    writer.WriteLine();
                }
            }
            return fileName;
        }

        private void WriteQlucoreTable(string fileNameBase)
        {
            string fileName = fileNameBase + MakeRPFileType() + ".gedata";
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            Dictionary<GeneFeature, double[]> normalizedData = GetNormalizedData(speciesBcIndexes);
            int nSamples = speciesBcIndexes.Length;
            int nSampleAttributes = GetNSampleAnnotationLines();
            int nVariables = normalizedData.First().Value.Length;
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine("Qlucore\tgedata\tversion 1.0");
                writer.WriteLine();
                writer.WriteLine("{0}\tsamples\twith\t{1}\tattributes", nSamples, nSampleAttributes);
                writer.WriteLine("{0}\tvariables\twith\t5\tannotations", nVariables);
                writer.WriteLine();
                WriteSampleAnnotationLines(writer, 5, false, speciesBcIndexes);
                writer.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\t");
                foreach (GeneFeature gf in normalizedData.Keys)
                {
                    writer.Write("{0}\t{1}\t{2}\t{3}\t{4}\t", gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength());
                    foreach (double v in normalizedData[gf])
                        writer.Write("\t{0}", v);
                    writer.WriteLine();
                }
            }
        }

        private Dictionary<GeneFeature, double[]> GetNormalizedData(int[] selectedBcIndexes)
        {
            int[] totalByBarcode = GetTotalTranscriptCountsByBarcode(false);
            int grandTotal = totalByBarcode.Sum();
            double normalizer = barcodes.HasUMIs ? (grandTotal / (double)totalByBarcode.Count(v => v > 0)) : 1.0E+6;
            double[] normFactors = CalcNormalizationFactors(totalByBarcode);
            double trLenFactor = 1.0;
            Dictionary<GeneFeature, double[]> normalizedData = new Dictionary<GeneFeature, double[]>();
            foreach (GeneFeature gf in IterOrderedGeneFeatures(false, true))
            {
                double[] expr = new double[selectedBcIndexes.Length];
                if (props.UseRPKM)
                    trLenFactor = gf.GetTranscriptLength() / 1000.0;
                int exprIdx = 0;
                foreach (int bcIdx in selectedBcIndexes)
                {
                    double normedValue = (normFactors[bcIdx] * gf.TrHits(bcIdx)) / trLenFactor;
                    expr[exprIdx++] = normedValue;
                }
                normalizedData[gf] = expr;
            }
            return normalizedData;
        }

        private string MakeRPFileType()
        {
            string ROrM = barcodes.HasUMIs ? "Mols" : "Reads";
            return ROrM + (props.UseRPKM ? "PerKBases" : "") + (barcodes.HasUMIs ? "Normalized" : "PerMillion");
        }

        private string WriteNormalizedExpression(string fileNameBase)
        {
            string exprFile = fileNameBase + "_" + MakeRPFileType() + ".tab";
            bool molCounts = barcodes.HasUMIs;
            using (StreamWriter exprWriter = new StreamWriter(exprFile))
            using (StreamWriter simpleWriter = new StreamWriter(fileNameBase + "_" + MakeRPFileType() + "_simple.txt"))
            {
                string rpDescr = (molCounts ? "molecules " : "reads ") + (props.UseRPKM ? "per kilobase transcript and " : "") +
                                 (molCounts ? "normalized to the average across all samples" : "million");
                exprWriter.WriteLine("Values in the table are " + rpDescr);
                exprWriter.WriteLine("Note that added spikes and transcripts are normalized separately.");
                if (props.DirectionalReads)
                {
                    exprWriter.WriteLine("The given estimated detection thresholds ('P') are calculated from 99% and 99.9% of the global distribution ");
                    exprWriter.WriteLine("of AntiSense Exon hits, and the normalized values for main transcripts in each barcode.");
                }
                if (!props.UseRPKM)
                    exprWriter.WriteLine("Single{0} is the value (in that barcode and spike or sample section) that corresponds to a single {1}.",
                                         (molCounts ? "Mol" : "Read"), (molCounts ? "molecule" : "read"));
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                WriteSampleAnnotationLines(exprWriter, (props.DirectionalReads ? 9 : 7), true, speciesBcIndexes);
                exprWriter.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tTotExonHits\t{0}Average\tCV", (props.DirectionalReads ? "P=0.01\tP=0.001\t" : ""));
                WriteNormalizedExprSection(exprWriter, true, null);
                exprWriter.WriteLine();
                foreach (int idx in speciesBcIndexes)
                    simpleWriter.Write("\t{0}", barcodes.GetWellId(idx));
                simpleWriter.WriteLine();
                WriteNormalizedExprSection(exprWriter, false, simpleWriter);
            }
            return exprFile;
        }

        private void WriteNormalizedExprSection(StreamWriter exprWriter, bool selectSpikes, StreamWriter simpleWriter)
        {
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            int[] totalByBarcode = GetTotalTranscriptCountsByBarcode(selectSpikes);
            int totCount = totalByBarcode.Sum();
            double[] normFactors = CalcNormalizationFactors(totalByBarcode);
            double RPkbM99, RPkbM999;
            GetDetectionThresholds(selectSpikes, totCount, out RPkbM99, out RPkbM999);
            string normName = (props.UseRPKM) ? "Normalizer" : (barcodes.HasUMIs) ? "SingleMol" : "SingleRead";
            exprWriter.Write("{0}\t\t\t\t\t\t\t", normName);
            if (props.DirectionalReads) exprWriter.Write("\t\t");
            foreach (int idx in speciesBcIndexes)
                exprWriter.Write("\t{0:G6}", normFactors[idx]);
            exprWriter.WriteLine();

            foreach (GeneFeature gf in IterOrderedGeneFeatures(selectSpikes, !selectSpikes))
            {
                double trLenFactor = (props.UseRPKM) ? gf.GetTranscriptLength() : 1000.0;
                string safeName = ExcelRescueGeneName(gf.Name);
                exprWriter.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                 safeName, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), gf.GetTranscriptHits());
                if (props.DirectionalReads)
                {
                    string RPkbMThres01 = string.Format("{0:G6}", RPkbM99 * gf.GetNonMaskedTranscriptLength() / trLenFactor);
                    string RPkbMThres001 = string.Format("{0:G6}", RPkbM999 * gf.GetNonMaskedTranscriptLength() / trLenFactor);
                    exprWriter.Write("\t{0}\t{1}", RPkbMThres01, RPkbMThres001);
                }
                StringBuilder sb = new StringBuilder();
                DescriptiveStatistics ds = new DescriptiveStatistics();
                foreach (int bcIdx in speciesBcIndexes)
                {
                    double normedValue = (normFactors[bcIdx] * gf.TrHits(bcIdx)) * 1000.0 / trLenFactor;
                    ds.Add(normedValue);
                    sb.AppendFormat("\t{0:G6}", normedValue);
                }
                string CV = "N/A";
                if (ds.Count > 2 && gf.GetTranscriptHits() > 0)
                    CV = string.Format("{0:G6}", (ds.StandardDeviation() / ds.Mean()));
                exprWriter.WriteLine("\t{0:G6}\t{1}{2}", ds.Mean(), CV, sb.ToString());
                if (simpleWriter != null)
                    simpleWriter.WriteLine(gf.Name + sb.ToString());
            }
        }

        private void GetDetectionThresholds(bool selectSpikes, int totCount, out double RPkbM99, out double RPkbM999)
        {
            List<double> allASReadsPerBase = new List<double>();
            foreach (GeneFeature gf in IterOrderedGeneFeatures(true, false))
            {
                int antiHits = gf.NonMaskedHitsByAnnotType[AnnotType.AEXON];
                double ASReadsPerBase = antiHits / (double)gf.GetNonMaskedTranscriptLength();
                allASReadsPerBase.Add(ASReadsPerBase);
            }
            RPkbM999 = Double.NaN;
            RPkbM99 = Double.NaN;
            if (!selectSpikes && totCount > 0 && allASReadsPerBase.Count > 0)
            {
                allASReadsPerBase.Sort();
                RPkbM99 = 1000 * 1.0E+6 * allASReadsPerBase[(int)Math.Floor(allASReadsPerBase.Count * 0.99)] / (double)totCount;
                RPkbM999 = 1000 * 1.0E+6 * allASReadsPerBase[(int)Math.Floor(allASReadsPerBase.Count * 0.999)] / (double)totCount;
            }
        }

        private double[] CalcNormalizationFactors(int[] totalByBarcode)
        {
            double normalizer = barcodes.HasUMIs ? (totalByBarcode.Sum() / (double)totalByBarcode.Count(v => v > 0)) : 1.0E+6;
            double[] normFactors = Array.ConvertAll(totalByBarcode, v => ((v > 0) ? (normalizer / (double)v) : 0.0));
            return normFactors;
        }

        private void WriteCAPRegionHitsTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_CAPRegionHits.tab";
            using (StreamWriter matrixFile = new StreamWriter(exprPath))
            {
                string hitType = barcodes.HasUMIs ? "molecule" : "read";
                matrixFile.WriteLine("{0} hits within {1} bases ('CAP Region') downstream of transcript start (moved {2} bases 5' from original annotation).",
                                     hitType, props.CapRegionSize, props.GeneFeature5PrimeExtension);
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                matrixFile.Write("Feature\tChr\tCAPPos\tStrand\tAllTrscrHits\tSumCAPHits");
                foreach (int idx in speciesBcIndexes)
                    matrixFile.Write("\t{0}", barcodes.GetWellId(idx));
                matrixFile.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    int totalHits = gf.CAPRegionHitsByBc.Sum();
                    string safeName = ExcelRescueGeneName(gf.Name);
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                     safeName, gf.Chr, gf.SavedCAPPos, gf.Strand, gf.GetTranscriptHits(), totalHits);
                    foreach (int idx in speciesBcIndexes)
                        matrixFile.Write("\t{0}", gf.CAPRegionHitsByBc[idx]);
                    matrixFile.WriteLine();
                }
            }
        }

        /// <summary>
        /// For each barcode and locus, write the total hit count
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteExpressionList(string fileNameBase)
        {
            // Create a normal-form table of hit counts
            using (StreamWriter tableFile = new StreamWriter(fileNameBase + "_expression_list.tab"))
            {
                string exonHitType = (props.UseMost5PrimeExonMapping && props.DirectionalReads) ? "ExonHits" : "MaxExonHits";
                tableFile.WriteLine("Barcode\tFeature\t{0}", exonHitType);
                tableFile.WriteLine();
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    foreach (int bcIdx in speciesBcIndexes)
                    {
                        tableFile.Write("{0}\t", barcodes.Seqs[bcIdx]);
                        tableFile.Write("{0}\t", gf.Name);
                        tableFile.WriteLine(gf.TrHits(bcIdx));
                    }
                }
            }
        }

        private int GetNSampleAnnotationLines()
        {
            return 3 + barcodes.GetAnnotationTitles().Count;
        }
        private void WriteSampleAnnotationLines(StreamWriter matrixFile, int nTabs, bool addColon, int[] selectedBcIndexes)
        {
            string colon = addColon ? ":" : "";
            String tabs = new String('\t', nTabs);
            matrixFile.Write("{0}Sample{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}_{1}", ProjectName, barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            matrixFile.Write("{0}Well{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}", barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            matrixFile.Write("{0}Barcode{1}", tabs, colon);
            foreach (int idx in selectedBcIndexes)
                matrixFile.Write("\t{0}", barcodes.Seqs[idx]);
            matrixFile.WriteLine();
            foreach (string annotation in barcodes.GetAnnotationTitles())
            {
                matrixFile.Write("{0}{1}{2}", tabs, annotation, colon);
                foreach (int idx in selectedBcIndexes)
                    matrixFile.Write("\t{0}", barcodes.GetAnnotation(annotation, idx));
                matrixFile.WriteLine();
            }
        }

        private void WriteSharedGenes(string fileNameBase)
        {
            using (StreamWriter trShareFile = new StreamWriter(fileNameBase + "_sharing_genes.tab"))
            {
                trShareFile.WriteLine("Transcripts/variants competing for reads (# shared reads within parenthesis)");
                trShareFile.WriteLine("Feature\t#Assigned Reads\tCompetes with genes...");
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (gf.sharingGenes == null)
                        continue;
                    List<string> sGfGroup = new List<string>();
                    foreach (KeyValuePair<IFeature, int> pair in gf.sharingGenes)
                    {
                        string sGfName = pair.Key.Name;
                        if (!sGfGroup.Contains(sGfName) && sGfName != gf.Name)
                            sGfGroup.Add(string.Format("{0}({1})", sGfName, pair.Value));
                    }
                    sGfGroup.Sort();
                    if (sGfGroup.Count > 0)
                        trShareFile.WriteLine("{0}\t{1}\t{2}", gf.Name, gf.TrReadsByBc.Sum(), string.Join("\t", sGfGroup.ToArray()));
                }
            }
        }

        /// <summary>
        /// For every gene, write the total hit count per annotation type (exon, intron, flank)
        /// and also hit count for each exon.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteAnnotTypeAndExonCounts(string fileNameBase)
        {
            int nExonsToShow = MaxExonCount();
            using (StreamWriter file = new StreamWriter(fileNameBase + "_exons.tab"))
            {
                string hitType = barcodes.HasUMIs ? "molecules" : "reads";
                file.WriteLine("Hits counts as " + hitType + " sorted by detected type of annotation, direction, and exon number.");
                file.WriteLine("MixGene shows overlapping genes in the same, ASGene in the opposite orientation.");
                file.Write("Gene\tChr\tPos\tStrand\tTrLen\tUSTRLen\tLocusLen\tDSTRLen\t#SenseHits\t#AntiHits\tIntronLen\t#Exons\tMixGene\tASGene");
                foreach (int i in AnnotType.GetGeneTypes())
                {
                    string annotName = AnnotType.GetName(i);
                    if (annotName == "EXON") annotName = "EXON+SPLC";
                    file.Write("\t#{0}Hits", annotName);
                }
                string exonTitle = props.DirectionalReads ? "SExon" : "Exon";
                for (int exonId = 1; exonId <= nExonsToShow; exonId++)
                    file.Write("\t{0}{1}", exonTitle, exonId);
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    string mixedSenseGene = "";
                    if (gf.HitsByAnnotType[AnnotType.INTR] >= 5 ||
                        (gf.HitsByAnnotType[AnnotType.INTR] >= gf.HitsByAnnotType[AnnotType.EXON] * 0.10))
                        mixedSenseGene = OverlappingExpressedGene(gf, 10, true);
                    string mixedASGene = "";
                    if (gf.HitsByAnnotType[AnnotType.AINTR] >= 5 || gf.HitsByAnnotType[AnnotType.AEXON] >= 5)
                        mixedASGene = OverlappingExpressedGene(gf, 10, false);
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}",
                                     safeName, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), gf.USTRLength,
                                     gf.GetLocusLength(), gf.DSTRLength, gf.GetTotalHits(true), gf.GetTotalHits(false),
                                     gf.GetIntronicLength(), gf.ExonCount, mixedSenseGene, mixedASGene);
                    foreach (int i in AnnotType.GetGeneTypes())
                    {
                        int nHits = gf.HitsByAnnotType[i];
                        file.Write("\t{0}", nHits);
                    }
                    int[] counts = CompactGenePainter.GetCountsPerExon(gf, props.DirectionalReads);
                    WriteCountsDirected(file, gf.Strand, counts);
                }
            }
        }

        private static void WriteCountsDirected(StreamWriter file, char strand, int[] counts)
        {
            if (strand == '-')
                for (int i = counts.Length - 1; i >= 0; i--)
                    file.Write("\t{0}", counts[i]);
            else
                for (int i = 0; i < counts.Length; i++)
                    file.Write("\t{0}", counts[i]);
            file.WriteLine();
        }

        private void WriteIntronCounts(string fileNameBase)
        {
            int nIntronsToShow = MaxExonCount() + 1;
            using (StreamWriter file = new StreamWriter(fileNameBase + "_introns.tab"))
            {
                file.Write("Gene\tChr\tStrand\tUSTRStart\tLocusLen\tDSTREnd\tUSTR({0}bp)\tDSTR({0}bp)", GeneFeature.LocusFlankLength);
                for (int intronId = 1; intronId <= nIntronsToShow; intronId++)
                    file.Write("\tIntr{0}", intronId);
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", safeName, gf.Chr, gf.Strand, gf.LocusStart, gf.GetLocusLength(), gf.LocusEnd);
                    int[] counts = CompactGenePainter.GetCountsPerIntron(gf, props.DirectionalReads);
                    if (gf.Strand == '-')
                    {
                        file.Write("\t{0}\t{1}", counts[1], counts[0]);
                        for (int i = counts.Length - 1; i >= 2; i--)
                            file.Write("\t{0}", counts[i]);
                    }
                    else
                    {
                        file.Write("\t{0}\t{1}", counts[0], counts[1]);
                        for (int i = 2; i < counts.Length; i++)
                            file.Write("\t{0}", counts[i]);
                    }
                    file.WriteLine();
                }
            }
        }

        private void WriteIntronCountsPerBarcode(string fileNameBase)
        {
            int nIntronsToShow = MaxExonCount() + 1;
            using (StreamWriter file = new StreamWriter(fileNameBase + "_introns_by_bc.tab"))
            {
                file.WriteLine("Only genes with any intronic hits are shown. Rows are truncated when only zeroes remain.");
                file.Write("Gene\tChr\tStrand\tUSTRStart\tLocusLen\tDSTREnd\tBarcode\tUSTR({0}bp\tDSTR({0}bp))",
                           GeneFeature.LocusFlankLength);
                for (int intronId = 1; intronId <= nIntronsToShow - 2; intronId++)
                    file.Write("\tIntr{0}", intronId);
                file.WriteLine();
                int[,] counts = new int[barcodes.Count, nIntronsToShow];
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    int n = CompactGenePainter.GetCountsPerIntronAndBarcode(gf, props.DirectionalReads, barcodes.Count, ref counts);
                    string safeName = ExcelRescueGeneName(gf.Name);
                    string firstCols = string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                                     safeName, gf.Chr, gf.Strand, gf.LocusStart, gf.GetLocusLength(), gf.LocusEnd);
                    string nextFirstCols = string.Format("{0}\t\t\t\t\t", gf.Name);
                    WriteCountsByBarcodeDirected(file, counts, gf, firstCols, nextFirstCols, 2, n - 1);
                }
            }
        }

        private void WriteCountsByBarcodeDirected(StreamWriter file, int[,] counts, GeneFeature gf,
                                                    string firstCols, string followingFirstCols, int startAt, int last)
        {
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                string bc = barcodes.Seqs[bcIdx];
                file.Write("{0}\t{1}", firstCols, bc);
                if (gf.Strand == '-')
                {
                    for (int i = startAt - 1; i >= 0; i--)
                        file.Write("\t{0}", counts[bcIdx, i]);
                    int firstNonZero = startAt;
                    while (firstNonZero < last && counts[bcIdx, firstNonZero] == 0) firstNonZero++;
                    for (int i = last; i >= firstNonZero; i--)
                        file.Write("\t{0}", counts[bcIdx, i]);
                }
                else
                {
                    for (int i = 0; i < startAt; i++)
                        file.Write("\t{0}", counts[bcIdx, i]);
                    int lastNonZero = last;
                    while (lastNonZero > startAt && counts[bcIdx, lastNonZero] == 0) lastNonZero--;
                    for (int i = startAt; i <= lastNonZero; i++)
                        file.Write("\t{0}", counts[bcIdx, i]);
                }
                file.WriteLine();
                firstCols = followingFirstCols;
            }
        }

        private void WriteExonCountsPerBarcode(string fileNameBase)
        {
            int nExonsToShow = MaxExonCount();
            using (StreamWriter file = new StreamWriter(fileNameBase + "_exons_by_bc.tab"))
            {
                file.WriteLine("Only expressed genes are shown. Rows are truncated when only zeroes remain.");
                file.Write("Gene\tChr\tStrand\tPos\tTrLen\tBarcode");
                for (int exonId = 1; exonId <= nExonsToShow; exonId++)
                    file.Write("\tExon{0}", exonId);
                file.WriteLine();
                int[,] counts = new int[barcodes.Count, nExonsToShow];
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    int n = CompactGenePainter.GetCountsPerExonAndBarcode(gf, props.DirectionalReads, barcodes.Count, ref counts);
                    string safeName = ExcelRescueGeneName(gf.Name);
                    string firstCols = string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                                                     safeName, gf.Chr, gf.Strand, gf.Start, gf.GetTranscriptLength());
                    string followingFirstCols = string.Format("{0}\t\t\t\t", gf.Name);
                    WriteCountsByBarcodeDirected(file, counts, gf, firstCols, followingFirstCols, 0, n - 1);
                }
            }
        }

        private string OverlappingExpressedGene(GeneFeature gf, int minIntrusion, bool sameStrand)
        {
            List<string> overlapNames = new List<string>();
            char searchStrand = gf.Strand;
            if (!sameStrand) searchStrand = (searchStrand == '+') ? '-' : '+';
            foreach (GeneFeature otherGf in geneFeatures.Values)
            {
                if (otherGf.Chr == gf.Chr && otherGf.Strand == searchStrand &&
                    otherGf != gf && otherGf.GetTranscriptHits() > 0 &&
                    otherGf.Overlaps(gf.Start, gf.End, minIntrusion))
                    overlapNames.Add(otherGf.Name);
            }
            return string.Join(",", overlapNames.ToArray());
        }

        /// <summary>
        /// For every gene, write the differential splicing.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteSplicesByGeneLocus(string fileNameBase)
        {
            string fPath = fileNameBase + "_diff_splice.tab";
            using (StreamWriter matrixFile = new StreamWriter(fPath))
            {
                matrixFile.WriteLine("Total hits to individual exons and splice junctions.");
                matrixFile.WriteLine("Gene\t#Exons\t#TrHits\t#JunctionHits\tExon/Junction IDs...");
                matrixFile.WriteLine("\t\t\t\tCounts...");
                matrixFile.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    string safeName = ExcelRescueGeneName(gf.Name);
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}", safeName, gf.ExonCount, gf.GetTranscriptHits(), gf.GetJunctionHits());
                    List<Pair<string, int>> spliceCounts = gf.GetSpliceCounts();
                    foreach (Pair<string, int> spliceAndCount in spliceCounts)
                        matrixFile.Write("\t{0}", spliceAndCount.First);
                    matrixFile.WriteLine();
                    matrixFile.Write("{0}\t\t\t", gf.Name);
                    foreach (Pair<string, int> spliceAndCount in spliceCounts)
                        matrixFile.Write("\t{0}", spliceAndCount.Second);
                    matrixFile.WriteLine();
                }
            }
        }

        private void WriteSplicesByGeneLocusAndBc(string fileNameBase)
        {
            string fPath = fileNameBase + "_diff_splice_by_bc.tab";
            using (StreamWriter matrixFile = new StreamWriter(fPath))
            {
                matrixFile.WriteLine("Hits to individual splice junctions by barcode.");
                matrixFile.WriteLine("Gene\t#Exons\t#TrHits\t#JunctionHits\t\tJunctionIDs...");
                matrixFile.WriteLine("\t\t\t\tBarcode\tCounts...");
                matrixFile.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    string safeName = ExcelRescueGeneName(gf.Name);
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}\t", safeName, gf.ExonCount, gf.GetTranscriptHits(), gf.GetJunctionHits());
                    List<Pair<string, ushort[]>> splicesAndBcCounts = gf.GetSpliceCountsPerBarcode();
                    foreach (Pair<string, ushort[]> spliceAndBcCounts in splicesAndBcCounts)
                        matrixFile.Write("\t{0}", spliceAndBcCounts.First);
                    matrixFile.WriteLine();
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    {
                        matrixFile.Write("{0}\t\t\t\t{1}", gf.Name, barcodes.Seqs[bcIdx]);
                        foreach (Pair<string, ushort[]> spliceAndBcCounts in splicesAndBcCounts)
                            matrixFile.Write("\t{0}", spliceAndBcCounts.Second[bcIdx]);
                        matrixFile.WriteLine();
                    }
                }
            }
        }

        /// <summary>
        /// For every expressed gene, write the binned hit count profile across the transcript.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteTranscriptHistograms(string fileNameBase, int averageReadLen)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_transcript_histograms.tab"))
            {
                file.WriteLine("Binned number of hits to transcripts counting from 5' end of transcript");
                file.Write("Gene\tTrLen\tTotHits\tExonHits\tFrom5'-");
                for (int i = 0; i < 100; i++)
                    file.Write("{0}\t", i * props.LocusProfileBinSize);
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (!gf.IsExpressed()) continue;
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.Write("{0}\t{1}\t{2}\t{3}\t", safeName, gf.GetTranscriptLength(), gf.GetTotalHits(), gf.GetTranscriptHits());
                    int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, props.LocusProfileBinSize,
                                                                                         props.DirectionalReads, averageReadLen);
                    foreach (int c in trBinCounts)
                        file.Write("{0}\t", c);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// For every locus, write the binned hit count profiles across the chromosome.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteLocusHitsByGeneLocus(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_locus_histograms.tab"))
            {
                file.WriteLine("Binned number of hits to either strand of gene loci (including flanks) relative to 5' end of gene:");
                file.Write("Gene\tTrscrDir\tLen\tChrStrand\tTotHits\t5'End->");
                for (int i = 0; i < 100000 / props.LocusProfileBinSize; i++)
                    file.Write("{0}bp\t", i * props.LocusProfileBinSize);
                file.WriteLine();
                int histoSize = 0;
                foreach (GeneFeature gf in geneFeatures.Values)
                    histoSize = Math.Max(histoSize, gf.GetLocusLength());
                int[] histo = new int[histoSize];
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (gf.GetTotalHits() == 0) continue;
                    WriteLocusHistogramLine(file, ref histo, gf, true);
                    WriteLocusHistogramLine(file, ref histo, gf, false);
                }
            }
        }

        private static void WriteLocusHistogramLine(StreamWriter file, ref int[] histo, GeneFeature gf, bool sense)
        {
            char chrStrand = ((gf.Strand == '+') ^ sense) ? '-' : '+';
            string safeName = ExcelRescueGeneName(gf.Name);
            file.Write("{0}\t{1}\t{2}\t{3}\t{4}", safeName, gf.Strand, gf.Length, chrStrand, gf.GetTotalHits(sense));
            int maxBin = CompactGenePainter.MakeLocusHistogram(gf, chrStrand, Props.props.LocusProfileBinSize, ref histo);
            for (int c = 0; c < maxBin; c++)
                file.Write("\t{0}", histo[c]);
            file.WriteLine();
        }

        private void WriteUniquehits(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_unique_hits.tab"))
            {
                file.WriteLine("Unique hit positions in gene loci relative to the first position of a {0} bp LeftFlank, counting in chr direction:",
                                GeneFeature.LocusFlankLength);
                file.Write("Gene\tTrscrDir\tChr\tLeftFlankStart\tRightFlankEnd\tStrand\t#Positions\t");
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    List<int> hisPoss = CompactGenePainter.GetLocusHitPositions(gf, '+');
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                               safeName, gf.Strand, gf.Chr, gf.LocusStart, gf.LocusEnd, "+", hisPoss.Count);
                    foreach (int p in hisPoss)
                        file.Write("\t{0}", p);
                    file.WriteLine();
                    hisPoss = CompactGenePainter.GetLocusHitPositions(gf, '-');
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                               safeName, gf.Strand, gf.Chr, gf.LocusStart, gf.LocusEnd, "-", hisPoss.Count);
                    foreach (int p in hisPoss)
                        file.Write("\t{0}", p);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write the gene images
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void PaintSelectedGeneLocusImages(string fileNameBase)
        {
            if (props.GenesToPaint.Length == 0) return;
            string imgDir = Directory.CreateDirectory(fileNameBase + "_gene_images").FullName;
            foreach (string gene in props.GenesToPaint)
            {
                if (!geneFeatures.ContainsKey(gene))
                    continue;
                GeneFeature gf = geneFeatures[gene];
                ushort[,] imgData = CompactGenePainter.GetLocusImageData(gf);
                string safeGene = PathHandler.MakeSafeFilename(gene);
                string gifFile = Path.Combine(imgDir, safeGene + ".gif");
                WriteGifImage(imgData, gifFile, new int[] { 0, 10, 100, 1000, 10000 },
                              new Color[] { Color.Wheat, Color.Yellow, Color.Orange, Color.Red, Color.Purple });
            }
        }

        private static void WriteGifImage(ushort[,] imgData, string gifFile, int[] levels, Color[] colors)
        {
            Sort.QuickSort(levels, colors);
            int rows = imgData.GetLength(0);
            int columns = imgData.GetLength(1);
            Bitmap bm = new Bitmap(rows, columns);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int v = (int)(imgData[r, c]);
                    Color color = Color.Black;
                    for (int i = levels.Length - 1; i >= 0; i--)
                    {
                        if (v > levels[i])
                        {
                            color = colors[i];
                            break;
                        }
                    }
                    bm.SetPixel(r, c, color);
                }
            }
            bm.Save(gifFile, ImageFormat.Gif);
            int p = (int)Environment.OSVersion.Platform;
            if ((p == 4) || (p == 6) || (p == 128))
            {
                CmdCaller.Run("chmod", "a+rw " + gifFile);
            }
        }

        /// <summary>
        /// Write transcript images
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void PaintSelectedGeneTranscriptImages(string fileNameBase)
        {
            if (props.GenesToPaint.Length == 0) return;
            string imgDir = Directory.CreateDirectory(fileNameBase + "_transcript_images").FullName;
            foreach (string gene in props.GenesToPaint)
            {
                if (!geneFeatures.ContainsKey(gene))
                    continue;
                int[] bcodeCounts = new int[barcodes.Count];
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    bcodeCounts[bcIdx] = geneFeatures[gene].TrHits(bcIdx);
                int[] bcodesOrderedByCount = new int[bcodeCounts.Length];
                for (int i = 0; i < bcodeCounts.Length; i++)
                    bcodesOrderedByCount[i] = i;
                Sort.QuickSort(bcodeCounts, bcodesOrderedByCount);
                GeneFeature gf = geneFeatures[gene];
                ushort[,] imgData = CompactGenePainter.GetTranscriptImageData(gf, bcodesOrderedByCount);
                string safeGene = PathHandler.MakeSafeFilename(gene);
                string gifFile = Path.Combine(imgDir, safeGene + "_mRNA.gif");
                WriteGifImage(imgData, gifFile, new int[] { 0, 3, 15 },
                              new Color[] { Color.Orange, Color.Yellow, Color.Wheat });
                string siteFile = Path.Combine(imgDir, safeGene + "_positions.tab");
                WriteSwitchSites(imgData, bcodesOrderedByCount, siteFile);
            }
        }

        private void WriteSwitchSites(ushort[,] imgData, int[] orderedBcIndexes, string txtFile)
        {
            int nPositions = imgData.GetLength(0);
            int nBarcodes = imgData.GetLength(1);
            using (StreamWriter file = new StreamWriter(txtFile))
            {
                for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                    file.Write("\t{0}", barcodes.GetWellId(orderedBcIndexes[bcRow]));
                file.WriteLine();
                file.Write("PosFrom5'End");
                for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                    file.Write("\t{0}", barcodes.Seqs[orderedBcIndexes[bcRow]]);
                file.WriteLine();
                for (int pos = 0; pos < nPositions; pos++)
                {
                    file.Write(pos);
                    for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                        file.Write("\t{0}", imgData[pos, bcRow]);
                    file.WriteLine();
                }
            }
        }

        private void WriteGCContentByTranscript(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_transcript_GC_content.tab"))
            {
                file.WriteLine("Gene\tTrLen\tTrFracGC\tReadCoveredLen\tReadCoveredDNAFracGC\tReadsFracGC");
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (gf.GetTranscriptHits() == 0 || !ChromosomeSequences.ContainsKey(gf.Chr)) continue;
                    DnaSequence chrSeq = ChromosomeSequences[gf.Chr];
                    int nTrGC = 0;
                    foreach (int p in  gf.IterExonPositionsInChrDir())
                    {
                        if ( chrSeq.GetNucleotide(p) == 'G' || chrSeq.GetNucleotide(p) == 'C')
                            nTrGC++;
                    }
                    double trFracGC = nTrGC / (double)gf.GetTranscriptLength();
                    double[] gc = CompactGenePainter.GetTranscriptFractionGC(gf, chrSeq, MappedTagItem.AverageReadLen);
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", safeName, gf.GetTranscriptLength(), trFracGC, gc[0], gc[1], gc[2]);
                }
            }
        }

        private void WriteSpikeProfilesByBc(string fileNameBase)
        {
            int minTotCount = barcodes.Count * 20;
            using (StreamWriter file = new StreamWriter(fileNameBase + "_spike_profiles.tab"))
            {
                string countType = (barcodes.HasUMIs) ? "molecule" : "read";
                file.WriteLine("Read {0} hits per barcode to abundant (>= {1} total counts) spike transcripts from 5' to 3' end.",
                               countType, minTotCount);
                file.Write("Spike\tTr5'Pos\tTrLen\tWell\tBarcode\t");
                for (int p = 1; p < 3000; p++)
                    file.Write("\tPos{0}", p);
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (!gf.IsSpike() || gf.GetTranscriptHits() < minTotCount) continue;
                    string safeName = ExcelRescueGeneName(gf.Name);
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    {
                        file.Write("{0}\t{1}\t{2}\t{3}\t{4}", 
                                safeName, gf.Start, gf.GetTranscriptLength(), barcodes.GetWellId(bcIdx), barcodes.Seqs[bcIdx]);
                        ushort[] trProfile = CompactGenePainter.GetTranscriptProfile(gf, bcIdx);
                        int i = trProfile.Length - 1;
                        while (i > 0 && trProfile[i] == 0) i--;
                        for (int p = 0; p <= i; p++)
                            file.Write("\t{0}", trProfile[p]);
                        file.WriteLine();
                    }
                }
            }
        }

        private void WriteTranscriptProfiles(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_transcript_profiles.tab"))
            {
                string countType = (barcodes.HasUMIs) ? "molecule" : "read";
                file.WriteLine("All hit {0} counts to expressed transcripts from 5' to 3' end. Counting stops at {1} in this table. Each data row truncated at last position > 0.",
                               countType, ushort.MaxValue);
                file.Write("Gene\tChr\tTrDir\tTr5'Pos\tTr3'Pos\tTrLen");
                for (int p = 1; p < 10000; p++)
                    file.Write("\tPos{0}", p);
                file.WriteLine();
                foreach (GeneFeature gf in IterOrderedGeneFeatures(true, true))
                {
                    if (gf.GetTranscriptHits() == 0) continue;
                    string safeName = ExcelRescueGeneName(gf.Name);
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                               safeName, gf.Chr, gf.Strand, gf.Start, gf.End, gf.GetTranscriptLength());
                    ushort[] trProfile = CompactGenePainter.GetTranscriptProfile(gf);
                    int i = trProfile.Length - 1;
                    while (i > 0 && trProfile[i] == 0) i--;
                    for (int p = 0; p <= i; p++)
                        file.Write("\t{0}", trProfile[p]);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write the distribution profiles of hits across genes and spikes from 5' to 3' end
        /// </summary>
        /// <param name="fileNameBase"></param>
        /// <param name="averageReadLen"></param>
        private void WriteElongationEfficiency(string fileNameBase, int averageReadLen)
        {
            int trLenBinSize = 500;
            int nSections = 20;
            using (StreamWriter capHitsFile = new StreamWriter(fileNameBase + "_5to3_profiles.tab"))
            {
                WriteSpikeElongationHitCurve(capHitsFile, trLenBinSize, nSections, averageReadLen);
                WriteElongationHitCurve(capHitsFile, trLenBinSize, nSections, averageReadLen);
                // The old style hit statistics profile below:
                int minHitsPerGene = 50;
                capHitsFile.WriteLine("\n\nFraction hits that are to the 5' {0} bases of genes with >= {1} hits, grouped by transcript length (BinSize={2})",
                                     props.CapRegionSize, minHitsPerGene, trLenBinSize);
                capHitsFile.WriteLine("\n\nSpike RNAs:");
                AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, props.CapRegionSize, trLenBinSize, true);
                capHitsFile.WriteLine("\nOther RNAs:");
                AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, props.CapRegionSize, trLenBinSize, false);
            }
        }

        /// <summary>
        /// Old style analysis comparing a 5' region to the rest.
        /// </summary>
        /// <param name="capHitsFile"></param>
        /// <param name="minHitsPerGene"></param>
        /// <param name="capRegionSize"></param>
        /// <param name="binSize"></param>
        /// <param name="analyzeSpikes"></param>
        private void AnalyzeWriteElongationSection(StreamWriter capHitsFile, int minHitsPerGene, int capRegionSize,
                                                   int binSize, bool analyzeSpikes)
        {
            DescriptiveStatistics allFracs = new DescriptiveStatistics();
            int nBins = 10000 / binSize;
            DescriptiveStatistics[] binnedEfficiencies = new DescriptiveStatistics[nBins];
            DescriptiveStatistics[,] bcBinnedEfficiencies = new DescriptiveStatistics[nBins, 96];
            int[] nGenes = new int[nBins];
            for (int di = 0; di < binnedEfficiencies.Length; di++)
            {
                binnedEfficiencies[di] = new DescriptiveStatistics();
                for (int bc = 0; bc < 96; bc++)
                    bcBinnedEfficiencies[di, bc] = new DescriptiveStatistics();
            }
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int trLen = gf.GetTranscriptLength();
                if (trLen < capRegionSize * 2)
                    continue;
                if (analyzeSpikes != gf.IsSpike())
                    continue;
                if (gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int bin = Math.Min(nBins - 1, trLen / binSize);
                int[] capCounts = CompactGenePainter.GetBarcodedTranscriptCounts(gf, 0, capRegionSize - 1);
                int[] allCounts = CompactGenePainter.GetBarcodedTranscriptCounts(gf, 0, trLen - 1);
                int capCountSum = 0;
                int allCountSum = 0;
                for (int bc = 0; bc < capCounts.Length; bc++)
                {
                    capCountSum += capCounts[bc];
                    allCountSum += allCounts[bc];
                    if (allCounts[bc] > 0)
                    {
                        double bcFrac = capCounts[bc] / (double)allCounts[bc];
                        bcBinnedEfficiencies[bin, bc].Add(bcFrac);
                    }
                }
                double frac = capCountSum / (double)allCountSum;
                binnedEfficiencies[bin].Add(frac);
                nGenes[bin]++;
                allFracs.Add(frac);
            }
            capHitsFile.WriteLine("Overall average fraction is {0}", allFracs.Mean());
            capHitsFile.Write("\nMidLength\tFraction\t#GenesInBin\t");
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t{0}", barcodes.Seqs[idx]);
            capHitsFile.WriteLine();
            capHitsFile.Write("\t\t\t");
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t{0}", barcodes.GetWellId(idx));
            capHitsFile.WriteLine();
            for (int di = 0; di < binnedEfficiencies.Length; di++)
            {
                int binMid = di * binSize + binSize / 2;
                capHitsFile.Write("{0}\t{1:0.####}\t{2}\t", binMid, binnedEfficiencies[di].Mean(), nGenes[di]);
                foreach (int idx in speciesBcIndexes)
                    capHitsFile.Write("\t{0:0.####}", bcBinnedEfficiencies[di, idx].Mean());
                capHitsFile.WriteLine();
            }
        }

        private void WriteElongationHitCurve(StreamWriter hitProfileFile, int trLenBinSize, int nSectionsOverTranscipt, int averageReadLen)
        {
            int nTrSizeBins = 10000 / trLenBinSize;
            int minHitsPerGene = nSectionsOverTranscipt * 10;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[nTrSizeBins, nSectionsOverTranscipt];
            for (int di = 0; di < nTrSizeBins; di++)
            {
                for (int section = 0; section < nSectionsOverTranscipt; section++)
                    binnedEfficiencies[di, section] = new DescriptiveStatistics();
            }
            int[] nGenesPerSizeClass = new int[nTrSizeBins];
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.IsSpike())
                    continue;
                if (gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                int trLenBin = Math.Min(nTrSizeBins - 1, trLen / trLenBinSize);
                double posBinSize = (trLen - averageReadLen) / (double)nSectionsOverTranscipt;
                int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, posBinSize, props.DirectionalReads, averageReadLen);
                for (int section = 0; section < Math.Min(nSectionsOverTranscipt, trBinCounts.Length); section++)
                    binnedEfficiencies[trLenBin, section].Add(trBinCounts[section] / (double)trBinCounts.Sum());
                nGenesPerSizeClass[trLenBin]++;
            }
            hitProfileFile.WriteLine("Hit distribution across gene transcripts, group averages by transcript length classes.");
            hitProfileFile.WriteLine("\nMidLength\tnGenes\t5' -> 3' hit distribution");
            for (int di = 0; di < nTrSizeBins; di++)
            {
                int binMid = averageReadLen + di * trLenBinSize + trLenBinSize / 2;
                hitProfileFile.Write("{0}\t{1}", binMid, nGenesPerSizeClass[di]);
                for (int section = 0; section < nSectionsOverTranscipt; section++)
                    hitProfileFile.Write("\t{0:0.####}", binnedEfficiencies[di, section].Mean());
                hitProfileFile.WriteLine();
            }
        }

        private void WriteSpikeElongationHitCurve(StreamWriter hitProfileFile, int trLenBinSize, int nSections, int averageReadLen)
        {
            int minHitsPerGene = nSections * 10;
            bool wroteHeader = false;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (!gf.IsSpike() || gf.GetTranscriptHits() < 25)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double binSize = (trLen - averageReadLen) / (double)nSections;
                int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, binSize, props.DirectionalReads, averageReadLen);
                if (trBinCounts.Length == 0) continue;
                double allCounts = 0.0;
                foreach (int c in trBinCounts) allCounts += c;
                if (!wroteHeader)
                {
                    hitProfileFile.WriteLine("Hit distribution across spike transcripts.");
                    hitProfileFile.WriteLine("\nSpike\tLength\t5' -> 3' hit distribution");
                    wroteHeader = true;
                }
                hitProfileFile.Write("{0}\t{1}", gf.Name, trLen);
                for (int section = 0; section < nSections; section++)
                {
                    double eff = trBinCounts[section] / allCounts;
                    hitProfileFile.Write("\t{0:0.####}", eff);
                }
                hitProfileFile.WriteLine();
            }
            hitProfileFile.WriteLine();
        }
        #endregion
    }

}
