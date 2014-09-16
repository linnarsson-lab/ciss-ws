using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace PeakAnnotator
{
    class PeakAnnotatorSettings
    {
        public List<string> infiles = new List<string>();
        public Dictionary<string, string[]> infileAnnotations = new Dictionary<string, string[]>();
        public int nAnnotations = 0;
        public string outfile = "peak_expression.tab";
        public string nonAnnotatedFolder = null;
        public string genomeName = "Mm";
        public int ext3Prime = 0;
        public int ext5Prime = 0;
        public StrtGenome genome;
        public string TSSModelFolder = "/data/seq/F5_data";

        public PeakAnnotatorSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "-g") genomeName = args[++argIdx];
                else if (args[argIdx] == "-3") ext3Prime = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "-5") ext5Prime = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "-o") outfile = args[++argIdx];
                else if (args[argIdx] == "-n") nonAnnotatedFolder = args[++argIdx];
                else if (args[argIdx] == "-t") TSSModelFolder = args[++argIdx];
                else if (args[argIdx] == "-f") ReadInfiles(args[++argIdx]);
                else infiles.Add(args[argIdx]);
            }
            if (genomeName.ToLower() != "ctrl")
                genome = StrtGenome.GetGenome(genomeName);
        }

        private void ReadInfiles(string filelistFile)
        {
            using (StreamReader reader = new StreamReader(filelistFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Trim().Split('\t');
                    string infile = fields[0];
                    infiles.Add(infile);
                    nAnnotations = Math.Max(nAnnotations, fields.Length - 1);
                    infileAnnotations[infile] = fields;
                }
            }
        }
    }

    class PeakAnnotatorProgram
    {
        static void Main(string[] args)
        {
            PeakAnnotatorSettings settings = new PeakAnnotatorSettings(args);
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono PeakAnnotator.exe [OPTIONS] -g GENOME -o OUTPUTFILE [INFILE [INFILE2...]]\n\n" +
                                  "N.B.: INFILEs are output files from Map2Pclu, e.g.: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "Options:\n" +
                                  "-3 N             Extend TSS region by N bases in the 3' end\n" +
                                  "-5 N             Extend TSS region by N bases in the 5' end\n" +
                                  "-n FOLDER        Write non-annotated data to files in FOLDER.\n" +
                                  "-t FOLDER        TSS model files are in this folder (default: " + settings.TSSModelFolder + ")\n" +
                                  "-f FILELISTFILE  Read infile names from given file, one at the start of each line (TAB and annotations may follow).\n" +
                                  "-g GENOME        Genome to analyze: 'Mm'/'Hs', or 'CTRL' for only spike-ins \n");
                Console.WriteLine("The TSS regions/models are supposed to be located in files named 'CTRL_peaks.tab' and 'Xx_peaks.tab' where\n" +
                                  "'Xx' is Mm ,Hs... These files consist of lines like this:\n" +
                                  "chr10:23851454..23851489,+ TAB p1@Vnn3\n");
            }
            else
            {
                PeakAnnotator pa = new PeakAnnotator(settings);
                pa.Process();
            }
        }
    }
}
