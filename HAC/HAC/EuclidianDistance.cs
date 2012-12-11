using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class EuclidianDistance : DistanceMetric
    {
        public EuclidianDistance(DistanceDataFilter filter)
            : base(filter)
        { }

        public override double GetDistance(object[] set1, object[] set2)
        {
            double s = 0.0;
            foreach (int i in DistanceDataFilter.IterValidIndexes(set1, set2))
            {
                double d = (double)set1[i] - (double)set2[i];
                s += d * d;
            }
            return Math.Sqrt(s);
        }
    }
}
