using System;
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
                distanceMetric = DistanceMetric.GetDistanceMetric(distanceMethod);
                fusion = Fusion.GetFusion(linkageMethod, distanceMetric);
                neighborhood = Neighborhood.GetNeighborhood(neighborhoodName);
            }
            catch (ArgumentException e)
            {
                if (e.Message != null && e.Message != "")
                    Console.WriteLine(e.Message);
                Console.WriteLine("Usage:\nmono SHAC.exe -d DISTANCEMETHOD -l LINKAGEMETHOD -c NEIGHBORHOOD EXPRFILE");
                Console.WriteLine("DISTANCEMETHOD is one of 'euclidian', 'manhattan', 'chebyshev', 'canberra', 'braycurtis', 'ess'");
                Console.WriteLine("LINKAGEMETHOD is one of 'single', 'complete', 'average', 'centroid'");
                Console.WriteLine("NEIGHBORHOOD is one of 'queen', 'rook'");
                Console.WriteLine("EXPRFILE contains a table of data. The first line contains the name of each sample.\n" +
                                  "   The second and third lines have to contain the X and Y coordinate, respectively,\n" +
                                  "   of each sample. Gene names are in column one, samples start by column two.");
                return;
            }
            Element[] elements = SpatialExpressionFileParser.Parse(exprFile, neighborhood);
            HAC.HAC hac = new HAC.HAC(elements, fusion);
            ClusterResult result = hac.Cluster(nFinalClusters);
            if (nFinalClusters > 1)
                WriteFinalClusters(result);
            WriteNodeList(result);
            using (StreamWriter writer = new StreamWriter("intree"))
            {
                writer.WriteLine(MakePHYLIP(result));
            }
            Console.WriteLine("Wrote PHYLIP tree for use with drawgram.exe to file 'intree'");
            Console.WriteLine("\nPress Enter to exit");
            Console.In.Read();
        }

        private static void WriteNodeList(ClusterResult result)
        {
            int nIdx = 1;
            foreach (ClusterNode node in result.nodes)
            {
                Cluster c1 = node.pair.Cluster1;
                Cluster c2 = node.pair.Cluster2;
                Console.Write("Node {0}: ", nIdx++);
                if (c2.ElementCount == 1)
                    WriteNodeLine(c2, c1, node.pair.Distance);
                else
                    WriteNodeLine(c1, c2, node.pair.Distance);
            }
        }

        private static void WriteNodeLine(Cluster c1, Cluster c2, double distance)
        {
            if (c1.ElementCount == 1)
                Console.Write(c1.GetElements()[0].Id);
            else
                Console.Write(c1.Id);
            Console.Write(" - " + distance + " - ");
            if (c2.ElementCount == 1)
                Console.WriteLine(c2.GetElements()[0].Id);
            else
                Console.WriteLine(c2.Id);
        }

        private static void WriteFinalClusters(ClusterResult result)
        {
            Console.WriteLine("Final top clusters:");
            int i = 1;
            foreach (Cluster c in result.GetTopClusters())
            {
                Console.WriteLine("Cluster {0}: ", i++);
                foreach (Element e in c)
                    Console.Write(e.Id + " ");
                Console.WriteLine();
            }
        }

        private static string MakePHYLIP(ClusterResult result)
        {
            if (result.GetTopClusters().Count > 1)
            {
                Console.WriteLine("Can only make PHYLIP output when there is one single top cluster.");
                return "";
            }
            StringBuilder sb = new StringBuilder();
            Cluster topCluster = result.GetTopClusters()[0];
            AddToPHYLIP(topCluster, sb);
            sb.Append(';');
            return sb.ToString();
        }

        private static void AddToPHYLIP(Cluster c, StringBuilder sb)
        {
            sb.Append('(');
            bool firstChild = true;
            foreach (Cluster child in c.Children)
            {
                if (!firstChild)
                    sb.Append(',');
                firstChild = false;
                if (child.IsLeaf)
                    sb.Append(string.Format("{0}:{1:0.000}", child.GetElements()[0].Id, c.ChildrenDistance));
                else
                {
                    AddToPHYLIP(child, sb);
                    sb.Append(string.Format(":{0:0.000}", c.ChildrenDistance - child.ChildrenDistance));
                }
            }
            sb.Append(')');
        }
    }

}
