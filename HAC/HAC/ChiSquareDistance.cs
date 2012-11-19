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
            return Math.Sqrt(set1.Cast<double>().Sum() - set2.Cast<double>().Sum());
        }
    }
}
