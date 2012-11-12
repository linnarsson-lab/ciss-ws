using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HAC;

namespace SHAC
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 1 || args[0].StartsWith("-h") || args[0] == "--help")
            {
                Console.WriteLine("Usage:\nSHAC -d DISTANCEMETHOD -l LINKAGEMETHOD -c NEIGHBORHOOD EXPRFILE");
                Console.WriteLine("DISTANCEMETHOD is one of 'Euclidian', 'Manhattan', 'Chebyshev', 'Canberra', 'BrayCurtis'");
                Console.WriteLine("LINKAGEMETHOD is one of 'Single', 'Complete', 'Average', 'Centroid'");
                Console.WriteLine("NEIGHBORHOOD is one of 'Queen', 'Rook'");
                Console.WriteLine("EXPRFILE is a table of data. The first and second lines have to contain\n" +
                                  "   the X and Y coordinate, respectively, of each sample (sample 1 in column 2, etc.).");
                return;
            }
        }
    }
}
