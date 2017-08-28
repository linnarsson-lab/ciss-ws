using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;


namespace Linnarsson.Strt
{
    public enum ReadCopierStatus { ALLREADSREADY, SOMEREADFAILED, SOMEREADMISSING };

    /// <summary>
    /// Methods to copy reads from .bcl or .qseq file into fq files.
    /// Looks for "Basecalling_Netcopy_complete_ReadN.txt" in the Run folder as an indication
    /// that the output base call files are available from the HiSeq instrument.
    /// </summary>
    public class ReadCopier
    {
        StreamWriter logWriter;
        IDB projectDB;

        public ReadCopier(StreamWriter logWriter, IDB projectDB)
        {
            this.logWriter = logWriter;
            this.projectDB = projectDB;
        }

        /// <summary>
        /// Extract lane and read count from runFolder/"RunInfo.xml"
        /// </summary>
        /// <param name="runFolder"></param>
        /// <param name="laneCount">defaults to 8 if RunInfo.xml is missing or non-parsable</param>
        /// <param name="readCount">defaults to 3 if RunInfo.xml is missing or non-parsable</param>
        /// <returns>true if file existed and was parsable</returns>
        public static bool ParseRunInfo(string runFolder, out int laneCount, out int readCount)
        {
            laneCount = readCount = 0;
            string runInfoPath = Path.Combine(runFolder, "RunInfo.xml");
            if (File.Exists(runInfoPath))
            {
                using (StreamReader reader = new StreamReader(runInfoPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match m = Regex.Match(line, "FlowcellLayout LaneCount=.([0-9]+).");
                        if (m.Success)
                            laneCount = int.Parse(m.Groups[1].Value);
                        m = Regex.Match(line, "Read Number=.([1-9]). NumCycles=.([0-9]+).");
                        if (m.Success)
                        {
                            int readNo = int.Parse(m.Groups[1].Value);
                            readCount = Math.Max(readCount, readNo);
                        }
                    }
                }
            }
            bool success = (laneCount > 0 && readCount > 0);
            if (laneCount == 0) laneCount = 8;
            if (readCount == 0) readCount = 3;
            return success;
        }

        /// <summary>
        /// Copy reads from runFolder into fq files in readsFolder. Set laneFrom/laneTo > 0 to restrict which lanes to copy.
        /// Input may be .bcl or .qseq data.
        /// </summary>
        /// <param name="runFolder"></param>
        /// <param name="readsFolder"></param>
        /// <param name="laneFrom">Set to 0 to copy all lanes</param>
        /// <param name="laneTo">Set to 0 to copy all lanes</param>
        /// <param name="forceOverwrite">false will skip copying of reads where output fq already exist</param>
        /// <param name="someReadFailed">true if some lane/read failed (details written to logFile)</param>
        /// <returns>One ReadFileResult for each successfully written fq file</returns>
        public List<ReadFileResult> SerialCopy(string runFolder, string readsFolder, int laneFrom, int laneTo,
                                                  bool forceOverwrite, out ReadCopierStatus status)
        {
            int maxLaneNo, maxReadNo;
            bool runInfoParsed = ParseRunInfo(runFolder, out maxLaneNo, out maxReadNo);
            if (laneFrom == 0) laneFrom = 1;
            if (laneTo == 0) laneTo = maxLaneNo;
            int runNo;
            string runId, runDate;
            if (!ParseRunFolderName(runFolder, out runNo, out runId, out runDate))
                logWriter.WriteLine(DateTime.Now.ToString() + " WARNING: Can not parse runNo from " + runFolder + " - setting to " + runNo);
            string runName = Path.GetFileName(runFolder);
            status = ReadCopierStatus.ALLREADSREADY;
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            for (int lane = laneFrom; lane <= laneTo; lane++)
            {
                for (int read = 1; read <= maxReadNo; read++)
                {
                    try
                    {
                        string readReadyFile = string.Format("Basecalling_Netcopy_complete_Read{0}.txt", read);
                        readReadyFile = Path.Combine(runFolder, readReadyFile);
                        bool readReady = File.Exists(readReadyFile);
                        bool readAlreadyCopied = LaneReadWriter.DataExists(readsFolder, runNo, lane, read, runName);
                        if (!readReady && (runInfoParsed || read <= 2))
                        {
                            status = ReadCopierStatus.SOMEREADMISSING;
                            continue;
                        }
                        if (readAlreadyCopied && !forceOverwrite)
                            continue;
                        else
                        {
                            ReadFileResult r;
                            r = CopyBclLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                            if (r == null)
                                r = CopyQseqLaneRead(runNo, readsFolder, runFolder, runName, lane, read);
                            if (r != null)
							{
								readFileResults.Add(r);
								if (projectDB != null)
									projectDB.ReportReadFileResult(runId, read, r);
							}
							else if (lane <= 2) // Error if less than two lanes (RapidRun)
                            {
                                logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Copying Run{0}_L{1}_{2}: No bcl/qseq files found", runNo, lane, read);
                                status = ReadCopierStatus.SOMEREADFAILED;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Copying Run{0}_L{1}_{2}: {3}", runNo, lane, read, e);
                        status = ReadCopierStatus.SOMEREADFAILED;
                    }
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
                    logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying Run{0}_L{1}_{2}...", runNo, lane, read);
                    readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
                }
                readWriter.Write(rec);
            }
            if (readWriter == null)
                return null;
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: " + r.PFPath + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            return r;
        }

        private ReadFileResult CopyQseqLaneRead(int runNo, string readsFolder, string runFolder, string runFolderName, int lane, int read)
        {
            string[] qseqFiles = Directory.GetFiles(runFolder, string.Format("s_{0}_{1}_*_qseq.txt", lane, read));
            if (qseqFiles.Length == 0)
                return null;
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying Run{0}_L{1}_{2}...", runNo, lane, read);
            LaneReadWriter readWriter = new LaneReadWriter(readsFolder, runFolderName, runNo, lane, read);
            foreach (string qseqFile in qseqFiles)
            {
                foreach (FastQRecord rec in FastQFile.Stream(qseqFile, Props.props.QualityScoreBase, true))
                    readWriter.Write(rec);
            }
            ReadFileResult r = readWriter.CloseAndSummarize();
            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: " + r.PFPath + " done. (" + r.nPFReads + " PFReads, " + r.readLen + " cycles)");
            return r;
        }

        public static bool ParseRunFolderName(string runFolder, out int runNo, out string runId, out string runDate)
        {
            runNo = 0;
            runId = "H" + DateTime.Now.ToString("HH") + "M" + DateTime.Now.ToString("mm") + "XXXX";
            runDate = DateTime.Now.ToString("yyy-MM-dd");
            Match m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]+)_FC$");
            if (!m.Success)
                m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]+)$");
            if (!m.Success)
                m = Regex.Match(runFolder, "([0-9]{6})_[^_]+_([0-9]{4})_[AB]([a-zA-Z0-9]+)$");
            if (m.Success)
            {
                runNo = int.Parse(m.Groups[2].Value);
                runId = (m.Groups.Count > 3) ? m.Groups[3].Value : runNo.ToString();
                runDate = m.Groups[1].Value;
                runDate = "20" + runDate.Substring(0, 2) + "-" + runDate.Substring(2, 2) + "-" + runDate.Substring(4);
            }
            return m.Success;
        }

        /// <summary>
        /// Used when starting parallell copying of several runs
        /// </summary>
        /// <param name="startObj"></param>
        public void CopyRun(object startObj)
        {
            CopierStart cs = (CopierStart)startObj;
            cs.readFileResults = SerialCopy(cs.runFolder, cs.readsFolder, cs.laneFrom, cs.laneTo, cs.forceOverwrite, out cs.status);
        }

        /// <summary>
        /// Copy reads from 8 lanes in runFolder into fq files in readsFolder. Extract two lanes each in four parallel processes.
        /// Will not overwrite already existing fq files.
        /// </summary>
        /// <param name="runFolder"></param>
        /// <param name="readsFolder"></param>
        /// <param name="status">true if some lane/read failed (details written to logFile)</param>
        /// <returns>One ReadFileResult for each successfully written fq file</returns>
        public List<ReadFileResult> ParallelCopy(string runFolder, string readsFolder, out ReadCopierStatus status)
        {
            status = ReadCopierStatus.ALLREADSREADY;
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            CopierStart start1 = new CopierStart(runFolder, readsFolder, 1, 2, false);
            Thread thread1 = new Thread(CopyRun);
            thread1.Start(start1);
            CopierStart start2 = new CopierStart(runFolder, readsFolder, 3, 4, false);
            Thread thread2 = new Thread(CopyRun);
            thread2.Start(start2);
            CopierStart start3 = new CopierStart(runFolder, readsFolder, 5, 6, false);
            Thread thread3 = new Thread(CopyRun);
            thread3.Start(start3);
            CopierStart start4 = new CopierStart(runFolder, readsFolder, 7, 8, false);
            Thread thread4 = new Thread(CopyRun);
            thread4.Start(start4);
            thread1.Join();
            thread2.Join();
            thread3.Join();
            thread4.Join();
            readFileResults.AddRange(start1.readFileResults);
            readFileResults.AddRange(start2.readFileResults);
            readFileResults.AddRange(start3.readFileResults);
            readFileResults.AddRange(start4.readFileResults);
            if (start1.status == ReadCopierStatus.SOMEREADFAILED || start2.status == ReadCopierStatus.SOMEREADFAILED ||
                start3.status == ReadCopierStatus.SOMEREADFAILED || start4.status == ReadCopierStatus.SOMEREADFAILED)
                status = ReadCopierStatus.SOMEREADFAILED;
            else if (start1.status == ReadCopierStatus.SOMEREADMISSING || start2.status == ReadCopierStatus.SOMEREADMISSING ||
                start3.status == ReadCopierStatus.SOMEREADMISSING || start4.status == ReadCopierStatus.SOMEREADMISSING)
                status = ReadCopierStatus.SOMEREADMISSING;
            return readFileResults;
        }
    }

    public class CopierStart
    {
        public string runFolder;
        public string readsFolder;
        public int laneFrom;
        public int laneTo;
        public bool forceOverwrite = false;
        public ReadCopierStatus status = ReadCopierStatus.ALLREADSREADY;
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
