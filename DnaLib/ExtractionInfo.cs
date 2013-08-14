using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public string extractionTopFolder { get; set; }

        public string readFilePath { get; set; }

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
        public string extractedFileFolder { get; set; }
        public string ExtractedFileFolderName { get { return Path.GetFileName(extractedFileFolder); } }

        public string[] mappedFilePaths { get; set; }
        public string mappedFileFolder { get; set; }
        public string bowtieLogFilePath { get; set; }

        public LaneInfo()
        { }

        public LaneInfo(string readFilePath, string runId, char laneNo)
            : this(readFilePath, runId, laneNo, "")
        { }

        public LaneInfo(string readFilePath, string runId, char laneNo, string idxSeqFilter)
        {
            this.readFilePath = readFilePath;
            this.illuminaRunId = runId;
            this.laneNo = laneNo.ToString();
            this.idxSeqFilter = idxSeqFilter;
        }

        public void SetMappedFileFolder(string splcIndexVersion)
        {
            string mapFolderName = PathHandler.MakeMapFolder(splcIndexVersion);
            mappedFileFolder = Path.Combine(Path.Combine(extractionTopFolder, mapFolderName), ExtractedFileFolderName);
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
                       "readFilePath=" + readFilePath + " nReads=" + nReads + "\n" +
                       "extrTopF= " + extractionTopFolder + "\n" +
                       ((extractedFilePaths != null && extractedFilePaths.Length > 0) ? "extrFilePaths[0]=" + extractedFilePaths[0] + "\n" : "") +
                       "extractedFileFolder=" + extractedFileFolder + "\n" +
                       " N=" + ((extractedFilePaths == null) ? "0" : extractedFilePaths.Length.ToString()) + "\n" +
                       "slaskFilePath=" + slaskFilePath + "\n" + 
                       "summaryFilePath= " + summaryFilePath + "\n" +
                       "mappedFileFolder= " + mappedFileFolder +
                       " N=" + ((mappedFilePaths == null) ? "0" : mappedFilePaths.Length.ToString());
            return s;
        }
    }
}
