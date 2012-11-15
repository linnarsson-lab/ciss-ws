using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class WardLinkage : Fusion
    {
        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            object[] averages1 = GetClusterAverage(cluster1);
            object[] averages2 = GetClusterAverage(cluster2);
            double f =  (cluster1.ElementCount * cluster2.ElementCount) / (cluster1.ElementCount + cluster2.ElementCount);
            return f * metric.GetDistance(averages1, averages2);
        }

    }
}
