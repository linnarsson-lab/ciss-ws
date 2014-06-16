﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bc2BcLeakageAnalyzer
{
    class Bc2BcLeakageAnalyzerSettings
    {
        public int nUMIs = 4096;
        public string outputFile = ".";
        public string mapFolder;
        public int MinReadRatioBc1ToBc2 = 100;
        public int MinReadToMolRatioBc1 = 20;
        public int MinReadsPerUMIInMaxBc = 100;

        public Bc2BcLeakageAnalyzerSettings()
        { }
        public Bc2BcLeakageAnalyzerSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx].StartsWith("--UMIs=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx] == "-o") outputFile = args[++argIdx];
                else mapFolder =args[argIdx];
            }
        }

        class Bc2BcLeakageAnalyzerProgram
        {
            static void Main(string[] args)
            {
                if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
                {
                    Console.WriteLine("Usage:\nmono Bc2BcLeakageAnalyzer.exe [OPTIONS] --nUMIs=N -o OUTPUTFILE MAPFILEFOLDER\n\n" +
                                      "Options:\n" +
                                      "--UMIs=N         Analyze N different UMIs.\n");

                }
                else
                {
                    Bc2BcLeakageAnalyzerSettings settings = new Bc2BcLeakageAnalyzerSettings(args);
                    Bc2BcLeakageAnalyzer bla = new Bc2BcLeakageAnalyzer(settings);
                    bla.ReadMapFiles();
                    bla.Analyze();
                }
            }
        }
    }
}
