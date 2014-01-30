﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Map2Bed
{
    class Map2BedSettings
    {
        public bool iterateBarcodes = false;
        public int maxBarcodeIdx = 95;
        public string barcodePattern = "0_";
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
                if (args[argIdx] == "--reads") countReads = true;
                else if (args[argIdx].StartsWith("--UMIs=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx].StartsWith("--multireads=")) maxMultiReadMappings = int.Parse(args[argIdx].Substring(13));
                else if (args[argIdx] == "--mergestrands") AllAsPlusStrand = true;
                else if (args[argIdx] == "--bybarcode") iterateBarcodes = true;
                else if (args[argIdx] == "-o") outputFolder = args[++argIdx];
                else inputFiles.Add(args[argIdx]);
            }
        }

        public string ReplaceBarcode(string filename, int bcIdx)
        {
            string replacement = barcodePattern.Replace("0", bcIdx.ToString());
            if (!filename.Contains(barcodePattern))
                Console.WriteLine("Warning: The barcode replacement pattern {0} is not in filename {1}!",
                                  barcodePattern, filename);
            return filename.Replace(barcodePattern, replacement);
        }
    }

    class Map2BedProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono Map2Bed.exe [OPTIONS] -o OUTPUTFOLDER MAPFILE [MAPFILE2...]\n\n" +
                                  "N.B.: Output is not a true bed files, it is: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "Options:\n" +
                                  "--bybarcode      Process all barcodes (0...95) - requires that all MAPFILE names start with '0_'\n" +
                                  "--reads          Output files with read counts.\n" +
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
