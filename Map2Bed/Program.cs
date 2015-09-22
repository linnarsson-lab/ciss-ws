﻿using System;
using System.Globalization;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Map2Pclu
{
    class Map2PcluSettings
    {
        public bool iterateBarcodes = false;
        public string filenamePrefix = "";
        public UMICountType countType = UMICountType.AllMolecules;
        public int minHammingDist = 2;
        public int UMILen = 6;
        public int nUMIs { get { return 1 << (2 * UMILen); } }
        public int nUMIBits { get { return 2 * UMILen; } }
        public bool HasUMIs { get { return UMILen > 0; } }
        public int maxMultiReadMappings = 1;
        public bool AllAsPlusStrand = false;
        public string outputFolderOrFilename = ".";
        public List<string> inputFiles = new List<string>();
        public bool estimateTrueMolCounts = false;
        public bool analyzeBcLeakage = false;
        public string readsPerMolFile = "";
        public string singletonFilenameBase = "";
        public int singletonSampleInterval = 10000;
        public bool AnalyzeSingletons { get { return singletonFilenameBase != ""; } }
        public bool sortMapFilesByBarcode = false;
        public bool AnalyzeReadsPerMol { get { return readsPerMolFile != ""; } }
        public bool IsCountingMols { get { return countType == UMICountType.AllMolecules || countType == UMICountType.NonSingletonMolecules; } }

        private int m_MaxBarcodeIdx = 95;
        public int MaxBarcodeIdx { get { return (iterateBarcodes || sortMapFilesByBarcode) ? m_MaxBarcodeIdx : 0; } }

        private string barcodePattern = "0_";
        private string m_BarcodePrefix = "*_";
        public string BarcodePrefix { get { return (iterateBarcodes || sortMapFilesByBarcode) ? m_BarcodePrefix : ""; } }
        
        public Map2PcluSettings()
        { }
        public Map2PcluSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "--reads") countType = UMICountType.Reads;
                else if (args[argIdx] == "--nosingletons") countType = UMICountType.NonSingletonMolecules;
                else if (args[argIdx].StartsWith("--minsingletondist="))
                {
                    countType = UMICountType.NonMutatedSingletonMolecules;
                    minHammingDist = int.Parse(args[argIdx].Split('=')[1]);
                }
                else if (args[argIdx].StartsWith("--UMILen=")) UMILen = int.Parse(args[argIdx].Substring(9));
                else if (args[argIdx].StartsWith("--multireads=")) maxMultiReadMappings = int.Parse(args[argIdx].Substring(13));
                else if (args[argIdx] == "--estimatetrue") estimateTrueMolCounts = true;
                else if (args[argIdx] == "--mergestrands") AllAsPlusStrand = true;
                else if (args[argIdx] == "--bybarcode") iterateBarcodes = true;
                else if (args[argIdx] == "--sortbybc") sortMapFilesByBarcode = true;
                else if (args[argIdx] == "--analyzebcleakage") analyzeBcLeakage = true;
                else if (args[argIdx].StartsWith("--prefix=")) filenamePrefix = args[argIdx].Substring(9);
                else if (args[argIdx].StartsWith("--readspermol=")) readsPerMolFile = args[argIdx].Substring(14);
                else if (args[argIdx].StartsWith("--singletonstats=")) singletonFilenameBase = args[argIdx].Substring(17);
                else if (args[argIdx].StartsWith("--sampleivl=")) singletonSampleInterval = int.Parse(args[argIdx].Substring(12));
                else if (args[argIdx] == "-o") outputFolderOrFilename = args[++argIdx];
                else if (args[argIdx] == "-i")
                {
                    using (StreamReader reader = new StreamReader(args[++argIdx]))
                    {
                        string line = reader.ReadLine();
                        while (line != null)
                        {
                            inputFiles.Add(line.Trim());
                            line = reader.ReadLine();
                        }
                    }
                    Console.WriteLine("Read " + inputFiles.Count + " input map files from " + args[argIdx]);
                }
                else inputFiles.Add(args[argIdx]);
            }
            if (singletonFilenameBase != "")
            {
                if (maxMultiReadMappings > 1)
                    Console.WriteLine("WARNING: When using --singletonstats, multimapping reads are always filtered away.");
                maxMultiReadMappings = 1;
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
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Map2PcluSettings settings = new Map2PcluSettings(args);
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono Map2Pclu.exe [OPTIONS] -o OUTPUT [-i FILELISTFILE] MAPFILE [MAPFILE2...]\n\n" +
                                  "N.B.: Output is in paraclu peak format: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "If OUTPUT ends with '.gz' it is taken as a filename pattern. Any '*' is replaced by the barcode index.\n" +
                                  "Otherwise it is taken as an output folder, and filenames are constructed automatically.\n" +
                                  "Options:\n" +
                                  "-i                       Read (additional) mapfile paths from FILELISTFILE, one per line.\n" +
                                  "--sortbybc               Combine data from MAPFILES that have same barcode. Filenames have to start with bcIdx + '_'.\n" +
                                  "--bybarcode              Process files for all barcodes (0-95). Filenames have to match 'N_*' where N is 0-95.\n" +
                                  "                            As mapfile(s) you specify the file(s) with N=0: '0_xxxxxx.map'.\n" +
                                  "                            Output is merged by barcode if several mapfiles are given.\n" +
                                  "--reads                  Output read counts.\n" +
                                  "--nosingletons           Output molecule counts after removal of singeltons.\n" +
                                  "--minsingletondist=N     Output molecule counts after removal of singeltons with Hamming dist<N to another UMI.\n" +
                                  "--estimatetrue           Compensate molecular counts for UMI collisions.\n" +
                                  "--multireads=N           Count multireads with <=N mappings [Default=" + settings.maxMultiReadMappings + "]. A random mapping will be selected.\n" +
                                  "--UMILen=N               Analyze UMIs of N length [Default=" + settings.UMILen + "]. Set N=0 to skip molecule counting.\n" +
                                  "--analyzebcleakage       Analyze bc-to-bc leakage frequencies.\n" +
                                  "--prefix=TXT             Prefix output filenames with some text (only valid with OUTPUT not ending '.gz').\n" +
                                  "--readspermol=FILE       Write reads per molecule profiles (one per map file) to specific file.\n" +
                                  "--singletonstats=PREFIX  Write statistics on singletons to files with this name prefix.\n" +
                                  "--sampleivl=N            Sample interval for singleton stats. [Default=" + settings.singletonSampleInterval + "]\n" +
                                  "--mergestrands           Reads are non-directional, all reads will be put on the '+' strand.");

            }
            else
            {
                if (!settings.HasUMIs && settings.IsCountingMols)
                {
                    Console.WriteLine("ERROR: You can not count molecules with no UMIs! Counting reads instead...");
                    settings.countType = UMICountType.Reads;
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
