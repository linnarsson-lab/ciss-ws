using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class ClusterPair
    {
        Cluster cluster1;
        Cluster cluster2;
        double distance;

        public Cluster Cluster1
        {
            get { return cluster1; }
        }

        public Cluster Cluster2
        {
            get { return cluster2; }
        }

        public double Distance
        {
            get { return distance; }
        }

        public ClusterPair(Cluster cluster1, Cluster cluster2, double distance)
        {
            this.cluster1 = cluster1;
            this.cluster2 = cluster2;
            this.distance = distance;
        }

        internal bool HasCluster(Cluster cluster)
        {
            return cluster1 == cluster || cluster2 == cluster;
        }

    }
}
