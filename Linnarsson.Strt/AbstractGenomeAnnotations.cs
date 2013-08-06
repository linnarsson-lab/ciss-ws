using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public abstract class AbstractGenomeAnnotations
    {
        public static int annotationBinSize = 30000;

        /// <summary>
        /// The actual chromosome sequences - only those needed for stat calc.
        /// </summary>
        public Dictionary<string, DnaSequence> ChromosomeSequences { get; set; }
        public Dictionary<string, int> ChromosomeLengths { get; set; }
        /// <summary>
        /// Map of all chrIds to their sequence files.
        /// </summary>
        public Dictionary<string, string> ChrIdToFileMap;
        /// <summary>
        /// A dictionary of annotations indexed by chromosome Id
        /// </summary>
        protected Dictionary<string, QuickAnnotationMap> ExonAnnotations { get; set; }
        protected Dictionary<string, QuickAnnotationMap> NonExonAnnotations { get; set; }

        public IterTranscriptMatcher IterTranscriptMatches;

        public Dictionary<string, GeneFeature> geneFeatures;
        public Dictionary<string, RepeatFeature> repeatFeatures;

        protected StrtGenome genome;
        public StrtGenome Genome { get { return genome; } }
        protected Barcodes barcodes;
        public Barcodes Barcodes { get { return barcodes; } }
        protected Props props;
        protected bool needChromosomeSequences;
        protected bool needChromosomeLengths;
        public bool noGeneVariants;

        protected AbstractGenomeAnnotations(Props props, StrtGenome genome)
		{
            this.props = props;
            this.genome = genome;
            this.barcodes = props.Barcodes;
            needChromosomeSequences = props.DetermineMotifs || props.AnalyzeSeqUpstreamTSSite;
            needChromosomeLengths = props.GenerateWiggle || props.GenerateBarcodedWiggle || props.GeneFeature5PrimeExtension > 0;
            noGeneVariants = !genome.GeneVariants;
            GeneFeature.LocusFlankLength = props.LocusFlankLength;
            CompactGenePainter.SetMaxLocusLen(props.MaxFeatureLength);
            ChromosomeSequences = new Dictionary<string, DnaSequence>();
            ChromosomeLengths = new Dictionary<string, int>();
            ExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            NonExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            geneFeatures = new Dictionary<string, GeneFeature>(50000);
            repeatFeatures = new Dictionary<string, RepeatFeature>(1500);
            IterTranscriptMatches = new IterTranscriptMatchers(ExonAnnotations).GetMatcher();
        }

        public abstract void Load();

        public string[] GetChromosomeIds()
        {
            return ExonAnnotations.Keys.ToArray();
        }

        /// <summary>
        /// Return the total hit (read or molecule) count.
        /// </summary>
        /// <param name="annotType">Selected annotation type</param>
        /// <param name="excludeMasked">True to only count hits to regions without overlap with other genes</param>
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

        public int GetTotalAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.REPT || annotType == AnnotType.AREPT)
                return GetTotalRepeatLength();
            int totLen = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
                totLen += gf.GetAnnotLength(annotType, excludeMasked);
            return totLen;
        }
        public int GetTotalAnnotLength(int annotType)
        {
            return GetTotalAnnotLength(annotType, false);
        }

        public int GetTotalRepeatLength()
        {
            int totLen = 0;
            foreach (RepeatFeature rf in repeatFeatures.Values)
                totLen += rf.GetLocusLength();
            return totLen;
        }

        /// <summary>
        /// Write locus oriented statistics
        /// </summary>
        /// <param name="fileBaseName"></param>
        public virtual void SaveResult(string fileBaseName, int averageReadLen)
        { }

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
            if (AnnotType.DirectionalReads)
            {
                foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
                    if (ivl.Strand != strand) yield return ivl;
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
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) return true;
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
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) return true;
            return false;
        }

        public IEnumerable<FtInterval> IterExonAnnotations(string chr, char strand, int hitMidPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) yield return ivl;
            }
        }

        public List<FtInterval> GetExonAnnotations(string chr, char strand, int hitMidPos)
        {
            List<FtInterval> ftIvls = new List<FtInterval>();
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) ftIvls.Add(ivl);
            }
            return ftIvls;
        }

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

        public bool HasChromosome(string chr)
        {
            return ChromosomeSequences.ContainsKey(chr);
        }
        public DnaSequence GetChromosome(string chr)
        {
            return ChromosomeSequences[chr];
        }

        public int GetNumExpressedGenes()
        {
            return geneFeatures.Values.Count(g => g.IsExpressed());
        }
        public int GetNumExpressedGenes(int barcodeIdx)
        {
            return geneFeatures.Values.Count(g => g.IsExpressed(barcodeIdx));
        }
        public int GetNumExpressedMainGeneVariants()
        {
            return geneFeatures.Values.Count(g => (g.IsMainVariant() && g.IsExpressed()));
        }
        public int GetNumExpressedRepeats()
        {
            return repeatFeatures.Values.Count(r => r.GetTotalHits() > 0);
        }
        /// <summary>
        /// Find largest number of exons for any gene
        /// </summary>
        /// <returns></returns>
        public int MaxExonCount()
        {
            return geneFeatures.Values.Max(gf => gf.ExonCount);
        }

        public int[] SampleBarcodeExpressedGenes()
        {
            int[] nBarcodeExpressedGenes = new int[barcodes.Count];
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                for (int bCode = 0; bCode < nBarcodeExpressedGenes.Length; bCode++)
                    if (gf.TranscriptHitsByBarcode[bCode] > 0) nBarcodeExpressedGenes[bCode]++;
            }
            return nBarcodeExpressedGenes;
        }

        public IEnumerable<GeneFeature> IterTranscripts(bool selectSpikes)
        {
            foreach (GeneFeature gf in geneFeatures.Values)
                if (gf.IsSpike() == selectSpikes && gf.IsMainVariant())
                    yield return gf;
            yield break;
        }

        public double GetEfficiencyFromSpikes(int bcIdx)
        {
            int nMols = 0;
            foreach (GeneFeature gf in IterTranscripts(true))
                nMols += gf.TranscriptHitsByBarcode[bcIdx];
            return nMols / (double)Props.props.TotalNumberOfAddedSpikeMolecules;
        }

        /// <summary>
        /// Summarize the total count that stem from transcripts or spikes within each barcode.
        /// Gene main variants contribute their Max count and secondary variants their Min count.
        /// </summary>
        /// <param name="selectSpikes">false to count non-spikes, true for spikes</param>
        /// <returns>Array of per-barcode total counts</returns>
        public int[] GetTotalTranscriptCountsByBarcode(bool selectSpikes)
        {
            int[] UniqueGeneCountsByBarcode = new int[barcodes.Count];
            foreach (GeneFeature gf in geneFeatures.Values)
                if (gf.IsSpike() == selectSpikes)
                {
                    int[] countsByBc = (gf.IsMainVariant())? gf.TranscriptHitsByBarcode : gf.NonConflictingTranscriptHitsByBarcode;
                    for (int bcodeIdx = 0; bcodeIdx < UniqueGeneCountsByBarcode.Length; bcodeIdx++)
                        UniqueGeneCountsByBarcode[bcodeIdx] += countsByBc[bcodeIdx];
                }
            return UniqueGeneCountsByBarcode;
        }

        public static DnaSequence ReadChromosomeFile(string seqFile)
        {
            DnaSequence chrSeq = null;
            if (seqFile.IndexOf(".gbk") > 0)
            {
                GenbankFile records = GenbankFile.Load(seqFile);
                chrSeq = records.Records[0].Sequence;
            }
            else
            {
                FastaFile records = FastaFile.Load(seqFile);
                chrSeq = records.Records[0].Sequence;
            }
            return chrSeq;
        }

    }

}
