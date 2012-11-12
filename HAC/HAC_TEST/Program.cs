using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HAC;

namespace HAC_TEST
{
    class Program
    {
        static void Main(string[] args)
        {
            Element e1 = new Element("1", new object[] { "a1", "a2", "a3", "a4", "a5" });
            Element e2 = new Element("2", new object[] { "b1", "b2", "b3", "b4", "b5" });
            Element e3 = new Element("3", new object[] { "a1", "a2", "a3", "a6", "a7" });
            Element e4 = new Element("4", new object[] { "a1", "a2", "a3", "a4", "a6" }); // similar to element 1 and 3
            Element e5 = new Element("5", new object[] { "b1", "b2", "b3", "c1", "c2" }); // similar to element 2

            HAC.HAC hac = new HAC.HAC(new Element[] { e1, e2, e3, e4, e5 });
            // Use public HAC(Element[] elements, Fusion fusion, IDistanceMetric metric) for other
            // fusion and distance functions than single-linkage and jaccard index.
            // Use single value arrays or the Element.add(...) method if you want to cluster single values.

            var clusters = hac.Cluster(2).GetClusters();

            for (int i = 0; i < clusters.Count(); i++)
            {
                Console.WriteLine("---Cluster " + (i + 1) + "---");
                foreach (Element e in clusters[i])
                    Console.Write("Element " + e.Id + " ");
                Console.WriteLine(string.Empty);
            }

            Console.WriteLine("\nPress Enter to exit");
            Console.In.Read();
        }
    }
}
