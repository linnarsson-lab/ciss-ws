using System;
using System.Threading;
using System.Globalization;
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
        public int ext3Prime = 0;
        public int ext5Prime = 0;
        public string build = "mm10";
        public string genomeFolder { get { return Path.Combine(Path.Combine(Props.props.GenomesFolder, build), "genome"); } }
        public bool includeSpikes = false;
        public string spikePeakPath { get { return Path.Combine(TSSModelFolder, "CTRL_peaks.tab"); } }
        public string TSSModelFolder = "/data/seq/F5_data";
        public string TSSModelFilePattern = "{0}_peaks.tab";
        private string m_TSSPeakPath = null;
        public string TSSPeakPath
        {
            get
            {
                if (m_TSSPeakPath != null)
                    return m_TSSPeakPath;
                return Path.Combine(TSSModelFolder, string.Format(TSSModelFilePattern, build));
            }
            set
            {
                m_TSSPeakPath = value;
            }
        }

        public PeakAnnotatorSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "-g") build = args[++argIdx];
                else if (args[argIdx] == "-3") ext3Prime = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "-5") ext5Prime = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "-o") outfile = args[++argIdx];
                else if (args[argIdx] == "-n") nonAnnotatedFolder = args[++argIdx];
                else if (args[argIdx] == "-t") TSSModelFolder = args[++argIdx];
                else if (args[argIdx] == "-p") TSSPeakPath = args[++argIdx];
                else if (args[argIdx] == "-s") includeSpikes = true;
                else if (args[argIdx] == "-f") ReadInfiles(args[++argIdx]);
                else infiles.Add(args[argIdx]);
            }
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
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            PeakAnnotatorSettings settings = new PeakAnnotatorSettings(args);
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono PeakAnnotator.exe [OPTIONS] [-s] [-g GENOME] -o OUTPUTFILE [INFILE [INFILE2...]]\n\n" +
                                  "N.B.: INFILEs are output files from Map2Pclu, e.g.: chr TAB strand TAB pos TAB count\n" +
                                  "      pos is where the read 5' end maps, i.e. if strand='-', pos is max of the aligned positions\n" +
                                  "Options:\n" +
                                  "-3 N             Extend TSS region by N bases in the 3' end\n" +
                                  "-5 N             Extend TSS region by N bases in the 5' end\n" +
                                  "-n FOLDER        Write non-annotated data to files in FOLDER.\n" +
                                  "-f FILELISTFILE  Read infile names from given file, one at the start of each line (TAB and annotations may follow).\n" +
                                  "-s               Include ERCC spike-ins in analysis (read from " + settings.spikePeakPath + ")\n" +
                                  "-g GENOME        Genome build to analyze: mm10 / mm9 / hg19\n" +
                                  "-t FOLDER        TSS model for '-g' and spikes are in this folder (default: " + settings.TSSModelFolder + ")\n" +
                                  "-p MODELPATH     Read TSS/exon models from specified file instead of default file for genome.\n\n" +
                                  "The file with TSS regions / exon models should be tab-delimited and lines conform to this pattern:\n" +
                                  "  chr10:23851454..23851489,+ TAB peakname\n" + 
                                  "Suffixes will be added to non-unique peaknames.\n" +
                                  "Peaknames like 'pN@GENE' will be made gene sortable by modifying to 'GENE:pN'.\n" + 
                                  "Default files should match this path pattern: " + Path.Combine(settings.TSSModelFolder, settings.TSSModelFilePattern));
            }
            else
            {
                PeakAnnotator pa = new PeakAnnotator(settings);
                pa.Process();
            }
        }
    }
}
