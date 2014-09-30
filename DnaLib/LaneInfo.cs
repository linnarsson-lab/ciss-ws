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
        public string illuminaRunId { get; set; }
        /// <summary>
        /// Number of the lane (0-7)
        /// </summary>
        public string laneNo { get; set; }
        /// <summary>
        /// Optionally filter the reads to keep only those starting the index (2nd) read with the given sequence.
        /// </summary>
        public string idxSeqFilter { get; set; }

        public string extractionFolder { get; set; }

        public string PFReadFilePath { get; set; }
        public string nonPFReadFilePath { get; set; }

        /// <summary>
        /// Number of reads in fastQ file
        /// </summary>
        public int nReads { get; set; }

        /// <summary>
        /// Number of valid STRT reads remaining after extraction filtering
        /// </summary>
        public int nValidReads { get; set; }

        public string slaskFilePath { get; set; }
        public string summaryFilePath { get; set; }
        public string[] extractedFilePaths { get; set; }
        public string laneExtractionFolder { get; set; }
        public string ExtractedFileFolderName { get { return Path.GetFileName(laneExtractionFolder); } }

        public string[] mappedFilePaths { get; set; }
        public string mappedFileFolder { get; set; }
        public string bowtieLogFilePath { get; set; }

        /// <summary>
        /// Needed for serialization
        /// </summary>
        public LaneInfo()
        { }

        public LaneInfo(string readFilePath, string runId, char laneNo, string idxSeqFilter)
        {
            this.PFReadFilePath = readFilePath;
            this.illuminaRunId = runId;
            this.laneNo = laneNo.ToString();
            this.idxSeqFilter = idxSeqFilter;
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
            : this(readFilePath, runId, laneNo, idxSeqFilter)
        {
            Match m = Regex.Match(readFilePath, PathHandler.readFileAndLaneFolderMatchPat);
            int readNo = int.Parse(m.Groups[1].Value);
            string laneExtractionName = string.Format(PathHandler.readFileAndLaneFolderCreatePattern,
                                                      readNo, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
            laneExtractionFolder = Path.Combine(GetFqSubFolder(extractionFolder), laneExtractionName);
            if (!Directory.Exists(laneExtractionFolder))
                Directory.CreateDirectory(laneExtractionFolder);
            SetExtractionFilePaths(extractionFolder, laneExtractionFolder, nBarcodes);
        }

        public static string GetFqSubFolder(string extractionFolder)
        {
            return Path.Combine(extractionFolder, "fq");
        }

        public static string[] GetLaneExtractionFolders(string extractionFolder)
        {
            return Directory.GetDirectories(GetFqSubFolder(extractionFolder));
        }

        public static string GetSummaryPath(string laneExtractionFolder)
        {
            return Path.Combine(laneExtractionFolder, PathHandler.extractionSummaryFilename);
        }

        private void SetExtractionFilePaths(string extractionFolder, string laneExtractionFolder, int nBarcodes)
        {
            this.extractionFolder = extractionFolder;
            this.laneExtractionFolder = laneExtractionFolder;
            this.extractedFilePaths = new string[Math.Max(1, nBarcodes)];
            for (int i = 0; i < extractedFilePaths.Length; i++)
                this.extractedFilePaths[i] = Path.Combine(laneExtractionFolder, i.ToString() + ".fq");
            this.slaskFilePath = Path.Combine(laneExtractionFolder, "slask.fq.gz");
            this.summaryFilePath = GetSummaryPath(laneExtractionFolder);
        }

        public bool AllExtractedFilesExist()
        {
            foreach (string path in this.extractedFilePaths)
                if (!File.Exists(path))
                    return false;
            return true;
        }

        /// <summary>
        /// Construct laneInfos by scanning existing data in the extractionFolder
        /// </summary>
        /// <param name="extractionFolder"></param>
        /// <returns></returns>
        public static List<LaneInfo> SetupLaneInfosFromExistingExtraction(string extractionFolder, int nBarcodes)
        {
            List<LaneInfo> laneInfos = new List<LaneInfo>();
            foreach (string laneExtractionFolder in GetLaneExtractionFolders(extractionFolder))
            {
                Match m = Regex.Match(Path.GetFileName(laneExtractionFolder), PathHandler.readFileAndLaneFolderMatchPat);
                if (!m.Success) continue;
                LaneInfo laneInfo = new LaneInfo(m.Groups[0].Value, m.Groups[1].Value, m.Groups[2].Value[0], "");
                laneInfo.SetExtractionFilePaths(extractionFolder, laneExtractionFolder, nBarcodes);
                laneInfos.Add(laneInfo);
            }
            return laneInfos;
        }

        public void SetMappedFileFolder(string splcIndexVersion)
        {
            string mapFolderName = PathHandler.MakeMapFolder(splcIndexVersion);
            this.mappedFileFolder = Path.Combine(Path.Combine(extractionFolder, mapFolderName), ExtractedFileFolderName);
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
        public static List<LaneInfo> LaneInfosFromLaneArgs(List<string> laneArgs, string extractionFolder, int nBarcodes)
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
                    string readFilePat = PathHandler.GetReadFileMatchPattern(runId, laneNo, '1', ".fq.gz");
                    string[] readFiles = Directory.GetFiles(Props.props.ReadsFolder, readFilePat);
                    if (readFiles.Length > 0)
                        laneInfos.Add(new LaneInfo(readFiles[0], runId, laneNo, extractionFolder, nBarcodes, idxSeqFilter[n++]));
                }
            }
            return laneInfos;
        }

        public static List<string> RetrieveAllMapFilePaths(List<LaneInfo> laneInfos)
        {
            List<string> mapFiles = new List<string>();
            foreach (LaneInfo info in laneInfos)
                mapFiles.AddRange(info.mappedFilePaths);
            return mapFiles;
        }

        public override string ToString()
        {
            string s = "LaneInfo: illuminaRunId=" + illuminaRunId + " laneNo=" + laneNo + "\n" +
                       "readFilePath=" + PFReadFilePath + " nReads=" + nReads + "\n" +
                       "extrTopF= " + extractionFolder + "\n" +
                       ((extractedFilePaths != null && extractedFilePaths.Length > 0) ? "extrFilePaths[0]=" + extractedFilePaths[0] + "\n" : "") +
                       "extractedFileFolder=" + laneExtractionFolder + "\n" +
                       " N=" + ((extractedFilePaths == null) ? "0" : extractedFilePaths.Length.ToString()) + "\n" +
                       "slaskFilePath=" + slaskFilePath + "\n" + 
                       "summaryFilePath= " + summaryFilePath + "\n" +
                       "mappedFileFolder= " + mappedFileFolder +
                       " N=" + ((mappedFilePaths == null) ? "0" : mappedFilePaths.Length.ToString());
            return s;
        }
    }
}
