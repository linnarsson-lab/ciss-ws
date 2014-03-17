using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C1AnnotationLoader
{
    class C1AnnotationLoaderProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono C1AnnotationLoader.exe ANNOTATIONTABFILE\n\n" +
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
