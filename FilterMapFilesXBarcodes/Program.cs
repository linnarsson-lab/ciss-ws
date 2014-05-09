using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace FilterMapFilesXBarcodes
{
    public class FilterMapFilesXBarcodesSettings
    {
        public int nBcs = 96;
        public int nUMIs = 4096;
        public string outputFolder = "";
        public List<string> inputFiles = new List<string>();
        public double ratioThresholdForFilter = 0.1;
        public Dictionary<int, string> bcIdx2Bc;

        public FilterMapFilesXBarcodesSettings()
        { }
        public FilterMapFilesXBarcodesSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx].StartsWith("--umis=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx].StartsWith("--bcs=")) nBcs = int.Parse(args[argIdx].Substring(6));
                else if (args[argIdx] == "-r") ratioThresholdForFilter = double.Parse(args[++argIdx]);
                else if (args[argIdx] == "-b")
                {
                    bcIdx2Bc = new Dictionary<int, string>();
                    int bcIdx = 0;
                    using (StreamReader reader = new StreamReader(args[++argIdx]))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!line.StartsWith("#"))
                                bcIdx2Bc[bcIdx] = line.Split('\t')[1];
                        }
                    }

                }
                else if (args[argIdx] == "-o") outputFolder = args[++argIdx];
                else
                {
                    foreach (string file in Directory.GetFiles(args[argIdx], "*.map"))
                        inputFiles.Add(file);
                }
            }
        }
    }

    class FilterMapFilesXBarcodesProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                FilterMapFilesXBarcodesSettings s = new FilterMapFilesXBarcodesSettings();
                Console.WriteLine("Usage:\nmono FilterMapFilesXBarcodes.exe [OPTIONS] MAPFILEFOLDER [MAPFILEFOLDER2...]\n\n" +
                                  "Options:\n" +
                                  "--umis=N        Number of UMIs (default " + s.nUMIs + ").\n" +
                                  "--bcs=N         Number of barcodes (default " + s.nBcs + ").\n" +
                                  "-r FACTOR       Set the threshold factor for filter of lesser (" + s.ratioThresholdForFilter + ").\n" +
                                  "-b BARCODESET   Specify barcode set for to see actual barcodes in statistics output.\n" +
                                  "-o OUTFOLDER    Output all filtered map files to this folder (default is same as input files).");

            }
            else
            {
                FilterMapFilesXBarcodesSettings settings = new FilterMapFilesXBarcodesSettings(args);
                FilterMapFilesXBarcodes fxb = new FilterMapFilesXBarcodes(settings);
                fxb.Process();
            }
        }
    }
}
