using System;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Strt;

namespace ESCAF_Strt
{
    class ESCAFStrtSettings
    {
        public string[] read1Files;
        public string[] read2Files;
        public string barcodesName = "";
        public string build = "mm10";
        public string annotation =  "UCSC";
        public bool variants = Props.props.AnalyzeAllGeneVariants;
        public string plateFolder = "";
        public int outputLevel = Props.props.OutputLevel;
        public int spikeMols = Props.props.TotalNumberOfAddedSpikeMolecules;
        public string aligner = Props.props.Aligner;
        public string resultFolder = null;
        public bool directionalReads = true;
        public bool senseReads = true;
        public bool useRPKM = false;
        public bool skipExtraction = false;
        public string specialLayoutFile = null;

        public string extractionFolder { get { return Path.Combine(plateFolder, "tmp"); } }
        public string layoutPath { get { return (specialLayoutFile != "")? specialLayoutFile : Path.Combine(plateFolder, "layout.txt"); } }

        public ESCAFStrtSettings()
        { }
        public ESCAFStrtSettings(string[] args)
        {
            int argIdx = 0;
            for (; argIdx < args.Length; argIdx++)
            {
                if (args[argIdx] == "-1") read1Files = args[++argIdx].Split(',');
                else if (args[argIdx] == "-2") read2Files = args[++argIdx].Split(',');
                else if (args[argIdx] == "-b") barcodesName = args[++argIdx];
                else if (args[argIdx] == "-g") build = args[++argIdx];
                else if (args[argIdx] == "-a") annotation = args[++argIdx];
                else if (args[argIdx] == "-m") skipExtraction = true;
                else if (args[argIdx] == "--all") variants = true;
                else if (args[argIdx] == "--single") variants = false;
                else if (args[argIdx] == "-p") plateFolder = args[++argIdx];
                else if (args[argIdx] == "-w") specialLayoutFile = args[++argIdx];
                else if (args[argIdx] == "-l") outputLevel = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "-o") resultFolder = args[++argIdx];
                else if (args[argIdx] == "-s") spikeMols = int.Parse(args[++argIdx]);
                else if (args[argIdx] == "--bowtie") aligner = "bowtie";
                else if (args[argIdx].ToLower() == "--star") aligner = "star";
                else if (args[argIdx] == "--help") Console.WriteLine(
                    "Arguments for ESCAF-Strt.exe:\n" +
                    "-p PLATEFOLDER        output folder for extraction, layout etc.\n" +
                    "-1 PATH1,PATH2...     paths to first read fq.gz files\n" +
                    "-2 PATH1,PATH2...     paths to index read fq.gz files\n" +
                    "-b BARCODESNAME       name of barcode set, e.g. 'C1Plate1'\n" +
                    "--bowtie              use bowtie for alignments\n" +
                    "--star                use STAR for alignments\n" +
                    "-g GENOMEBUILD        build, e.g. 'mm10'\n" +
                    "-m                    go directly to mapping, extraction is done already\n" + 
                    "-a ANNOTATION         annotation source, e.g. 'UCSC'\n" +
                    "--single              summarize all transcript models per gene\n" +
                    "--all                 analyze all transcript models of each gene\n" +
                    "-s SPIKEMOLECULES     number of spike molcules per well\n" +
                    "-l OUTPUTLEVEL        file richness of result folder, e.g. '3'\n" +
                    "-w LAYOUTFILE         specify non-standard plate layout file location\n" +
                    "-o RESULTFOLDER       non-standard result output folder\n");

            }
        }

        public bool IsValid()
        {
            List<string> errors = new List<string>();
            if (read1Files == null) errors.Add("Argument missing in ESCAF-Strt.exe: You need to specify the input fq file(s) with -1 option.");
            if (barcodesName == "") errors.Add("Argument missing in ESCAF-Strt.exe: You need to specify barcode set with -b option.");
            if (plateFolder == "") errors.Add("Argument missing in ESCAF-Strt.exe: You need to specify plateId with -p option.");
            if (errors.Count > 0)
            {
                Console.WriteLine(string.Join("\n", errors.ToArray()));
                return false;
            }
            return true;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            ESCAFStrtSettings settings = new ESCAFStrtSettings(args);
            if (!settings.IsValid())
                return 1;
            Process(settings);
            return 0;
        }

        static void Process(ESCAFStrtSettings options)
        {
            Props.props.InsertCellDBData = false;
            Props.props.DirectionalReads = options.directionalReads;
            Props.props.UseRPKM = options.useRPKM;
            Props.props.SenseStrandIsSequenced = options.senseReads;
            Props.props.BarcodesName = options.barcodesName;
            Props.props.TotalNumberOfAddedSpikeMolecules = options.spikeMols;
            Props.props.Aligner = options.aligner;
            Props.props.AnalyzeLoci = false;
            Props.props.LayoutFile = options.layoutPath;
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string readFile in options.read1Files)
                laneInfos.Add(new LaneInfo(readFile, "", '0', options.extractionFolder, Props.props.Barcodes.Count, ""));
            if (!options.skipExtraction)
            {
                foreach (LaneInfo laneInfo in laneInfos)
                {
                    Console.WriteLine("Extracting {0} using {1}...", laneInfo.PFReadFilePath, options.barcodesName);
                    SampleReadWriter srw = new SampleReadWriter(Props.props.Barcodes, laneInfo);
                    srw.ProcessLane();
                }
            }
            Console.WriteLine("Mapping using {0} and annotating...", options.aligner);
            StrtReadMapper mapper = new StrtReadMapper();
            mapper.MapAndAnnotate(options.build, options.variants, options.annotation, options.resultFolder, null, 
                                  laneInfos, options.plateFolder);
        }
    }
}
