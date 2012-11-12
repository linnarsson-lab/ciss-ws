using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class CentroidLinkage : Fusion
    {
        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            object[] averages1 = GetClusterAverage(cluster1);
            object[] averages2 = GetClusterAverage(cluster2);
            return metric.GetDistance(averages1, averages2);
        }

        private static object[] GetClusterAverage(Cluster cluster)
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
