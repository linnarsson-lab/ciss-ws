using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Strt;
using C1;

namespace C1SeqPlateLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            int argIdx = 0;
            bool force = false;
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine("Usage:\nmono C1SeqPlateLoader.exe [-f] CHIPID_OR_MIXPLATEID");
                Console.WriteLine("The argument is either the Id of a C1 chip in the Cell database,\n" +
                                  " or the name of a C1 plate mix file in the {0} directory.\n" +
                                  "In the latter case, the new seq project will be named '{1}-name'.\n" +
                                  "Use -f to force reloading of an already loaded plate.",
                                  C1Props.props.C1SeqPlatesFolder, C1Props.C1ProjectPrefix);
                return;
            }
            else if (args[0] == "-f")
            {
                force = true;
                argIdx++;
            }
            string plateOrChip = args[argIdx];
            if (plateOrChip.EndsWith(".txt") || plateOrChip.EndsWith(".tab"))
                plateOrChip = plateOrChip.Substring(plateOrChip.Length - 4, 4);
            if (plateOrChip.StartsWith(C1Props.C1ProjectPrefix))
                plateOrChip = plateOrChip.Substring(C1Props.C1ProjectPrefix.Length);
            List<string> loadedC1Plates = new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix + "%", "plateid");
            if (loadedC1Plates.Contains(C1Props.C1ProjectPrefix + plateOrChip))
            {
                if (!force)
                {
                    Console.WriteLine("ERROR: {0} is already loaded! Use '-f' to force reload.", plateOrChip);
                    return;
                }
                else
                {
                    Console.WriteLine("Reloading plate {0}", plateOrChip);
                }
            }
            string result = new C1SeqPlateLoader().LoadC1SeqPlate(plateOrChip);
            Console.WriteLine(result);
        }

    }
}
