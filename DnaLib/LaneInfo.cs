using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace Linnarsson.Dna
{
    [Serializable()]
    public class LaneInfo
    {
        /// <summary>
        /// Unique Illumina 9-char run/plate Id
        /// </summary>
        public string illuminaRunId { get; private set; }
        /// <summary>
        /// Number of the lane (0-7)
        /// </summary>
        public string laneNo { get; private set; }
        /// <summary>
        /// Optionally filter the reads to keep only those starting the index (2nd) read with the given sequence.
        /// </summary>
        public string idxSeqFilter { get; private set; }

        public string extractionFolder { get; private set; }

        public string PFReadFilePath { get; private set; }
        public string nonPFReadFilePath { get; private set; }

        /// <summary>
        /// Number of reads in fastQ file
        /// </summary>
        public int nReads { get; set; }

        /// <summary>
        /// Number of valid STRT reads remaining after extraction filtering
        /// </summary>
        public int nValidReads { get; set; }

        public string slaskWBcFilePath { get; private set; }
        public string slaskNoBcFilePath { get; private set; }
        public string summaryFilePath { get; private set; }
        public string[] extractedFilePaths { get; private set; }
        public string laneExtractionFolder { get; private set; }

        public string[] mappedFilePaths { get; set; }

        private string mappedFileFolder { get; set; }
        public string GetMappedFileFolder(string mapFolderName)
        {
            mappedFileFolder = Path.Combine(Path.Combine(extractionFolder, mapFolderName), Path.GetFileName(laneExtractionFolder));
            return mappedFileFolder;
        }

        /// <summary>
        /// Needed for serialization
        /// </summary>
        public LaneInfo()
        { }

        public LaneInfo(string extractionFolder, string laneExtractionFolder, string runId, char laneNo, int nBarcodes)
        {
            this.illuminaRunId = runId;
            this.laneNo = laneNo.ToString();
            SetExtractionFilePaths(extractionFolder, laneExtractionFolder, nBarcodes);
        }

        /// <summary>
        /// Create fq subfolder, and lane-specific subsubfolder (templated from readFilePath) under
        /// extractionFolder and setup file names for all fq output files
        /// </summary>
        /// <param name="readFilePath"></param>
        /// <param name="runId"></param>
        /// <param name="laneNo"></param>
        /// <param name="extractionFolder"></param>
        /// <param name="nBarcodes"></param>
        public LaneInfo(string readFilePath, string runId, char laneNo, string extractionFolder, int nBarcodes, string idxSeqFilter)
        {
            this.PFReadFilePath = readFilePath;
            this.nonPFReadFilePath = PathHandler.ConvertToNonPFFilePath(readFilePath);
            this.illuminaRunId = runId;
            this.laneNo = laneNo.ToString();
            this.idxSeqFilter = idxSeqFilter;
            Match m = Regex.Match(readFilePath, PathHandler.readFileAndLaneFolderMatchPat);
            int readNo = int.Parse(m.Groups[1].Value);
            string laneFolderName = string.Format(PathHandler.readFileAndLaneFolderCreatePattern,
                                                      readNo, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
            laneExtractionFolder = GetLaneExtractionFolder(extractionFolder, laneFolderName);
            if (!Directory.Exists(laneExtractionFolder))
                Directory.CreateDirectory(laneExtractionFolder);
            SetExtractionFilePaths(extractionFolder, laneExtractionFolder, nBarcodes);
        }

        private static string GetLaneExtractionFolder(string extractionFolder, string laneFolderName)
        {
            return Path.Combine(GetFqSubFolder(extractionFolder), laneFolderName);
        }

        private static string GetFqSubFolder(string extractionFolder)
        {
            return Path.Combine(extractionFolder, "fq");
        }

        public static string GetSummaryPath(string extractionFolder, string laneFolderName)
        {
            string laneExtractionFolder = GetLaneExtractionFolder(extractionFolder, laneFolderName);
            return GetSummaryPath(laneExtractionFolder);
        }

        private static string GetSummaryPath(string laneExtractionFolder)
        {
            return Path.Combine(laneExtractionFolder, PathHandler.extractionSummaryFilename);
        }

        private void SetExtractionFilePaths(string extractionFolder, string laneExtractionFolder, int nBarcodes)
        {
            this.extractionFolder = extractionFolder;
            this.laneExtractionFolder = laneExtractionFolder;
            extractedFilePaths = new string[Math.Max(1, nBarcodes)];
            for (int i = 0; i < extractedFilePaths.Length; i++)
                extractedFilePaths[i] = Path.Combine(laneExtractionFolder, i.ToString() + ".fq");
            slaskWBcFilePath = Path.Combine(laneExtractionFolder, "slask_w_bc.fq.gz");
            slaskNoBcFilePath = Path.Combine(laneExtractionFolder, "slask_no_bc.fq.gz");
            summaryFilePath = GetSummaryPath(laneExtractionFolder);
        }

        /// <summary>
        /// Search for a .fq or .fq.gz file for each barcode
        /// </summary>
        /// <returns></returns>
        public bool AllExtractedFilesExist()
        {
            foreach (string path in this.extractedFilePaths)
                if (!File.Exists(path) && !File.Exists(path + ".gz"))
                    return false;
            return true;
        }

        /// <summary>
        /// Construct laneInfos by scanning existing data in the extractionFolder
        /// </summary>
        /// <param name="extractionFolder"></param>
        /// <returns></returns>
        public static List<LaneInfo> SetupLaneInfosFromExistingExtraction(string extractionFolder)
        {
            int nBarcodes = Props.props.Barcodes.Count;
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            string[] laneExtractionFolders = Directory.GetDirectories(GetFqSubFolder(extractionFolder));
            foreach (string laneExtractionFolder in laneExtractionFolders)
            {
                Match m = Regex.Match(Path.GetFileName(laneExtractionFolder), PathHandler.readFileAndLaneFolderMatchPat);
                if (!m.Success) continue;
                LaneInfo laneInfo = new LaneInfo(extractionFolder, laneExtractionFolder, m.Groups[0].Value, m.Groups[1].Value[0], nBarcodes);
                laneInfos.Add(laneInfo);
            }
            return laneInfos;
        }

        /// <summary>
        /// laneArgs have the form RUN:LANENOS[:IDXSEQS]
        /// RUN is a run number or flowcell id, LANENOS may be several digits, each one lane, and IDXSEQS is the index sequence
        /// to filter by (or "" for using all indexes in that lane) for each of the lanes, separated with ','
        /// </summary>
        /// <param name="laneArgs"></param>
        /// <param name="extractionFolder"></param>
        /// <param name="nBarcodes"></param>
        /// <returns></returns>
        public static List<LaneInfo> LaneInfosFromLaneArgs(List<string> laneArgs, string extractionFolder)
        {
            int nBarcodes = Props.props.Barcodes.Count;
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string laneArg in laneArgs)
            {
                string[] parts = laneArg.Split(':');
                string runId = parts[0];
                string idxSeqFilterString = new string(',', parts[1].Length - 1);
                if (parts.Length >= 3)
                {
                    if (parts[2].Split(',').Length != parts[1].Length)
                        throw new ArgumentException("One (possibly empty) index filter seq must exist for each lane");
                    idxSeqFilterString = parts[2];
                }
                string[] idxSeqFilter = idxSeqFilterString.Split(',');
                int n = 0;
                foreach (char laneNo in parts[1])
                {
                    string readFilePat = PathHandler.GetReadFileMatchPattern(runId, laneNo, '1', "");
                    string[] readFiles = Directory.GetFiles(Props.props.ReadsFolder, readFilePat + ".fq.gz");
                    if (readFiles.Length == 0)
                        readFiles = Directory.GetFiles(Props.props.ReadsFolder, readFilePat + ".fq");
                    if (readFiles.Length > 0)
                        laneInfos.Add(new LaneInfo(readFiles[0], runId, laneNo, extractionFolder, nBarcodes, idxSeqFilter[n++]));
                }
            }
            return laneInfos;
        }

        public static List<string> RetrieveAllMapFilePaths(List<LaneInfo> laneInfos)
        {
            List<string> mapFiles = new List<string>();
            foreach (LaneInfo laneInfo in laneInfos)
                mapFiles.AddRange(laneInfo.mappedFilePaths);
            return mapFiles;
        }

    }
}
