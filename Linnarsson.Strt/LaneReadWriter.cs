using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    public class LaneReadWriter
    {
        int lane;
        int read;
        public string PFFilePath { get; private set; }
        private StreamWriter PFWriter;
        public string nonPFFilePath { get; private set; }
        private StreamWriter nonPFWriter;
        private string statsFilePath;
        uint nReads = 0;
        uint nPFReads = 0;
        int readLen = 0;

        public LaneReadWriter(string readsFolder, string runFolderName, int runNo, int lane, int read)
        {
            this.lane = lane;
            this.read = read;
            PFFilePath = PathHandler.GetPFFilePath(readsFolder, runFolderName, runNo, lane, read);
            if (!Directory.Exists(Path.GetDirectoryName(PFFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(PFFilePath));
            PFWriter = PFFilePath.OpenWrite();
            nonPFFilePath = PathHandler.ConvertToNonPFFilePath(PFFilePath);
            if (!Directory.Exists(Path.GetDirectoryName(nonPFFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(nonPFFilePath));
            nonPFWriter = nonPFFilePath.OpenWrite();
            statsFilePath = PathHandler.GetReadStatsFilePath(readsFolder, runFolderName, runNo, lane, read);
            if (!Directory.Exists(Path.GetDirectoryName(statsFilePath)))
                Directory.CreateDirectory(Path.GetDirectoryName(statsFilePath));
        }

        /// <summary>
        /// Return true if both the PF and statistics output files exist
        /// </summary>
        /// <param name="readsFolder"></param>
        /// <param name="runId"></param>
        /// <param name="lane"></param>
        /// <param name="read"></param>
        /// <param name="runFolderName"></param>
        /// <returns></returns>
        public static bool DataExists(string readsFolder, int runId, int lane, int read, string runFolderName)
        {
            string PFPath = PathHandler.GetPFFilePath(readsFolder, runFolderName, runId, lane, read);
            string statsPath = PathHandler.GetReadStatsFilePath(readsFolder, runFolderName, runId, lane, read);
            return (File.Exists(PFPath) && File.Exists(statsPath));
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
            return new ReadFileResult(PFFilePath, nonPFFilePath, statsFilePath, lane, read, nPFReads, nReads - nPFReads, (uint)readLen);
        }

    }
}
