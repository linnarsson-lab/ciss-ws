using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class NullDistanceDataFilter : DistanceDataFilter
    {
        public override IEnumerable<int> IterValidIndexes(object[] set1, object[] set2)
        {
            for (int idx = 0; idx < set1.Length; idx++)
                yield return idx;
        }
    }
}
