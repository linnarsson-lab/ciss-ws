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
        public readonly static string variantIndicator = "_v";
        public readonly static string pseudoGeneIndicator = "_p";
        public readonly static string nonUTRExtendedIndicator = variantIndicator + "Original";
        public static bool GenerateTranscriptProfiles = false;
        public static bool GenerateLocusProfiles = false;
        public static int LocusProfileBinSize = 50;
        public static int LocusFlankLength;
        public static int SpliceFlankLen;

        /// <summary>
        /// Hits stored as ints of pp..pppbbbbbbbs where p is position in chromosome orientation,
        /// b is barcode, and s is chromosome strand (0 = '+', 1 = '-')
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

        public override int GetLocusLength()
        {
            return End - Start + 1 + 2 * LocusFlankLength;
        }

        public int LocusStart { get { return Start - LocusFlankLength; } }
        public int LocusEnd { get { return End + LocusFlankLength; } }
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
        public Dictionary<int, int> SenseHitsByExonIdx;
        public int[] HitsByAnnotType;
        public int[] NonMaskedHitsByAnnotType;
        public CompactGenePainter cPainter;

        // Will be adjusted to not cover nearby genes in same orientation
        public int LeftFlankLength;
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
            TranscriptHitsByBarcode = new int[Barcodes.MaxCount];
            NonConflictingTranscriptHitsByBarcode = new int[Barcodes.MaxCount];
            VariationSamples = new List<double>();
            SenseHitsByExonIdx = new Dictionary<int, int>();
            HitsByAnnotType = new int[AnnotType.Count];
            NonMaskedHitsByAnnotType = new int[AnnotType.Count];
            m_LocusHits = new int[1000];
            locusHitIdx = 0;
        }

        public int GetExonLength(int i)
        {
            return ExonEnds[i] - ExonStarts[i] + 1;
        }

        public int GetAnnotCounts(int annotType, bool excludeMasked)
        {
            if (excludeMasked)
                return NonMaskedHitsByAnnotType[annotType];
            else
                return HitsByAnnotType[annotType];
        }

        public override bool IsExpressed()
        {
            return HitsByAnnotType[AnnotType.EXON] > 0;
        }
        public bool IsMainVariant()
        {
            return ! Name.Contains(variantIndicator);
        }
        public bool IsSpike()
        {
            return Name.Contains("_SPIKE");
        }

        public int GetAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.EXON) return GetTranscriptLength();
            if (annotType == AnnotType.AEXON) return GetTranscriptLength(excludeMasked);
            if (annotType == AnnotType.INTR || annotType == AnnotType.AINTR)
                 return GetIntronicLength(excludeMasked);
            if (annotType == AnnotType.USTR || annotType == AnnotType.AUSTR)
                return (MaskedUSTR && excludeMasked)? 0 : (Strand == '+')? LeftFlankLength : RightFlankLength;
            if (annotType == AnnotType.DSTR || annotType == AnnotType.ADSTR)
                return (MaskedDSTR && excludeMasked) ? 0 : (Strand == '+')? RightFlankLength : LeftFlankLength;
            if (annotType == AnnotType.SPLC || annotType == AnnotType.ASPLC)
                return SpliceFlankLen * 2 * (ExonCount - 1); // TODO: Calc better, squared for N < 40
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

        public int GetChrPos(int transcriptPos)
        {
            if (Strand == '+')
            {
                for (int i = 0; i < ExonCount; i++)
                {
                    int len = ExonEnds[i] - ExonStarts[i] + 1;
                    if (transcriptPos < len) return ExonStarts[i] + transcriptPos;
                    transcriptPos -= len;
                }
            }
            else
            {
                for (int i = ExonCount - 1; i >= 0 ; i--)
                {
                    int len = ExonEnds[i] - ExonStarts[i] + 1;
                    if (transcriptPos < len) return ExonEnds[i] - transcriptPos;
                    transcriptPos -= len;
                }
            }
            return -1; // Given pos is outside transcript
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

        public override IFeature Clone()
        {
            return new GeneFeature(Name, Chr, Strand, ExonStarts, ExonEnds);
        }

        private MarkResult MarkUpstreamFlankHit(int chrHitPos, int junkW, char strand, int bcodeIdx,
                                                int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.USTR, chrHitPos, strand, bcodeIdx, markType);
        }
        private MarkResult MarkDownstreamFlankHit(int chrHitPos, int junkW, char strand, int bcodeIdx,
                                                  int junk, MarkStatus markType)
        {
            return MarkFlankHit(AnnotType.DSTR, chrHitPos, strand, bcodeIdx, markType);
        }
        private MarkResult MarkFlankHit(int annotType, int chrHitPos, char strand, int bcodeIdx,
                                        MarkStatus markType)
        {
            int undirAnnotType = annotType;
            if (strand != Strand) annotType = AnnotType.MakeAntisense(annotType);
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(annotType, this);
            MarkLocusHitPos(chrHitPos, strand, bcodeIdx);
            if (strand == Strand) // Sense hit
                TotalSenseHits++;
            else
                TotalAntiSenseHits++;
            HitsByAnnotType[annotType]++;
            if ((undirAnnotType == AnnotType.USTR && !MaskedUSTR) ||
                (undirAnnotType == AnnotType.DSTR && !MaskedDSTR))
                NonMaskedHitsByAnnotType[annotType]++;
            return new MarkResult(annotType, this);
        }

        private MarkResult MarkIntronHit(int chrHitPos, int junk, char strand, int bcodeIdx,
                                         int intronIdx, MarkStatus markType)
        {
            int annotType = (strand == Strand) ? AnnotType.INTR : AnnotType.AINTR;
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(annotType, this);
            MarkLocusHitPos(chrHitPos, strand, bcodeIdx);
            if (strand == Strand) // Sense hit
                TotalSenseHits++;
            else
                TotalAntiSenseHits++;
            HitsByAnnotType[annotType]++;
            if (!MaskedINTR[intronIdx]) NonMaskedHitsByAnnotType[annotType]++;
            return new MarkResult(annotType, this);
        }

        public MarkResult MarkExonHit(int chrHitPos, int halfWidth, char strand, int bcodeIdx,
                                       int exonIdx, MarkStatus markType)
        {
            int annotType = (strand == Strand) ? AnnotType.EXON : AnnotType.AEXON;
            if (markType == MarkStatus.TEST_EXON_SKIP_OTHER)
                return new MarkResult(annotType, this);
            MarkLocusHitPos(chrHitPos, strand, bcodeIdx);
            if (markType == MarkStatus.TEST_EXON_MARK_OTHER)
            {
                if (annotType == AnnotType.AEXON)
                {
                    TotalAntiSenseHits++;
                    HitsByAnnotType[AnnotType.AEXON]++;
                    if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[AnnotType.AEXON]++;
                }
                return new MarkResult(annotType, this);
            }
            if (strand == Strand) // Sense hit
            {
                TotalSenseHits++;
                MarkSenseExonHit(exonIdx);
                TranscriptHitsByBarcode[bcodeIdx]++;
                if (markType == MarkStatus.SINGLE_MAPPING)
                    NonConflictingTranscriptHitsByBarcode[bcodeIdx]++;
                HitsByAnnotType[AnnotType.EXON]++;
                NonMaskedHitsByAnnotType[AnnotType.EXON]++;
            }
            return new MarkResult(annotType, this);
        }

        // Called when splice chromosome got a unique alignment to one half of a junction.
        // Convert it to the exon to which the hit should have been.
        public MarkResult ConvertSpliceHit(int realChrHitPos, int halfWidth, char strand, int bcodeIdx,
                                           int exonId, MarkStatus markType)
        {
            // Convert the single mapping not spanning splice to real exon mapping
            int exonIdx = (Strand == '+') ? exonId - 1 : ExonCount - exonId;
            return MarkExonHit(realChrHitPos, halfWidth, strand, bcodeIdx, exonIdx, markType);
        }

        public MarkResult MarkSpliceHit(int realChrHitPos, int halfWidth, char strand, int bcodeIdx,
                                        int exonId, int junctionId, MarkStatus markType)
        {
            int exonIdx = (Strand == '+') ? exonId - 1 : ExonCount - exonId;
            int annotType = AnnotType.SPLC;
            if (strand == Strand)
            {
                MarkSenseExonHit(junctionId);
                NonMaskedHitsByAnnotType[AnnotType.SPLC]++;
            }
            else
            {
                annotType = AnnotType.ASPLC;
                if (!MaskedAEXON[exonIdx]) NonMaskedHitsByAnnotType[AnnotType.ASPLC]++;
            }
            HitsByAnnotType[annotType]++;
            MarkResult res = MarkExonHit(realChrHitPos, halfWidth, strand, bcodeIdx, exonIdx, markType);
            return new MarkResult(annotType, this);
        }

        private void MarkSenseExonHit(int exonOrJunctionIdx)
        {
            if (!SenseHitsByExonIdx.ContainsKey(exonOrJunctionIdx))
                SenseHitsByExonIdx[exonOrJunctionIdx] = 1;
            else
                SenseHitsByExonIdx[exonOrJunctionIdx]++;
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

        public int[][] GetSpliceCounts()
        {
            int nExons = ExonStarts.Length;
            int[][] result = new int[nExons][];
            for (int e1 = 1; e1 <= nExons - 1; e1++)
            {
                result[e1] = new int[nExons + 1];
                for (int e2 = e1 + 1; e2 <= nExons; e2++)
                {
                    int jId = SplicedGeneFeature.GetJunctionId(e1, e2);
                    if (SenseHitsByExonIdx.ContainsKey(jId))
                        result[e1][e2] = SenseHitsByExonIdx[jId];
                    else
                        result[e1][e2] = 0;
                }
            }
            return result;
        }

        public void SampleVariation(int[] totByBarcode)
        {
            double cv = 0.0;
            if (HitsByAnnotType[AnnotType.EXON] >= 2)
            {
                double n = (double)TranscriptHitsByBarcode.Length;
                double fracsSum = 0;
                double[] fracsByBarcode = new double[totByBarcode.Length];
                for (int bcodeIdx = 0; bcodeIdx < totByBarcode.Length; bcodeIdx++)
                {
                    fracsByBarcode[bcodeIdx] = (double)TranscriptHitsByBarcode[bcodeIdx] / (double)totByBarcode[bcodeIdx];
                    fracsSum += fracsByBarcode[bcodeIdx];
                }
                double meanFrac = fracsSum / n;
                double sqDiffSum = 0.0;
                foreach (double frac in fracsByBarcode)
                    sqDiffSum += (frac - meanFrac) * (frac - meanFrac);
                cv = Math.Sqrt(sqDiffSum / (n - 1.0)) / meanFrac;
            }
            VariationSamples.Add(cv);
        }

        public static IFeature FromRefFlatLine(string refFlatLine)
        {
            string[] record = refFlatLine.Split('\t');
            string name = record[0].Trim();
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], nExons, 0);
            int[] exonEnds = SplitField(record[10], nExons, -1);
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds);
            int[] offsets = SplitField(record[11], nExons, 0);
            string[] spliceIds = record[12].Split(',');
            Array.Resize(ref spliceIds, nExons);
            return new SplicedGeneFeature(name, chr, strand, exonStarts, exonEnds, offsets, spliceIds);
        }

        private static int[] SplitField(string field, int nParts, int offset)
        {
            int[] parts = new int[nParts];
            string[] items = field.Split(',');
            for (int i = 0; i < nParts; i++)
                parts[i] = int.Parse(items[i]) + offset;
            return parts;
        }

        public static string StripVersionPart(string featureName)
        {
            int pos = featureName.IndexOf(variantIndicator);
            if (pos == -1) return featureName;
            return featureName.Substring(0, pos);
        }

        public bool IsVariant()
        {
            return Name.Contains(variantIndicator);
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
                    if (maskStart < Start && maskEnd > LocusStart)
                    {
                        if (Strand == '+') MaskedUSTR = true; 
                        else MaskedDSTR = true;
                    }
                    if (maskStart < LocusEnd && maskEnd > End)
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
        public void MarkLocusHitPos(int chrPos, char strand, int bcodeIdx)
        {
            int locusPos = chrPos - LocusStart;
            locusHitsSorted = false;
            if (locusHitIdx == m_LocusHits.Length)
            {
                Array.Resize(ref m_LocusHits, Math.Max(1000, m_LocusHits.Length * 2));
            }
            int s = (strand == '+') ? 0 : 1;
            int hit = (locusPos << 8) | (bcodeIdx << 1) | s;
            m_LocusHits[locusHitIdx++] = hit;
        }

    }
}
