﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class ClusterNode
    {
        public ClusterPair pair;
        public ClusterNode left;
        public ClusterNode right;

        public ClusterNode(ClusterPair pair)
        {
            this.pair = pair;
        }
        public bool HasCluster(Cluster cluster)
        {
            return pair.HasCluster(cluster);
        }

    }

    public class ClusterResult
    {
        public List<ClusterNode> nodes = new List<ClusterNode>();
        public HashSet<Cluster> clusters = new HashSet<Cluster>();

        public void AddPair(ClusterPair pair)
        {
            ClusterNode newNode = new ClusterNode(pair);
            foreach (ClusterNode node in nodes)
            {
                if (node.HasCluster(pair.Cluster1))
                    newNode.left = node;
                else if (node.HasCluster(pair.Cluster2))
                    newNode.right = node;
            }
            nodes.Add(newNode);
        }

        public void SetClusters(HashSet<Cluster> clusters)
        {
            this.clusters = clusters;
        }

        public Cluster[] GetClusters()
        {
            return clusters.ToArray<Cluster>();
        }
    }
}
