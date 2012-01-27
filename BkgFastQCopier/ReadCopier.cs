using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using Linnarsson.Strt;


namespace BkgFastQCopier
{
    public class ReadCopier
    {
        private static readonly string statsSubFolder = "statistics";
        private static readonly string nonPFSubFolder = "nonPF";
        private static readonly string fileIdCreatePattern = "Run{0:00000}_L{1}_{2}_{3}";
        private static readonly string fileIdMatchPattern = "Run([0-9]+)_L([0-9]+)_([0-9])";

        private Dictionary<int, object> copiedRunIds;
        string illuminaRunsFolder;
        string outputReadsFolder;
        StreamWriter logWriter;

        public ReadCopier(string illuminaRunsFolder, string outputReadsFolder, StreamWriter logWriter)
        {
            this.illuminaRunsFolder = illuminaRunsFolder;
            this.outputReadsFolder = outputReadsFolder;
            this.logWriter = logWriter;
            AssertOutputFolders(outputReadsFolder);
            ListCompletelyCopiedRuns(outputReadsFolder);
        }

        private void ListCompletelyCopiedRuns(string outputReadsFolder)
        {
            copiedRunIds = new Dictionary<int, object>();
            Dictionary<int, byte[]> laneCountsByRunId = new Dictionary<int, byte[]>();
            string statsFolder = Path.Combine(outputReadsFolder, statsSubFolder);
            foreach (string file in Directory.GetFiles(statsFolder, "Run*"))
            {
                Match m = Regex.Match(file, fileIdMatchPattern);
                if (m.Success)
                {
                    int runId = int.Parse(m.Groups[1].Value);
                    int lane0 = int.Parse(m.Groups[2].Value) - 1;
                    int read0 = int.Parse(m.Groups[3].Value) - 1;
                    if (! laneCountsByRunId.ContainsKey(runId))
                        laneCountsByRunId[runId] = new byte[3];
                    laneCountsByRunId[runId][read0] |= (byte)(1 << lane0);
                }
            }
            foreach (int runId in laneCountsByRunId.Keys)
            {
                byte rls1 = laneCountsByRunId[runId][0];
                byte rls2 = laneCountsByRunId[runId][1];
                byte rls3 = laneCountsByRunId[runId][2];
                if (rls1 == 255 && (rls2 == 0 || rls2 == 255) && (rls3 == 0 || rls3 == 255))
                    copiedRunIds[runId] = null;
            }
            logWriter.WriteLine(DateTime.Now.ToString() + " " + copiedRunIds.Count + " completely copied runs already in " + statsFolder);
            logWriter.Flush();
        }

        private static void AssertOutputFolders(string outputReadsFolder)
        {
            if (!File.Exists(outputReadsFolder))
            {
                Directory.CreateDirectory(outputReadsFolder);
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, nonPFSubFolder));
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, statsSubFolder));
            }
        }

        public void Scan()
        {
            string[] runFolderNames = Directory.GetDirectories(illuminaRunsFolder);
            foreach (string runFolder in runFolderNames)
            {
                Match m = MatchRunFolderName(runFolder);
                if (m.Success)
                {
                    string readyFilePath = Path.Combine(runFolder, Props.props.IlluminaRunReadyFilename);
                    int runNo = int.Parse(m.Groups[2].Value);
                    string runId = (m.Groups.Count > 3)? m.Groups[3].Value : runNo.ToString();
                    string runDate = m.Groups[1].Value;
                    runDate = "20" + runDate.Substring(0, 2) + "-" + runDate.Substring(2, 2) + "-" + runDate.Substring(4);
                    if (!copiedRunIds.ContainsKey(runNo) && File.Exists(readyFilePath))
                    {
                        List<ReadFileResult> readFileResults;
                        string status = "copied";
                        new ProjectDB().UpdateRunStatus(runId, "copying", runNo, runDate);
                        try
                        {
                            readFileResults = Copy(runNo, runFolder, outputReadsFolder, 1, 8);
                            status = (readFileResults.Count > 0) ? "copied" : "copyfail";
                        }
                        catch (Exception e)
                        {
                            new ProjectDB().UpdateRunStatus(runId, "copyfail", runNo, runDate);
                            throw (e);
                        }
                        new ProjectDB().UpdateRunStatus(runId, status, runNo, runDate);
                        int[] cycles = new int[4];
                        foreach (ReadFileResult readFileResult in readFileResults)
                        {
                            cycles[readFileResult.read] = (int)readFileResult.readLen;
                            new ProjectDB().AddToBackupQueue(readFileResult.readFile, 10);
                        }
                        new ProjectDB().UpdateRunCycles(runId, cycles[1], cycles[2], cycles[3]);
                        copiedRunIds[runNo] = null;
                    }
                }
            }
        }

        private static Match MatchRunFolderName(string runFolder)
        {
            Match m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]+)_FC$");
            if (!m.Success)
                m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]+)$");
            if (!m.Success)
                m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]{4})_[AB]([a-zA-Z0-9]+)$");
            return m;
        }

        public string Copy(string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string result = "copyfail";
            Match m = MatchRunFolderName(runFolder);
            if (m.Success)
            {
                int runNo = int.Parse(m.Groups[2].Value);
                List<ReadFileResult> readFiles = Copy(runNo, runFolder, readsFolder, laneFrom, laneTo);
                if (readFiles.Count > 0)
                    result = "copied";
            }
            return result;
        }

        public List<ReadFileResult> Copy(int runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string qseqFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
            if (!Directory.Exists(qseqFolder))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: qseq folder does not exist: {0}", qseqFolder);
                logWriter.Flush();
                return new List<ReadFileResult>();
            }
            string runName = Path.GetFileName(runFolder);
            List<ReadFileResult> readFileResults = CopyQseqFiles(runId, readsFolder, qseqFolder, runName, laneFrom, laneTo);
            if (readFileResults.Count == 0)
                readFileResults = CopyBclFiles(runId, readsFolder, runFolder, runName, laneFrom, laneTo);
            if (readFileResults.Count == 0)
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: No qseq or bcl files found in {0}", qseqFolder);
                logWriter.Flush();
            }
            return readFileResults;
        }

        private List<ReadFileResult> CopyBclFiles(int runId, string readsFolder, string runFolder, string runName, int laneFrom, int laneTo)
        {
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            logWriter.WriteLine(DateTime.Now.ToString() + " Processing bcl files from " + runFolder + ".");
            logWriter.Flush();
			for (int lane = laneFrom; lane <= laneTo; lane++)
				{
					for (int read = 1; read <= 3; read++)
					{
                        if (Outputter.DataExists(readsFolder, runId, lane, read, runName))
                            continue;
						Outputter outputter = null;
						foreach (FastQRecord rec in BclFile.Stream(runFolder, lane, read))
						{
							if (outputter == null)
                                outputter = new Outputter(readsFolder, runId, lane, read, runName);
							outputter.Write(rec);
						}
                        if (outputter != null)
                        {
                            readFileResults.Add(outputter.Close());
                        }
					}
				};
            return readFileResults;
        }

        private List<ReadFileResult> CopyQseqFiles(int runId, string readsFolder, string qseqFolder, string runName, int laneFrom, int laneTo)
        {
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            string[] files = Directory.GetFiles(qseqFolder, "*s_*_qseq.txt");
            if (files.Length == 0)
                return readFileResults;
            logWriter.WriteLine(DateTime.Now.ToString() + " Processing {0} qseq files from {1}.", files.Length, qseqFolder);
            logWriter.Flush();
            Array.Sort(files);
            string currLane = "";
            string currRead = "";
            Outputter outputter = null;
            foreach (string qseqFile in files)
            {
                Match m = Regex.Match(qseqFile, "s_([0-9]+)_([0-9])_[0-9]+_qseq.txt");
                string lane = m.Groups[1].Value;
                int laneNo = int.Parse(lane);
                if (laneNo < laneFrom || laneNo > laneTo)
                    continue;
                string read = m.Groups[2].Value;
                if (lane != currLane || read != currRead)
                {
                    if (outputter != null)
                    {
                        readFileResults.Add(outputter.Close());
                    }
                    currLane = lane;
                    currRead = read;
                    outputter = new Outputter(readsFolder, runId, laneNo, int.Parse(read), runName);
                }
                foreach (FastQRecord rec in FastQFile.Stream(qseqFile, Props.props.QualityScoreBase, true))
                    outputter.Write(rec);
            }
            if (outputter != null)
            {
                readFileResults.Add(outputter.Close());
            }
            return readFileResults;
        }

        public class ReadFileResult
        {
            public string readFile;
            public int lane;
            public int read;
            public uint nPFReads;
            public uint nNonPFReads;
            public uint readLen;
            public ReadFileResult(string readFile, int lane, int read, uint nPFReads, uint nNonPFReads, uint readLen)
            {
                this.readFile = readFile;
                this.lane = lane;
                this.read = read;
                this.nPFReads = nPFReads;
                this.nNonPFReads = nNonPFReads;
                this.readLen = readLen;
            }
        }

        class Outputter
        {
            StreamWriter PFFile;
            string readsFolder;
            string PFFilename;
            StreamWriter nonPFFile;
            string statsFilePath;
            uint nReads = 0;
            uint nPFReads = 0;
            ulong totalPFReadLength = 0;
            ulong totalNonPFReadLength = 0;
            int lane;
            int read;

            public Outputter(string readsFolder, int runId, int lane, int read, string runName)
            {
                this.readsFolder = readsFolder;
                this.lane = lane;
                this.read = read;
                string fileId = GetFileId(runId, lane, read, runName);
                PFFilename = GetPFFilePath(readsFolder, fileId);
                PFFile = new StreamWriter(PFFilename);
                nonPFFile = Path.Combine(readsFolder, Path.Combine(nonPFSubFolder, fileId + "_nonPF.fq.gz")).OpenWrite();
                statsFilePath = GetStatsFilePath(readsFolder, fileId);
            }

            public static bool DataExists(string readsFolder, int runId, int lane, int read, string runName)
            {
                string fileId = GetFileId(runId, lane, read, runName);
                return (File.Exists(GetStatsFilePath(readsFolder, fileId)) &&
                        File.Exists(GetPFFilePath(readsFolder, fileId)));
            }

            private static string GetFileId(int runId, int lane, int read, string runName)
            {
                return string.Format(fileIdCreatePattern, runId, lane, read, runName);
            }
            private static string GetStatsFilePath(string readsFolder, string fileId)
            {
                return Path.Combine(readsFolder, Path.Combine(statsSubFolder, fileId + ".txt"));
            }
            private static string GetPFFilePath(string readsFolder, string fileId)
            {
                return Path.Combine(readsFolder, fileId + ".fq");
            }

            public void Write(FastQRecord rec)
            {
                nReads++;
                if (rec.PassedFilter)
                {
                    nPFReads++;
                    totalPFReadLength += (ulong)rec.Sequence.Length;
                    PFFile.WriteLine(rec.ToString(Props.props.QualityScoreBase));
                }
                else
                {
                    totalNonPFReadLength += (ulong)rec.Sequence.Length;
                    nonPFFile.WriteLine(rec.ToString(Props.props.QualityScoreBase));
                }
            }
            public ReadFileResult Close()
            {
                PFFile.Close();
                CmdCaller.Run("chmod", "a-w " + PFFilename);
                nonPFFile.Close();
                StreamWriter statsFile = new StreamWriter(statsFilePath);
                statsFile.WriteLine("TotalReadsNumber\t" + nReads);
                statsFile.WriteLine("PassedFilterReadsNumber\t" + nPFReads);
                double passedAvLen = (nPFReads > 0) ? (totalPFReadLength / (double)nPFReads) : 0.0;
                double nonPassedAvLen = (nReads - nPFReads > 0) ? (totalNonPFReadLength / (double)(nReads - nPFReads)) : 0.0;
                statsFile.WriteLine("PassedFilterReadsAverageLength\t{0:0.##}", passedAvLen);
                statsFile.WriteLine("NonPassedFilterReadsAverageLength\t{0:0.##}", nonPassedAvLen);
                statsFile.Close();
                return new ReadFileResult(PFFilename, lane, read, nPFReads, nReads - nPFReads, (uint)passedAvLen);
            }

        }

    }
}
