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
        protected int SpliceFlankLen { get; set; }
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
        protected Dictionary<string, QuickAnnotationMap> QuickAnnotations { get; set; }
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
            SpliceFlankLen = props.SpliceFlankLength;
            SplicedGeneFeature.SetSpliceFlankLen(SpliceFlankLen);
            GeneFeature.SpliceFlankLen = SpliceFlankLen;
            this.props = props;
            this.genome = genome;
            this.barcodes = props.Barcodes;
            needChromosomeSequences = props.DetermineMotifs;
            needChromosomeLengths = props.GenerateWiggle;
            noGeneVariants = !genome.GeneVariants;
            GeneFeature.LocusFlankLength = props.LocusFlankLength;
            GeneFeature.GenerateTranscriptProfiles = props.GenerateTranscriptProfiles;
            GeneFeature.GenerateLocusProfiles = props.GenerateGeneLocusProfiles;
            CompactGenePainter.SetMaxLocusLen(props.MaxFeatureLength);
            ChromosomeSequences = new Dictionary<string, DnaSequence>();
            ChromosomeLengths = new Dictionary<string, int>();
            QuickAnnotations = new Dictionary<string, QuickAnnotationMap>();
            geneFeatures = new Dictionary<string, GeneFeature>(60000);
            repeatFeatures = new Dictionary<string, RepeatFeature>(1500);
		}

        public abstract void Load();

        public abstract string[] GetChromosomeNames();
        public abstract int GetTotalAnnotLength(int annotType);
        public abstract int GetTotalAnnotLength(int annotType, bool excludeMasked);
        public abstract int GetTotalAnnotCounts(int annotType, bool excludeMasked);
        public abstract void WriteSpikeDetection(StreamWriter file, StreamWriter xmlFile);

        public virtual List<string> GetSummaryLines()
        {
            return summaryLines;
        }

        /// <summary>
        /// Write locus oriented statistics
        /// </summary>
        /// <param name="fileBaseName"></param>
        public virtual void WriteStats(string fileBaseName)
        { }

        public IEnumerable<FtInterval> GetMatching(string chr, int hitPos)
        {
            try
            {
                return QuickAnnotations[chr].GetItems(hitPos);
            }
            catch (KeyNotFoundException)
            {
                throw new ChromosomeMissingException("Got matches to missing chromosome " + chr);
            }
        }

        protected void AddIntervals(LocusFeature ft)
        {
            foreach (FtInterval ivl in ft.IterIntervals())
            {
                try
                {
                    AddInterval(ft.Chr, ivl.Start, ivl.End, ivl.Mark, ivl.ExtraData);
                }
                catch (KeyNotFoundException)
                {
                    QuickAnnotations[ft.Chr] = new QuickAnnotationMap(annotationBinSize);
                    Console.Error.WriteLine("The sequence file for chr " + ft.Chr + " seems to be missing.");
                    AddInterval(ft.Chr, ivl.Start, ivl.End, ivl.Mark, ivl.ExtraData);
                }
            }
        }
        protected void AddInterval(string chr, int start, int end, DoMarkHit marker, int extraData)
        {
            QuickAnnotations[chr].Add(new FtInterval(start, end, marker, extraData));
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
