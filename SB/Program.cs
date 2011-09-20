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
                            mapper.Extract(projectFolder, laneArgs);
                            if (speciesArg != "")
                                mapper.MapAndAnnotate(projectFolder, speciesArg, analyzeAllGeneVariants);
                            else
                                mapper.MapAndAnnotateWithLayout(projectFolder, "NotSpecified", analyzeAllGeneVariants);
                            break;

                        case "rerunall":
                            ProjectDB pdb = new ProjectDB();
                            foreach (ProjectDescription pd in pdb.GetProjectDescriptions())
                            {
                                Console.WriteLine("Updating {0}...", pd.projectName);
                                props.BarcodesName = pd.barcodeSet;
                                mapper = new StrtReadMapper(props);
                                mapper.Extract(pd);
                                mapper.MapAndAnnotateWithLayout(pd.ProjectFolder, pd.defaultSpecies, Props.props.AnalyzeAllGeneVariants);
                            }
                            Console.WriteLine("Finished updating.");
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
                            BowtieMapFileSorter bmfs = new BowtieMapFileSorter();
                            bmfs.SortMapFile(args[argOffset]);
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
                            projectFolder = args[3];
                            genome = StrtGenome.GetGenome(args[1], args[2].StartsWith("a"));
                            string buildName = genome.GetBowtieIndexName();
                            mapper = new StrtReadMapper(props);
                            mapper.RunBowtie(buildName, projectFolder);
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

                        case "aw":
                            CheckArgs(args, 3, 3);
                            genome = StrtGenome.GetGenome(args[1]);
                            string wigFolder = args[2];
                            mapper = new StrtReadMapper(props);
                            mapper.AnnotateFromWiggles(wigFolder, genome);
                            break;

                        case "idx":
                            CheckArgs(args, 2, 6);
                            string newBtIdxName = "";
                            bool definedVariants = false;
                            genome = StrtGenome.GetGenome(args[1]);
                            if (args.Length > 2)
                            {
                                int argIdx = 2;
                                if (args[2] == "single" || args[2] == "all")
                                {
                                    genome.GeneVariants = (args[2] == "all");
                                    definedVariants = true;
                                    argIdx++;
                                }
                                if (args.Length == argIdx + 1) newBtIdxName = args[argIdx];
                                else if (args.Length > argIdx + 1)
                                {
                                    genome.Build = args[argIdx];
                                    genome.Annotation = args[argIdx + 1];
                                    if (args.Length == argIdx + 3)
                                        newBtIdxName = args[argIdx + 2];
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
                            int readLength = 0, step = 1, maxPerGene = 20;
                            int junk;
                            string outputPath = "";
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
                            mapper = new StrtReadMapper(props);
                            mapper.DumpTranscripts(genome, readLength, step, maxPerGene, outputPath);
                            break;

                        case "jct":
                            CheckArgs(args, 3, 5);
                            genome = StrtGenome.GetGenome(args[1], args[2].StartsWith("a"));
                            if (args.Length == 5)
                            {
                                genome.Build = args[3];
                                genome.Annotation = args[4];
                            }
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctions(genome);
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
                            CheckArgs(args, 3, 3);
                            genome = StrtGenome.GetGenome(args[1]);
                            mapper = new StrtReadMapper(props);
                            mapper.UpdateSilverBulletGenes(genome, args[args.Length - 1]);
                            break;

                        case "synt":
                            CheckArgs(args, 5, 5);
                            props.BarcodesName = args[1];
                            mapper = new StrtReadMapper(props);
                            genome = StrtGenome.GetGenome(args[2], args[3].StartsWith("a"));
                            mapper.SynthetizeReads(genome, args[4]);
                            break;

                        default:
                            Console.WriteLine("Unknown command: {0}", cmd);
                            break;
                    }
                    return;
                }
                catch (NoAnnotationsFileFoundException)
                {
                    Console.WriteLine("\nERROR: The gene annotations file was not found (use idx function)");
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
            }
            Console.WriteLine("\nUsage:\n\n" +
                "SB.exe rerunall\n   - reprocesses (with up-to-date software/annotations) all projects in database\n" +
                "SB.exe q [<RunLaneSpec>]+ [rpkm] <BcSet> [<Sp>|<IdxName>] [all|single] <ProjectPath>\n" +
                "    - extract data, run Bowtie, and annotate in one sweep using default parameters\n" +
                "SB.exe x [<RunLaneSpec>]+ <BcSet> <ProjectPath>\n    - extract data from the [common] reads folder\n" +
                "SB.exe ab [rpkm] [<Sp>|<IdxName>] [all|single] <ProjectPath>|<ExtractedPath>\n" +
                "    - annotate data from .map files in latest/specified Extracted folder.\n" +
                "      Use 'all'/'single' to force analysis of all/single transcript variants. First runs Bowtie if .map files are missing.\n" +
                "SB.exe download <Genus_species>\n    - download latest genome build and annotations for the given species from UCSC\n" +
                "SB.exe downloadmart <Genus_species> <GenomeFolderPath>\n" +
                "    - download BioMart VEGA/ENSEMBL for the given species. Specify the path to folder where chromosomes of same build reside.\n" +
                "SB.exe idx <Sp_or_Build> [all|single] [<IdxName> | <Build> <Annot> [<IdxName]>]]\n" +
                "    - build annotations and Bowtie index [using specified genome build and annotations when Sp is given as first arg]\n" +
                "      if neither 'all', 'single', nor IdxName is given, both transcript variant versions of index will be built\n" +
                "SB.exe upd [<Build> <Annot> | <Sp>] <AnnotErrorFile>\n    - update SilverBullet annotations of 5' ends using the specified XXX_annot_errors_xxx.tab file.\n" +
                "SB.exe bt <Sp>|<IdxName> all|single <ProjectPath>|<ExtractedPath>\n    - run Bowtie on latest/specified Extracted folder\n" +
                "SB.exe aw <Sp> <MapFolderPath>/n    - annotate data from Wiggles .wig files folder\n" +
                "SB.exe split [<BcSet>] <ProjectPath>\n    - split data by barcode\n" +
                "SB.exe synt <BcSet> <IdxName> all|single <OutputFolder>\n" +
                "    - generate synthetic reads from a genome\n" +
                "SB.exe stats [<BcSet>] <ProjectPath>\n    - calculate barcode statistics\n" +
                "SB.exe dump <IdxName> [<readLen> [<Step> [<MaxPerGene>]]] [<OutputPath>]      - make fq file of transcript fragments\n\n" + 
                "<RunLaneSpec> is e.g. '17:235' indicating lanes 2,3, and 5 of run 17.\n" + 
                "              If left out, defaults to all sequence files in Reads/ folder under ProjectPath\n" +
                "rpkm will change analysis method to non-directional reads and output RPKM instead of RPM\n" + 
                "<Sp> is 'Mm' or 'Hs'. If left out, species are taken from the <ProjectName>_SampleLayout.txt file in the project folder.\n" +
                "<Build> is 'mm9', 'hg19', or 'gg3', <Annot> is 'UCSC', 'VEGA', or 'ENSE',\n" + 
                "Annot defaults to 'UCSC' when only species is given.\n" +
                "<IdxName> is a specific Bowtie index, e.g. 'hg19_UCSC' or 'mm9_sVEGA'.\n" +
                "<BcSet> is 'v1' (5 bases), 'v2' (6 bases, default), or 'no' for no barcodes\n" +
                "Define non-standard barcode sets in files in the barcodes directory in the project folder\n" +
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
