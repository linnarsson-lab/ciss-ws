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

        string illuminaRunsFolder;
        string outputReadsFolder;
        StreamWriter logWriter;

        public ReadCopier(string illuminaRunsFolder, string outputReadsFolder, StreamWriter logWriter)
        {
            this.illuminaRunsFolder = illuminaRunsFolder;
            this.outputReadsFolder = outputReadsFolder;
            this.logWriter = logWriter;
            AssertOutputFolders(outputReadsFolder);
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
                    string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
                    if (File.Exists(readyFilePath) && Directory.Exists(callFolder))
                    {
                        List<ReadFileResult> readFileResults;
                        string status = "copied";
                        new ProjectDB().UpdateRunStatus(runId, "copying", runNo, runDate);
                        try
                        {
                            readFileResults = Copy(runNo, runFolder, outputReadsFolder, 1, 8);
                        }
                        catch (Exception e)
                        {
                            new ProjectDB().UpdateRunStatus(runId, "copyfail", runNo, runDate);
                            throw (e);
                        }
                        new ProjectDB().UpdateRunStatus(runId, status, runNo, runDate);
                        if (readFileResults.Count > 0)
                        {
                            int[] cycles = new int[4];
                            foreach (ReadFileResult readFileResult in readFileResults)
                            {
                                cycles[readFileResult.read] = (int)readFileResult.readLen;
                                new ProjectDB().AddToBackupQueue(readFileResult.readFile, 10);
                                if (readFileResult.read == 1)
                                    new ProjectDB().SetIlluminaYield(runId, readFileResult.nReads, readFileResult.nPFReads, readFileResult.lane);
                            }
                            new ProjectDB().UpdateRunCycles(runId, cycles[1], cycles[2], cycles[3]);
                        }
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
            string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
            if (!Directory.Exists(callFolder))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: BaseCalls folder does not exist: {0}", callFolder);
                logWriter.Flush();
                return new List<ReadFileResult>();
            }
            string runName = Path.GetFileName(runFolder);
            List<ReadFileResult> readFileResults = CopyBclFiles(runId, readsFolder, runFolder, runName, laneFrom, laneTo);
            if (readFileResults.Count == 0)
                readFileResults = CopyQseqFiles(runId, readsFolder, runFolder, runName, laneFrom, laneTo);
            return readFileResults;
        }

        private List<ReadFileResult> CopyBclFiles(int runId, string readsFolder, string runFolder, string runName, int laneFrom, int laneTo)
        {
            string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
			for (int lane = laneFrom; lane <= laneTo; lane++)
				{
					for (int read = 1; read <= 3; read++)
					{
                        string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                        string readyFilePath = Path.Combine(runFolder, readyFileName);
                        if (!File.Exists(readyFilePath) || Outputter.DataExists(readsFolder, runId, lane, read, runName))
                            continue;
						Outputter outputter = null;
						foreach (FastQRecord rec in BclFile.Stream(runFolder, lane, read))
						{
                            if (outputter == null)
                            {
                                logWriter.Write(DateTime.Now.ToString() + " Copying lane {0} read {1} from run {2}...", lane, read, runId);
                                logWriter.Flush();
                                outputter = new Outputter(readsFolder, runId, lane, read, runName);
                            }
							outputter.Write(rec);
						}
                        if (outputter != null)
                        {
                            ReadFileResult r = outputter.Close();
                            readFileResults.Add(r);
                            logWriter.WriteLine(r.readFile + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
                            logWriter.Flush();
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
                string read = m.Groups[2].Value;
                int laneNo = int.Parse(lane);
                if (laneNo < laneFrom || laneNo > laneTo || Outputter.DataExists(readsFolder, runId, laneNo, int.Parse(read), runName))
                    continue;
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

        /// <summary>
        /// Describes the extracted fastQ records from one read of a lane.
        /// </summary>
        public class ReadFileResult
        {
            public string readFile { get; private set; }
            public int lane { get; private set; }
            public int read { get; private set; }
            public uint nPFReads { get; private set; }
            public uint nNonPFReads { get; private set; }
            public uint readLen { get; private set; }
            public uint nReads { get { return nPFReads + nNonPFReads; } }

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
                string PFPath = GetPFFilePath(readsFolder, fileId);
                return (File.Exists(GetStatsFilePath(readsFolder, fileId)) &&
                        (File.Exists(PFPath)) || File.Exists(PFPath + ".gz"));
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
                CmdCaller.Run("chmod", "go-w " + PFFilename);
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
