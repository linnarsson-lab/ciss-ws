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
        public bool useMost5PrimeExonMapping = true;
        public string speciesAbbrev = "";
        public string geneVariantsChar = Props.props.AnalyzeAllGeneVariants? "a" : "s";
        public bool analyzeAllGeneVariants { get { return geneVariantsChar == "a"; } }
        public string annotation = StrtGenome.DefaultAnnotationSource;
        public string resultFolder;
        public string projectFolder;
        public int[] specificBcIdxs = null;
        public int totalSpikeMols = Props.props.TotalNumberOfAddedSpikeMolecules;

        public QXMAOptions(string[] args)
        {
            int argOffset = 1;
            int tempVer;
            Match m;
            string[] allBcSetNames = Barcodes.GetAllPredefinedBarcodeSetNames();
            while (argOffset < args.Length - 1)
            {
                string opt = args[argOffset];
                if (Array.IndexOf(StrtGenome.AnnotationSources, opt) >= 0)
                    annotation = opt;
                else
                {
                    switch (opt)
                    {
                        case "insertc1data":
                            Props.props.InsertCells10Data = true;
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
                            break;
                        case "multimap":
                            useMost5PrimeExonMapping = false;
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
                        case "-BcIndexes":
                            specificBcIdxs = Array.ConvertAll(args[++argOffset].Split(','), v => int.Parse(v));
                            break;
                        default:
                            if (opt.StartsWith("-o"))
                                resultFolder = opt.Substring(2);
                            else if (opt.StartsWith("-c"))
                                totalSpikeMols = int.Parse(opt.Substring(2));
                            else if (opt.StartsWith("--genome="))
                                speciesAbbrev = opt.Substring(9);
                            else if (Array.IndexOf(allBcSetNames, opt) >= 0)
                                barcodesName = opt;
                            else if (Array.IndexOf(new string[] { "hs", "hg", "mm", "gg", "ce", "cg" }, opt.ToLower()) >= 0)
                                speciesAbbrev = opt.ToLower();
                            else if ((opt[0] == 'a' || opt[0] == 's') && Array.IndexOf(StrtGenome.AnnotationSources, opt.Substring(1)) >= 0)
                            {
                                geneVariantsChar = opt.Substring(0, 1);
                                annotation = opt.Substring(1);
                            }
                            else if (opt.Length >= 3 && Array.IndexOf(new string[] { "hs", "hg", "mm", "gg", "ce", "cg" }, opt.Substring(0, 2).ToLower()) >= 0
                                && int.TryParse(opt.Substring(2), out tempVer))
                                speciesAbbrev = opt;
                            else if (Regex.Match(opt, "[0-9A-Za-z]+:[0-9]+").Success)
                                laneArgs.Add(opt);
                            else
                            {
                                string srcOr = string.Join("|", StrtGenome.AnnotationSources);
                                if ((m = Regex.Match(opt, "(.+[0-9]*)_([as]?)(" + srcOr + ")")).Success)
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
