using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class HAC
    {
        public bool debug = true;

        List<Element> elements = new List<Element>();
        ClusterPairs pairs = new ClusterPairs();
        Fusion fusion;
        DistanceMetric metric;

        /// <summary>
        /// Creates a new HAC object that uses single-linkage as fusion function and the Jaccard index as distance metric 
        /// to cluster the specified elements.
        /// </summary>
        /// <param name="elements"></param>
        public HAC(Element[] elements)
        {
            setElements(elements);
            this.fusion = new SingleLinkage();
            this.metric = new JaccardDistance();
            this.fusion.Metric = metric;
        }

        /// <summary>
        /// Creates a new HAC object to cluster the specified elements with the specified fusion and 
        /// metric function.
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="fusion"></param>
        /// <param name="metric"></param>
        public HAC(Element[] elements, Fusion fusion)
        {
            setElements(elements);
            this.fusion = fusion;
        }

        private void setElements(Element[] elements)
        {
            this.elements.AddRange(elements);
        }

        /// <summary>
        /// Creates countCluster clusters of the elements specified in the constructor.
        /// </summary>
        /// <param name="countCluster"></param>
        /// <returns></returns>
        public ClusterResult Cluster(int countCluster)
        {
            ClusterResult result = new ClusterResult();
            HashSet<Cluster> clusters = new HashSet<Cluster>();
            
            // 1. Initialize each element as a cluster
            foreach (Element el in elements)
            {
                Cluster cl = new Cluster(this.fusion, el);
                clusters.Add(cl);
            }

            // Return if the element (cluster) count is lower than countCluster
            if (clusters.Count <= countCluster)
                return result;

            // 2. Calculate the distances of all clusters to all other clusters
            foreach (Cluster cl1 in clusters)
            {
                foreach (Cluster cl2 in clusters)
                {
                    if (cl1 == cl2)
                        continue;
                    ClusterPair pair = new ClusterPair(cl1, cl2, cl1.CalculateDistance(cl2));
                    pairs.AddPair(pair);
                }
            }

            // 3. Merge clusters to new clusters and recalculate distances in a loop until there are only countCluster clusters
            while (clusters.Count > countCluster)
            {
                // a) Merge: Create a new cluster and add the elements of the two old clusters                
                ClusterPair lowestDistancePair = pairs.LowestDistancePair;
                Cluster newCluster = new Cluster(this.fusion);
                //newCluster.AddElements(lowestDistancePair.Cluster1.GetElements());
                newCluster.AddCluster(lowestDistancePair.Cluster1);
                //newCluster.AddElements(lowestDistancePair.Cluster2.GetElements());
                newCluster.AddCluster(lowestDistancePair.Cluster2);
                newCluster.ChildrenDistance = lowestDistancePair.Distance;
                // b)Remove the two old clusters from clusters
                clusters.Remove(lowestDistancePair.Cluster1);
                clusters.Remove(lowestDistancePair.Cluster2);
                // c) Remove the two old clusters from pairs
                pairs.RemovePairsByOldClusters(lowestDistancePair.Cluster1, lowestDistancePair.Cluster2);
                result.AddPair(lowestDistancePair);

                // d) Calculate the distance of the new cluster to all other clusters and save each as pair
                foreach (Cluster cluster in clusters)
                {
                    ClusterPair pair = new ClusterPair(cluster, newCluster, cluster.CalculateDistance(newCluster));
                    pairs.AddPair(pair);
                }
                // e) Add the new cluster to clusters
                clusters.Add(newCluster);
                result.AddNextFusion(newCluster);
            }
            return result;
        }

        /// <summary>
        /// Creates countCluster clusters of the elements specified in the constructor,
        /// taking the spatial layout in account as a restriction on which elements that can fuse.
        /// </summary>
        /// <param name="countCluster"></param>
        /// <returns></returns>
        public ClusterResult SpatialCluster(int countCluster)
        {
            ClusterResult result = new ClusterResult();
            HashSet<Cluster> clusters = new HashSet<Cluster>();

            // 1. Initialize each element as a cluster
            foreach (Element el in elements)
            {
                Cluster cl = new Cluster(this.fusion, el);
                el.InitialCluster = cl;
                clusters.Add(cl);
            }
            foreach (Cluster c in clusters)
                c.InitNeighborsFromElements();

            // Return if the element (cluster) count is lower than countCluster
            if (clusters.Count <= countCluster)
                return result;

            if (debug)
                Console.WriteLine("----- Initial elements: -----");
            // 2. Calculate the distances of neighboring clusters to each other
            foreach (Cluster cl1 in clusters)
            {
                if (debug)
                    Console.WriteLine(cl1.ToString());
                foreach (Cluster cl2 in clusters)
                {
                    if (cl1 != cl2 && cl1.HasNeighbor(cl2))
                    {
                        ClusterPair pair = new ClusterPair(cl1, cl2, cl1.CalculateDistance(cl2));
                        pairs.AddPair(pair);
                    }
                }
            }

            // 3. Merge clusters to new clusters and recalculate distances in a loop until there are only countCluster clusters
            while (clusters.Count > countCluster)
            {
                // a) Merge: Create a new cluster and add the elements of the two old clusters                
                ClusterPair lowestDistancePair = pairs.LowestDistancePair;
                Cluster newCluster = new Cluster(this.fusion);
                newCluster.FromPair(lowestDistancePair);
                // b)Remove the two old clusters from clusters and adjust the neighbors of clusters
                foreach (Cluster cl in clusters)
                {
                    cl.UpdateNeighbors(newCluster, lowestDistancePair.Cluster1, lowestDistancePair.Cluster2);
                }
                clusters.Remove(lowestDistancePair.Cluster1);
                clusters.Remove(lowestDistancePair.Cluster2);
                // c) Remove the two old clusters from pairs
                pairs.RemovePairsByOldClusters(lowestDistancePair.Cluster1, lowestDistancePair.Cluster2);
                result.AddPair(lowestDistancePair);

                // d) Calculate the distance of the new cluster to all other clusters and save each as pair
                foreach (Cluster cluster in clusters)
                {
                    if (newCluster.HasNeighbor(cluster))
                    {
                        ClusterPair pair = new ClusterPair(cluster, newCluster, cluster.CalculateDistance(newCluster));
                        pairs.AddPair(pair);
                    }
                }
                // e) Add the new cluster to clusters
                clusters.Add(newCluster);
                result.AddNextFusion(newCluster);
                if (debug)
                {
                    Console.WriteLine("----- Cluster forming at next fusion: -----");
                    Console.WriteLine(newCluster.ToString());
                }
            }
            return result;
        }

    }
}
