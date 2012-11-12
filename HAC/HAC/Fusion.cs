using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class Fusion
    {
        protected IDistanceMetric metric;
        public IDistanceMetric Metric
        {
            set { metric = value; }
        }
        public abstract double CalculateDistance(Cluster cluster1, Cluster cluster2);
    }
}
