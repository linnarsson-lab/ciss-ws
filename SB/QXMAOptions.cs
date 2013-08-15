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
        public string barcodesName;
        public bool useRPKM = false;
        public bool directionalReads { get { return !useRPKM; } }
        public ReadLimitType extractionReadLimitType = ReadLimitType.None;
        public int extractionReadLimit = 0;
        public bool useMost5PrimeExonMapping = true;
        public string speciesAbbrev = "";
        public string geneVariantsChar = Props.props.AnalyzeAllGeneVariants? "a" : "s";
        public bool analyzeAllGeneVariants { get { return geneVariantsChar == "a"; } }
        public string annotation = "UCSC";
        public string resultFolder;
        public string projectFolder;

        public QXMAOptions(string[] args)
        {
            int argOffset = 1;
            int tempVer;
            Match m;
            string[] allBcSetNames = Barcodes.GetAllBarcodeSetNames();
            while (argOffset < args.Length - 1)
            {
                string opt = args[argOffset];
                switch (opt)
                {
                    case "rpkm":
                        useRPKM = true;
                        break;
                    case "rpm":
                        useRPKM = false;
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
                    case "UCSC":
                        annotation = "UCSC";
                        break;
                    case "VEGA":
                        annotation = "VEGA";
                        break;
                    case "ENSE":
                        annotation = "ENSE";
                        break;
                    default:
                        if (opt.StartsWith("-o"))
                            resultFolder = opt.Substring(1);
                        else if (Array.IndexOf(allBcSetNames, opt) >= 0)
                            barcodesName = opt;
                        else if (Array.IndexOf(new string[] { "hs", "hg", "mm", "gg", "ce", "cg" }, opt.ToLower()) >= 0)
                            speciesAbbrev = opt.ToLower();
                        else if ((opt[0] == 'a' || opt[0] == 's') && Array.IndexOf(new string[] { "UCSC", "VEGA", "ENSE" }, opt.Substring(1)) >= 0)
                        {
                            geneVariantsChar = opt.Substring(0,1);
                            annotation = opt.Substring(1);
                        }
                        else if (opt.Length >= 3 && Array.IndexOf(new string[] { "hs", "hg", "mm", "gg", "ce", "cg" }, opt.Substring(0, 2).ToLower()) >= 0
                            && int.TryParse(opt.Substring(2), out tempVer))
                            speciesAbbrev = opt;
                        else if (Regex.Match(opt, "[0-9A-Za-z]+:[0-9]+").Success)
                            laneArgs.Add(opt);
                        else
                        {
                            if ((m = Regex.Match(opt, "(..[0-9]*)_([as]?)(UCSC|VEGA|ENSE)")).Success)
                            {
                                speciesAbbrev = m.Groups[0].Value;
                                geneVariantsChar = m.Groups[1].Value;
                                annotation = m.Groups[2].Value;
                            }
                        }
                        break;
                }
                argOffset++;
            }
            projectFolder = args[argOffset];
        }
    }
}
