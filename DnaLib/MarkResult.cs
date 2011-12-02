using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public enum MarkStatus
    { TEST_EXON_MARK_OTHER, TEST_EXON_SKIP_OTHER, UNIQUE_EXON_MAPPING, NONUNIQUE_EXON_MAPPING, MARK_ALT_MAPPINGS };

    public struct MarkResult
    {
        public int annotType;
        public IFeature feature;
        public MarkResult(int annotType, IFeature feature)
        {
            this.annotType = annotType;
            this.feature = feature;
        }
    }
    public delegate MarkResult DoMarkHit(int chrHitPos, int halfWidth, char strand,
                                         int bcodeIdx, int otherIdx, MarkStatus markType);
    
    public delegate MarkResult NewMarkHit(MappedTagItem mappedTagItem, int otherIdx, MarkStatus markType);

}
