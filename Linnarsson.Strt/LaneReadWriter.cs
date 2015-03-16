using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Describes the extracted fastQ records from one read of a lane.
    /// </summary>
    public class ReadFileResult
    {
        public string readFile { get; private set; }
        public int lane { get; private set; }
        public char read { get; private set; }
        public uint nPFReads { get; private set; }
        public uint nNonPFReads { get; private set; }
        public uint readLen { get; private set; }
        public uint nReads { get { return nPFReads + nNonPFReads; } }

        public ReadFileResult(string readFile, int lane, char read, uint nPFReads, uint nNonPFReads, uint readLen)
        {
            this.readFile = readFile;
            this.lane = lane;
            this.read = read;
            this.nPFReads = nPFReads;
            this.nNonPFReads = nNonPFReads;
            this.readLen = readLen;
        }
    }

    public class LaneReadWriter
    {
        string readsFolder;
        int lane;
        char read;
        public string PFFilePath { get; private set; }
        public StreamWriter PFWriter { get; private set; }
        public string nonPFFilePath { get; private set; }
        public StreamWriter nonPFWriter { get; private set; }
        string statsFilePath;
        uint nReads = 0;
        uint nPFReads = 0;
        int readLen = 0;

        public LaneReadWriter(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            this.readsFolder = readsFolder;
            this.lane = lane;
            this.read = read.ToString()[0];
            PFFilePath = PathHandler.GetPFFilePath(readsFolder, runFolderName, runNo, lane, read);
            PFWriter = PFFilePath.OpenWrite();
            nonPFFilePath = PathHandler.GetNonPFFilePath(readsFolder, runFolderName, runNo, lane, read);
            nonPFWriter = nonPFFilePath.OpenWrite();
            statsFilePath = PathHandler.GetReadStatsFilePath(readsFolder, runFolderName, runNo, lane, read);
        }

        public static bool DataExists(string readsFolder, int runId, int lane, int read, string runFolderName)
        {
            string PFPath = PathHandler.GetPFFilePath(readsFolder, runFolderName, runId, lane, read);
            string statsPath = PathHandler.GetReadStatsFilePath(readsFolder, runFolderName, runId, lane, read);
            return (File.Exists(PFPath) && File.Exists(PFPath));
        }

        public void Write(FastQRecord rec)
        {
            readLen = rec.Sequence.Length;
            nReads++;
            StreamWriter writer = rec.PassedFilter ? PFWriter : nonPFWriter;
            writer.WriteLine(rec.ToString(Props.props.QualityScoreBase));
            if (rec.PassedFilter) nPFReads++;
        }

        public void Write(string headerBeforeReadChar, string headerAfterReadChar, char[] readSeq, char[] quals, bool passedFilter)
        {
            readLen = readSeq.Length;
            nReads++;
            StreamWriter writer = passedFilter ? PFWriter : nonPFWriter;
            writer.WriteLine("@" + headerBeforeReadChar + read + headerAfterReadChar + Environment.NewLine +
                                new string(readSeq) + Environment.NewLine +
                                "+" + Environment.NewLine +
                                new string(quals));
            if (passedFilter) nPFReads++;
        }

        public ReadFileResult CloseAndSummarize()
        {
            PFWriter.Close();
            PFWriter.Dispose();
            CmdCaller.Run("chmod", "a+rw " + PFFilePath);
            nonPFWriter.Close();
            nonPFWriter.Dispose();
            using (StreamWriter statsFile = new StreamWriter(statsFilePath))
            {
                statsFile.WriteLine("TotalReadsNumber\t" + nReads);
                statsFile.WriteLine("PassedFilterReadsNumber\t" + nPFReads);
                statsFile.WriteLine("PassedFilterReadsAverageLength\t{0:0.##}", readLen);
                statsFile.WriteLine("NonPassedFilterReadsAverageLength\t{0:0.##}", readLen);
            }
            return new ReadFileResult(PFFilePath, lane, read, nPFReads, nReads - nPFReads, (uint)readLen);
        }

    }
}
