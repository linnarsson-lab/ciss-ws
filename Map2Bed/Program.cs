﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Map2Pclu
{
    class Map2PcluSettings
    {
        public bool iterateBarcodes = false;
        public int maxBarcodeIdx = 95;
        public string barcodePattern = "0_";
        public string filenamePrefix = "";
        public CountType countType = CountType.AllMolecules;
        public int nUMIs = 4096;
        public bool HasUMIs { get { return nUMIs > 0; }}
        public int maxMultiReadMappings = 1;
        public bool AllAsPlusStrand = false;
        public string outputFolderOrFilename = ".";
        public List<string> inputFiles = new List<string>();
        public bool estimateTrueMolCounts = false;
        public bool IsCountingMols { get { return countType == CountType.AllMolecules || countType == CountType.NonSingeltonMolecules; } }

        public Map2PcluSettings()
        { }
        public Map2PcluSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "--reads") countType = CountType.Reads;
                else if (args[argIdx] == "--nosingletons") countType = CountType.NonSingeltonMolecules;
                else if (args[argIdx].StartsWith("--UMIs=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx].StartsWith("--multireads=")) maxMultiReadMappings = int.Parse(args[argIdx].Substring(13));
                else if (args[argIdx] == "--estimatetrue") estimateTrueMolCounts = true;
                else if (args[argIdx] == "--mergestrands") AllAsPlusStrand = true;
                else if (args[argIdx] == "--bybarcode") iterateBarcodes = true;
                else if (args[argIdx] == "--prefix=") filenamePrefix = args[argIdx].Substring(9);
                else if (args[argIdx] == "-o") outputFolderOrFilename = args[++argIdx];
                else inputFiles.Add(args[argIdx]);
            }
        }

        public string ReplaceBarcode(string filename, int bcIdx)
        {
            string replacement = barcodePattern.Replace("0", bcIdx.ToString());
            if (!filename.StartsWith(barcodePattern))
                Console.WriteLine("Warning: Filename {1} does not start with barcode replacement pattern {0}!",
                                  barcodePattern, filename);
            return replacement + filename.Substring(barcodePattern.Length);
        }
    }

    class Map2PcluProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono Map2Pclu.exe [OPTIONS] -o OUTPUT MAPFILE [MAPFILE2...]\n\n" +
                                  "N.B.: Output is in paraclu peak format: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "If OUTPUT ends with '.gz' it is taken as a filename pattern. Any '*' is replaced by the barcode index.\n" +
                                  "Otherwise it is taken as an output folder, and filenames are constructed automatically.\n" +
                                  "Options:\n" +
                                  "--bybarcode      Process all barcodes (0...95) - all MAPFILE names have to start with 'N_' where N is a barcode index\n" +
                                  "--reads          Output read counts.\n" +
                                  "--nosingletons   Output molecule counts after removal of singeltons.\n" +
                                  "--estimatetrue   Compensate molecular counts for UMI collisions.\n" +
                                  "--multireads=N   Count also multireads with up to N mappings. A random mapping will be selected.\n" +
                                  "--UMIs=N         Analyze N different UMIs. Set N=0 to skip molecule counting.\n" +
                                  "--prefix=TXT     Prefix output filenames with some text (only valid with OUTPUT not ending '.gz').\n" +
                                  "--mergestrands   Reads are non-directional, all reads will be put on the '+' strand.");

            }
            else
            {
                Map2PcluSettings settings = new Map2PcluSettings(args);
                if (!settings.HasUMIs && settings.IsCountingMols)
                {
                    Console.WriteLine("ERROR: You can not count molecules with no UMIs! Counting reads instead...");
                    settings.countType = CountType.Reads;
                }
                if (settings.estimateTrueMolCounts && !settings.IsCountingMols)
                {
                    Console.WriteLine("WARNING: --estimatetrue will have no effect on read counts.");
                }
                Map2Pclu m2b = new Map2Pclu(settings);
                m2b.Convert();
            }
        }
    }
}
