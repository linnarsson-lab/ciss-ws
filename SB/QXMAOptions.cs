﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public string geneVariantsChar = "";
        public bool analyzeAllGeneVariants { get { return geneVariantsChar == "a"; } }
        public string annotation = "";
        public string resultFolder;
        public string projectFolder;

        public QXMAOptions(string[] args)
        {
            int argOffset = 1;
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
                        else if (Array.IndexOf(new string[] { "hs", "hg", "mm", "gg", "ce", "cg" }, opt.ToLower()) >= 0)
                            speciesAbbrev = opt.ToLower();
                        else if ((opt[0] == 'a' || opt[0] == 's') && Array.IndexOf(new string[] { "UCSC", "VEGA", "ENSE" }, opt.Substring(1)) >= 0)
                        {
                            geneVariantsChar = opt.Substring(0,1);
                            annotation = opt.Substring(1);
                        }
                        else if (Array.IndexOf(allBcSetNames, opt) >= 0)
                            barcodesName = opt;
                        break;
                }
                argOffset++;
            }
            projectFolder = args[argOffset];
        }
    }
}
