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
        public string plateRead1FilePath { get; private set; }
        public string plateRead2FilePath { get; private set; }
        public string plateRead3FilePath { get; private set; }
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

        public LaneInfo(string extractionFolder, string laneExtractionFolder, string runId, char laneNo, Barcodes barcodes)
        {
            this.illuminaRunId = runId;
            this.laneNo = laneNo.ToString();
            SetExtractionFilePaths(extractionFolder, laneExtractionFolder, barcodes);
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
        public LaneInfo(string readFilePath, string runId, char laneNo, string extractionFolder, Barcodes barcodes, string idxSeqFilter)
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
            SetExtractionFilePaths(extractionFolder, laneExtractionFolder, barcodes);
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

        private void SetExtractionFilePaths(string extractionFolder, string laneExtractionFolder, Barcodes barcodes)
        {
            this.extractionFolder = extractionFolder;
            this.laneExtractionFolder = laneExtractionFolder;
            SetupExtractedFilePaths(barcodes);
            slaskWBcFilePath = Path.Combine(laneExtractionFolder, "slask_w_bc.fq.gz");
            slaskNoBcFilePath = Path.Combine(laneExtractionFolder, "slask_no_bc.fq.gz");
            string laneName = Path.GetFileName(laneExtractionFolder);
            plateRead1FilePath = Path.Combine(extractionFolder, laneName + "_read1_index" + barcodes.Name + ".fq.gz");
            plateRead2FilePath = Path.Combine(extractionFolder, laneName + "_read2_index" + barcodes.Name + ".fq.gz");
            plateRead3FilePath = Path.Combine(extractionFolder, laneName + "_read3_index" + barcodes.Name + ".fq.gz");
            summaryFilePath = GetSummaryPath(laneExtractionFolder);
        }

        private void SetupExtractedFilePaths(Barcodes barcodes)
        {
            extractedFilePaths = new string[Math.Max(1, barcodes.Count)];
            for (int bcIdx = 0; bcIdx < extractedFilePaths.Length; bcIdx++)
            {
                string extractedFileName = MakeExtractedFileName(barcodes, bcIdx);
                extractedFilePaths[bcIdx] = Path.Combine(laneExtractionFolder, extractedFileName);
            }
        }

        public static string MakeExtractedFileName(Barcodes barcodes, int bcIdx)
        {
            string extractedFileName = string.Format("{0}_{1}_{2}.fq", bcIdx, barcodes.WellIds[bcIdx], barcodes.Seqs[bcIdx]);
            return extractedFileName;
        }

        /// <summary>
        /// Search for a N_Wxx_BBBBBB.fq file for each barcode. If legacy N.fq files exist, rename it.
        /// </summary>
        /// <returns>true if all exist or could be made exist by renaming </returns>
        public bool AllExtractedFilesExist()
        {
            bool allexist = true;
            foreach (string extractedFilePath in this.extractedFilePaths)
                allexist = allexist && ExtractedFileExists(extractedFilePath);
            return allexist;
        }

        /// <summary>
        /// Search for (a N_Wxx_BBBBBB.fq[.gz] ) file. If legacy N.fq[.gz] files exist, rename it.
        /// </summary>
        /// <param name="extractedFilePath"></param>
        /// <returns>true if exists or legacy file existed and was renamed</returns>
        public static bool ExtractedFileExists(string extractedFilePath)
        {
            if (!File.Exists(extractedFilePath) && !File.Exists(extractedFilePath + ".gz"))
            {
                int bcIdx = ParseBcIdx(extractedFilePath);
                string legacyFilePath = Path.Combine(Path.GetDirectoryName(extractedFilePath), bcIdx.ToString() + ".fq");
                if (!File.Exists(legacyFilePath))
                {
                    legacyFilePath = legacyFilePath + ".gz";
                    if (!File.Exists(legacyFilePath))
                        return false;
                    extractedFilePath += ".gz";
                }
                File.Move(legacyFilePath, extractedFilePath);
            }
            return true;
        }

        /// <summary>
        /// Extract the barcode index from file name (legacy or new)
        /// </summary>
        /// <param name="extractedFilePath"></param>
        /// <returns></returns>
        public static int ParseBcIdx(string extractedFilePath)
        {
            string filename = Path.GetFileName(extractedFilePath);
            return int.Parse(Regex.Match(filename, "^[0-9]+").Groups[0].Value);
        }

        /// <summary>
        /// Construct laneInfos by scanning existing data in the extractionFolder
        /// </summary>
        /// <param name="extractionFolder"></param>
        /// <returns></returns>
        public static List<LaneInfo> SetupLaneInfosFromExistingExtraction(string extractionFolder)
        {
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            string[] laneExtractionFolders = Directory.GetDirectories(GetFqSubFolder(extractionFolder));
            foreach (string laneExtractionFolder in laneExtractionFolders)
            {
                Match m = Regex.Match(Path.GetFileName(laneExtractionFolder), PathHandler.readFileAndLaneFolderMatchPat);
                if (!m.Success) continue;
                LaneInfo laneInfo = new LaneInfo(extractionFolder, laneExtractionFolder, m.Groups[0].Value, m.Groups[1].Value[0], Props.props.Barcodes);
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
                        laneInfos.Add(new LaneInfo(readFiles[0], runId, laneNo, extractionFolder, Props.props.Barcodes, idxSeqFilter[n++]));
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
