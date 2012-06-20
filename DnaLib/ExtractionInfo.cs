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
        public string runId { get; set; }
        public char laneNo { get; set; }
        public string idxSeqFilter { get; set; }

        public string extractionTopFolder { get; set; }

        public string readFilePath { get; set; }
        public int nReads { get; set; }
        public int nPFReads { get; set; }

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
            this.runId = runId;
            this.laneNo = laneNo;
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
            string s = "ExtrInfo: runId=" + runId + " laneNo=" + laneNo + "\n" +
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
