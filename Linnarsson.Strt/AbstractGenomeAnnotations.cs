﻿using System;
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
        protected List<string> summaryLines;

        protected AbstractGenomeAnnotations(Props props, StrtGenome genome)
		{
            summaryLines = new List<string>();
            this.props = props;
            this.genome = genome;
            this.barcodes = props.Barcodes;
            needChromosomeSequences = props.DetermineMotifs;
            needChromosomeLengths = props.GenerateWiggle;
            noGeneVariants = !genome.GeneVariants;
            GeneFeature.LocusFlankLength = props.LocusFlankLength;
            CompactGenePainter.SetMaxLocusLen(props.MaxFeatureLength);
            ChromosomeSequences = new Dictionary<string, DnaSequence>();
            ChromosomeLengths = new Dictionary<string, int>();
            ExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            NonExonAnnotations = new Dictionary<string, QuickAnnotationMap>();
            geneFeatures = new Dictionary<string, GeneFeature>(60000);
            repeatFeatures = new Dictionary<string, RepeatFeature>(1500);
		}

        public abstract void Load();

        public abstract string[] GetChromosomeIds();
        public abstract int GetTotalAnnotLength(int annotType);
        public abstract int GetTotalAnnotLength(int annotType, bool excludeMasked);
        public abstract int GetTotalAnnotCounts(int annotType, bool excludeMasked);
        public abstract void WriteSpikeDetection(StreamWriter xmlFile);

        public virtual List<string> GetSummaryLines()
        {
            return summaryLines;
        }

        /// <summary>
        /// Write locus oriented statistics
        /// </summary>
        /// <param name="fileBaseName"></param>
        public virtual void SaveResult(string fileBaseName, int averageReadLen)
        { }

        /// <summary>
        /// Finds the matching annotated intervals that correspond to (forward strand for directional reads) transcripts.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="strand">Strand of hit (for directional reads)</param>
        /// <param name="hitPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> GetTranscriptMatches(string chr, char strand, int hitPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].GetItems(hitPos))
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) yield return ivl; 
        }

        /// <summary>
        /// Finds the matching annotated intervals that do NOT correspond to (forward strand for directional reads) transcripts,
        /// like USTR, INTR, DSTR and REPT.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="strand">Strand of hit (for directional reads)</param>
        /// <param name="hitPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> GetNonTrMatches(string chr, char strand, int hitPos)
        {
            if (AnnotType.DirectionalReads)
                foreach (FtInterval ivl in ExonAnnotations[chr].GetItems(hitPos))
                    if (ivl.Strand != strand) yield return ivl;
            foreach (FtInterval ivl in NonExonAnnotations[chr].GetItems(hitPos))
                yield return ivl;
        }

        /// <summary>
        /// Checks if there is a matching interval annotated as transcript or repeat
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="hitPos"></param>
        /// <returns>True if any matching interval defines an (forward) exon or repeat</returns>
        public bool HasTrOrRepeatMatches(string chr, char strand, int hitPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].GetItems(hitPos))
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) return true;
            foreach (FtInterval ivl in NonExonAnnotations[chr].GetItems(hitPos))
                if (ivl.AnnotType == AnnotType.REPT) return true;
            return false;
        }

        /// <summary>
        /// Chreck if a matching interval is an exon (forward for directional reads)
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="hitPos"></param>
        /// <returns>True if an annotation says exon (sense)</returns>
        public bool IsTranscript(string chr, char strand, int hitPos)
        {
            foreach (FtInterval ivl in ExonAnnotations[chr].GetItems(hitPos))
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) return true;
            return false;
        }

        protected void AddGeneIntervals(LocusFeature ft)
        {
            foreach (FtInterval ivl in ft.IterIntervals())
            {
                if (AnnotType.IsTranscript(ivl.AnnotType))
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

        public static DnaSequence readChromosomeFile(string seqFile)
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
