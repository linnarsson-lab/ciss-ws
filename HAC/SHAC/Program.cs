﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using HAC;

namespace SHAC
{
    class Program
    {
        static void Main(string[] args)
        {
            int nFinalClusters = 1;
            Fusion fusion;
            DistanceMetric distanceMetric;
            Neighborhood neighborhood;
            string neighborhoodName = "queen";
            string exprFile = null;
            try
            {
                if (args == null || args.Length < 4)
                    throw new ArgumentException("");
                string distanceMethod = "euclidian";
                string linkageMethod = "average";
                for (int argIdx = 0; argIdx < args.Length; argIdx++)
                {
                    switch (args[argIdx])
                    {
                        case "-d":
                            distanceMethod = args[++argIdx];
                            break;
                        case "-l":
                            linkageMethod = args[++argIdx];
                            break;
                        case "-c":
                            neighborhoodName = args[++argIdx];
                            break;
                        default:
                            exprFile = args[argIdx];
                            if (exprFile.StartsWith("-"))
                                throw new ArgumentException("Unknown option: " + exprFile);
                            break;
                    }
                }
                if (!File.Exists(exprFile))
                    throw new ArgumentException("Input file does not exist: " + exprFile);
                fusion = Fusion.GetFusion(linkageMethod);
                distanceMetric = DistanceMetric.GetDistanceMetric(distanceMethod);
                neighborhood = Neighborhood.GetNeighborhood(neighborhoodName);
            }
            catch (ArgumentException e)
            {
                if (e.Message != null && e.Message != "")
                    Console.WriteLine(e.Message);
                Console.WriteLine("Usage:\nSHAC -d DISTANCEMETHOD -l LINKAGEMETHOD -c NEIGHBORHOOD EXPRFILE");
                Console.WriteLine("DISTANCEMETHOD is one of 'euclidian', 'manhattan', 'chebyshev', 'canberra', 'braycurtis', 'ess'");
                Console.WriteLine("LINKAGEMETHOD is one of 'single', 'complete', 'average', 'centroid'");
                Console.WriteLine("NEIGHBORHOOD is one of 'queen', 'rook'");
                Console.WriteLine("EXPRFILE contains a table of data. The first line contains the name of each sample.\n" +
                                  "   The second and third lines have to contain the X and Y coordinate, respectively,\n" +
                                  "   of each sample. Gene names are in column one, samples start by column two.");
                return;
            }
            Element[] elements = SpatialExpressionFileParser.Parse(exprFile, neighborhood);
            HAC.HAC hac = new HAC.HAC(elements, fusion, distanceMetric);
            ClusterResult result = hac.Cluster(nFinalClusters);
            WriteFinalClusters(result);
            WriteNodeList(result);
            Console.WriteLine("\nPress Enter to exit");
            Console.In.Read();
        }

        private static void WriteNodeList(ClusterResult result)
        {
            int nIdx = 1;
            foreach (ClusterNode node in result.nodes)
            {
                Console.Write("Node {0}: ", nIdx++);
                if (node.pair.Cluster1.ElementCount == 1)
                    Console.Write("Elem" + node.pair.Cluster1.GetElements()[0].Id);
                else
                    Console.Write(node.pair.Cluster1.Id);
                Console.Write(" - " + node.pair.Distance + " - ");
                if (node.pair.Cluster2.ElementCount == 1)
                    Console.WriteLine("Elem" + node.pair.Cluster2.GetElements()[0].Id);
                else
                    Console.WriteLine(node.pair.Cluster2.Id);
            }
        }

        private static void WriteFinalClusters(ClusterResult result)
        {
            Console.WriteLine("Final cluster(s):");
            int i = 1;
            foreach (Cluster c in result.clusters)
            {
                Console.WriteLine("Cluster {0}: ", i++);
                foreach (Element e in c)
                    Console.Write(e.Id + " ");
                Console.WriteLine();
            }
        }
    }
}
