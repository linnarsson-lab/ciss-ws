using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    internal class ClusterPairs
    {
        HashSet<ClusterPair> pairs = new HashSet<ClusterPair>();

        internal ClusterPair LowestDistancePair
        {
            get 
            {
                ClusterPair lowestDistancePair = null;
                foreach (ClusterPair pair in pairs)
                    if (lowestDistancePair == null || lowestDistancePair.Distance > pair.Distance)
                        lowestDistancePair = pair;
                return lowestDistancePair;
            }
        }

        internal int Count
        {
            get { return pairs.Count; }
        }

        internal void AddPair(ClusterPair pair)
        {
            pairs.Add(pair);
        }

        internal void RemovePairsByOldClusters(Cluster cluster1, Cluster cluster2)
        {
            List<ClusterPair> toRemove = new List<ClusterPair>();
            foreach(ClusterPair pair in pairs)
            {
                if (pair.HasCluster(cluster1) || pair.HasCluster(cluster2))
                {
                    toRemove.Add(pair);
                }
            }
            foreach (ClusterPair pair in toRemove)
            {
                pairs.Remove(pair);
            }
        }
    }
}
