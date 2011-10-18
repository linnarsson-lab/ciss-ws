using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class SplicedGeneFeature : LocusFeature
    {
        public static string RealChrId;
        public static int RealChrOffset;

        public readonly static int MaxExonBits = 10;
        private static int rightExonMask = (1 << MaxExonBits) - 1;
        private static int junctionSize;
        private static int spliceFlankLen;
        private int[] offsets;
        private int[] junctionIds;
        private int[] realExonIds;
        private int[] exonStarts; // Starts in junction chromosome.
        private GeneFeature realFeature;

        public SplicedGeneFeature(string name, string chr, char strand, int[] exonStarts, int[] exonEnds,
                                int[] offsets, string[] spliceIds)
            : base(name, chr, strand, exonStarts[0], exonEnds.Max())
        {
            this.exonStarts = exonStarts;
            this.offsets = offsets;
            junctionIds = new int[spliceIds.Length];
            realExonIds = new int[spliceIds.Length];
            for (int i = 0; i < spliceIds.Length; i++)
            {
                realExonIds[i] = GetRealExonId(spliceIds[i]);
                junctionIds[i] = GetJunctionId(spliceIds[i]);
            }
        }
        public SplicedGeneFeature(string name, string chr, char strand, int start, int end, int partLen,
                                  int[] exonStarts, int[] offsets, int[] junctionIds, int[] realExonIds)
            : base(name, chr, strand, start, end)
        {
            this.exonStarts = exonStarts;
            this.offsets = offsets;
            this.junctionIds = junctionIds;
            this.realExonIds = realExonIds;
        }

        public static void SetSpliceFlankLen(int spliceFlankLen)
        {
            SplicedGeneFeature.spliceFlankLen = spliceFlankLen;
            SplicedGeneFeature.junctionSize = 2 * spliceFlankLen;
        }

        public override IFeature Clone()
        {
            return new SplicedGeneFeature(Name, Chr, Strand, Start, End, spliceFlankLen,
                                          exonStarts, offsets, junctionIds, realExonIds);
        }

        public void BindToRealFeature(GeneFeature actualFeature)
        {
            realFeature = actualFeature;
        }

        private static int GetRealExonId(string spliceId)
        {
            if (spliceId.Contains('>')) return int.Parse(spliceId.Split('>')[0]);
            return int.Parse(spliceId.Split('<')[1]);
        }
        private static int GetJunctionId(string spliceId)
        {
            string[] e12 = spliceId.Replace('<', '>').Split('>');
            int e1 = int.Parse(e12[0]);
            int e2 = int.Parse(e12[1]);
            return GetJunctionId(e1, e2);
        }
        public static int GetJunctionId(int e1, int e2)
        {
            if (e1 > e2) return (e1 << MaxExonBits) + e2;
            return (e2 << MaxExonBits) + e1;
        }

        private string MakeSpliceId(int partIdx)
        {
            int jId = junctionIds[partIdx];
            int e1 = jId >> MaxExonBits;
            int e2 = jId & rightExonMask;
            if (e1 > e2)
            {
                int t = e1; e1 = e2; e2 = t;
            }
            char sep = (realExonIds[partIdx] == e1)? '>' : '<';
            return string.Format("{0}{1}{2}", e1, sep, e2);
        }

        public override MarkResult MarkHit(int chrHitPos, int halfWidth, char strand,
                                           int bcodeIdx, int partIdx, MarkStatus markType)
        {
            int hitStart = chrHitPos - halfWidth;
            int hitEnd = chrHitPos + halfWidth;
            // Assert we have hit across splice
            if ( (hitStart % junctionSize) >= spliceFlankLen - 2 || (hitEnd % junctionSize) <= spliceFlankLen + 2 )
                return new MarkResult(AnnotType.NOHIT, this);
            int annotType = (strand == Strand) ? AnnotType.SPLC : AnnotType.ASPLC;
            if ((markType == MarkStatus.TEST_EXON_MARK_OTHER && AnnotType.IsTranscript(annotType)) //&& strand == Strand)
                || markType == MarkStatus.TEST_EXON_SKIP_OTHER)
                return new MarkResult(annotType, this);
            RealChrId = realFeature.Chr;
            RealChrOffset = offsets[partIdx];
            int realChrHitPos = chrHitPos + RealChrOffset;
            return realFeature.MarkSpliceHit(realChrHitPos, halfWidth, strand, bcodeIdx,
                                             realExonIds[partIdx], junctionIds[partIdx], markType);
        }

/*        public override MarkResult MarkHit(int chrHitPos, int halfWidth, char strand, 
                                           int bcodeIdx, int partIdx, MarkStatus markType)
        {
            int hitStart = chrHitPos - halfWidth;
            int hitEnd = chrHitPos + halfWidth;
            if ((hitStart + 3) / junctionSize != (hitEnd - 3) / junctionSize)
                return new MarkResult(AnnotType.NOHIT, this); // Assert we have hit within the junction
            int annotType = (strand == Strand) ? AnnotType.SPLC : AnnotType.ASPLC;
            if ((markType == MarkStatus.TEST_EXON_MARK_OTHER && AnnotType.IsTranscript(annotType)) //&& strand == Strand)
                || markType == MarkStatus.TEST_EXON_SKIP_OTHER)
                return new MarkResult(annotType, this);
            RealChrId = realFeature.Chr;
            RealChrOffset = offsets[partIdx];
            int realChrHitPos = chrHitPos + RealChrOffset;
            // Assert we hit across splice site.
            if ((hitStart % junctionSize) >= spliceFlankLen - 2 || (hitEnd % junctionSize) <= spliceFlankLen + 2)
            { // Now mapping is beside splice site
                return realFeature.ConvertSpliceHit(realChrHitPos, halfWidth, strand, bcodeIdx,
                                                    realExonIds[partIdx], markType);
            }
            return realFeature.MarkSpliceHit(realChrHitPos, halfWidth, strand, bcodeIdx,
                                             realExonIds[partIdx], junctionIds[partIdx], markType);
        }*/

        public override IEnumerable<FtInterval> IterIntervals()
        {
            for (int pIdx = 0; pIdx < offsets.Length; pIdx++)
            {
                int pStart = exonStarts[pIdx];
                int pEnd = pStart + spliceFlankLen - 1;
                yield return new FtInterval(pStart, pEnd, MarkHit, pIdx);
            }
            yield break;
        }

        /// <summary>
        /// Makes a refFlat file formatted string.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (spliceFlankLen == 0)
                throw new Exception("SplicedGeneFeature.spliceFlankLen is not set - internal error!");
            StringBuilder s = new StringBuilder();
            s.Append(Name + "\t\t");
            s.Append(Chr + "\t");
            s.Append(Strand + "\t");
            s.Append(Start + "\t");
            s.Append((End + 1) + "\t");
            s.Append("\t\t");
            s.Append(offsets.Length);
            s.Append("\t");
            for (int i = 0; i < exonStarts.Length; i++)
                s.Append((exonStarts[i]).ToString() + ",");
            s.Append("\t");
            for (int i = 0; i < exonStarts.Length; i++)
                s.Append((exonStarts[i] + spliceFlankLen).ToString() + ",");
            s.Append("\t");
            foreach (int offset in offsets)
                s.Append(offset.ToString() + ",");
            s.Append("\t");
            for (int i = 0; i < junctionIds.Length; i++)
                s.Append(MakeSpliceId(i) + ",");
            return s.ToString();
        }

    }
}
