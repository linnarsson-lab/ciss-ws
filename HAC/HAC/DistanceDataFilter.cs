using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class DistanceDataFilter
    {
        abstract public IEnumerable<int> IterValidIndexes(object[] set1, object[] set2);

        public static DistanceDataFilter GetDistanceDataFilter(string filterMethod, object param)
        {
            switch (filterMethod)
            {
                case "commonthreshold":
                    return new CommonThresholdDataFilter(param);
                default:
                    return new NullDistanceDataFilter();
            }
        }
    }
}
