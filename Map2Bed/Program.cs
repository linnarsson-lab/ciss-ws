using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Map2Bed
{
    class Map2BedSettings
    {
        public bool countReads = true;
        public int nUMIs = 4096;
        public bool CountMols { get { return nUMIs > 0; }}
        public int maxMultiReadMappings = 1;
        public bool AllAsPlusStrand = false;
        public string outputFolder = ".";
        public List<string> inputFiles = new List<string>();

        public Map2BedSettings()
        { }
        public Map2BedSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                Console.WriteLine(args[argIdx]);
                if (args[argIdx] == "--reads") countReads = true;
                else if (args[argIdx].StartsWith("--UMIs=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx].StartsWith("--multireads=")) maxMultiReadMappings = int.Parse(args[argIdx].Substring(13));
                else if (args[argIdx] == "--mergestrands") AllAsPlusStrand = true;
                else if (args[argIdx] == "-o") outputFolder = args[++argIdx];
                else inputFiles.Add(args[argIdx]);
            }
        }
    }

    class Map2BedProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono Map2Bed.exe [OPTIONS] -o OUTPUTFOLDER MAPFILE [MAPFILE2...]\n\n" +
                                  "Options:\n" +
                                  "--reads          Output bed files with read counts.\n" +
                                  "--multireads=N   Count also multireads with up to N mappings. A random mapping will be selected.\n" +
                                  "--UMIs=N         Analyze N different UMIs. Set N=0 to skip molecule counting.\n" +
                                  "--mergestrands   Reads are non-directional, all reads will be put on the '+' strand.");

            }
            else
            {
                Map2BedSettings settings = new Map2BedSettings(args);
                Map2Bed m2b = new Map2Bed(settings);
                m2b.Convert();
            }
        }
    }
}
