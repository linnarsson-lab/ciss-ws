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
                string annotationFile = "";
                string cmd = args[0];
                try
                {
                    switch (cmd)
                    {
                        case "q":
                            props.InsertCells10Data = false; // Default to not insert
                            options = new QXMAOptions(args);
                            props.DirectionalReads = options.directionalReads;
                            props.UseRPKM = options.useRPKM;
                            props.SenseStrandIsSequenced = options.readSequenceIsSense;
                            props.BarcodesName = options.barcodesName;
                            props.TotalNumberOfAddedSpikeMolecules = options.totalSpikeMols;
                            projectFolder = options.projectFolder;
                            mapper = new StrtReadMapper(props);
                            List<LaneInfo> extrInfos = mapper.Extract(projectFolder, options.laneArgs, options.resultFolder);
                            string extractedFolder = extrInfos[0].extractionFolder;
                            mapper.MapAndAnnotate(extractedFolder, options.speciesAbbrev, options.analyzeAllGeneVariants,
                                                  options.annotation, "", options.specificBcIdxs);
                            break;

                        case "downloadmart":
                            UCSCGenomeDownloader gdm = new UCSCGenomeDownloader();
                            string abbrev, threeName;
                            gdm.ParseSpecies(args[argOffset], out threeName, out abbrev);
                            string destDir = (args.Length > argOffset + 1) ? args[argOffset + 1] : "";
                            gdm.DownloadMartAnnotations(abbrev, destDir);
                            break;

                        case "knowngene2refflat":
                        case "mart2refflat":
                            genome = StrtGenome.GetGenome(args[1], Props.props.AnalyzeAllGeneVariants, "", false);
                            if (!args[1].Contains("_s"))
                                genome.GeneVariants = true;
                            string outfile = Path.Combine(genome.GetOriginalGenomeFolder(), genome.BuildVarAnnot + "_refFlat.txt");
                            if (args.Length > 2)
                                outfile = args[2];
                            AnnotationReader ar = AnnotationReader.GetAnnotationReader(genome);
                            int nModels = ar.BuildGeneModelsByChr(false);
                            using (StreamWriter mw = outfile.OpenWrite())
                            {
                                foreach (GeneFeature gf in ar.IterChrSortedGeneModels())
                                {
                                    gf.Name = gf.Name.Split('_')[0];
                                    mw.WriteLine(gf.ToRefFlatString());
                                }
                            }
                            Console.WriteLine("Wrote {0} gene models to {1}", nModels, outfile);
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
                            props.InsertCells10Data = false; // Default to not insert
                            options = new QXMAOptions(args);
                            props.DirectionalReads = options.directionalReads;
                            props.UseRPKM = options.useRPKM;
                            props.SenseStrandIsSequenced = options.readSequenceIsSense;
                            props.TotalNumberOfAddedSpikeMolecules = options.totalSpikeMols;
                            mapper = new StrtReadMapper(props);
                            mapper.MapAndAnnotate(options.projectFolder, options.speciesAbbrev, options.analyzeAllGeneVariants,
                                                            options.annotation, options.resultFolder, options.specificBcIdxs);
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
                            else
                                readLen = props.StandardReadLen;
                            genome = StrtGenome.GetBaseGenome(args[argOffset++]);
                            if (args.Length > argOffset && StrtGenome.IsASpliceAnnotation(args[argOffset]))
                                genome.Annotation = args[argOffset++];
                            if (args.Length > argOffset)
                                annotationFile = args[argOffset++];
                            genome.ReadLen = readLen;
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctions(genome, annotationFile);
                            break;

                        case "idx":
                            CheckArgs(args, 2, 6);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            else
                                readLen = props.StandardReadLen;
                            genome = StrtGenome.GetBaseGenome(args[argOffset++]);
                            if (args.Length > argOffset && StrtGenome.IsASpliceAnnotation(args[argOffset]))
                                genome.Annotation = args[argOffset++];
                            if (args.Length > argOffset)
                                annotationFile = args[argOffset++];
                            genome.ReadLen = readLen;
                            mapper = new StrtReadMapper(props);
                            mapper.BuildJunctionsAndIndex(genome, annotationFile);
                            break;

                        case "dumpfasta":
                            genome = StrtGenome.GetGenome(args[1]);
                            string fastaPath = args[2];
                            int flankLength = (args.Length > 3)? int.Parse(args[3]) : 0;
                            mapper = new StrtReadMapper(props);
                            mapper.DumpTranscripts(null, genome, 0, 0, 0, fastaPath, false, false, 0, 0, flankLength);
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
                                                   makeSplices, minOverhang, maxSkip, 0);
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

                        case "upd":
                            CheckArgs(args, 3, 5);
                            if (int.TryParse(args[argOffset], out readLen))
                                argOffset++;
                            else
                                readLen = props.StandardReadLen;
                            genome = StrtGenome.GetGenome(args[argOffset++]);
                            genome.ReadLen = readLen;
                            annotationFile = "";
                            if (args.Length > argOffset + 1)
                                annotationFile = args[argOffset++];
                            string errorsPath = args[argOffset];
                            mapper = new StrtReadMapper(props);
                            mapper.UpdateSilverBulletGenes(genome, errorsPath, annotationFile);
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
            Console.WriteLine("\nValid commands (mono SB.exe command options...):\n\n" +
                "x [RUNLANESPEC]+ BC [-Lt N] PROJECTPATH              extract data from the reads folder.\n" +
                "   -Lt N     limit number of reads used to N. t is one of TotalReads, ValidReads, TotalReadsPerBc, ValidReadsPerBc\n" +
                "q [RUNLANESPEC]+ BC [ANNOTATIONOPTION]* [-cN] [-BcIndexes...] [BUILD|IDX] PROJECTPATH\n" +
                "   extract data, run Bowtie, and annotate in one sweep using default parameters.\n" +
                "ab [-oNAME] [ANNOTATIONOPTION]* [-cN] [-BcIndexes...] [BUILD|IDX] PROJECTPATH|EXTRACTEDPATH\n" +
                "   annotate extracted reads in latest/specified Extracted folder. Will start by running Bowtie if .map files are missing.\n" +
                "   RUNLANESPEC  E.g. '17:235[:,,AGCTTG]', i.e. lanes 2,3,5 of run 17 [and only idx read AGCTTG of lane 5].\n" +
                "                Regexps are allowed for idx read matching, e.g. AG?TTG.\n" +
                "   -oNAME       Use a non-standard output folder\n" +
                "   -cN          Specify total # of spike molecules.\n" +
                "                Individual fractions are taken from 2nd column of " + PathHandler.GetCTRLConcPath() + "\n" +
                "   -BcIndexes M,N[,...] Only process the specified barcodes indexes, even if the barcode set contains more indexes.\n" +
                "   ANNOTATIONOPTION can be (default values first):\n" +
                "     single/all              select between one per-gene summarizing value or separate values for all known transcript variants.\n" +
                "     rpm/rpkm                specify rpkm to calculate rpkm values instead of rpm, for e.g. TruSeq samples.\n" +
                "     sense/antisense/nondir  specify sequence direction of reads\n" +
                "     5primemap/multimap      annotate reads/molecules to (one of) the transcript(s) they match closest to the 5' end of,\n" +
                "                             or multi-annotate to every alternative transcript they match.\n" +
                "     insertc1data            insert data into the Sanger cells10k database when this is a C1 sample.\n" +
                "   If BUILD/IDX is left out, these are taken from the xxx_SampleLayout.txt file in the project folder.\n" +
                "bt BUILD|IDX all|single PROJECTPATH|EXTRACTEDPATH    run Bowtie on latest/specified extracted data folder.\n" +
                "download GENUS_SPECIES                               download latest genome build and annotations, for e.g. 'Mus_musculus'\n" +
                "mart2refflat IDX [OUTFILE]                           make a refFlat file from mart-style annotations.\n" +
                "idx READLEN BUILD [ANNOT]  [ANNOTFILE]               build annotations and Bowtie index. Specify 'annotfile' to overide default.\n" +
                "knowngene2refflat IDX [OUTFILE]                      make a refFlat file from UCSC knownGene.txt annotations.\n" +
                "upd [READLEN] IDX [ANNOTFILE] ERRORFILE              update 5' end annotations from an 'annot_errors.tab' file. Specify 'annotfile' to overide default.\n" +
                "synt BC IDX all|single OUTFOLDER                     generate synthetic reads from a genome.\n" +
                "rf [rpkm] BUILD [ANNOT] OUTFILE                      load transcripts and dump to refFlat-like file.\n" +
                "dumpfasta IDX OUTFILE [FLANKLEN]\n" +
                "   dump all transcript sequences, including any config-extension, [with additional 5' and 3' flanks] to fasta file.\n" +
                "dump IDX READLEN [STEP [MAXPERGENE [MINOVERHANG [Splices|Linear [BCSET]]]]] [OUTFILE]\n" +
                "   make a fq file containing transcript fragments as reads defined by IDX, at every STEP bases in transript models.\n" +
                "   Make all if MAXPERGENE=0. Linear=never splice out exons. MINOVERHANG limits exonic end size at a junction. Adds barcodes+GGG if BCSET given.\n\n" + 
                "   BUILD E.g. 'mm10', 'hg19', or 'gg3'\n" +
                "   ANNOT is 'UCSC', 'GENC', 'UALL', 'VEGA', 'ENSE', or 'RFSQ' (Default: 'UCSC', synonymous with 'RFSQ')\n" +
                "   IDX   Specific Bowtie index, e.g. 'hg19_UCSC' or 'mm9_aVEGA'.\n" +
                "   BC   'C1Plate1', 'C1Plate2', 'TruSeq', 'v4' (48x6-mer, random tags), 'v4r' (no random tags), or 'no' for no barcodes.\n" +
                "   Define other barcode sets in 'Bc.barcodes' files in the barcodes sub-directory of the data folder\n" +
                "   READLEN Seq len after barcode and GGG. The Bowtie index that is 0-5 below actual read len is used during annotation.\n" +
                "   Paths are per default rooted in the data folder, so that e.g. 'S066' is enough as a PROJECTPATH.\n"
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
