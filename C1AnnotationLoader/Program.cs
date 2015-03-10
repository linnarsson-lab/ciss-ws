using System;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C1AnnotationLoader
{
    class C1AnnotationLoaderProgram
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("This program inserts additional cell annotations into the cells10k database from a TAB-delimited file." +
                                  "\nUsage:\nmono C1AnnotationLoader.exe ANNOTATIONTABFILE\n\n" +
                                  "ANNOTATIONTABFILE should consist of lines of:\n" +
                                  "Chip TAB ChipWell TAB AnnotName TAB AnnotValue [ AnnotName2 TAB AnnotValue2... ]");
            }
            else
            {
                int argIdx = 0;
                string infile = args[argIdx];
                new CellAnnotationLoader().Process(infile);
            }
        }
    }
}
