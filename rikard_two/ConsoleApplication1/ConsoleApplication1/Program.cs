using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace ConsoleApplication1
{
    class Program
    {
        static int Main(string[] args)
        {
            // The Length property provides the number of array elements
            System.Console.WriteLine("parameter count = {0}", args.Length);

            for (int i = 0; i < args.Length; i++)
            {
                System.Console.WriteLine("Arg[{0}] = [{1}]", i, args[i]);
            }

            foreach (string s in args)
            {
                System.Console.Write(s);
                Console.WriteLine(System.IO.File.Exists(s) ? " - File exists." : " - File does not exist.");
                if (File.Exists(s))
                {
                    string[] lines = System.IO.File.ReadAllLines(@s);
                }
            }

            string[] array2 = Directory.GetFiles(@"/media/ext5tb1/runs/111111_SN893_0092_BD036JACXX/Aligned.new.20111208/variantDetection", "P*_*_*");
            foreach (string s in array2)
            {
                System.Console.Write(s + Environment.NewLine);
            }

            return (0);
        }

        private static void TraverseDirs(DirectoryInfo dir, string Pattern)
        {
            // Subdirs
            try         // Avoid errors such as "Access Denied"
            {
                foreach (DirectoryInfo iInfo in dir.GetDirectories())
                {
                    if (iInfo.Name.StartsWith(Pattern))
                        Console.WriteLine("Found dir:  " + iInfo.FullName);

                    TraverseDirs(iInfo, Pattern);
                }
            }
            catch (Exception)
            {
            }

            // Subfiles
            try         // Avoid errors such as "Access Denied"
            {
                foreach (FileInfo iInfo in dir.GetFiles())
                {
                    if (iInfo.Name.StartsWith(Pattern))
                        Console.WriteLine("Found file: " + iInfo.FullName);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}