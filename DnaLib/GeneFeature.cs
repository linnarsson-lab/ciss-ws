﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;

namespace Linnarsson.Dna
{
    public interface TranscriptFeature
    {
        int GetTranscriptPos(int hitMidPos, int partIdxForSplices);
    }

    public class GeneFeature : LocusFeature, TranscriptFeature
    {
        public readonly static string pseudoGeneIndicator = "_p";
        public readonly static string altLocusIndicator = "_loc";
        public readonly static char metadataSubDelim = ',';
        public readonly static char metadataDelim = ';';
        public readonly static string nonUTRExtendedIndicator = variantIndicator + "Original";
        public static int LocusFlankLength = Props.props.LocusFlankLength;

        /// <summary>
        /// Counts number of reads that are shared with each of other GeneFeatures
        /// </summary>
        public Dictionary<IFeature, int> sharingGenes;

        public int SpliceLen; // Set by the corresponding SplicedGeneLocus
        private int locusHitIdx;
        private bool locusHitsSorted;
        private int[] m_LocusHits;
        /// <summary>
        /// Hits (molecules when using UMIs) are stored as ints of pp..pppbbbbbbbs where
        /// p is hitMidPosition relative to LocusStart
        /// in chromosome orientation, b is barcode, and s is chromosome strand (0 = '+', 1 = '-')
        /// Always returned sorted when accessed.
        /// </summary>
        public int[] LocusHits
        {
            get
            {
                if (!locusHitsSorted)
                {
                    Array.Resize(ref m_LocusHits, locusHitIdx);
                    Array.Sort(m_LocusHits);
                    locusHitsSorted = true;
                }
                return m_LocusHits;
            }
        }

        public int NumLocusHitEntries { get { return locusHitIdx; } }

        /// <summary>
        /// SNPCounter arrays by bcIdx for every SNP positions
        /// </summary>
        public SortedDictionary<int, SNPCountsByBarcode> bcSNPCountsByRealChrPos;

        public override int GetLocusLength()
        {
            return End - Start + 1 + 2 * LocusFlankLength;
        }

        /// <summary>
        /// Start position on chromosome of the gene locus including 5' flank sequence. Not adjusted for neighboring overlapping genes.
        /// </summary>
        public int LocusStart { get { return Start - LocusFlankLength; } }
        /// <summary>
        /// End position on chromosome of the gene locus including 3' flank sequence. Not adjusted for neighboring overlapping genes.
        /// </summary>
        public int LocusEnd { get { return End + LocusFlankLength; } }

        /// <summary>
        /// 0-based chromosomal position of the CAP site
        /// </summary>
        public int SavedCAPPos { get; private set; }

        /// <summary>
        /// 0-based start position on chromosome of the leftmost exon. May be adjusted by 5' end extension defined by Props for '+' strand genes.
        /// </summary>
        public override int Start
        {
            get
            {
                return base.Start;
            }
            set
            {
                base.Start = value;
                ExonStarts[0] = value;
            }
        }
        /// <summary>
        /// 0-based inclusive end position on chromosome of the rightmost exon. 
        /// May be adjusted by 5' end extension defined by Props for '-' strand genes.
        /// </summary>
        public override int End
        {
            get
            {
                return base.End;
            }
            set
            {
                base.End = value;
                ExonEnds[ExonEnds.Length - 1] = value;
            }
        }

        /// <summary>
        /// The extension that was made of the 5' end of the 1st exon
        /// </summary>
        public int Extension5Prime { get { return (Strand == '+') ? SavedCAPPos - Start : End - SavedCAPPos; } }

        /// <summary>
        /// In some genes, there are extremely short splices of one or a few bases. These may be true in some case,
        /// but some seem erratic, and importantly, they mess up processing. They are simply joined together.
        /// </summary>
        /// <param name="minEvents"></param>
        public void JoinSpuriousSplices(int minEvents)
        {
            List<int> newStarts = new List<int>(ExonStarts.Length);
            List<int> newEnds = new List<int>(ExonEnds.Length);
            int startIdx = 0;
            while (startIdx < ExonStarts.Length)
            {
                int endIdx = startIdx;
                while (endIdx < ExonEnds.Length - 1 && ExonEnds[endIdx] + 10 >= ExonStarts[endIdx + 1])
                    endIdx += 1;
                newStarts.Add(ExonStarts[startIdx]);
                newEnds.Add(ExonEnds[endIdx]);
                startIdx = endIdx + 1;
            }
            if (newStarts.Count + minEvents <= ExonStarts.Length)
            {
                Console.WriteLine("JoinSpuriousSplices: Before:\n{0}", ToString());
                ExonStarts = newStarts.ToArray();
                ExonEnds = newEnds.ToArray();
                Console.WriteLine("                     After:\n{0}", ToString());
            }
        }

        /// <summary>
        /// Always total reads per barcode
        /// </summary>
        private int[] TrReadsByBc;
        /// <summary>
        /// Unique reads per barcode
        /// </summary>
        public int[] NonConflictingTrReadsByBc;

        public int TrReads(int bcIdx)
        {
            return TrReadsByBc[bcIdx];
        }
        public int TrReadSum()
        {
            return TrReadsByBc.Sum();
        }

        /// <summary>
        /// Molecules per barcode after UMI mutation filtering
        /// </summary>
        private ushort[] TrMolsByBc;
        private ushort[] NonConflictingTrMolsByBc;
        private ushort[] EstimatedTrueMolsByBc;
        private ushort[] MaxOccupiedUMIsOnEXONByBc;

        /// <summary>
        /// Will return molecules if UMIs are in use, else reads
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <returns></returns>
        public int TrHits(int bcIdx)
        {
            return (TrMolsByBc != null) ? TrMolsByBc[bcIdx] : TrReadsByBc[bcIdx];
        }
        public int TrHitSum()
        {
            return (TrMolsByBc != null) ? TrMolsByBc.Sum(v => (int)v) : TrReadsByBc.Sum();
        }
        /// <summary>
        /// Return molecule count if useMols == true and UMIs are use, else read count
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <param name="useMols"></param>
        /// <returns></returns>
        public int TrHits(int bcIdx, bool useMols)
        {
            return (useMols && TrMolsByBc != null) ? TrMolsByBc[bcIdx] : TrReadsByBc[bcIdx];
        }

        /// <summary>
        /// Will return molecules if UMIs are in use, else reads
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <returns></returns>
        public int TrNCHits(int bcIdx)
        {
            return (TrMolsByBc != null) ? NonConflictingTrMolsByBc[bcIdx] : NonConflictingTrReadsByBc[bcIdx];
        }
        public int TrNCHitSum()
        {
            return (TrMolsByBc != null) ? NonConflictingTrMolsByBc.Sum(v => (int)v) : NonConflictingTrReadsByBc.Sum();
        }

        public static IEnumerable<int> IterTrMaxHits(GeneFeature gf, int[] bcIndexes)
        {
            foreach (int bcIdx in bcIndexes)
                yield return (gf.TrMolsByBc != null) ? gf.TrMolsByBc[bcIdx] : gf.TrReadsByBc[bcIdx];
        }
        public static IEnumerable<int> IterTrNCHits(GeneFeature gf, int[] bcIndexes)
        {
            foreach (int bcIdx in bcIndexes)
                yield return (gf.NonConflictingTrMolsByBc != null) ? gf.NonConflictingTrMolsByBc[bcIdx] : gf.NonConflictingTrReadsByBc[bcIdx];
        }
        public static IEnumerable<int> IterTrReads(GeneFeature gf, int[] bcIndexes)
        {
            foreach (int bcIdx in bcIndexes)
                yield return gf.TrReadsByBc[bcIdx];
        }
        public static IEnumerable<int> IterTrEstTrueMolCounts(GeneFeature gf, int[] bcIndexes)
        {
            foreach (int bcIdx in bcIndexes)
                yield return gf.EstimatedTrueMolsByBc[bcIdx];
        }
        public static IEnumerable<int> IterMaxOccupiedUMIsByEXON(GeneFeature gf, int[] bcIndexes)
        {
            foreach (int bcIdx in bcIndexes)
                yield return gf.MaxOccupiedUMIsOnEXONByBc[bcIdx];
        }

        /// <summary>
        /// Either molecules per barcode after UMI mutation filtering, or total reads per barcode when no UMIs are used.
        /// Only contains unquely mapping hits.
        /// </summary>
        public int[] CAPRegionHitsByBc;

        /// <summary>
        /// Histograms of number of reads per molecule (accu across barcodes+UMIs) at selected chromosomal positions within gene.
        /// </summary>
        public Dictionary<int, ushort[]> readsPerMoleculeData;

        public List<double> VariationSamples;
        /// <summary>
        /// Used to analyse exon hit distribution
        /// </summary>
        public int[] TranscriptHitsByExonIdx;
        /// <summary>
        /// Used to analyse cross-junction hit distribution. If !Props.props.AnalyzeSpliceHitsByBarcode, all data is collected in [0]
        /// </summary>
        public Dictionary<string, ushort[]> TranscriptHitsByJunctionAndBc;
        /// <summary>
        /// Total hits for every annotation type. Note that EXON/AEXON counts will include SPLC/ASPLC counts
        /// </summary>
        public int[] HitsByAnnotType;
        public int[] NonMaskedHitsByAnnotType;
        public CompactGenePainter cPainter;

        /// <summary>
        ///  Adjusted to not cover any nearby gene in same orientation
        /// </summary>
        public int LeftFlankLength;
        /// <summary>
        ///  Adjusted to not cover any nearby gene in same orientation
        /// </summary>
        public int RightFlankLength;
        public int LeftMatchStart { get { return Start - LeftFlankLength; } }
        public int RightMatchEnd { get { return End + RightFlankLength; } }
        public int USTRLength { get { return (Strand == '+') ? LeftFlankLength : RightFlankLength; } }
        public int DSTRLength { get { return (Strand == '+') ? RightFlankLength : LeftFlankLength; } }

        public int ExonCount { get { return ExonStarts.Length; } }
        /// <summary>
        /// 0-based start positions in chromosome
        /// </summary>
        public int[] ExonStarts;
        /// <summary>
        /// 0-based inclusive last positions in chromosome
        /// </summary>
        public int[] ExonEnds;

        /// <summary>
        /// True for exons that have an exon of some other gene on the opposite strand
        /// </summary>
        public bool[] MaskedAEXON;
        /// <summary>
        /// True for introns that overlap with an exon of some other gene on either strand
        /// </summary>
        public bool[] MaskedINTR;
        /// <summary>
        /// True for flanks that have an exon of some other gene on the opposite strand
        /// (such marked flanks should be skipped in the global statistics for S/AS ratio)
        /// </summary>
        public bool MaskedUSTR;
        public bool MaskedDSTR;

        /// <summary>
        /// C1 database ID of transcript
        /// </summary>
        public int TranscriptID;
        /// <summary>
        /// C1 database expression BLOB ID
        /// </summary>
        public int ExprBlobIdx;

        /// <summary>
        /// Type of gene, e.g. "mRNA", "microRNA,gene", "pseudogene"
        /// </summary>
        public string GeneType;
        /// <summary>
        /// TranscriptIds and CAP close cut sites: id1 [/id2...] ; pos1 [,pos2...]
        /// </summary>
        public string GeneMetadata;

        public GeneFeature(string name, string chr, char strand, int[] exonStarts, int[] exonEnds, string geneType, string geneMetadata,
                           int transcriptID, int exprBlobIdx)
            : this(name, chr, strand, exonStarts, exonEnds, geneType, geneMetadata)
        {
            this.TranscriptID = transcriptID;
            this.ExprBlobIdx = exprBlobIdx;
        }

        public GeneFeature(string name, string chr, char strand, int[] exonStarts, int[] exonEnds, string geneType, string geneMetadata)
            : base(name, chr, strand, exonStarts[0], exonEnds[exonEnds.Length - 1])
        {
            ExonStarts = exonStarts;
            ExonEnds = exonEnds;
            GeneType = geneType;
            GeneMetadata = geneMetadata;
            LeftFlankLength = RightFlankLength = LocusFlankLength; // Init with default length
            MaskedAEXON = new bool[exonEnds.Length];
            MaskedINTR = new bool[exonEnds.Length - 1];
            if (Props.props.Barcodes.HasUMIs)
            {
                TrMolsByBc = new ushort[Props.props.Barcodes.Count];
                EstimatedTrueMolsByBc = new ushort[Props.props.Barcodes.Count];
                NonConflictingTrMolsByBc = new ushort[Props.props.Barcodes.Count];
                MaxOccupiedUMIsOnEXONByBc = new ushort[Props.props.Barcodes.Count];
            }
            TrReadsByBc = new int[Props.props.Barcodes.Count];
            NonConflictingTrReadsByBc = new int[Props.props.Barcodes.Count];
            if (Props.props.WriteCAPRegionHits)
                CAPRegionHitsByBc = new int[Props.props.Barcodes.Count];
            VariationSamples = new List<double>();
            TranscriptHitsByExonIdx = new int[exonStarts.Length];
            TranscriptHitsByJunctionAndBc = new Dictionary<string, ushort[]>();
            HitsByAnnotType = new int[AnnotType.Count];
            NonMaskedHitsByAnnotType = new int[AnnotType.Count];
            m_LocusHits = new int[400];
            locusHitIdx = 0;
            SavedCAPPos = (strand == '+') ? exonStarts[0] : exonEnds[exonEnds.Length - 1];
        }

        public bool IsPseudogeneType()
        {
            return GeneType.Contains("pseudogene");
        }

        public int GetExonLength(int i)
        {
            return ExonEnds[i] - ExonStarts[i] + 1;
        }

        /// <summary>
        /// Return the biological 1-based exon number from an 0-based exon index in chromosome direction
        /// </summary>
        /// <param name="exonIdxOnChr"></param>
        /// <returns></returns>
        public int GetRealExonId(int exonIdxOnChr)
        {
            if (Strand == '+') return exonIdxOnChr + 1;
            return ExonCount - exonIdxOnChr;
        }

        public int GetAnnotCounts(int annotType, bool excludeMasked)
        {
            if (excludeMasked)
                return NonMaskedHitsByAnnotType[annotType];
            else
                return HitsByAnnotType[annotType];
        }

        /// <summary>
        /// # of hits to SPLC (+ASPLC for non-directional data)
        /// </summary>
        /// <returns></returns>
        public int GetJunctionHits()
        {
            int senseHits = HitsByAnnotType[AnnotType.SPLC];
            return (Props.props.DirectionalReads) ? senseHits : senseHits + HitsByAnnotType[AnnotType.ASPLC];
        }
        /// <summary>
        /// # of hits to EXON (+AEXON for non-directional data)
        /// </summary>
        /// <returns></returns>
        public int GetTranscriptHits()
        {
            int senseHits = HitsByAnnotType[AnnotType.EXON];
            return (Props.props.DirectionalReads) ? senseHits : senseHits + HitsByAnnotType[AnnotType.AEXON];
        }
        public override bool IsExpressed()
        {
            return GetTranscriptHits() > 0;
        }
        public bool IsExpressed(int bcIdx)
        {
            return (TrMolsByBc != null) ? TrMolsByBc[bcIdx] > 0 : TrReadsByBc[bcIdx] > 0;
        }
        /// <summary>
        /// # of hits to INTR, USTR and DSTR. (+Anti-versions for non-directional data)
        /// </summary>
        /// <returns></returns>
        public int GetIntronHits()
        {
            int hits = HitsByAnnotType[AnnotType.INTR] + HitsByAnnotType[AnnotType.USTR] + HitsByAnnotType[AnnotType.DSTR];
            if (!Props.props.DirectionalReads)
                hits += HitsByAnnotType[AnnotType.AINTR] + HitsByAnnotType[AnnotType.AUSTR] + HitsByAnnotType[AnnotType.ADSTR];
            return hits;
        }

        public bool IsSpike()
        {
            return Chr == Props.props.ChrCTRLId;
        }

        public int GetAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.EXON || (!Props.props.DirectionalReads && annotType == AnnotType.AEXON))
                return GetTranscriptLength();
            if (annotType == AnnotType.AEXON) return GetTranscriptLength(excludeMasked);
            if (annotType == AnnotType.INTR || annotType == AnnotType.AINTR)
                 return GetIntronicLength(excludeMasked);
            if (annotType == AnnotType.USTR || annotType == AnnotType.AUSTR)
                return (MaskedUSTR && excludeMasked)? 0 : (Strand == '+')? LeftFlankLength : RightFlankLength;
            if (annotType == AnnotType.DSTR || annotType == AnnotType.ADSTR)
                return (MaskedDSTR && excludeMasked) ? 0 : (Strand == '+')? RightFlankLength : LeftFlankLength;
            if (annotType == AnnotType.SPLC || annotType == AnnotType.ASPLC)
                return SpliceLen;
            return 0;
        }

        public int GetTranscriptLength()
        {
            int len = 0;
            for (int i = 0; i < ExonStarts.Length; i++)
                len += ExonEnds[i] - ExonStarts[i] + 1;
            return len;
        }
        public int GetNonMaskedTranscriptLength()
        {
            return GetTranscriptLength(true);
        }
        public int GetTranscriptLength(bool nonMasked)
        {
            int len = 0;
            for (int i = 0; i < ExonStarts.Length; i++)
                if (!(nonMasked && MaskedAEXON[i])) len += ExonEnds[i] - ExonStarts[i] + 1;
            return len;
        }

        public int GetIntronicLength()
        {
            int len = 0;
            for (int i = 0; i < ExonStarts.Length - 1; i++)
                len += ExonStarts[i + 1] - ExonEnds[i] - 1;
            return len;
        }
        public int GetIntronicLength(bool nonMasked)
        {
            int len = 0;
            for (int i = 0; i < ExonStarts.Length - 1; i++)
                if (!(nonMasked && MaskedINTR[i])) len += ExonStarts[i + 1] - ExonEnds[i] - 1;
            return len;
        }

        /// <summary>
        /// Convert from 0-based position within transcript in sense direction to chromosome position
        /// </summary>
        /// <param name="transcriptPos">0-based position within transcript</param>
        /// <returns>-1 if transcriptPos is larger than transcript length</returns>
        public int GetChrPos(int transcriptPos)
        {
            if (Strand == '+')
            {
                return GetChrPosFromTrPosInChrDir(transcriptPos);
            }
            for (int i = ExonCount - 1; i >= 0 ; i--)
            {
                int len = ExonEnds[i] - ExonStarts[i] + 1;
                if (transcriptPos < len) return ExonEnds[i] - transcriptPos;
                transcriptPos -= len;
            }
            return -1; // Given pos is outside transcript
        }

        /// <summary>
        /// Convert from 0-based transcript position in chr direction to chromosome position
        /// </summary>
        /// <param name="posInTrInChrDir">Transcript pos, counting 0 as the leftmost nt on chr, even for '-' oriented genes</param>
        /// <returns></returns>
        public int GetChrPosFromTrPosInChrDir(int posInTrInChrDir)
        {
            for (int i = 0; i < ExonCount; i++)
            {
                int len = ExonEnds[i] - ExonStarts[i] + 1;
                if (posInTrInChrDir < len) return ExonStarts[i] + posInTrInChrDir;
                posInTrInChrDir -= len;
            }
            return -1;
        }

        /// <summary>
        /// Generates all exonic positions on chromosome starting at the lowest value, even for '-'-strand transcripts.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> IterExonPositionsInChrDir()
        {
            for (int i = 0; i < ExonCount; i++)
            {
                for (int chrPos = ExonStarts[i]; chrPos <= ExonEnds[i]; chrPos++)
                    yield return chrPos;
            }
        }

        public int GetTranscriptPos(int chrPos, int junkExtraData)
        {
            return GetTranscriptPos(chrPos);
        }
        /// <summary>
        /// Convert from position on chromosome to position within transcript relative 5' end
        /// (all positions zero-based)
        /// </summary>
        /// <param name="chrPos"></param>
        /// <returns>-1 if chrPos is not within any of the exons</returns>
        public int GetTranscriptPos(int chrPos)
        {
            int posFrom5Prime = 0;
            if (Strand == '+')
            {
                for (int i = 0; i < ExonStarts.Length; i++)
                {
                    if (ExonStarts[i] > chrPos)
                        return -1;
                    if (ExonEnds[i] >= chrPos)
                        return chrPos - ExonStarts[i] + posFrom5Prime;
                    posFrom5Prime += ExonEnds[i] - ExonStarts[i] + 1;
                }
            }
            else
            {
                for (int i = ExonStarts.Length - 1; i >= 0; i--)
                {
                    if (ExonEnds[i] < chrPos)
                        return -1;
                    if (ExonStarts[i] <= chrPos)
                        return ExonEnds[i] - chrPos + posFrom5Prime;
                    posFrom5Prime += ExonEnds[i] - ExonStarts[i] + 1;
                }
            }
            return -1; // Given pos is not inside transcript exons
        }

        /// <summary>
        /// Checks if the exons and strand are the same as another GeneFeature
        /// </summary>
        /// <param name="otherGf"></param>
        /// <param name="internalMargin">Allow for some bases wobble at exon margins</param>
        /// <param name="endMargin">Max allowed base wobble at 5' and 3' end</param>
        /// <returns>true if they represent the same transcript</returns>
        public bool IsSameTranscript(GeneFeature otherGf, int internalMargin, int endMargin)
        {
            if (Strand != otherGf.Strand || ExonCount != otherGf.ExonCount) return false;
            for (int i = 0; i < ExonCount; i++)
            {
                int leftMargin = (i == 0) ? endMargin : internalMargin;
                int rightMargin = (i == ExonCount - 1) ? endMargin : internalMargin;
                if (Math.Abs(ExonStarts[i] - otherGf.ExonStarts[i]) > leftMargin
                    || Math.Abs(ExonEnds[i] - otherGf.ExonEnds[i]) > rightMargin)
                    return false;
            }
            return true;
        }

        public bool Contains(int start, int end)
        {
            return (start >= Start && end <= End);
        }
        public bool Overlaps(int start, int end, int minIntrusion)
        {
            return (start <= End - minIntrusion && end >= Start + minIntrusion);
        }

        /// <summary>
        /// Checks if any of the exons overlaps the start-end interval by at least minIntrusion
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="minIntrusion"></param>
        /// <returns></returns>
        public bool ExonsWithin(int start, int end, int minIntrusion)
        {
            for (int i = 0; i < ExonCount; i++)
                if (start <= ExonEnds[i] - minIntrusion && end >= ExonStarts[i] + minIntrusion)
                    return true;
            return false;
        }

        /// <summary>
        /// Sums up total expression from specified exons
        /// </summary>
        /// <param name="selectedExonIdxs"></param>
        /// <returns></returns>
        public int GetExpressionFromExons(List<int> selectedExonIdxs)
        {
            int count = 0;
            foreach (int exonIdx in selectedExonIdxs)
                count += TranscriptHitsByExonIdx[exonIdx];
            return count;
        }

        private int MarkUpstreamFlankHit(MappedTagItem item, int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.USTR, item, markType);
        }
        private int MarkDownstreamFlankHit(MappedTagItem item, int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.DSTR, item, markType);
        }
        private int MarkFlankHit(int annotType, MappedTagItem item, MarkStatus markType)
        {
            int undirAnnotType = annotType;
            if (item.DetectedStrand != Strand) annotType = AnnotType.MakeAntisense(annotType);
            MarkLocusHitPos(item);
            AddToTotalHits(item);
            HitsByAnnotType[annotType] += item.MolCount;
            if ((undirAnnotType == AnnotType.USTR && !MaskedUSTR) ||
                (undirAnnotType == AnnotType.DSTR && !MaskedDSTR))
                NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            return annotType;
        }

        private int MarkIntronHit(MappedTagItem item, int intronIdx, MarkStatus markType)
        {
            int annotType = (item.DetectedStrand == Strand) ? AnnotType.INTR : AnnotType.AINTR;
            MarkLocusHitPos(item);
            AddToTotalHits(item);
            HitsByAnnotType[annotType] += item.MolCount;
            if (!MaskedINTR[intronIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            return annotType;
        }

        public int MarkExonHit(MappedTagItem item, int exonIdx, MarkStatus markType)
        {
            if (Name == "Crx")
                Console.WriteLine("Crx at " + item.ToString());
            MarkSNPs(item);
            int annotType = (item.DetectedStrand == Strand) ? AnnotType.EXON : AnnotType.AEXON;
            if (markType == MarkStatus.NONEXONIC_MAPPING)
            { // Only happens for directional AEXON reads
                MarkLocusHitPos(item);
                AddToTotalHits(item);
                HitsByAnnotType[annotType] += item.MolCount;
                if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
                return annotType; // new MarkResult(annotType, this);
            } 
            // Now hit is to transcript and should be marked as EXON, or AEXON for antisense undirectional reads
            MarkLocusHitPos(item);
            AddToTotalHits(item);
            TranscriptHitsByExonIdx[exonIdx] += item.MolCount;
            if (Props.props.Barcodes.HasUMIs)
            {
                TrMolsByBc[item.bcIdx] += (ushort)item.MolCount;
                EstimatedTrueMolsByBc[item.bcIdx] += (ushort)item.EstTrueMolCount;
                MaxOccupiedUMIsOnEXONByBc[item.bcIdx] = Math.Max((ushort)item.ObservedMolCount, MaxOccupiedUMIsOnEXONByBc[item.bcIdx]);
            }
            TrReadsByBc[item.bcIdx] += item.ReadCount;
            if (markType == MarkStatus.UNIQUE_EXON_MAPPING)
            {
                if (NonConflictingTrMolsByBc != null) NonConflictingTrMolsByBc[item.bcIdx] += (ushort)item.MolCount;
                NonConflictingTrReadsByBc[item.bcIdx] += item.ReadCount;
                if (CAPRegionHitsByBc != null && Math.Abs(item.HitMidPos - SavedCAPPos) < Props.props.CapRegionSize)
                    CAPRegionHitsByBc[item.bcIdx] += item.MolCount;
            }
            HitsByAnnotType[annotType] += item.MolCount;
            NonMaskedHitsByAnnotType[annotType] += item.MolCount; // Count all EXON/SPLC hits for counter-oriented genes in statistics
            if (item.tagItem.SharingGenes != null)
                AddSharingGenes(item);
            return annotType;
        }

        /// <summary>
        /// Register the counts that are shared for this TagItem with other features
        /// </summary>
        /// <param name="item"></param>
        private void AddSharingGenes(MappedTagItem item)
        {
            if (sharingGenes == null)
                sharingGenes = new Dictionary<IFeature, int>();
            foreach (KeyValuePair<IFeature, int> pair in item.tagItem.SharingGenes)
                if (pair.Key != this)
                {
                    if (sharingGenes.ContainsKey(pair.Key))
                        sharingGenes[pair.Key] += pair.Value;
                    else
                        sharingGenes[pair.Key] = pair.Value;
                }
        }

        public int MarkSpliceHit(MappedTagItem item, int exonId, string junctionId, MarkStatus markType)
        {
            int exonIdx = (Strand == '+') ? exonId - 1 : ExonCount - exonId;
            int annotType = (item.DetectedStrand == Strand) ? AnnotType.SPLC : AnnotType.ASPLC;
            if (markType == MarkStatus.NONEXONIC_MAPPING)
            { // Now we have a directional ASPLC hit
                if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            }
            else
            {
                MarkJunctionHit(junctionId, item);
                NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            }
            HitsByAnnotType[annotType] += item.MolCount;
            int res = MarkExonHit(item, exonIdx, markType);
            return annotType;
        }

        private void MarkJunctionHit(string junctionId, MappedTagItem item)
        {
            if (!TranscriptHitsByJunctionAndBc.ContainsKey(junctionId))
            {
                int n = Props.props.AnalyzeSpliceHitsByBarcode ? Props.props.Barcodes.Count : 1;
                TranscriptHitsByJunctionAndBc[junctionId] = new ushort[n];
            }
            ushort c = Math.Min(ushort.MaxValue, (ushort)item.MolCount);
            int idx = Props.props.AnalyzeSpliceHitsByBarcode ? item.bcIdx : 0;
            TranscriptHitsByJunctionAndBc[junctionId][idx] += c;
        }

        public override IEnumerable<FtInterval> IterIntervals()
        {
            if (Strand == '+')
            {
                yield return new FtInterval(LeftMatchStart, Start - 1, MarkUpstreamFlankHit, 0, this, AnnotType.USTR, Strand);
                yield return new FtInterval(End + 1, RightMatchEnd, MarkDownstreamFlankHit, 0, this, AnnotType.DSTR, Strand);
            }
            else
            {
                yield return new FtInterval(LeftMatchStart, Start - 1, MarkDownstreamFlankHit, 0, this, AnnotType.DSTR, Strand);
                yield return new FtInterval(End + 1, RightMatchEnd, MarkUpstreamFlankHit, 0, this, AnnotType.USTR, Strand);
            }
            for (int eIdx = 0; eIdx < ExonStarts.Length; eIdx++)
                yield return new FtInterval(ExonStarts[eIdx], ExonEnds[eIdx], MarkExonHit, eIdx, this, AnnotType.EXON, Strand);
            for (int iIdx = 0; iIdx < ExonStarts.Length - 1; iIdx++)
                yield return new FtInterval(ExonEnds[iIdx] + 1, ExonStarts[iIdx + 1], MarkIntronHit, iIdx, this, AnnotType.INTR, Strand);
            yield break;
        }

        /// <summary>
        /// Generate pairs of [featureId, count] where featureId is either 1,2... for exons, or 1-2, 1-3,... for splice junctions
        /// </summary>
        /// <returns></returns>
        public List<Pair<string, int>> GetSpliceCounts()
        {
            int nExons = ExonStarts.Length;
            List<Pair<string, int>> result = new List<Pair<string, int>>();
            for (int exonIdx = 0; exonIdx < TranscriptHitsByExonIdx.Length; exonIdx++)
                result.Add(new Pair<string,int>((exonIdx+1).ToString(), TranscriptHitsByExonIdx[exonIdx]));
            string[] junctionIds = TranscriptHitsByJunctionAndBc.Keys.ToArray();
            Array.Sort(junctionIds);
            foreach (string junctionId in junctionIds)
                result.Add(new Pair<string, int>(junctionId, TranscriptHitsByJunctionAndBc[junctionId].Sum(v => (int)v)));
            return result;
        }

        public List<Pair<string, ushort[]>> GetSpliceCountsPerBarcode()
        {
            int nExons = ExonStarts.Length;
            List<Pair<string, ushort[]>> result = new List<Pair<string, ushort[]>>();
            string[] junctionIds = TranscriptHitsByJunctionAndBc.Keys.ToArray();
            Array.Sort(junctionIds);
            foreach (string junctionId in junctionIds)
                result.Add(new Pair<string, ushort[]>(junctionId, TranscriptHitsByJunctionAndBc[junctionId]));
            return result;
        }

        /// <summary>
        /// Samples the expression CV across the wells (of the current species) at the current read depth.
        /// The CV is calculated after the counts of each well has been normalized against the
        /// total count in that well, and only the wells where the total count is higher than
        /// minTotByBarcode are used. The sampled CV is added to the VariationSamples array.
        /// </summary>
        /// <param name="totByBarcode">Total count in each barcode</param>
        /// <param name="minTotByBarcode">Threshold in totByBarcode array for use of that well</param>
        /// <param name="speciesBcIndexes">The barcode indexes that are samples from the current species</param>
        public void SampleVariation(int[] totByBarcode, int minTotByBarcode, int[] speciesBcIndexes)
        {
            double cv = 0.0;
            if (GetTranscriptHits() >= 2)
            {
                double fracsSum = 0;
                List<double> fracsByBarcode = new List<double>();
                foreach (int bcodeIdx in speciesBcIndexes)
                {
                    if (totByBarcode[bcodeIdx] > minTotByBarcode)
                    {
                        double frac = (double)TrMolsByBc[bcodeIdx] / (double)totByBarcode[bcodeIdx];
                        fracsByBarcode.Add(frac);
                        fracsSum += frac;
                    }
                }
                double n = (double)fracsByBarcode.Count;
                double meanFrac = fracsSum / n;
                double sqDiffSum = 0.0;
                foreach (double frac in fracsByBarcode)
                    sqDiffSum += (frac - meanFrac) * (frac - meanFrac);
                cv = Math.Sqrt(sqDiffSum / (n - 1.0)) / meanFrac;
            }
            VariationSamples.Add(cv);
        }

        /// <summary>
        /// Makes a refFlat file like string
        /// </summary>
        /// <returns></returns>
        public string ToRefFlatString()
        {
            StringBuilder s = new StringBuilder();
            s.AppendFormat("{0}\t{1}{2}{3}\t", Name, GeneType, metadataDelim, GeneMetadata);
            string chrName = Props.props.CommonChrIds.Contains(Chr) ? Chr : "chr" + Chr;
            //string chrName = (Chr == StrtGenome.chrCTRLId) ? StrtGenome.chrCTRLId : "chr" + Chr;
            s.AppendFormat("{0}\t", chrName);
            s.AppendFormat("{0}\t", Strand);
            s.AppendFormat("{0}\t", Start);
            s.AppendFormat("{0}\t", End + 1);
            s.Append("\t\t");
            s.Append(ExonStarts.Length);
            s.Append("\t");
            foreach (int start in ExonStarts)
                s.AppendFormat("{0},", start);
            s.Append("\t");
            foreach (int end in ExonEnds)
                s.AppendFormat("{0},", end + 1);
            return s.ToString();
        }

        public override string ToString()
        {
            StringBuilder s = new StringBuilder();
            s.Append("\nExonStarts=");
            foreach (int start in ExonStarts)
                s.AppendFormat("{0},", start);
            s.Append("\nExonEnds=");
            foreach (int end in ExonEnds)
                s.AppendFormat("{0},", end + 1);
            return string.Format("GeneFeature(Name={0}, TrName={1}, Chr={2}, Strand={3}, Start={4} End={5}, " +
                                 "TrType={6}, TrId={7} Extension5Prime={8}{9})",
                                 Name, GeneMetadata, Chr, Strand, Start, End,
                                 GeneType, TranscriptID, Extension5Prime, s);
        }

        /// <summary>
        /// 0-based psl-like string of start positions
        /// </summary>
        public string ExonStartsString { get { return string.Join(",", Array.ConvertAll(ExonStarts, v => v.ToString())) + ","; } }
        /// <summary>
        /// 0-based psl-like string of exclusive end positions
        /// </summary>
        public string ExonEndsString { get { return string.Join(",", Array.ConvertAll(ExonEnds, v => (v+1).ToString())) +","; } }

        /// <summary>
        /// Extend 5' end according to GeneFeature5PrimeExtension or as far as the closest neighboring gene exon (in same orientation).
        /// </summary>
        /// <param name="startSortedMaskStarts">starts of all gene's exons on chr</param>
        /// <param name="endSortedMaskEnds">ends of all gene's exons on chr</param>
        /// <param name="sortedMaskStrands">strands of all gene's exons. true = '+', false = '-'</param>
        /// <returns>Actual length of 5' extension</returns>
        public int Extend5Prime(int[] startSortedMaskStarts, bool[] startSortedMaskStrands, int[] endSortedMaskEnds, bool[] endSortedMaskStrands)
        {
            int extension = 0;
            int idx;
            bool strand = (Strand == '+') ? true : false;
            bool strandMatters = (Props.props.DirectionalReads)? true : false;
            if (Strand == '+')
            {
                idx = Array.BinarySearch(endSortedMaskEnds, Start - 1);
                if (idx < 0) idx = ~idx;
                idx -= 1;
                while (idx >= 0 && strandMatters && endSortedMaskStrands[idx] != strand)
                    idx--;
                if (idx >= 0)
                {
                    int newStart = Math.Max(Start - Props.props.GeneFeature5PrimeExtension, endSortedMaskEnds[idx] + 1);
                    extension = Start - newStart;
                    Start = newStart;
                }
            }
            else
            {
                idx = Array.BinarySearch(startSortedMaskStarts, End + 1);
                if (idx < 0) idx = ~idx;
                while (idx < startSortedMaskStarts.Length && strandMatters && startSortedMaskStrands[idx] != strand)
                    idx++;
                if (idx < startSortedMaskStarts.Length)
                {
                    int newEnd = Math.Min(End + Props.props.GeneFeature5PrimeExtension, startSortedMaskStarts[idx] - 1);
                    extension = newEnd - End;
                    End = newEnd;
                }
            }
            return extension;
        }

        public void AddOffsetToCutSites(int extension)
        {
            Match m = Regex.Match(GeneMetadata, "^(.+" + GeneFeature.metadataDelim + ")([0-9\\-,]+)$");
            if (m.Success)
            {
                string[] oldSites = m.Groups[2].Value.Split(GeneFeature.metadataSubDelim);
                string[] newSites = Array.ConvertAll(oldSites, s => (int.Parse(s) + extension).ToString());
                GeneMetadata = m.Groups[1].Value + string.Join(GeneFeature.metadataSubDelim.ToString(), newSites);
            }
        }

        /// <summary>
        /// Decrease length of flank(s) if there is a too close neighboring gene exon (in same orientation).
        /// </summary>
        /// <param name="sortedMaskStarts">starts of all gene's exons on chr</param>
        /// <param name="endSortedMaskEnds">ends of all gene's exons on chr</param>
        /// <param name="sortedMaskStrands">strands of all gene's exons. true = '+', false = '-'</param>
        public int AdjustFlanks(int[] sortedMaskStarts, bool[] startSortedMaskStrands, int[] endSortedMaskEnds, bool[] endSortedMaskStrands)
        {
            int idx;
            bool strand = (Strand == '+') ? true : false;
            bool strandMatters = (Props.props.DirectionalReads) ? true : false;
            idx = Array.BinarySearch(endSortedMaskEnds, Start - 1);
            if (idx < 0) idx = ~idx;
            idx -= 1;
            while (idx >= 0 && strandMatters && endSortedMaskStrands[idx] != strand)
                idx--;
            if (idx >= 0)
                LeftFlankLength = Math.Min(LocusFlankLength, Start - endSortedMaskEnds[idx]);
            idx = Array.BinarySearch(sortedMaskStarts, End + 1);
            if (idx < 0) idx = ~idx;
            while (idx < sortedMaskStarts.Length && strandMatters && startSortedMaskStrands[idx] != strand)
                idx++;
            if (idx < sortedMaskStarts.Length)
                RightFlankLength = Math.Min(LocusFlankLength, sortedMaskStarts[idx] - End);
            return (LocusFlankLength - LeftFlankLength) + (LocusFlankLength - RightFlankLength);
        }

        /// <summary>
        /// Mark up all USTR,DSTR, INTR that overlap with other genes exons, irrespective of orientation
        /// </summary>
        /// <param name="sortedMaskStarts"></param>
        /// <param name="sortedMaskEnds"></param>
        /// <returns>Number of features masked</returns>
        public int MaskOverlappingUSTRDSTRINTR(int[] sortedMaskStarts, int[] sortedMaskEnds)
        {
            int nMaskedFeatures = 0;
            int idx = Array.BinarySearch(sortedMaskEnds, LocusStart);
            if (idx < 0) idx = ~idx;
            if (idx >= 0)
            {
                while (idx < sortedMaskStarts.Length && sortedMaskStarts[idx] <= LocusEnd)
                {
                    int maskStart = sortedMaskStarts[idx];
                    int maskEnd = sortedMaskEnds[idx];
                    if (maskStart < Start && maskEnd > LeftMatchStart)
                    {
                        nMaskedFeatures++;
                        if (Strand == '+') MaskedUSTR = true;
                        else MaskedDSTR = true;
                    }
                    if (maskStart < RightMatchEnd && maskEnd > End)
                    {
                        nMaskedFeatures++;
                        if (Strand == '+') MaskedDSTR = true;
                        else MaskedUSTR = true;
                    }
                    for (int i = 0; i < ExonCount - 1; i++)
                    {
                        if (maskStart < ExonStarts[i + 1] && maskEnd > ExonEnds[i])
                        {
                            nMaskedFeatures++;
                            MaskedINTR[i] = true;
                        }
                    }
                    idx++;
                }
            }
            return nMaskedFeatures;
        }

        /// <summary>
        /// Will mark exons that are overlapping with the intervals defined in the parameters
        /// </summary>
        /// <param name="sortedMaskStarts">Sorted list of mask interval starts on chromosome</param>
        /// <param name="sortedMaskEnds">Sorted list of mask interval ends on chromsome</param>
        /// <returns>Indices of mask intervals that overlapped</returns>
        public List<int> MaskOverlappingAntisenseExons(int[] sortedMaskStarts, int[] sortedMaskEnds, bool[] sortedMaskStrands)
        {
            List<int> idxOfMasked = new List<int>();
            if (!Props.props.DirectionalReads) return idxOfMasked;
            bool antisenseStrand = (Strand == '+')? false: true;
            int maskRegionIdx = Array.BinarySearch(sortedMaskEnds, Start);
            if (maskRegionIdx < 0) maskRegionIdx = ~maskRegionIdx;
            while (maskRegionIdx < sortedMaskStarts.Length && sortedMaskStarts[maskRegionIdx] <= End)
            {
                if (sortedMaskStrands[maskRegionIdx] == antisenseStrand)
                {
                    int maskStart = sortedMaskStarts[maskRegionIdx];
                    int maskEnd = sortedMaskEnds[maskRegionIdx];
                    for (int gfExonsIdx = 0; gfExonsIdx < ExonCount; gfExonsIdx++)
                    {
                        if (maskStart < ExonEnds[gfExonsIdx] && maskEnd > ExonStarts[gfExonsIdx])
                        {
                            MaskedAEXON[gfExonsIdx] = true;
                            idxOfMasked.Add(maskRegionIdx);
                        }
                    }
                }
                maskRegionIdx++;
            }
            return idxOfMasked;
        }

        public bool HasAnyMaskedExon()
        {
            return MaskedAEXON.Any((flag => flag == true));
        }

        /// <summary>
        /// The stored hit position is always midPos of read relative to LocusStart, 
        /// i.e., 0 is first pos of untruncated left flank
        /// </summary>
        /// <param name="chrPos"></param>
        /// <param name="strand"></param>
        /// <param name="bcodeIdx"></param>
        public void MarkLocusHitPos(MappedTagItem item)
        {
            locusHitsSorted = false;
            while (locusHitIdx + item.MolCount >= m_LocusHits.Length)
            {
                //Console.WriteLine(Name + ":Resize m_LocusHits to " + (m_LocusHits.Length * 2));
                Array.Resize(ref m_LocusHits, m_LocusHits.Length * 2);
            }
            int s = GetStrandAsInt(item.DetectedStrand);
            int locusPos = item.HitMidPos - LocusStart;
            int hit = (locusPos << 8) | (item.bcIdx << 1) | s;
            for (int n = 0; n < item.MolCount; n++)
                m_LocusHits[locusHitIdx++] = hit;
        }
        /// <summary>
        /// Strand is coded as 0 for forward ('+') and 1 for reverse ('-')
        /// </summary>
        /// <param name="strand"></param>
        /// <returns></returns>
        public static int GetStrandAsInt(char strand)
        {
            return (strand == '+') ? 0 : 1;
        }
        /// <summary>
        /// Strand is coded as 0 for forward ('+') and 1 for reverse ('-')
        /// </summary>
        /// <returns></returns>
        public int GetStrandAsInt()
        {
            return GetStrandAsInt(Strand); 
        }

        /// <summary>
        /// Add to barcode sorted data for SNP positions contained within the MappedTagItem.
        /// </summary>
        /// <param name="item"></param>
        private void MarkSNPs(MappedTagItem item)
        {
            if (item.SNPCounts == null)
                return;
            if (bcSNPCountsByRealChrPos == null)
                bcSNPCountsByRealChrPos = new SortedDictionary<int, SNPCountsByBarcode>();
            foreach (SNPCounter snpCounter in item.SNPCounts)
            {
                int snpPosOnRealChr = item.splcToRealChrOffset + snpCounter.posOnChr;
                SNPCountsByBarcode bcSnpCounts;
                if (!bcSNPCountsByRealChrPos.TryGetValue(snpPosOnRealChr, out bcSnpCounts))
                {
                    bcSnpCounts = new SNPCountsByBarcode(Props.props.Barcodes.Count, snpCounter.refNt);
                    bcSNPCountsByRealChrPos[snpPosOnRealChr] = bcSnpCounts;
                }
                bcSnpCounts.Add(item.bcIdx, snpCounter);
            }
        }
    }

}
