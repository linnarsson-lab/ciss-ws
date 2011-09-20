using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class ExtractionInfo
    {
        public string readFilePath { get; set; }
        public string extractedFilePath { get; set; }
        public string runId { get; set; }
        public char laneNo { get; set; }
        public int nReads { get; set; }
        public int nPFReads { get; set; }

        public ExtractionInfo(string readFilePath, string runId, char laneNo)
        {
            this.readFilePath = readFilePath;
            this.runId = runId;
            this.laneNo = laneNo;
        }
    }
}
