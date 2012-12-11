using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class CommonThresholdDataFilter : DistanceDataFilter
    {
        private double threshold = 0.0;
        public CommonThresholdDataFilter(object param)
        {
            if (param.GetType() == typeof(string))
                this.threshold = double.Parse((string)param);
            else
                this.threshold = (double)param;
        }
        public override IEnumerable<int> IterValidIndexes(object[] set1, object[] set2)
        {
            for (int idx = 0; idx < set1.Length; idx++)
                if ((double)set1[idx] >= threshold && (double)set2[idx] >= threshold)
                    yield return idx;
        }
    }
}
