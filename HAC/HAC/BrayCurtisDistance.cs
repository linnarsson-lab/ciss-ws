using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class BrayCurtisDistance : DistanceMetric
    {
        public override double GetDistance(object[] set1, object[] set2)
        {
            double s1 = 0.0, s2 = 0.0;
            for (int i = 0; i < set1.Length; i++)
            {
                s1 += Math.Abs((double)set1[i] - (double)set2[i]);
                s2 += Math.Abs((double)set1[i] + (double)set2[i]);
            }
            return s1 / s2;
        }
    }
}
