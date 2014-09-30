using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.Strt;


namespace BkgFastQCopier
{
    /// <summary>
    /// Methods to scan, at regular intervals, Illumina Runs folder
    /// for new data and copy the reads from .bcl or .qseq file into
    /// the Reads folder for further processing.
    /// Looks for "Basecalling_Netcopy_complete_ReadN.txt" in the Run folder as an indication
    /// that the output base call files are available from the HiSeq instrument.
    /// If a fastq file is missing or has been removed in the Reads folder, the copying will
    /// be performed again.
    /// </summary>
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

        /// <summary>
        /// Make the read, non past filter, and statistics subfolders if they don't exist
        /// </summary>
        /// <param name="outputReadsFolder"></param>
        private static void AssertOutputFolders(string outputReadsFolder)
        {
            if (!File.Exists(outputReadsFolder))
            {
                Directory.CreateDirectory(outputReadsFolder);
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, PathHandler.nonPFReadsSubFolder));
                Directory.CreateDirectory(Path.Combine(outputReadsFolder, PathHandler.readStatsSubFolder));
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
                    }
                }
            }
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
            List<ReadFileResult> readFileResults = CopyRunFqData(runNo, runId, runFolder, readsFolder, laneFrom, laneTo);
            //List<ReadFileResult> readFileResults = NewCopyRunFqData(runNo, runId, runFolder, readsFolder, laneFrom, laneTo);
            return readFileResults;
        }

        private List<ReadFileResult> CopyRunFqData(int runNo, string runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string runFolderName = Path.GetFileName(runFolder);
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            for (int lane = laneFrom; lane <= laneTo; lane++)
		    {
                int[] cycles = new int[4];
                cycles[1] = cycles[2] = cycles[3] = -1;
                for (int read = 1; read <= 3; read++)
				{
                    string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                    string readyFilePath = Path.Combine(runFolder, readyFileName);
                    if (File.Exists(readyFilePath) && !LaneReadWriter.DataExists(readsFolder, runNo, lane, read, runFolderName))
                    {
                        ReadFileResult r;
                        r = CopyBclLaneRead(runNo, readsFolder, runFolder, runFolderName, lane, read);
                        if (r == null)
                            r = CopyQseqLaneRead(runNo, readsFolder, runFolder, runFolderName, lane, read);
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
                                if (r.read == '1')
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

        /// <summary>
        /// This is a speed-up version that does Extraction of data into projectFolders at the same time as copying.
        /// Not used yet, and needs bug fixing!
        /// </summary>
        /// <param name="runNo"></param>
        /// <param name="runId"></param>
        /// <param name="runFolder"></param>
        /// <param name="readsFolder"></param>
        /// <param name="laneFrom"></param>
        /// <param name="laneTo"></param>
        /// <returns></returns>
        private List<ReadFileResult> NewCopyRunFqData(int runNo, string runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string runFolderName = Path.GetFileName(runFolder);
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            for (int lane = laneFrom; lane <= laneTo; lane++)
            {
                string readyFilePath = Path.Combine(runFolder, "Basecalling_Netcopy_complete.txt");
                string statsFilePath = PathHandler.GetReadStatsFilePath(readsFolder, runFolderName, runNo, lane, 1);
                if (File.Exists(readyFilePath) && !File.Exists(statsFilePath))
                {
                    Console.WriteLine("Processing " + statsFilePath);
                    List<LaneReadWriter> lrws = new List<LaneReadWriter>();
                    lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 1));
                    if (File.Exists(Path.Combine(runFolder, "Basecalling_Netcopy_complete_Read2.txt")))
                        lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 2));
                    if (File.Exists(Path.Combine(runFolder, "Basecalling_Netcopy_complete_Read3.txt")))
                        lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 3));
                    List<SampleReadWriter> srws = new List<SampleReadWriter>();
                    foreach (Pair<string, string> bcAndProj in projectDB.GetBarcodeSetsAndProjects(runNo, lane))
                    {
                        string extractionFolder = PathHandler.MakeExtractionFolderSubPath(bcAndProj.Second, bcAndProj.First, StrtReadMapper.EXTRACTION_VERSION);
                        Barcodes barcodes = Barcodes.GetBarcodes(bcAndProj.First);
                        LaneInfo laneInfo = new LaneInfo(lrws[0].PFFilePath, runFolderName, lane.ToString()[0], extractionFolder, barcodes.Count, "");
                        if (barcodes.IncludeNonPF)
                            laneInfo.nonPFReadFilePath = lrws[0].nonPFFilePath;
                        srws.Add(new SampleReadWriter(barcodes, laneInfo));
                    }
                    BclReadExtractor bre = new BclReadExtractor(lrws, srws);
                    bre.Process(runFolder, lane);
                    ReadFileResult rfr1 = lrws[0].CloseAndSummarize();
                    for (int readIdx = 1; readIdx < lrws.Count; readIdx++)
                        readFileResults.Add(lrws[readIdx].CloseAndSummarize());
                    readFileResults.Add(rfr1);
                    projectDB.AddToBackupQueue(rfr1.readFile, 10);
                    projectDB.SetIlluminaYield(runId, rfr1.nReads, rfr1.nPFReads, rfr1.lane);
                    int[] cycles = bre.GetNCyclesByReadIdx();
                    projectDB.UpdateRunCycles(runFolderName, cycles[0], cycles[1], cycles[2]);
                    foreach (SampleReadWriter srw in srws)
                        srw.CloseAndWriteSummary();
                }
            }
            return readFileResults;
        }


        private ReadFileResult CopyBclLaneRead(int runNo, string readsFolder, string runFolder, string runFolderName, int lane, int read)
        {
            LaneReadWriter readWriter = null;
            foreach (FastQRecord rec in BclFile.Stream(runFolder, lane, read))
            {
                if (readWriter == null)
                {
                    logWriter.Write(DateTime.Now.ToString() + " Copying lane {0} read {1} from run {2}...", lane, read, runNo);
                    logWriter.Flush();
                    readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
                }
                readWriter.Write(rec);
            }
            if (readWriter == null)
                return null;
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(r.readFile + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            logWriter.Flush();
            return r;
        }

        private ReadFileResult CopyQseqLaneRead(int runNo, string readsFolder, string runFolder, string runFolderName, int lane, int read)
        {
            string[] qseqFiles = Directory.GetFiles(runFolder, string.Format("s_{0}_{1}_*_qseq.txt", lane, read));
            if (qseqFiles.Length == 0)
                return null;
            logWriter.Write(DateTime.Now.ToString() + " Copying lane {0} read {1} from run {2}...", lane, read, runNo);
            logWriter.Flush();
            LaneReadWriter readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
            foreach (string qseqFile in qseqFiles)
            {
                foreach (FastQRecord rec in FastQFile.Stream(qseqFile, Props.props.QualityScoreBase, true))
                    readWriter.Write(rec);
            }
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(r.readFile + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            logWriter.Flush();
            return r;
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

        /// <summary>
        /// Method to call when manually copying (e.g. repairing lost data) some specific lane(s)
        /// </summary>
        /// <param name="runFolder"></param>
        /// <param name="readsFolder"></param>
        /// <param name="laneFrom"></param>
        /// <param name="laneTo"></param>
        /// <returns></returns>
        public int SingleUseCopy(string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            int nFilesCopied = 0;
            Match m = MatchRunFolderName(runFolder);
            if (m.Success)
            {
                int runNo = int.Parse(m.Groups[2].Value);
                string runName = Path.GetFileName(runFolder);
                for (int lane = laneFrom; lane <= laneTo; lane++)
                {
                    for (int read = 1; read <= 3; read++)
                    {
                        string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                        string readyFilePath = Path.Combine(runFolder, readyFileName);
                        if (File.Exists(readyFilePath) && !LaneReadWriter.DataExists(readsFolder, runNo, lane, read, runName))
                        {
                            ReadFileResult r;
                            r = CopyBclLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                            if (r == null)
                                r = CopyQseqLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                            if (r != null)
                                nFilesCopied++;
                        }
                    }
                }
            }
            return nFilesCopied;
        }

    }
}
