using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public enum MarkStatus
    { NONEXONIC_MAPPING, UNIQUE_EXON_MAPPING, NONUNIQUE_EXON_MAPPING };

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

    public delegate int NewMarkHit(MappedTagItem mappedTagItem, int otherIdx, MarkStatus markType);

}
