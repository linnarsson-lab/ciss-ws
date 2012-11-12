using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class CanberraDistance : IDistanceMetric
    {
        public double GetDistance(object[] set1, object[] set2)
        {
            double snum = 0.0, sdenom_u = 0.0, sdenom_v = 0.0;
            for (int i = 0; i < set1.Length; i++)
            {
                snum += Math.Abs((double)set1[i] - (double)set2[i]);
                sdenom_u += Math.Abs((double)set1[i]);
                sdenom_v += Math.Abs((double)set2[i]);
            }
            return snum / (sdenom_u + sdenom_v);
        }
    }
}
