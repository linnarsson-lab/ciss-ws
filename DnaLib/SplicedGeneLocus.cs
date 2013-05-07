using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class SplicedGeneFeature : LocusFeature, TranscriptFeature
    {
        public override IFeature RealFeature { get { return realFeature; } }

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

        //public override MarkResult MarkHit(MappedTagItem item, int partIdx, MarkStatus markType)
        public override int MarkHit(MappedTagItem item, int partIdx, MarkStatus markType)
        {
            item.splcToRealChrOffset = offsets[partIdx];
            if (item.HitMidPos < realFeature.Start || item.HitMidPos > realFeature.End)
                Console.WriteLine("ERROR in SplicedGeneLocus.MarkHit: PartIdx=" + partIdx + " offset=" + offsets[partIdx] + " Strand=" + Strand + 
                                  "\n  Gene=" + realFeature.Name + " Start=" + realFeature.Start + " End=" + realFeature.End + 
                                  " LocusStart= + " + realFeature.LocusStart + " LocusEnd=" + realFeature.LocusEnd +
                                  "\n  " + item.ToString());
            return realFeature.MarkSpliceHit(item, realExonIds[partIdx], junctionIds[partIdx], markType);
        }

        public override IEnumerable<FtInterval> IterIntervals()
        {
            for (int partIdx = 0; partIdx < offsets.Length; partIdx++)
            {
                int pStart = exonStarts[partIdx];
                int pEnd = exonEnds[partIdx];
                FtInterval ivl = new FtInterval(pStart, pEnd, MarkHit, partIdx, this, AnnotType.SPLC, Strand);
                yield return ivl;
            }
            yield break;
        }

        public int GetTranscriptPos(int hitMidPos, int extraData)
        {
            return realFeature.GetTranscriptPos(hitMidPos + offsets[extraData]);
        }
    }
}
