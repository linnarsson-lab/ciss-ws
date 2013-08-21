using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Strt;

namespace CmdSilverBullet
{
    class Program
    {
        static void CheckArgs(string[] args, int min, int max)
        {
            if (args.Length < min)
                throw new ArgumentException("Missing argument");
            if (args.Length > max)
                throw new ArgumentException("Too many arguments");
        }

        static void Main(string[] args)
        {
            if (args.Length >= 1 + 0)
            {
                bool analyzeAllGeneVariants = Props.props.AnalyzeAllGeneVariants;
                StrtGenome genome;
                Props props = Props.props;
                StrtReadMapper mapper;
                string projectFolder;
                QXMAOptions options;
                int readLen = props.StandardReadLen;
                int argOffset = 1;
                string cmd = args[0];
                try
                {
                    switch (cmd)
                    {
                        case "q":
                            options = new QXMAOptions(args);
                            props.DirectionalReads = options.directionalReads;
                            props.UseRPKM = options.useRPKM;
                            props.BarcodesName = options.barcodesName;
                            projectFolder = options.projectFolder;
                            mapper = new StrtReadMapper(props);
                            List<LaneInfo> extrInfos = mapper.Extract(projectFolder, options.laneArgs, options.resultFolder);
                            mapper.MapAndAnnotate(projectFolder, options.speciesAbbrev, options.analyzeAllGeneVariants, options.annotation, "");
                            break;

                        case "downloadmart":
                            UCSCGenomeDownloader gdm = new UCSCGenomeDownloader();
                            string abbrev, threeName;
                            gdm.ParseSpecies(args[argOffset], out threeName, out abbrev);
                            string destDir = (args.Length > argOffset + 1) ? args[argOffset + 1] : "";
                            gdm.DownloadMartAnnotations(abbrev, destDir);
                            break;

                        case "download":
                            UCSCGenomeDownloader gd = new UCSCGenomeDownloader();
                            gd.DownloadGenome(args[argOffset]);
                            break;

                        case "x":
                            options = new QXMAOptions(args);
                            props.BarcodesName = options.barcodesName;
                            props.ExtractionReadLimitType = options.extractionReadLimitType;
                            props.ExtractionReadLimit = options.extractionReadLimit;
                            projectFolder = options.projectFolder;
                            mapper = new StrtReadMapper(props);
                            mapper.Extract(projectFolder, options.laneArgs, options.resultFolder);
                            break;

                        case "bt":
                            options = new QXMAOptions(args);
                            mapper = new StrtReadMapper(props);
                            mapper.Map(options.projectFolder, options.speciesAbbrev, options.analyzeAllGeneVariants, options.annotation);
                            break;

                        case "ab":
                            options = new QXMAOptions(args);
                            props.DirectionalReads = options.directionalReads;
                            props.UseRPKM = options.useRPKM;
                            mapper = new StrtReadMapper(props);
                            mapper.MapAndAnnotate(options.projectFolder, options.speciesAbbrev, options.analyzeAllGeneVariants,
                                                            options.annotation, options.resultFolder);
                            break;

                        case "rf":
                            options = new QXMAOptions(args);
                            props.DirectionalReads = options.directionalReads;
                            props.UseRPKM = options.useRPKM;
                            genome = StrtGenome.GetGenome(options.speciesAbbrev, options.analyzeAllGeneVariants, options.annotation, false);
                            GenomeAnnotations annotations = new GenomeAnnotations(props, genome);
                            annotations.Load();
                            StreamWriter writer = options.projectFolder.OpenWrite();
                            foreach (GeneFeature gf in annotations.geneFeatures.Values)
                                writer.WriteLine(gf);
                            writer.Close();
                            break;

                        case "jct":
                            CheckArgs(args, 2, 6);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            genome = StrtGenome.GetBaseGenome(args[argOffset++]);
                            if (args.Length > argOffset)
                                genome.Annotation = args[argOffset];
                            genome.ReadLen = readLen;
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctions(genome);
                            break;

                        case "idx":
                            CheckArgs(args, 2, 6);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            genome = StrtGenome.GetBaseGenome(args[argOffset++]);
                            if (args.Length > argOffset)
                                genome.Annotation = args[argOffset];
                            genome.ReadLen = readLen;
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctionsAndIndex(genome);
                            break;

                        case "dumpfasta":
                            genome = StrtGenome.GetGenome(args[1]);
                            string fastaPath = args[2];
                            mapper = new StrtReadMapper(props);
                            mapper.DumpTranscripts(null, genome, 0, 0, 0, fastaPath, false, false, 0, 0);
                            break;

                        case "dump":
                            genome = StrtGenome.GetGenome(args[1]);
                            int readLength = props.StandardReadLen, step = 1, maxPerGene = 0;
                            int minOverhang = props.MaxAlignmentMismatches;
                            int junk;
                            string outputPath = "";
                            Barcodes barcodes = null;
                            if (!int.TryParse(args[args.Length - 1], out junk))
                            {
                                outputPath = args[args.Length - 1];
                                Array.Resize(ref args, args.Length - 1);
                            }
                            if (args.Length > 2)
                                readLength = int.Parse(args[2]);
                            if (args.Length > 3)
                                step = int.Parse(args[3]);
                            if (args.Length > 4)
                                maxPerGene = int.Parse(args[4]);
                            if (args.Length > 5)
                                minOverhang = int.Parse(args[5]);
                            bool makeSplices = true;
                            if (args.Length > 6)
                                makeSplices = args[6].ToUpper().StartsWith("S");
                            if (args.Length > 7)
                                barcodes = Barcodes.GetBarcodes(args[7]);
                            mapper = new StrtReadMapper(props);
                            int maxSkip = props.MaxExonsSkip;
                            mapper.DumpTranscripts(barcodes, genome, readLength, step, maxPerGene, outputPath, true,
                                                   makeSplices, minOverhang, maxSkip);
                            break;

                        case "mapsnp":
                            MapFileSnpFinder mfsf = new MapFileSnpFinder(Barcodes.GetBarcodes(args[1]));
                            List<string> files = new List<string>();
                            for (int i = 3; i < args.Length; i++)
                                files.Add(args[i]);
                            mfsf.ProcessMapFiles(files);
                            mfsf.WriteToFile(args[2]);
                            break;

                        case "maskchr":
                            genome = StrtGenome.GetGenome(args[argOffset++]);
                            int minFlank = int.Parse(args[argOffset++]);
                            int minIntronFlank = int.Parse(args[argOffset++]);
                            int maxIntronToKeep = int.Parse(args[argOffset++]);
                            string outFolder = args[argOffset++];
                            NonExonRepeatMasker nerm = new NonExonRepeatMasker(minFlank, minIntronFlank, maxIntronToKeep);
                            nerm.Mask(genome, outFolder);
                            break;

                        case "split":
                            CheckArgs(args, 2, 3);
                            if (args.Length == 3)
                                props.BarcodesName = args[1];
                            projectFolder = args[args.Length - 1];
                            mapper = new StrtReadMapper(props);
                            mapper.Split(projectFolder);
                            break;

                        case "stats":
                            CheckArgs(args, 2, 3);
                            if (args.Length == 3)
                                props.BarcodesName = args[1];
                            projectFolder = args[args.Length - 1];
                            mapper = new StrtReadMapper(props);
                            mapper.BarcodeStats(projectFolder);
                            break;

                        case "upd":
                            CheckArgs(args, 3, 4);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            genome = StrtGenome.GetGenome(args[argOffset++]);
                            genome.ReadLen = readLen;
                            mapper = new StrtReadMapper(props);
                            mapper.UpdateSilverBulletGenes(genome, args[args.Length - 1]);
                            break;

                        case "synt":
                            CheckArgs(args, 5, 5);
                            barcodes = Barcodes.GetBarcodes(args[1]);
                            genome = StrtGenome.GetGenome(args[2], args[3].StartsWith("a"));
                            SyntReadMaker srm = new SyntReadMaker(barcodes, genome);
                            Console.WriteLine(srm.SettingsString());
                            srm.SynthetizeReads(args[4]);
                            break;

                        default:
                            Console.WriteLine("Unknown command: {0}", cmd);
                            break;
                    }
                    return;
                }
                catch (ArgumentException exp)
                {
                    Console.WriteLine("\nERROR: " + exp);
                }
                catch (Exception exp)
                {
                    Console.WriteLine("\nFATAL ERROR: " + exp);
                    return;
                }
            }
            Console.WriteLine("\nUsage:\n\n" +
                "SB.exe rf [rpkm] <Build> [<Annot>] <Outfile>       -    load transcripts and dump to refFlat-like file.\n" +
                "SB.exe q [<RunLaneSpec>]+ [rpkm] <Bc> [<Build>|<Idx>] [all|single] <ProjectPath>\n" +
                "      extract data, run Bowtie, and annotate in one sweep using default parameters.\n" +
                "      Use 'rpkm' to analyze standard Illumina non-directional random primed reads.\n" +
                "SB.exe x [<RunLaneSpec>]+ <Bc> [-Lt n] <ProjectPath>  -   extract data from the reads folder.\n" +
                "SB.exe ab [-oNAME] [rpkm|rpm|multimap|5primemap|all|single]* [<Build>|<Idx>] <ProjectPath>|<ExtractedPath>\n" +
                "      annotate data from .map files in latest/specified Extracted folder.\n" +
                "      you can give a non-standard output folder name using -o\n" +
                "      Use 'all'/'single' to force analysis of all/single transcript variants.\n" +
                "      Use 'rpkm' to analyze standard Illumina non-directional random primed reads.\n" +
                "      Use '5primemap' to annotate reads/molecules to (one of) the transcript(s) they match closest to 5' end.\n" +
                "      Use 'multimap' to annotate reads/molecules to every alternative transcript they match.\n" +
                "      If Build/Idx is left out, these are taken from the <ProjectName>_SampleLayout.txt file in the project folder.\n" +
                "      Will start by running Bowtie if .map files are missing.\n" +
                "SB.exe download <Genus_species>                      -   download latest genome build and annotations.\n" +
                "SB.exe idx <readLen> <Build> [<Annot>]               -   build annotations and Bowtie index.\n" +
                "SB.exe bt <Build>|<Idx> all|single <ProjectPath>|<ExtractedPath>\n" +
                "      run Bowtie on latest/specified extracted data folder.\n" +
                "SB.exe synt <Bc> <Idx> all|single <OutputFolder>     -   generate synthetic reads from a genome.\n" +
                "SB.exe stats [<Bc>] <ProjectPath>                    -   calculate barcode statistics.\n" +
                "SB.exe dumpfasta <Idx> <OutputPath>\n                -   dump all transcript sequences to fasta file.\n" +
                "SB.exe dump <Idx> <readLen> [<Step> [<MaxPerGene> [<MinOverhang> [Splices|Linear [<bcSet>]]]]] [<OutputPath>]\n" +
                "      make fq file of transcript fragments. Makes all if MaxPerGene=0. Adds barcodes+GGG if bcSet given.\n\n" + 
                "<RunLaneSpec> E.g. '17:235[:,,AGCTTG]', i.e. lanes 2,3,5 of run 17 [and only idx read AGCTTG of lane 5].\n" +
                "              Regexps are allowed for idx read matching, e.g. AG?TTG.\n" +
                "<Build> E.g. 'mm9', 'hg19', or 'gg3', <Annot> is 'UCSC', 'VEGA', or 'ENSEMBL' (Default: 'UCSC')\n" +
                "<Idx>   Specific Bowtie index, e.g. 'hg19_UCSC' or 'mm9_VEGA'.\n" +
                "<Bc>    'v2' (96x6-mer), 'v4' (48x6-mer, random tags), 'v4r' (no random tags), or 'no' for no barcodes.\n" +
                "-Lt n   Limit number of reads used to n. t is one of TotalReads, ValidReads, TotalReadsPerBc, ValidReadsPerBc\n" +
                "Define other barcode sets in 'Bc.barcodes' files in the barcodes directory in the project folder\n" +
                "<readLen> Sequence length after barcode and GGG. For idx it should be 0-5 below actual data length.\n" +
                "Paths are per default rooted in the data directory, so that e.g. 'L006' is enough as a ProjectPath.\n"
            );
        }

        private static List<string> ExtractLaneArgs(string[] args, ref int argIdx)
        {
            List<string> laneArgs = new List<string>();
            while (argIdx < args.Length && args[argIdx].Contains(":"))
            {
                string[] parts = args[argIdx].Split(':');
                int junkInt;
                if (!int.TryParse(parts[0], out junkInt) || !int.TryParse(parts[1], out junkInt))
                    throw new Exception("Illegal specification of run and lane: " + args[argIdx]);
                if (parts.Length >= 3 && parts[1].Length != parts[2].Split(',').Length)
                    throw new Exception("# of fields in index read filter part must equal # of lanes: " + args[argIdx]); 
                laneArgs.Add(args[argIdx]);
                argIdx++;
            }
            return laneArgs;
        }

    }
}
