using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class SplicedGeneFeature : LocusFeature
    {
        private int[] offsets;
        /// <summary>
        /// Symbolic strings representing each junction, e.g. '2-3-6'
        /// </summary>
        private string[] junctionIds;
        /// <summary>
        /// The 1-based biological exon numbers on the real chromosome
        /// </summary>
        private int[] realExonIds;
        /// <summary>
        /// Start positions in junction chromosome.
        /// </summary>
        private int[] exonStarts;
        /// <summary>
        /// End positions (actual last pos) in junction chromosome.
        /// </summary>
        private int[] exonEnds;
        private GeneFeature realFeature;

        public SplicedGeneFeature(string name, string chr, char strand, int[] exonStarts, int[] exonEnds,
                                int[] offsets, int[] realExonIds, string[] spliceStrings)
            : base(name, chr, strand, exonStarts[0], exonEnds.Max())
        {
            this.exonStarts = exonStarts;
            this.exonEnds = exonEnds;
            this.offsets = offsets;
            this.realExonIds = realExonIds;
            junctionIds = spliceStrings;
        }

        public void BindToRealFeature(GeneFeature actualFeature)
        {
            realFeature = actualFeature;
            realFeature.SpliceLen = End - Start;
        }

        public override MarkResult MarkHit(int chrHitPos, int halfWidth, char strand,
                                           int bcodeIdx, int partIdx, MarkStatus markType)
        {
            int annotType = (strand == Strand) ? AnnotType.SPLC : AnnotType.ASPLC;
            if ((markType == MarkStatus.TEST_EXON_MARK_OTHER && AnnotType.IsTranscript(annotType))
                || markType == MarkStatus.TEST_EXON_SKIP_OTHER)
                return new MarkResult(annotType, this);
            int realChrHitPos = offsets[partIdx] + chrHitPos;
            if (realChrHitPos < realFeature.LocusStart || realChrHitPos > realFeature.LocusEnd)
                Console.WriteLine("Error");
            return realFeature.MarkSpliceHit(realChrHitPos, halfWidth, strand, bcodeIdx,
                                             realExonIds[partIdx], junctionIds[partIdx], markType);
        }

        public override IEnumerable<FtInterval> IterIntervals()
        {
            for (int pIdx = 0; pIdx < offsets.Length; pIdx++)
            {
                int pStart = exonStarts[pIdx];
                int pEnd = exonEnds[pIdx];
                yield return new FtInterval(pStart, pEnd, MarkHit, pIdx);
            }
            yield break;
        }

    }
}
