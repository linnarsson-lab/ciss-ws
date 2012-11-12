using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class SingleLinkage : Fusion
    {
        private const double INITIAL_LEAST_DISTANCE = double.MaxValue;

        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            double leastDistance = INITIAL_LEAST_DISTANCE;
            foreach (Element elementCluster1 in cluster1)
            {
                foreach (Element elementCluster2 in cluster2)
                {
                    double distance = metric.GetDistance(elementCluster1.GetDataPoints(), elementCluster2.GetDataPoints());
                    if (distance < leastDistance)
                        leastDistance = distance;
                }
            }
            return leastDistance;
        }
    }
}
