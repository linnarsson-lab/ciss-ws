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


namespace Linnarsson.Strt
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
        StreamWriter logWriter;
        ProjectDB projectDB;

        public ReadCopier(StreamWriter logWriter)
        {
            this.logWriter = logWriter;
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

        public void Scan(string illuminaRunsFolder, string outputReadsFolder)
        {
            AssertOutputFolders(outputReadsFolder);
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
                    bool readyFileExists = File.Exists(readyFilePath);
                    bool callFolderExists = Directory.Exists(callFolder);
                    if (readyFileExists && callFolderExists)
                    {
                        if (projectDB.SecureStartRunCopy(runId, runNo, runDate))
                        {
                            try
                            {
                                Copy(runNo, runId, runFolder, outputReadsFolder, 1, 8);
                            }
                            catch (Exception e)
                            {
                                projectDB.UpdateRunStatus(runId, "copyfail", runNo);
                                throw (e);
                            }
                            projectDB.UpdateRunStatus(runId, "copied", runNo);
                        }
                    }
                }
            }
        }

        public void Copy(int runNo, string runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
            if (Directory.Exists(callFolder))
                CopyRunFqData(runNo, runId, runFolder, readsFolder, laneFrom, laneTo);
            else
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: BaseCalls folder does not exist: {0}", callFolder);
                logWriter.Flush();
            }
        }

        private void CopyRunFqData(int runNo, string runId, string runFolder, string readsFolder, int laneFrom, int laneTo)
        {
            string runFolderName = Path.GetFileName(runFolder);
            bool someError = false;
            for (int lane = laneFrom; lane <= laneTo; lane++)
		    {
                int[] cycles = new int[4];
                cycles[1] = cycles[2] = cycles[3] = -1;
                for (int read = 1; read <= 3; read++)
				{
                    try
                    {
                        string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                        string readyFilePath = Path.Combine(runFolder, readyFileName);
                        bool readyFileExists = File.Exists(readyFilePath);
                        bool readAlreadyCopied = LaneReadWriter.DataExists(readsFolder, runNo, lane, read, runFolderName);
                        if (readyFileExists && !readAlreadyCopied)
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
                                cycles[read] = (int)r.readLen;
                                if (projectDB != null)
                                {
                                    projectDB.AddToBackupQueue(r.PFPath, 10);
                                    if (r.read == 1)
                                        projectDB.SetIlluminaYield(runId, r.nReads, r.nPFReads, r.lane);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Copying Run{0}_L{1}_{2}: {3}", runNo, lane, read, e);
                        someError = true;
                    }
                }
                if (projectDB != null)
                    projectDB.UpdateRunCycles(runId, cycles[1], cycles[2], cycles[3]);
            }
            if (someError)
                throw new Exception("Some error(s) occured during copying of Run" + runNo);
        }

/*        /// <summary>
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
                bool readyFileExists = File.Exists(readyFilePath);
                Console.WriteLine("{0} exists = {1}", readyFilePath, readyFileExists);
                bool read1AlreadyCopied = LaneReadWriter.DataExists(readsFolder, runNo, lane, 1, runFolderName);
                Console.WriteLine("read1AlreadyCopied = {0}", read1AlreadyCopied);
                if (readyFileExists && !read1AlreadyCopied)
                {
                    List<LaneReadWriter> lrws = new List<LaneReadWriter>();
                    lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 1));
                    if (File.Exists(Path.Combine(runFolder, "Basecalling_Netcopy_complete_Read2.txt")))
                        lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 2));
                    if (File.Exists(Path.Combine(runFolder, "Basecalling_Netcopy_complete_Read3.txt")))
                        lrws.Add(new LaneReadWriter(readsFolder, runFolderName, runNo, lane, 3));
                    List<SampleReadWriter> srws = new List<SampleReadWriter>();
                    List<ExtractionTask> tasks = projectDB.InitiateExtractionOfLaneAnalyses(runNo, lane);
                    string[] projNames = tasks.ConvertAll(t => t.projectName).ToArray();
                    string projIds = string.Join(",", projNames);
                    logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying Run{0}:L{1} and extracting {2}...", runNo, lane, projIds);
                    logWriter.Flush();
                    foreach (ExtractionTask task in tasks)
                    {
                        string projectFolder = PathHandler.GetRooted(task.projectName);
                        string extractionFolder = PathHandler.MakeExtractionFolderSubPath(projectFolder, task.barcodeSet, StrtReadMapper.EXTRACTION_VERSION);
                        Barcodes barcodes = Barcodes.GetBarcodes(task.barcodeSet);
                        LaneInfo laneInfo = new LaneInfo(lrws[0].PFFilePath, runFolderName, lane.ToString()[0], extractionFolder, barcodes.Count, "");
                        srws.Add(new SampleReadWriter(barcodes, laneInfo));
                    }
                    BclReadExtractor bre = new BclReadExtractor(lrws, srws);
                    bre.Process(runFolder, lane);
                    ReadFileResult rfr1 = lrws[0].CloseAndSummarize();
                    for (int readIdx = 1; readIdx < lrws.Count; readIdx++)
                        readFileResults.Add(lrws[readIdx].CloseAndSummarize());
                    readFileResults.Add(rfr1);
                    projectDB.AddToBackupQueue(rfr1.PFPath, 10);
                    projectDB.SetIlluminaYield(runId, rfr1.nReads, rfr1.nPFReads, rfr1.lane);
                    int[] cycles = bre.GetNCyclesByReadIdx();
                    projectDB.UpdateRunCycles(runFolderName, cycles[0], cycles[1], cycles[2]);
                    foreach (SampleReadWriter srw in srws)
                        srw.CloseAndWriteSummary();
                    foreach (ExtractionTask task in tasks)
                        projectDB.UpdateAnalysisStatus(task.analysisId, ProjectDescription.STATUS_INQUEUE);
                }
            }
            return readFileResults;
        }
*/

        private ReadFileResult CopyBclLaneRead(int runNo, string readsFolder, string runFolder, string runFolderName, int lane, int read)
        {
            LaneReadWriter readWriter = null;
            foreach (FastQRecord rec in BclFile.Stream(runFolder, lane, read))
            {
                if (readWriter == null)
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying lane {0} read {1} from run {2}...", lane, read, runNo);
                    logWriter.Flush();
                    readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
                }
                readWriter.Write(rec);
            }
            if (readWriter == null)
                return null;
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: " + r.PFPath + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            logWriter.Flush();
            return r;
        }

        private ReadFileResult CopyQseqLaneRead(int runNo, string readsFolder, string runFolder, string runFolderName, int lane, int read)
        {
            string[] qseqFiles = Directory.GetFiles(runFolder, string.Format("s_{0}_{1}_*_qseq.txt", lane, read));
            if (qseqFiles.Length == 0)
                return null;
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying lane {0} read {1} from run {2}...", lane, read, runNo);
            logWriter.Flush();
            LaneReadWriter readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
            foreach (string qseqFile in qseqFiles)
            {
                foreach (FastQRecord rec in FastQFile.Stream(qseqFile, Props.props.QualityScoreBase, true))
                    readWriter.Write(rec);
            }
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: " + r.PFPath + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
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


        public List<ReadFileResult> SingleUseCopy(string runFolder, string readsFolder, int laneFrom, int laneTo,
                                                  bool forceOverwrite)
        {
            bool junk;
            return SingleUseCopy(runFolder, readsFolder, laneFrom, laneTo, forceOverwrite, out junk);
        }

        /// <summary>
        /// Method to call when manually copying (e.g. repairing lost data) some specific lane(s)
        /// </summary>
        /// <param name="runFolder"></param>
        /// <param name="readsFolder"></param>
        /// <param name="laneFrom"></param>
        /// <param name="laneTo"></param>
        /// <param name="forceOverwrite">if true will overwrite any existing fastq files</param>
        /// <returns></returns>
        public List<ReadFileResult> SingleUseCopy(string runFolder, string readsFolder, int laneFrom, int laneTo,
                                                  bool forceOverwrite, out bool someReadFailed)
        {
            someReadFailed = false;
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            int runNo = 0;
            Match m = MatchRunFolderName(runFolder);
            if (m.Success)
                runNo = int.Parse(m.Groups[2].Value);
            else
                logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Can not parse runNo from " + runFolder + " setting to 0.");
            string runName = Path.GetFileName(runFolder);
            for (int lane = laneFrom; lane <= laneTo; lane++)
            {
                for (int read = 1; read <= 3; read++)
                {
                    string readyFileName = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                    string readyFilePath = Path.Combine(runFolder, readyFileName);
                    bool readyFileExists = File.Exists(readyFilePath);
                    if (!readyFileExists)
                    {
                        if (read < 3)
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping lane {0} read {1}: {2} is missing.", lane, read, readyFileName);
                            someReadFailed = true;
                        }
                        continue;
                    }
                    if (LaneReadWriter.DataExists(readsFolder, runNo, lane, read, runName) && !forceOverwrite)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Skipping lane {0} read {1}: Output PF and statistics files already exist.", lane, read);
                        someReadFailed = true;
                        continue;
                    }
                    else 
                    {
                        ReadFileResult r;
                        r = CopyBclLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                        if (r == null)
                            r = CopyQseqLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                        if (r == null)
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Could not find any .bcl or .qseq files for lane {0} read {1}.", lane, read);
                            someReadFailed = true;
                        }
                        else
                            readFileResults.Add(r);
                    }
                }
            }
            return readFileResults;
        }

        /// <summary>
        /// Used when starting parallell copying of several runs
        /// </summary>
        /// <param name="startObj"></param>
        public void CopyRun(object startObj)
        {
            CopierStart cs = (CopierStart)startObj;
            cs.readFileResults = SingleUseCopy(cs.runFolder, cs.readsFolder, cs.laneFrom, cs.laneTo, cs.forceOverwrite, out cs.someReadFailed);
        }

    }

    public class CopierStart
    {
        public string runFolder;
        public string readsFolder;
        public int laneFrom;
        public int laneTo;
        public bool forceOverwrite = false;
        public bool someReadFailed = false;
        public List<ReadFileResult> readFileResults = new List<ReadFileResult>();

        public CopierStart(string runFolder, string readsFolder, int laneFrom, int laneTo, bool forceOverwrite)
        {
            this.runFolder = runFolder;
            this.readsFolder = readsFolder;
            this.laneFrom = laneFrom;
            this.laneTo = laneTo;
            this.forceOverwrite = forceOverwrite;
        }
    }
}
