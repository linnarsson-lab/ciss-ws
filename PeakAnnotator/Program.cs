using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace PeakAnnotator
{
    class PeakAnnotatorSettings
    {
        public List<string> infiles = new List<string>();
        public string outfile = "repeat_expression.tab";
        public string genomeName = "Mm";
        public StrtGenome genome;

        public PeakAnnotatorSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "-g") genomeName = args[++argIdx];
                else if (args[argIdx] == "-o") outfile = args[++argIdx];
                else infiles.Add(args[argIdx]);
            }
            genome = StrtGenome.GetGenome(genomeName);
        }
    }

    class PeakAnnotatorProgram
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono PeakAnnotator.exe -g GENOME -o OUTPUTFILE INFILE [INFILE2...]\n\n" +
                                  "N.B.: INFILEs are output files from Map2Bed, e.g.: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "Options:\n" +
                                  "-g GENOME        Genome to analyze, e.g. 'Mm' \n");

            }
            else
            {
                PeakAnnotatorSettings settings = new PeakAnnotatorSettings(args);
                PeakAnnotator pa = new PeakAnnotator(settings);
                pa.Process();
            }
        }
    }
}
