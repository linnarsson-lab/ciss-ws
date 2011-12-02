using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;

namespace Linnarsson.Dna
{
    public class GeneFeature : LocusFeature
    {
        public readonly static string pseudoGeneIndicator = "_p";
        public readonly static string altLocusIndicator = "_loc";
        public readonly static string nonUTRExtendedIndicator = variantIndicator + "Original";
        public static int LocusProfileBinSize = 50;
        public static int LocusFlankLength;

        public int SpliceLen; // Set by the corresponding SplicedGeneLocus
        /// <summary>
        /// Hits stored as ints of pp..pppbbbbbbbs where p is position relative to LocusStart
        /// in chromosome orientation, b is barcode, and s is chromosome strand (0 = '+', 1 = '-')
        /// </summary>
        private int locusHitIdx;
        private bool locusHitsSorted;
        private int[] m_LocusHits;
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

        /// <summary>
        /// SNPCounter arrays by bcIdx for every SNP positions
        /// </summary>
        public Dictionary<int,  SNPCounter[]> SNPCountersByBcIdx;

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
        /// Start position on chromosome of the leftmost exon
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
        /// Inclusive end position on chromosome of the rightmost exon
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
        public int[] TranscriptHitsByBarcode;
        public int[] NonConflictingTranscriptHitsByBarcode;
        public List<double> VariationSamples;
        public int[] TranscriptHitsByExonIdx; // Used to analyse exon hit distribution
        public Dictionary<string, int> TranscriptHitsByJunction; // Used to analyse cross-junction hit distribution
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
        /// Zero-based start positions in chromosome
        /// </summary>
        public int[] ExonStarts;
        /// <summary>
        /// Zero-based last positions in chromosome
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

        public GeneFeature(string name, string chr, char strand, int[] exonStarts, int[] exonEnds)
            : base(name, chr, strand, exonStarts[0], exonEnds[exonEnds.Length - 1])
        {
            ExonStarts = exonStarts;
            ExonEnds = exonEnds;
            LeftFlankLength = RightFlankLength = LocusFlankLength; // Init with default length
            MaskedAEXON = new bool[exonEnds.Length];
            MaskedINTR = new bool[exonEnds.Length - 1];
            TranscriptHitsByBarcode = new int[Props.props.Barcodes.Count];
            NonConflictingTranscriptHitsByBarcode = new int[Props.props.Barcodes.Count];
            VariationSamples = new List<double>();
            TranscriptHitsByExonIdx = new int[exonStarts.Length];
            TranscriptHitsByJunction = new Dictionary<string, int>();
            HitsByAnnotType = new int[AnnotType.Count];
            NonMaskedHitsByAnnotType = new int[AnnotType.Count];
            m_LocusHits = new int[1000];
            locusHitIdx = 0;
            SNPCountersByBcIdx = new Dictionary<int, SNPCounter[]>();
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

        public int GetJunctionHits()
        {
            int senseHits = HitsByAnnotType[AnnotType.SPLC];
            return (AnnotType.DirectionalReads) ? senseHits : senseHits + HitsByAnnotType[AnnotType.ASPLC];
        }
        public int GetTranscriptHits()
        {
            int senseHits = HitsByAnnotType[AnnotType.EXON];
            return (AnnotType.DirectionalReads) ? senseHits : senseHits + HitsByAnnotType[AnnotType.AEXON];
        }
        public override bool IsExpressed()
        {
            return GetTranscriptHits() > 0;
        }
        public bool IsExpressed(int barcodeIdx)
        {
            return TranscriptHitsByBarcode[barcodeIdx] > 0;
        }

        public bool IsSpike()
        {
            return Name.Contains("_SPIKE");
        }

        public int GetAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.EXON || (!AnnotType.DirectionalReads && annotType == AnnotType.AEXON))
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
                for (int i = 0; i < ExonCount; i++)
                {
                    if (ExonStarts[i] > chrPos)
                        return -1;
                    if (ExonEnds[i] >= chrPos)
                        return chrPos - ExonStarts[i] + posFrom5Prime;
                    int exonLen = ExonEnds[i] - ExonStarts[i] + 1;
                    posFrom5Prime += exonLen;
                }
            }
            else
            {
                for (int i = ExonCount - 1; i >= 0; i--)
                {
                    if (ExonEnds[i] < chrPos)
                        return -1;
                    if (ExonStarts[i] <= chrPos)
                        return ExonEnds[i] - chrPos + posFrom5Prime;
                    int len = ExonEnds[i] - ExonStarts[i] + 1;
                    int exonLen = ExonEnds[i] - ExonStarts[i] + 1;
                    posFrom5Prime += exonLen;
                }
            }
            return -1; // Given pos is not inside transcript exons
        }

        public bool Contains(int start, int end)
        {
            return (start >= Start && end <= End);
        }
        public bool Overlaps(int start, int end, int minIntrusion)
        {
            return (start <= End - minIntrusion && end >= Start + minIntrusion);
        }

        public bool ExonsWithin(int start, int end, int minIntrusion)
        {
            for (int i = 0; i < ExonCount; i++)
                if (start <= ExonEnds[i] - minIntrusion && end >= ExonStarts[i] + minIntrusion)
                    return true;
            return false;
        }

        private MarkResult MarkUpstreamFlankHit(MappedTagItem item, int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.USTR, item, markType);
        }
        private MarkResult MarkDownstreamFlankHit(MappedTagItem item, int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.DSTR, item, markType);
        }
        private MarkResult MarkFlankHit(int annotType, MappedTagItem item, MarkStatus markType)
        {
            int undirAnnotType = annotType;
            if (item.strand != Strand) annotType = AnnotType.MakeAntisense(annotType);
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(annotType, this);
            MarkLocusHitPos(item);
            AddToTotalHits(item);
            HitsByAnnotType[annotType] += item.MolCount;
            if ((undirAnnotType == AnnotType.USTR && !MaskedUSTR) ||
                (undirAnnotType == AnnotType.DSTR && !MaskedDSTR))
                NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            return new MarkResult(annotType, this);
        }

        private MarkResult MarkIntronHit(MappedTagItem item, int intronIdx, MarkStatus markType)
        {
            int annotType = (item.strand == Strand) ? AnnotType.INTR : AnnotType.AINTR;
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(annotType, this);
            MarkLocusHitPos(item);
            AddToTotalHits(item);
            HitsByAnnotType[annotType] += item.MolCount;
            if (!MaskedINTR[intronIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            return new MarkResult(annotType, this);
        }

        public MarkResult MarkExonHit(MappedTagItem item, int exonIdx, MarkStatus markType)
        {
            int annotType = (item.strand == Strand) ? AnnotType.EXON : AnnotType.AEXON;
            if (markType == MarkStatus.TEST_EXON_SKIP_OTHER)
                return new MarkResult(annotType, this);
            if (markType == MarkStatus.TEST_EXON_MARK_OTHER)
            {
                if (!AnnotType.IsTranscript(annotType))
                {   // Only happens for directional AEXON reads
                    MarkLocusHitPos(item);
                    AddToTotalHits(item);
                    HitsByAnnotType[annotType] += item.MolCount;
                    if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
                    MarkSNPs(item);
                }
                return new MarkResult(annotType, this);
            } // Now hit should be marked
            if (AnnotType.IsTranscript(annotType)) //(strand == Strand) // Sense hit
            {
                MarkLocusHitPos(item);
                AddToTotalHits(item);
                TranscriptHitsByExonIdx[exonIdx] += item.MolCount;
                TranscriptHitsByBarcode[item.bcIdx] += item.MolCount;
                if (markType == MarkStatus.UNIQUE_EXON_MAPPING)
                    NonConflictingTranscriptHitsByBarcode[item.bcIdx] += item.MolCount;
                HitsByAnnotType[annotType] += item.MolCount;
                NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            }
            return new MarkResult(annotType, this);
        }

        public MarkResult MarkSpliceHit(MappedTagItem item, int exonId, string junctionId, MarkStatus markType)
        {
            int exonIdx = (Strand == '+') ? exonId - 1 : ExonCount - exonId;
            int annotType = (item.strand == Strand) ? AnnotType.SPLC : AnnotType.ASPLC;
            if (AnnotType.IsTranscript(annotType))
            {
                MarkJunctionHit(junctionId, item.MolCount);
                NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            }
            else // Only happens for directional ASPLC hits
            {
                if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[annotType] += item.MolCount;
            }
            HitsByAnnotType[annotType] += item.MolCount;
            MarkResult res = MarkExonHit(item, exonIdx, markType);
            return new MarkResult(annotType, this);
        }

        private void MarkJunctionHit(string junctionId, int count)
        {
            if (!TranscriptHitsByJunction.ContainsKey(junctionId))
                TranscriptHitsByJunction[junctionId] = count;
            else
                TranscriptHitsByJunction[junctionId] += count;
        }

        public override IEnumerable<FtInterval> IterIntervals()
        {
            if (Strand == '+')
            {
                yield return new FtInterval(LeftMatchStart, Start - 1, MarkUpstreamFlankHit, 0);
                yield return new FtInterval(End, RightMatchEnd, MarkDownstreamFlankHit, 0);
            }
            else
            {
                yield return new FtInterval(LeftMatchStart, Start - 1, MarkDownstreamFlankHit, 0);
                yield return new FtInterval(End, RightMatchEnd, MarkUpstreamFlankHit, 0);
            }
            for (int eIdx = 0; eIdx < ExonStarts.Length; eIdx++)
                yield return new FtInterval(ExonStarts[eIdx], ExonEnds[eIdx], MarkExonHit, eIdx);
            for (int iIdx = 0; iIdx < ExonStarts.Length - 1; iIdx++)
                yield return new FtInterval(ExonEnds[iIdx] + 1, ExonStarts[iIdx + 1], MarkIntronHit, iIdx);
            yield break;
        }

        public List<Pair<string, int>> GetSpliceCounts()
        {
            int nExons = ExonStarts.Length;
            List<Pair<string, int>> result = new List<Pair<string, int>>();
            for (int exonIdx = 0; exonIdx < TranscriptHitsByExonIdx.Length; exonIdx++)
                result.Add(new Pair<string,int>(exonIdx.ToString(), TranscriptHitsByExonIdx[exonIdx]));
            string[] junctionIds = TranscriptHitsByJunction.Keys.ToArray();
            Array.Sort(junctionIds);
            foreach (string junctionId in junctionIds)
                result.Add(new Pair<string, int>(junctionId, TranscriptHitsByJunction[junctionId]));
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
                        double frac = (double)TranscriptHitsByBarcode[bcodeIdx] / (double)totByBarcode[bcodeIdx];
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
        public override string ToString()
        {
            StringBuilder s = new StringBuilder();
            s.Append(Name + "\t\t");
            string chrName = (Chr == StrtGenome.chrCTRLId)? StrtGenome.chrCTRLId : "chr" + Chr;
            s.Append(chrName + "\t");
            s.Append(Strand + "\t");
            s.Append(Start + "\t");
            s.Append((End+1) + "\t");
            s.Append("\t\t");
            s.Append(ExonStarts.Length);
            s.Append("\t");
            foreach (int start in ExonStarts)
                s.Append(start.ToString() + ",");
            s.Append("\t");
            foreach (int end in ExonEnds)
                s.Append((end+1).ToString() + ",");
            return s.ToString();
        }

        /// <summary>
        /// Decrease length of flank(s) if ther is a too close neighboring gene in same orientation.
        /// Also mark up flanks and introns that overlap with some other gene, irrespective of orientation.
        /// </summary>
        /// <param name="sortedMaskStarts"></param>
        /// <param name="sortedMaskEnds"></param>
        /// <param name="strand"></param>
        public void MaskInterExons(int[] sortedMaskStarts, int[] sortedMaskEnds, char strand)
        {
            if (strand == Strand)
            {
                int closestEndBefore = Array.FindLast(sortedMaskEnds, (p => p < Start));
                LeftFlankLength = (closestEndBefore > 0)? Math.Min(LocusFlankLength, Start - closestEndBefore) : LocusFlankLength;
                int closestStartAfter = Array.Find(sortedMaskStarts, (p => p > End));
                RightFlankLength = (closestStartAfter > 0) ? Math.Min(LocusFlankLength, closestStartAfter - End) : LocusFlankLength;
            }
            int idx = Array.FindIndex(sortedMaskEnds, (end => end >= LocusStart));
            if (idx >= 0)
            {
                while (idx < sortedMaskStarts.Length && sortedMaskStarts[idx] <= LocusEnd)
                {
                    int maskStart = sortedMaskStarts[idx];
                    int maskEnd = sortedMaskEnds[idx];
                    if (maskStart < Start && maskEnd > LocusStart) // Should it not be "...&& maskEnd > LocusMatchStart" ??
                    {
                        if (Strand == '+') MaskedUSTR = true; 
                        else MaskedDSTR = true;
                    }
                    if (maskStart < LocusEnd && maskEnd > End) // Should it not be "maskStart < LocusMatchEnd..." ??
                    {
                        if (Strand == '+') MaskedDSTR = true;
                        else MaskedUSTR = true;
                    }
                    for (int i = 0; i < ExonCount - 1; i++)
                    {
                        if (maskStart < ExonStarts[i + 1] && maskEnd > ExonEnds[i])
                            MaskedINTR[i] = true;
                    }
                    idx++;
                }
            }
        }

        /// <summary>
        /// Will mark exons that are overlapping with the intervals defined in the parameters
        /// </summary>
        /// <param name="sortedMaskStarts">Sorted list of mask interval starts on chromosome</param>
        /// <param name="sortedMaskEnds">Sorted list of mask interval ends on chromsome</param>
        /// <returns>Indices of mask intervals that overlapped</returns>
        public List<int> MaskExons(int[] sortedMaskStarts, int[] sortedMaskEnds)
        {
            List<int> idxOfMasked = new List<int>();
            int maskRegionIdx = Array.FindIndex(sortedMaskEnds, (end => end >= Start));
            if (maskRegionIdx >= 0)
            {
                while (maskRegionIdx < sortedMaskStarts.Length && sortedMaskStarts[maskRegionIdx] <= End)
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
                    maskRegionIdx++;
                }
            }
            return idxOfMasked;
        }

        public bool HasAnyMaskedExon()
        {
            return MaskedAEXON.Any((flag => flag == true));
        }

        /// <summary>
        /// The stored hit position is always relative to LocusStart, 
        /// i.e., 0 is first pos of untruncated left flank
        /// </summary>
        /// <param name="chrPos"></param>
        /// <param name="strand"></param>
        /// <param name="bcodeIdx"></param>
        public void MarkLocusHitPos(MappedTagItem item)
        {
            int locusPos = item.HitMidPos - LocusStart;
            locusHitsSorted = false;
            if (locusHitIdx == m_LocusHits.Length)
            {
                Array.Resize(ref m_LocusHits, Math.Max(1000, m_LocusHits.Length * 2));
            }
            int s = (item.strand == '+') ? 0 : 1;
            int hit = (locusPos << 8) | (item.bcIdx << 1) | s;
            m_LocusHits[locusHitIdx++] = hit;
        }

        /// <summary>
        /// Add to barcode sorted data for SNP positions contained within the MappedTagItem.
        /// </summary>
        /// <param name="item"></param>
        private void MarkSNPs(MappedTagItem item)
        {
            foreach (LocatedSNPCounter locCounter in item.IterMolSNPCounts())
            {
                int chrPos = locCounter.chrPos;
                if (!SNPCountersByBcIdx.ContainsKey(chrPos))
                {
                    SNPCountersByBcIdx[chrPos] = new SNPCounter[Props.props.Barcodes.Count];
                    for (int bcIdx = 0; bcIdx < Props.props.Barcodes.Count; bcIdx++)
                        SNPCountersByBcIdx[chrPos][bcIdx] = new SNPCounter();
                }
                SNPCountersByBcIdx[chrPos][item.bcIdx].Add(locCounter.counter);
            }
        }

        public void GetSNPData()
        {
        }
    }
}
