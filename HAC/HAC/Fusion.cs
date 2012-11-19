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

        public static Fusion GetFusion(string fusionMethod, DistanceMetric metric)
        {
            Fusion fusion;
            switch (fusionMethod.ToLower())
            {
                case "average":
                    fusion = new AverageLinkage();
                    break;
                case "single":
                    fusion = new SingleLinkage();
                    break;
                case "complete":
                    fusion = new CompleteLinkage();
                    break;
                case "centroid":
                    fusion = new CentroidLinkage();
                    Console.WriteLine("Setting distance metric to squared Euclidian.");
                    metric = new SquaredEuclidianDistance(); // Implicit metric of centroid
                    break;
                case "ward":
                    fusion = new WardLinkage();
                    Console.WriteLine("Setting distance metric to squared Euclidian.");
                    metric = new SquaredEuclidianDistance(); // Implicit metric of Ward's
                    break;
                default:
                    throw new ArgumentException("Unknown linkage type: " + fusionMethod);

            }
            fusion.metric = metric;
            return fusion;
        }

        protected double GetSquaredEuclidianDist(object[] set1, object[] set2)
        {
            double s = 0.0;
            for (int i = 0; i < set1.Length; i++)
            {
                double d = (double)set1[i] - (double)set2[i];
                s += d * d;
            }
            return s;
        }

        protected static object[] GetClusterAverage(Cluster cluster)
        {
            int nPoints = cluster.DataPointCount;
            double[] averages = new double[nPoints];
            foreach (Element elementCluster1 in cluster)
            {
                object[] dp = elementCluster1.GetDataPoints();
                for (int i = 0; i < nPoints; i++)
                    averages[i] += (double)dp[i];
            }
            for (int i = 0; i < nPoints; i++)
                averages[i] /= cluster.ElementCount;
            return Array.ConvertAll(averages, v => (object)v);
        }

    }
}
