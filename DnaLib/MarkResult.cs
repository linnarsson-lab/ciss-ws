using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public enum MarkStatus
    { TEST_EXON_MARK_OTHER, TEST_EXON_SKIP_OTHER, SINGLE_MAPPING, ALT_MAPPINGS, MARK_ALT_MAPPINGS };

    public struct MarkResult
    {
        public int annotType;
        public IFeature feature;
        //public List<Pair<int, GeneFeature>> AltGfPositions; // Alternative mapping genes and their chr positions
        public MarkResult(int annotType, IFeature feature)
        {
            this.annotType = annotType;
            this.feature = feature;
            //this.AltGfPositions = null;
        }
    }
    public delegate MarkResult DoMarkHit(int chrHitPos, int halfWidth, char strand,
                                         int bcodeIdx, int otherIdx, MarkStatus markType);

}
