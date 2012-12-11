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
            bool debug = false;
            int nFinalClusters = 1;
            Fusion fusion;
            string transformationMethod = "";
            string distanceMethod = "euclidian";
            string linkageMethod = "average";
            string dataFilterMethod = "";
            DistanceMetric distanceMetric;
            Neighborhood neighborhood;
            Transformation transformation;
            DistanceDataFilter distanceDataFilter;
            string neighborhoodName = "queen";
            string exprFile = null;
            try
            {
                if (args == null || args.Length < 4)
                    throw new ArgumentException("");
                for (int argIdx = 0; argIdx < args.Length; argIdx++)
                {
                    switch (args[argIdx])
                    {
                        case "--debug":
                            debug = true;
                            break;
                        case "-t":
                            transformationMethod = args[++argIdx];
                            break;
                        case "-d":
                            distanceMethod = args[++argIdx];
                            break;
                        case "-f":
                            dataFilterMethod = args[++argIdx];
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
                if (distanceMethod == "chisq" && transformationMethod != "chisq")
                    Console.WriteLine("WARNING: Note that 'chisq' distance requires 'chisq' transformation for correct results.");
                transformation = Transformation.GetTransformation(transformationMethod);
                string filterParam = null;
                if (dataFilterMethod.Contains('/'))
                {
                    dataFilterMethod = dataFilterMethod.Split('/')[0];
                    filterParam = dataFilterMethod.Split('/')[1];
                }
                distanceDataFilter = DistanceDataFilter.GetDistanceDataFilter(dataFilterMethod, filterParam);
                distanceMetric = DistanceMetric.GetDistanceMetric(distanceMethod, distanceDataFilter);
                fusion = Fusion.GetFusion(linkageMethod, distanceMetric);
                neighborhood = Neighborhood.GetNeighborhood(neighborhoodName);
            }
            catch (ArgumentException e)
            {
                if (e.Message != null && e.Message != "")
                    Console.WriteLine(e.Message);
                Console.WriteLine("Usage:\nmono SHAC.exe [--debug] [-t TRANSFORMATION] [-f DATAFILTER] -d DISTANCEMETHOD -l LINKAGEMETHOD -c NEIGHBORHOOD EXPRFILE");
                Console.WriteLine("TRANSFORMATION is 'score' or 'chisq'. Note that chisq distance for count data requires the transformation.");
                Console.WriteLine("DATAFILTER is 'commonthreshold/F'. Only data points where both sample's values >= F will be used for distance.");
                Console.WriteLine("DISTANCEMETHOD is 'chisq', 'euclidian', 'manhattan', 'chebyshev', 'canberra', 'braycurtis', 'ess' or 'sqeuclidian'");
                Console.WriteLine("LINKAGEMETHOD is 'single', 'complete', 'average', 'centroid' or 'ward'");
                Console.WriteLine("NEIGHBORHOOD is 'queen' or 'rook'");
                Console.WriteLine("EXPRFILE contains a table of data. The first line contains the name of each sample.\n" +
                                  "   The second and third lines have to contain the X and Y coordinate, respectively,\n" +
                                  "   of each sample. Gene names are in column one, samples start by column two.");
                return;
            }
            Element[] elements = SpatialExpressionFileParser.Parse(exprFile, neighborhood);
            transformation.Transform(elements);
            HAC.HAC hac = new HAC.HAC(elements, fusion);
            hac.debug = debug;
            ClusterResult result = hac.SpatialCluster(nFinalClusters);
            string t = (transformationMethod != "") ? (", " + transformationMethod + "-transformed") : "";
            Console.WriteLine("Spatial clustering of {0} [{1} cells X {2} values{3}].\nDistance method: {4}, linkage method: {5}.",
                               exprFile, elements.Length, elements[0].DataPointCount, t, distanceMethod, linkageMethod);
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
                Console.Write("Cluster{0}: ", nIdx++);
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
            Console.WriteLine("Order of fusions:");
            int i = 1;
            foreach (Cluster c in result.GetFusionList())
            {
                Console.WriteLine("{0}. {1}:", i++, c.Id);
                foreach (Element e in c)
                    Console.Write(" " + e.Id);
                Console.WriteLine();
            }
        }

        private static string MakePHYLIP(ClusterResult result)
        {
            List<Cluster> fusionList = result.GetFusionList();
            StringBuilder sb = new StringBuilder();
            Cluster topCluster = fusionList[fusionList.Count - 1];
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
