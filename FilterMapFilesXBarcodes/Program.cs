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

        public FilterMapFilesXBarcodesSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx].StartsWith("--umis=")) nUMIs = int.Parse(args[argIdx].Substring(7));
                else if (args[argIdx].StartsWith("--bcs=")) nBcs = int.Parse(args[argIdx].Substring(6));
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
                Console.WriteLine("Usage:\nmono FilterMapFilesXBarcodes.exe [OPTIONS] MAPFILEFOLDER [MAPFILEFOLDER2...]\n\n" +
                                  "Options:\n" +
                                  "--umis=N         Number of UMIs (default 4096).\n" +
                                  "--bcs=N         Number of barcodes (default 96).\n" +
                                  "-o OUTFOLDER    Output all filtered map files to this folder.");

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
