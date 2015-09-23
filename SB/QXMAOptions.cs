using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Linnarsson.Dna;

namespace CmdSilverBullet
{
    public class QXMAOptions
    {
        public List<string> laneArgs = new List<string>();
        public string barcodesName = "Unknown";
        public bool useRPKM = false;
        public bool directionalReads = true;
        public bool readSequenceIsSense = true;
        public ReadLimitType extractionReadLimitType = ReadLimitType.None;
        public int extractionReadLimit = 0;
        public bool useMost5PrimeExonMapping = Props.props.UseMost5PrimeExonMapping;
        public MultiReadMappingType multiReadMappingType = Props.props.DefaultExonMapping;
        public bool analyzeLoci = false;
        public string speciesAbbrev = "";
        public string geneVariantsChar = Props.props.AnalyzeAllGeneVariants? "a" : "s";
        public bool analyzeAllGeneVariants { get { return geneVariantsChar == "a"; } }
        public string annotation = StrtGenome.DefaultAnnotationSource;
        public string resultFolder;
        public string projectFolder;
        public int[] specificBcIdxs = null;
        public int totalSpikeMols = Props.props.TotalNumberOfAddedSpikeMolecules;
        public string Aligner = Props.props.Aligner;
        public byte qualityScoreBase = Props.props.QualityScoreBase;
        public string layoutFile = "";

        public QXMAOptions(string[] args)
        {
            int argOffset = 1;
            Match m;
            string[] allBcSetNames = Barcodes.GetAllKnownBarcodeSetNames();
            while (argOffset < args.Length - 1)
            {
                string opt = args[argOffset];
                if (Array.IndexOf(StrtGenome.AnnotationSources, opt) >= 0)
                    annotation = opt;
                else
                {
                    switch (opt)
                    {
                        case "compact-filter-singletons":
                        case "compact":
                        case "compact-no-filter":
                            Props.props.DenseUMICounter = true;
                            Props.props.RndTagMutationFilter = UMIMutationFilter.LowPassFilter;
                            Props.props.RndTagMutationFilterParam = (opt == "compact-no-filter")? 0 : 1;
                            break;
                        case "insertc1data":
                            Props.props.InsertCellDBData = true;
                            break;
                        case "rpkm":
                            useRPKM = true;
                            break;
                        case "rpm":
                            useRPKM = false;
                            break;
                        case "sense":
                            directionalReads = true;
                            readSequenceIsSense = true;
                            break;
                        case "antisense":
                            directionalReads = true;
                            readSequenceIsSense = false;
                            break;
                        case "nondir":
                            directionalReads = false;
                            break;                             
                        case "all":
                            geneVariantsChar = "a";
                            break;
                        case "single":
                            geneVariantsChar = "s";
                            break;
                        case "5primemap":
                            useMost5PrimeExonMapping = true;
                            multiReadMappingType = MultiReadMappingType.Most5Prime;
                            break;
                        case "multimap":
                            useMost5PrimeExonMapping = false;
                            multiReadMappingType = MultiReadMappingType.All;
                            break;
                        case "randommap":
                            useMost5PrimeExonMapping = false;
                            multiReadMappingType = MultiReadMappingType.Random;
                            break;
                        case "-STAR":
                            Aligner = "STAR";
                            break;
                        case "-bowtie":
                            Aligner = "bowtie";
                            break;
                        case "-loci":
                            analyzeLoci = true;
                            break;
                        case "-layout":
                            layoutFile = args[++argOffset];
                            break;
                        case "-LTotalReads":
                            extractionReadLimitType = ReadLimitType.TotalReads;
                            extractionReadLimit = int.Parse(args[++argOffset]);
                            break;
                        case "-LTotalReadsPerBc":
                            extractionReadLimitType = ReadLimitType.TotalReadsPerBarcode;
                            extractionReadLimit = int.Parse(args[++argOffset]);
                            break;
                        case "-LValidReads":
                            extractionReadLimitType = ReadLimitType.TotalValidReads;
                            extractionReadLimit = int.Parse(args[++argOffset]);
                            break;
                        case "-LValidReadsPerBc":
                            extractionReadLimitType = ReadLimitType.TotalValidReadsPerBarcode;
                            extractionReadLimit = int.Parse(args[++argOffset]);
                            break;
                        case "-scoreBase":
                            qualityScoreBase = byte.Parse(args[++argOffset]);
                            break;
                        case "-BcIndexes":
                            HashSet<int> idxs = new HashSet<int>();
                            string[] items = args[++argOffset].Split(',');
                            foreach (string item in items)
                            {
                                if (item.Contains('-'))
                                {
                                    string[] f = item.Split('-');
                                    for (int i = int.Parse(f[0]); i <= int.Parse(f[1]); i++)
                                        idxs.Add(i);
                                }
                                else
                                    idxs.Add(int.Parse(item));
                            }
                            specificBcIdxs = idxs.ToArray();
                            break;
                        default:
                            if (opt.StartsWith("-o"))
                                resultFolder = opt.Substring(2);
                            else if (opt.StartsWith("-c"))
                                totalSpikeMols = int.Parse(opt.Substring(2));
                            else if (opt.StartsWith("--genome="))
                                speciesAbbrev = opt.Substring(9);
                            else if (allBcSetNames.Contains(opt.ToLower()))
                                barcodesName = opt;
                            else if (Regex.Match(opt.ToLower(), "^(hs|hg|mm|gg|ce|cg)[0-9\\.]*$").Success)
                                speciesAbbrev = opt.ToLower();
                            else if (Array.IndexOf(StrtGenome.AnnotationSources, opt.ToUpper()) == 0)
                                annotation = opt.Substring(1);
                            else if (Regex.Match(opt, "^[0-9A-Za-z]+:[0-9]+").Success)
                                laneArgs.Add(opt);
                            else
                            {
                                string srcOr = string.Join("|", StrtGenome.AnnotationSources);
                                if ((m = Regex.Match(opt.ToLower(), "^([a-z]+[0-9\\.]*)_([as])(" + srcOr + ")$")).Success)
                                {
                                    speciesAbbrev = m.Groups[0].Value;
                                    geneVariantsChar = m.Groups[1].Value;
                                    annotation = m.Groups[2].Value;
                                }
                            }
                            break;
                    }
                }
                argOffset++;
            }
            projectFolder = args[argOffset];
        }
    }
}
