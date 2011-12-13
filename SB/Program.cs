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
                string speciesArg = "";
                bool analyzeAllGeneVariants = Props.props.AnalyzeAllGeneVariants;
                StrtGenome genome;
                Props props = Props.props;
                StrtReadMapper mapper;
                string projectFolder;
                List<string> laneArgs;
                int readLen = props.StandardReadLen;
                int argOffset = 1;
                string cmd = args[0];
                try
                {
                    switch (cmd)
                    {
                        case "q":
                            laneArgs = ExtractLaneArgs(args, ref argOffset);
                            if (args[argOffset].ToLower() == "rpkm")
                            {
                                argOffset++;
                                props.DirectionalReads = false;
                                props.UseRPKM = true;
                            }
                            CheckArgs(args, argOffset + 2, argOffset + 4);
                            props.BarcodesName = args[argOffset++];
                            if (args[argOffset] != "all" && args[argOffset] != "single" && args.Length > argOffset + 1)
                                speciesArg = args[argOffset++];
                            if (args[argOffset] == "all" || args[argOffset] == "single" && args.Length > argOffset + 1)
                                analyzeAllGeneVariants = (args[argOffset++].ToLower().StartsWith("a")) ? true : false;
                            projectFolder = args[argOffset];
                            mapper = new StrtReadMapper(props);
                            List<LaneInfo> extrInfos = mapper.Extract(projectFolder, laneArgs);
                            if (speciesArg != "")
                                mapper.MapAndAnnotate(projectFolder, speciesArg, analyzeAllGeneVariants);
                            else
                                mapper.MapAndAnnotateWithLayout(projectFolder, "NotSpecified", analyzeAllGeneVariants);
                            break;

                        case "downloadmart":
                            UCSCGenomeDownloader gdm = new UCSCGenomeDownloader();
                            string abbrev, threeName;
                            gdm.ParseSpecies(args[argOffset], out threeName, out abbrev);
                            string destDir = args[argOffset + 1];
                            gdm.DownloadMartAnnotations(abbrev, destDir);
                            break;

                        case "download":
                            UCSCGenomeDownloader gd = new UCSCGenomeDownloader();
                            gd.DownloadGenome(args[argOffset]);
                            break;

                        case "sort":
                            props.BarcodesName = args[argOffset++];
                            MapMergeSorter bmfs = new MapMergeSorter(props.Barcodes);
                            List<string> inFiles = new List<string>();
                            for (int i = argOffset; i < args.Length - 1; i++)
                                inFiles.Add(args[i]);
                            bmfs.MergeSort(inFiles, args[args.Length - 1]);
                            break;

                        case "x":
                            laneArgs = ExtractLaneArgs(args, ref argOffset);
                            CheckArgs(args, argOffset + 2, argOffset + 2);
                            props.BarcodesName = args[argOffset];
                            mapper = new StrtReadMapper(props);
                            projectFolder = args[argOffset + 1];
                            mapper.Extract(projectFolder, laneArgs);
                            break;

                        case "bt":
                            CheckArgs(args, 4, 4);
                            speciesArg = args[1];
                            analyzeAllGeneVariants = (args[2].ToLower().StartsWith("a")) ? true : false;
                            projectFolder = args[3];
                            mapper = new StrtReadMapper(props);
                            mapper.Map(projectFolder, speciesArg, analyzeAllGeneVariants);
                            break;

                        case "ab":
                            if (args[argOffset].ToLower() == "rpkm")
                            {
                                argOffset++;
                                props.DirectionalReads = false;
                                props.UseRPKM = true;
                            }
                            CheckArgs(args, argOffset + 1, argOffset + 3);
                            if (args[argOffset] != "all" && args[argOffset] != "single" && args.Length > argOffset + 1)
                                speciesArg = args[argOffset++];
                            if (args[argOffset] == "all" || args[argOffset] == "single" && args.Length > argOffset + 1)
                                analyzeAllGeneVariants = (args[argOffset++].ToLower().StartsWith("a")) ? true : false;
                            projectFolder = args[argOffset];
                            mapper = new StrtReadMapper(props);
                            if (speciesArg != "")
                                mapper.MapAndAnnotate(projectFolder, speciesArg, analyzeAllGeneVariants);
                            else
                                mapper.MapAndAnnotateWithLayout(projectFolder, "NotSpecified", analyzeAllGeneVariants);
                            break;

                        case "jct":
                            CheckArgs(args, 3, 6);
                            argOffset = 1;
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            genome = StrtGenome.GetGenome(args[argOffset++], args[argOffset++].StartsWith("a"));
                            genome.ReadLen = readLen;
                            if (args.Length >= argOffset + 2)
                            {
                                genome.Build = args[argOffset++];
                                genome.Annotation = args[argOffset++];
                            }
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctions(genome);
                            break;

                        case "idx":
                            CheckArgs(args, 2, 7);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            string newBtIdxName = "";
                            bool definedVariants = false;
                            genome = StrtGenome.GetGenome(args[argOffset]);
                            if (args[argOffset++].IndexOf(genome.GeneVariantsChar + genome.Annotation) > 0)
                                definedVariants = true;
                            genome.ReadLen = readLen;
                            if (args.Length > argOffset)
                            {
                                if (args[argOffset] == "single" || args[argOffset] == "all")
                                {
                                    genome.GeneVariants = (args[argOffset++] == "all");
                                    definedVariants = true;
                                    argOffset++;
                                }
                                if (args.Length == argOffset + 1) newBtIdxName = args[argOffset];
                                else if (args.Length > argOffset + 1)
                                {
                                    genome.Build = args[argOffset];
                                    genome.Annotation = args[argOffset + 1];
                                    if (args.Length == argOffset + 3)
                                        newBtIdxName = args[argOffset + 2];
                                }
                            }
                            mapper = new StrtReadMapper(props);
                            if (!definedVariants && newBtIdxName == "")
                            {
                                genome.GeneVariants = true;
                                mapper.BuildJunctionsAndIndex(genome, newBtIdxName);
                                genome.GeneVariants = false;
                            }
                            mapper.BuildJunctionsAndIndex(genome, newBtIdxName);
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
                                makeSplices = args[6] .ToUpper().StartsWith("S");
                            if (args.Length > 7)
                                barcodes = Barcodes.GetBarcodes(args[7]);
                            mapper = new StrtReadMapper(props);
                            int maxSkip = props.MaxExonsSkip;
                            mapper.DumpTranscripts(barcodes, genome, readLength, step, maxPerGene, outputPath, makeSplices, minOverhang, maxSkip);
                            break;

                        case "writehitmap":
                            HitMapAnnotator hma2 = new HitMapAnnotator();
                            hma2.InitHitMapFromReadMapFile(args[1]);
                            Console.WriteLine("Writing SilverBullet txt formatted data to " + args[2]);
                            hma2.WriteHitMapToSbaFile(args[2]);
                            break;


                        case "serializehitmap":
                            HitMapAnnotator hma1 = new HitMapAnnotator();
                            hma1.InitHitMapFromReadMapFile(args[1]);
                            Console.WriteLine("Writing .NET serialized data to " + args[2]);
                            hma1.Serialize(args[2]);
                            string sbaFile = Path.GetFileNameWithoutExtension(args[2]) + ".sba";
                            Console.WriteLine("Writing SilverBullet txt formatted data to " + sbaFile);
                            hma1.WriteHitMapToSbaFile(sbaFile);
                            break;

                        case "makehmap":
                            readLen = int.Parse(args[argOffset++]);
                            genome = StrtGenome.GetGenome(args[argOffset++]);
                            genome.ReadLen = readLen;
                            string origFqPath = args[argOffset++];
                            string mapPath = args[argOffset++];
                            string hmapPath = args[argOffset++];
                            string remainFqPath = args[argOffset++];
                            TagMappingFile tmf = new TagMappingFile(genome);
                            tmf.TranslateMapFile(origFqPath, mapPath, hmapPath, remainFqPath);
                            break;

                        case "hitmapannotate":
                            HitMapAnnotator hma = new HitMapAnnotator();
                            hma.InitHitMapFromFile(args[1]);
                            Console.WriteLine("Setting barcodes to " + args[2]);
                            props.BarcodesName = args[2];
                            hma.InitAnalysis(props.Barcodes);
                            string[] mapFiles = Directory.GetFiles(args[3], args[4]);
                            hma.AnnotatateMapFiles(args[3], mapFiles);
                            hma.WriteSummary();
                            Console.WriteLine("Writing output to " + args[args.Length - 1]);
                            hma.WriteRawCounts(args[args.Length - 1]);
                            if (args[1].EndsWith(".map"))
                            {
                                string outf = args[1].Replace(".map", ".sba");
                                Console.WriteLine("Saving annotations to SilverBullet formatted txt file " + outf);
                                hma.WriteHitMapToSbaFile(outf);
                            }
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
                            NonExonRepeatMasker nerm = new NonExonRepeatMasker();
                            genome = StrtGenome.GetGenome(args[argOffset++]);
                            int minFlank = int.Parse(args[argOffset++]);
                            int maxIntronToKeep = int.Parse(args[argOffset++]);
                            string outFolder = args[argOffset++];
                            nerm.Mask(genome, outFolder, minFlank, maxIntronToKeep);
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
                            SyntReadMaker srm = new SyntReadMaker(barcodes);
                            genome = StrtGenome.GetGenome(args[2], args[3].StartsWith("a"));
                            srm.SynthetizeReads(genome, args[4]);
                            break;

                        default:
                            Console.WriteLine("Unknown command: {0}", cmd);
                            break;
                    }
                    return;
                }
                catch (NoAnnotationsFileFoundException nafe)
                {
                    Console.WriteLine("\nERROR: " + nafe.Message);
                }
                catch (NoMapFilesFoundException)
                {
                    Console.WriteLine("\nERROR: No .map files were found (use bt function)");
                }
                catch (ChromosomeMissingException ec)
                {
                    Console.WriteLine("\nERROR: " + ec.Message);
                    Console.WriteLine("Make sure that the proper fasta/genbank file is in the genomes directory.");
                }
                catch (Exception exp)
                {
                    Console.WriteLine("\nFATAL ERROR: " + exp);
                }
                return;
            }
            Console.WriteLine("\nUsage:\n\n" +
                "SB.exe q [<RunLaneSpec>]+ [rpkm] <BcSet> [<Sp>|<IdxName>] [all|single] <ProjectPath>\n" +
                "    - extract data, run Bowtie, and annotate in one sweep using default parameters\n" +
                "SB.exe x [<RunLaneSpec>]+ <BcSet> <ProjectPath>\n    - extract data from the [common] reads folder\n" +
                "SB.exe ab [rpkm] [<Sp>|<IdxName>] [all|single] <ProjectPath>|<ExtractedPath>\n" +
                "    - annotate data from .map files in latest/specified Extracted folder.\n" +
                "      Use 'all'/'single' to force analysis of all/single transcript variants. First runs Bowtie if .map files are missing.\n" +
                "SB.exe download <Genus_species>\n    - download latest genome build and annotations for the given species from UCSC\n" +
                "SB.exe downloadmart <Genus_species> <GenomeFolderPath>\n" +
                "    - download BioMart VEGA/ENSEMBL for the given species. Specify the path to folder where chromosomes of same build reside.\n" +
                "SB.exe idx [<readLen>] <Sp_or_Build> [all|single] [<IdxName> | <Build> <Annot> [<IdxName]>]]\n" +
                "    - build annotations and Bowtie index [using specified genome build and annotations when Sp is given as first arg]\n" +
                "      if neither 'all', 'single', nor IdxName is given, both transcript variant versions of index will be built\n" +
                "SB.exe upd [<readLen>] [<Build> <Annot> | <Sp>] <AnnotErrorFile>\n    - update SilverBullet annotations of 5' ends using the specified XXX_annot_errors_xxx.tab file.\n" +
                "SB.exe bt <Sp>|<IdxName> all|single <ProjectPath>|<ExtractedPath>\n    - run Bowtie on latest/specified Extracted folder\n" +
                "SB.exe aw <Sp> <MapFolderPath>/n    - annotate data from Wiggles .wig files folder\n" +
                "SB.exe sort <BcSet> [<MapFile>]+ <outFile>\n    - sort and merge specified .map files\n" +
                "SB.exe hitmapannotate <annotFile> <BcSet> <MapFilesFolder> <MapFilePattern> <outputFile>\n" +
                "SB.exe split [<BcSet>] <ProjectPath>\n    - split data by barcode\n" +
                "SB.exe synt <BcSet> <IdxName> all|single <OutputFolder>\n" +
                "    - generate synthetic reads from a genome\n" +
                "SB.exe stats [<BcSet>] <ProjectPath>\n    - calculate barcode statistics\n" +
                "SB.exe makehmap <readLen> <genome> <origFqFile> <mapFile> <hmapPath> <remainFqFile>\n" +
                "    - make .hmap file of multiread mappings using a bowtie mapping of all reads for a genome\n" +
                "SB.exe mapsnp <BcSet> <outputFile> [<mapFile>]+\n    - analyze a set of map files to find potential SNP locations\n" +
                "SB.exe maskchr <genome> <minLocusFlank> <maxIntronSizeToSave> <outputFolder>\n    - mask non-exon regions of chromosomes with 'N':s\n" +
                "SB.exe dump <IdxName> [<readLen> [<Step> [<MaxPerGene> [<MinOverhang> [Splices|Linear [<bcSet>]]]]]] [<OutputPath>]\n" +
                "    - make fq file of transcript fragments. Makes all if MaxPerGene=0. Adds barcodes+GGG if bcSet given \n\n" + 
                "<RunLaneSpec> is e.g. '17:235' indicating lanes 2,3, and 5 of run 17.\n" + 
                "              If left out, defaults to all sequence files in Reads/ folder under ProjectPath\n" +
                "rpkm will change analysis method to non-directional reads and output RPKM instead of RPM\n" + 
                "<Sp> is 'Mm' or 'Hs'. If left out, species are taken from the <ProjectName>_SampleLayout.txt file in the project folder.\n" +
                "<Build> is 'mm9', 'hg19', or 'gg3', <Annot> is 'UCSC', 'VEGA', or 'ENSE',\n" + 
                "Annot defaults to 'UCSC' when only species is given.\n" +
                "<IdxName> is a specific Bowtie index, e.g. 'hg19_UCSC' or 'mm9_sVEGA'.\n" +
                "<BcSet> is 'v1' (96x5-mer), 'v2' (96x6-mer), 'v4' (48x6-mer), 'v4rnd' (48x6-mer w. molecule counting), or 'no' for no barcodes\n" +
                "Define non-standard barcode sets in files in the barcodes directory in the project folder\n" +
                "<readLen> is the sequence length after barcode and GGG. When omitted, standard len is taken from config.\n" +
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
                laneArgs.Add(args[argIdx]);
                argIdx++;
            }
            return laneArgs;
        }

    }
}
