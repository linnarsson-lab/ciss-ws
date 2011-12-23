using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace getsnps
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

//            string[] TheProjs = new string[0];
//            string[] ThePars = new string[0];
//            string[] The000s = new string[0];
            List<string> allsitefiles = new List<string>();

            Console.WriteLine();

            try
            {
                // Only get certain subdirectories 
                string[] dirs = Directory.GetDirectories(@path, "Project_*_*");
                Console.WriteLine("\tThe number of directories starting with 'Project' is {0}.", dirs.Length);

                foreach (string dir in dirs)
                {
                    string[] pars = Directory.GetDirectories(@dir, "Parsed*");
                    foreach (string par in pars)
                    {
                        string[] chrs = Directory.GetDirectories(@par, "*.fa");
                        foreach (string chr in chrs)
                        {
                            string[] zeros = Directory.GetDirectories(@chr, "00*");
                            foreach (string zero in zeros)
                            {
                                string[] files = Directory.GetFiles(@zero, "sites.txt");
                                foreach (string file in files)
                                {
                                    allsitefiles.Add(file);
                                }
                            }
                            
                        }
                      
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }

            Console.WriteLine();
            foreach (string element in allsitefiles)
            {
                Console.WriteLine(element);
            }
            Console.WriteLine();
            return(0);
        }

        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\treadFileContent Usage:");
            Console.WriteLine("\treadFileContent <path-to-variantDectionDir>" + Environment.NewLine);
        }
    }
}
