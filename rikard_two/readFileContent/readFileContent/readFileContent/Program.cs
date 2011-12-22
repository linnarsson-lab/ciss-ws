using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace readFileContent
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                usage();
                return(1);
            }

            string path = args[0];

            if (Directory.Exists(path))
            {
                Console.WriteLine(Environment.NewLine + "\tThe '" + path + "' is set as root.");
            }
            else
            {
                Console.WriteLine(Environment.NewLine + "\tFolder '" + path + "' not found!");
                return(2);
            }

            string[] TheBams = new string[0];
            Console.WriteLine();

            try
            {
                // Only get certain subdirectories 
                string[] dirs = Directory.GetDirectories(@path, "Project_*_*");
                Console.WriteLine("\tThe number of directories starting with 'Project' is {0}.", dirs.Length);
                TheBams = new string[dirs.Length];
                int i = 0;
                foreach (string dir in dirs)
                {
                    Console.WriteLine("\t" + dir);
                    if (File.Exists(dir + "/genome/bam/sorted.bam"))
                    {
                        TheBams[i] = dir + "/genome/bam/sorted.bam";
                        i++;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }

            Console.WriteLine();

            foreach (string bFile in TheBams)
            {
                Console.WriteLine("\t" + bFile);
                BamFile BF = new BamFile(bFile);
                for (int i = 0; i < BF.Chromosomes.Length; i++)
                {
                    List<BamAlignedRead> MyList = BF.Fetch(BF.Chromosomes[i], 1, 5000000);
                    Console.WriteLine("\t\t" + BF.Chromosomes[i] + " - " + BF.ChromosomeLengths[i] + " - " + MyList.Count);
                }
                Console.WriteLine();
            }


            Console.WriteLine();
            return(0);
        }

        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\treadFileContent Usage:");
            Console.WriteLine("\treadFileContent <path-to-variantDectionDir>");
        }
    }
}
