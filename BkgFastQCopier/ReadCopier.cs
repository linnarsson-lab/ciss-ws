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
        string illuminaRunsFolder;
        string outputReadsFolder;
        StreamWriter logWriter;
        ProjectDB projectDB;

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
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, PathHandler.nonPFSubFolder));
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, PathHandler.statsSubFolder));
            }
        }

        public void Scan()
        {
            projectDB = new ProjectDB();
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
                        string status = "copied";
                        projectDB.UpdateRunStatus(runId, "copying", runNo, runDate);
                        try
                        {
                            Copy(runNo, runId, runFolder, outputReadsFolder, 1, 8);
                        }
                        catch (Exception e)
                        {
                            projectDB.UpdateRunStatus(runId, "copyfail", runNo, runDate);
                            throw (e);
                        }
                        projectDB.UpdateRunStatus(runId, status, runNo, runDate);
                        if (status == "copied")
                            projectDB.AutoStartC1Analyses(runId);
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

        public int Copy(string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            projectDB = null;
            List<ReadFileResult> readFiles = new List<ReadFileResult>();
            Match m = MatchRunFolderName(runFolder);
            if (m.Success)
            {
                int runNo = int.Parse(m.Groups[2].Value);
                readFiles = Copy(runNo, "", runFolder, readsFolder, laneFrom, laneTo);
            }
            return readFiles.Count;
        }

        public List<ReadFileResult> Copy(int runNo, string runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
            if (!Directory.Exists(callFolder))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: BaseCalls folder does not exist: {0}", callFolder);
                logWriter.Flush();
                return new List<ReadFileResult>();
            }
            List<ReadFileResult> readFileResults = CopyRunFqData(runNo, runId, readsFolder, runFolder, laneFrom, laneTo);
            return readFileResults;
        }

        private List<ReadFileResult> CopyRunFqData(int runNo, string runId, string readsFolder, string runFolder, int laneFrom, int laneTo)
        {
            string runName = Path.GetFileName(runFolder);
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            for (int lane = laneFrom; lane <= laneTo; lane++)
		    {
                int[] cycles = new int[4];
                cycles[1] = cycles[2] = cycles[3] = -1;
                for (int read = 1; read <= 3; read++)
				{
                    string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                    string readyFilePath = Path.Combine(runFolder, readyFileName);
                    if (File.Exists(readyFilePath) && !Outputter.DataExists(readsFolder, runNo, lane, read, runName))
                    {
                        ReadFileResult r;
                        r = CopyBclLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                        if (r == null)
                            r = CopyQseqLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                        if (r == null)
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Could not find any bcl or qseq files in run " + runId +
                                                                            " lane " + lane.ToString() + " read " + read.ToString());
                            logWriter.Flush();
                        }
                        else
                        {
                            readFileResults.Add(r);
                            cycles[read] = (int)r.readLen;
                            if (projectDB != null)
                            {
                                projectDB.AddToBackupQueue(r.readFile, 10);
                                if (r.read == 1)
                                    projectDB.SetIlluminaYield(runId, r.nReads, r.nPFReads, r.lane);
                            }
                        }
                    }
                }
                if (projectDB != null)
                    projectDB.UpdateRunCycles(runId, cycles[1], cycles[2], cycles[3]);
            };
            return readFileResults;
        }

        private ReadFileResult CopyBclLaneRead(int runNo, string readsFolder, string runFolder, string runName, int lane, int read)
        {
            Outputter outputter = null;
            foreach (FastQRecord rec in BclFile.Stream(runFolder, lane, read))
            {
                if (outputter == null)
                {
                    logWriter.Write(DateTime.Now.ToString() + " Copying lane {0} read {1} from run {2}...", lane, read, runNo);
                    logWriter.Flush();
                    outputter = new Outputter(readsFolder, runNo, lane, read, runName);
                }
                outputter.Write(rec);
            }
            if (outputter == null)
                return null;
            ReadFileResult r = outputter.Close();
            logWriter.WriteLine(r.readFile + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            logWriter.Flush();
            return r;
        }

        private ReadFileResult CopyQseqLaneRead(int runNo, string readsFolder, string runFolder, string runName, int lane, int read)
        {
            string[] qseqFiles = Directory.GetFiles(runFolder, string.Format("s_{0}_{1}_*_qseq.txt", lane, read));
            if (qseqFiles.Length == 0)
                return null;
            logWriter.Write(DateTime.Now.ToString() + " Copying lane {0} read {1} from run {2}...", lane, read, runNo);
            logWriter.Flush();
            Outputter outputter = new Outputter(readsFolder, runNo, lane, read, runName);
            foreach (string qseqFile in qseqFiles)
            {
                foreach (FastQRecord rec in FastQFile.Stream(qseqFile, Props.props.QualityScoreBase, true))
                    outputter.Write(rec);
            }
            ReadFileResult r = outputter.Close();
            logWriter.WriteLine(r.readFile + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            logWriter.Flush();
            return r;
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
                PFFilename = GetPFFilePath(readsFolder, fileId) + ".gz";
                PFFile = PFFilename.OpenWrite();
                nonPFFile = Path.Combine(readsFolder, Path.Combine(PathHandler.nonPFSubFolder, fileId + "_nonPF.fq.gz")).OpenWrite();
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
                return string.Format(PathHandler.readFileIdCreatePattern, runId, lane, read, runName);
            }
            private static string GetStatsFilePath(string readsFolder, string fileId)
            {
                return Path.Combine(readsFolder, Path.Combine(PathHandler.statsSubFolder, fileId + ".txt"));
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
                PFFile.Dispose();
                CmdCaller.Run("chmod", "a+rw " + PFFilename);
                nonPFFile.Close();
                nonPFFile.Dispose();
                double passedAvLen = (nPFReads > 0) ? (totalPFReadLength / (double)nPFReads) : 0.0;
                double nonPassedAvLen = (nReads - nPFReads > 0) ? (totalNonPFReadLength / (double)(nReads - nPFReads)) : 0.0;
                using (StreamWriter statsFile = new StreamWriter(statsFilePath))
                {
                    statsFile.WriteLine("TotalReadsNumber\t" + nReads);
                    statsFile.WriteLine("PassedFilterReadsNumber\t" + nPFReads);
                    statsFile.WriteLine("PassedFilterReadsAverageLength\t{0:0.##}", passedAvLen);
                    statsFile.WriteLine("NonPassedFilterReadsAverageLength\t{0:0.##}", nonPassedAvLen);
                }
                return new ReadFileResult(PFFilename, lane, read, nPFReads, nReads - nPFReads, (uint)passedAvLen);
            }

        }

    }
}
