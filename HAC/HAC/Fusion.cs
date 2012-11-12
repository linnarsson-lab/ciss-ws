using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class Fusion
    {
        protected DistanceMetric metric;
        public DistanceMetric Metric
        {
            set { metric = value; }
        }
        public abstract double CalculateDistance(Cluster cluster1, Cluster cluster2);

        public static Fusion GetFusion(string fusionMethod)
        {
            switch (fusionMethod.ToLower())
            {
                case "average":
                    return new AverageLinkage();
                case "single":
                    return new SingleLinkage();
                case "complete":
                    return new CompleteLinkage();
                case "centroid":
                    return new CentroidLinkage();
                default:
                    throw new ArgumentException("Unknown linkage type: " + fusionMethod);
            }
        }
    }
}
