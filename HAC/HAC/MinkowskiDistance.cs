using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class MinkowskiDistance : DistanceMetric
    {
        private double p;

        public MinkowskiDistance(double p) : base()
        {
            this.p = p;
        }
        public MinkowskiDistance(double p, DistanceDataFilter filter)
            : base(filter)
        {
            this.p = p;
        }

        public override double GetDistance(object[] set1, object[] set2)
        {
            double s = 0.0;
            foreach (int i in DistanceDataFilter.IterValidIndexes(set1, set2))
            {
                double d = Math.Abs((double)set1[i] - (double)set2[i]);
                s += Math.Pow(d, p);
            }
            return Math.Pow(s, 1.0 / p);
        }
    }
}
