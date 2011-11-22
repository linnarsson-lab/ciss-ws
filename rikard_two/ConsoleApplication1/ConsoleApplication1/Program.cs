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


                }

            }





            return (0);
        }

    }
}
