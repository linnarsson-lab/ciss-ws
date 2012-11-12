using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class CompleteLinkage : Fusion
    {
        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            double maxDistance = 0.0;
            foreach (Element elementCluster1 in cluster1)
            {
                foreach (Element elementCluster2 in cluster2)
                {
                    double distance = metric.GetDistance(elementCluster1.GetDataPoints(), elementCluster2.GetDataPoints());
                    if (distance > maxDistance)
                        maxDistance = distance;
                }
            }
            return maxDistance;
        }
    }
}
