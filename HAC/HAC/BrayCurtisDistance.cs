using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class BrayCurtisDistance : DistanceMetric
    {
        public BrayCurtisDistance(DistanceDataFilter filter)
            : base(filter)
        { }

        public override double GetDistance(object[] set1, object[] set2)
        {
            double s1 = 0.0, s2 = 0.0;
            foreach (int i in DistanceDataFilter.IterValidIndexes(set1, set2))
            {
                s1 += Math.Abs((double)set1[i] - (double)set2[i]);
                s2 += Math.Abs((double)set1[i] + (double)set2[i]);
            }
            return s1 / s2;
        }
    }
}
