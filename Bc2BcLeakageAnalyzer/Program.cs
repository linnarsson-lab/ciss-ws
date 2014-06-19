using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bc2BcLeakageAnalyzer
{
    class Bc2BcLeakageAnalyzerSettings
    {
        public int nUMIs = 4096;
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
                else if (args[argIdx].StartsWith("--bc12ratio=")) MinReadRatioBc1ToBc2 = int.Parse(args[argIdx].Substring(12));
                else if (args[argIdx].StartsWith("--rpmratio=")) MinReadToMolRatioBc1 = int.Parse(args[argIdx].Substring(11));
                else if (args[argIdx].StartsWith("--minreads=")) MinReadsPerUMIInMaxBc = int.Parse(args[argIdx].Substring(11));
                else mapFolder =args[argIdx];
            }
        }

        class Bc2BcLeakageAnalyzerProgram
        {
            static void Main(string[] args)
            {
                if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
                {
                    Console.WriteLine("Usage:\nmono Bc2BcLeakageAnalyzer.exe [OPTIONS] --nUMIs=N MAPFILEFOLDER\n\n" +
                                      "Required option:\n" +
                                      "--UMIs=N         Analyze N different UMIs.\n" +
                                      "Optional options that control which chr-positions that will be considered:\n" +
                                      "--bc12ratio=F    Min ratio of reads in max to second max read barcode.\n" +
                                      "                 (make sure there is only one expressing cell)" + 
                                      "--rpmratio=F     Min ratio of reads to occupied UMIs in max read barcode.\n" +
                                      "                 (e.g. a top expressing cell with only singletons is not informative)\n" +
                                      "--minreads=F     Min reads in a UMI of the max read barcode that will output a line." +
                                      "                 (avoid non-informative lines with e.g. 1 as max and rest 0)");

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
