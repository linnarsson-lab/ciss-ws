using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using C1;

namespace C1SeqPlateLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            int argIdx = 0;
            bool useExcluded = false;
            bool force = false;
            string barcodeSet = C1Props.props.C1BarcodeSet1;
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine("Usage:\nmono C1SeqPlateLoader.exe [-f] [--all] [-1|-2Old|-2New|-3|-4|-bBCSET] CHIPID_OR_MIXPLATEID");
                Console.WriteLine("The argument is either the Id of a C1 chip in the the Sanger DB,\n" +
                                  "or the name of a C1 sequencing plate mix file in the {0} directory.\n" +
                                  "In the latter case, all chips on the plate have to exist in the DB, and the sequencing project will be named '{1}name'.\n" +
                                  "Use -1/-2xxx/-3/-4 for the standard barcode sets of a C1 chip 1-96/97-192/193-288/289-384 combo plate.\n" +
                                  "Use -bBCSET to specifya different barcode set.\n" +
                                  "Without --all, cells that have been excluded will be counted as empty in global statistics.\n" +
                                  "Use -f to force reloading of an already loaded plate (only possible for un-analyzed plates).",
                                  C1Props.props.C1SeqPlatesFolder, C1Props.C1ProjectPrefix);
                return;
            }
            while (args[argIdx].StartsWith("-"))
            {
                if (args[argIdx] == "-f")
                    force = true;
                else if (args[argIdx] == "--all")
                    useExcluded = true;
                else if (args[argIdx] == "-2Old")
                    barcodeSet = C1Props.props.C1BarcodeSet2;
                else if (args[argIdx] == "-2New")
                    barcodeSet = C1Props.props.C1BarcodeSet2New;
                else if (args[argIdx] == "-3")
                    barcodeSet = C1Props.props.C1BarcodeSet3;
                else if (args[argIdx] == "-4")
                    barcodeSet = C1Props.props.C1BarcodeSet4;
                else if (args[argIdx].StartsWith("-b"))
                    barcodeSet = args[argIdx].Substring(2);
                argIdx++;
            }
            string plateOrChip = args[argIdx];
            if (plateOrChip.EndsWith(".txt") || plateOrChip.EndsWith(".tab"))
                plateOrChip = plateOrChip.Substring(plateOrChip.Length - 4, 4);
            if (plateOrChip.StartsWith(C1Props.C1ProjectPrefix))
                plateOrChip = plateOrChip.Substring(C1Props.C1ProjectPrefix.Length);
            try
            {
                List<string> loadedC1Plates = new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix + "%", "plateid");
                if (loadedC1Plates.Contains(C1Props.C1ProjectPrefix + plateOrChip))
                {
                    if (!force)
                        throw new Exception(string.Format("ERROR: {0} is already loaded! Use '-f' to force reload.", plateOrChip));
                    Console.WriteLine("Reloading plate {0}", plateOrChip);
                }
                new C1SeqPlateLoader(useExcluded).LoadC1SeqPlate(plateOrChip, barcodeSet);
                Console.WriteLine("Ready");
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
        }

    }
}
