using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class AverageLinkage : Fusion
    {
        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            double sumDistance = 0.0;
            foreach (Element elementCluster1 in cluster1)
            {
                foreach (Element elementCluster2 in cluster2)
                {
                    sumDistance += metric.GetDistance(elementCluster1.GetDataPoints(), elementCluster2.GetDataPoints());
                }
            }
            return sumDistance / (cluster1.ElementCount * cluster2.ElementCount);
        }
    }
}
