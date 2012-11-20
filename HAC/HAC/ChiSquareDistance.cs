using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    /// <summary>
    /// Calculates distance for count data as Chi Square. Expects ChiSqTransform to have been used
    /// </summary>
    class ChiSquareDistance : DistanceMetric
    {
        public override double GetDistance(object[] set1, object[] set2)
        {
            double s = 0.0;
            for (int i = 0; i < set1.Length; i++)
            {
                double v1 = (double)set1[i];
                double v2 = (double)set2[i];
                if (!double.IsNaN(v1) && !double.IsNaN(v2))
                    s += v1 + v2;
            }
            return Math.Sqrt(s);
        }
    }
}
