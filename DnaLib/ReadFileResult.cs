using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Describes the extracted fastQ records from one read of a lane.
    /// </summary>
    public class ReadFileResult
    {
        public string PFPath { get; private set; }
        public string nonPFPath { get; private set; }
        public string statsPath { get; private set; }
        public int lane { get; private set; }
        public int read { get; private set; }
        public uint nPFReads { get; private set; }
        public uint nNonPFReads { get; private set; }
        public uint readLen { get; private set; }
        public uint nReads { get { return nPFReads + nNonPFReads; } }

        public ReadFileResult(string PFPath, string nonPFPath, string summaryPath,
                              int lane, int read, uint nPFReads, uint nNonPFReads, uint readLen)
        {
            this.PFPath = PFPath;
            this.nonPFPath = nonPFPath;
            this.statsPath = summaryPath;
            this.lane = lane;
            this.read = read;
            this.nPFReads = nPFReads;
            this.nNonPFReads = nNonPFReads;
            this.readLen = readLen;
        }
    }

}
