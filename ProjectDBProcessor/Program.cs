using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace ProjectDBProcessor
{
    class Program
    {
        private static string logFile;
        private static StreamWriter logWriter;
        private static ProjectDB projectDB;
        private static Dictionary<string, string> lastMsgByProject = new Dictionary<string, string>();
        private static bool reverseSortProjects = false;

        static void Main(string[] args)
        {
            int minutesWait = 10; // Time between scans to wait for new data to appear in queue.
            int maxExceptions = 200; // Max number of exceptions before giving up.
            Props.props.InsertCellDBData = true; // Update cells10k data by default
            logFile = new FileInfo("PDBP_" + Process.GetCurrentProcess().Id + ".log").FullName;
            try
            {
                int i = 0;
                while (i < args.Length)
                {
                    string arg = args[i];
                    if (arg == "-t")
                        minutesWait = int.Parse(args[++i]);
                    else if (arg.StartsWith("-t"))
                        minutesWait = int.Parse(arg.Substring(2));
                    else if (arg == "-e")
                        maxExceptions = int.Parse(args[++i]);
                    else if (arg.StartsWith("-e"))
                        maxExceptions = int.Parse(arg.Substring(2));
                    else if (arg.StartsWith("-i=T"))
                        Props.props.InsertCellDBData = true;
                    else if (arg.StartsWith("-i=F"))
                        Props.props.InsertCellDBData = false;
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
                    else if (arg == "-r")
                        reverseSortProjects = true;
                    else throw new ArgumentException();
                    i++;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Typical usage:\nnohup mono ProjectDBProcessor.exe [options] > PDBP.out &");
                Console.WriteLine("Options: -t N   scan for new data every N  minutes. [default={0}]", minutesWait);
                Console.WriteLine("         -e N   exit if more than N exceptions occured. [default={0}]", maxExceptions);
                Console.WriteLine("         -i=[True|False] switch on/off data insertion into cells10k DB for C1-samples. [default={0}]",
                                  Props.props.InsertCellDBData);
                Console.WriteLine("         -l F   log to file F instead of default logfile.");
                Console.WriteLine("         -r     reverse sort projects, i.e. start with the most recent project in database.");
                return;
            }
            if (!File.Exists(logFile))
            {
                File.Create(logFile).Close();
            }
            using (logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                string now = DateTime.Now.ToString();
                logWriter.WriteLine(DateTime.Now.ToString() + " ProjectDBProcessor started. ScanInterval=" + minutesWait +
                                    " minutes. Max#Exceptions=" + maxExceptions);
                logWriter.Flush();
                Console.WriteLine("ProjectDBProcessor started " + now + " and logging to " + logFile);

                projectDB = new ProjectDB();
                Run(minutesWait, maxExceptions);
            }
        }

        private static void Run(int minutesWait, int maxExceptions)
        {
            int nExceptions = 0;
            while (nExceptions < maxExceptions)
            {
                try
                {
                    ScanDB();
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                catch (Exception exp)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: ProjectDBProcessor ***\n" + exp);
                    logWriter.Flush();
                }
            }
            logWriter.WriteLine(DateTime.Now.ToString() + " ProjectDBProcessor quit.");
            if (nExceptions > 0)
                ReportExceptionTermination(logFile);
        }

        private static void ReportExceptionTermination(string logFile)
        {
            string subject = "ProjectDBProcessor PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.\n" +
                          "After fixing the error, restart with 'nohup ProjectDBProcessor.exe > PDBP.out &'";
            EmailSender.ReportFailureToAdmin(subject, body, false);
        }

        /// <summary>
        /// Check that all needed read files and corresponding summary files have been created,
        /// and set run numbers in projDescr
        /// </summary>
        /// <param name="projDescr"></param>
        /// <returns>false if some fq file is missing, or there was an error</returns>
        private static bool CheckAllReadsCollected(ref ProjectDescription projDescr)
        {
            try
            {
                char reqReadNo = Barcodes.GetBarcodes(projDescr.barcodeSet).HighestNeededReadNo;
                List<int> runNos = new List<int>();
                foreach (string laneArg in projDescr.runIdsLanes)
                {
                    string[] parts = laneArg.Split(':');
                    int runNo = PathHandler.CheckReadsCollected(parts[0], parts[1], reqReadNo);
                    if (runNo == -1) return false;
                    runNos.Add(runNo);
                }
                projDescr.runNumbers = runNos.ToArray();
            }
            catch (Exception e)
            {
                List<string> messages = new List<string>();
                if (HandleError(projDescr, messages , e, true))
                    NotifyManager(projDescr, messages);
                return false;
            }
            return true;
        }

        private static void ScanDB()
        {
            projectDB.ResetQueue();
            ProjectDescription pd = projectDB.GetNextProjectInQueue(reverseSortProjects);
            while (pd != null)
            {
                if (CheckAllReadsCollected(ref pd))
                {
                    if (HandleDBTask(pd))
                        projectDB.ResetQueue();
                }
                pd = projectDB.GetNextProjectInQueue(reverseSortProjects);
            }
        }

        /// <summary>
        /// Will return false if an error occured that may be solved by trying again later, in which
        /// case status is reset to 'inqueue'
        /// </summary>
        /// <param name="projDescr"></param>
        /// <returns></returns>
        private static bool HandleDBTask(ProjectDescription projDescr)
        {
            bool success = false;
            bool notifyManager = true;
            projDescr.status = ProjectDescription.STATUS_PROCESSING;
            int nRowsAffected = projectDB.SecureStartAnalysis(projDescr);
            if (nRowsAffected == 0) return false;
            List<string> messages = new List<string>();
            try
            {
                ProcessItem(projDescr);
                messages = PublishResultsForDownload(projDescr);
                projDescr.status = ProjectDescription.STATUS_READY;
                projectDB.PublishResults(projDescr);
                success = true;
                logWriter.WriteLine(DateTime.Now.ToString() + " " + projDescr.plateId + "[analysisId=" + projDescr.analysisId +
                                    "] finished with status " + projDescr.status);
                logWriter.Flush();
                lastMsgByProject.Remove(projDescr.plateId);
            }
            catch (BarcodeFileException e)
            {
                notifyManager = HandleError(projDescr, messages, e, false);
            }
            catch (SampleLayoutFileException e)
            {
                notifyManager = HandleError(projDescr, messages, e, false);
            }
            catch (Exception e)
            {
                bool recoverable = e.Message.Contains("Sharing violation");
                notifyManager = HandleError(projDescr, messages, e, recoverable);
            }
            if (notifyManager)
                NotifyManager(projDescr, messages);
            projectDB.UpdateAnalysisStatus(projDescr);
            return success;
        }

        private static bool HandleError(ProjectDescription projDescr, List<string> messages, Exception e, bool recoverable)
        {
            projDescr.managerEmails += ";" + Props.props.FailureReportAndAnonDownloadEmail;
            logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: ProjectDBProcessor processing " + projDescr.plateId + " ***\n" + e);
            logWriter.Flush();
            Console.WriteLine("\n===============" + projDescr.plateId + " finished with errors: ==============\n" + e + 
                            "\n=============== check details in " + logFile  + " ================\n");
            string errorMsg = e.Message;
            projDescr.status = recoverable ? ProjectDescription.STATUS_INQUEUE : ProjectDescription.STATUS_FAILED;
            if (lastMsgByProject.ContainsKey(projDescr.plateId) && lastMsgByProject[projDescr.plateId].Equals(errorMsg))
                return false;
            messages.Add(errorMsg);
            lastMsgByProject[projDescr.plateId] = errorMsg;
            return true;
        }

        private static void ProcessItem(ProjectDescription projDescr)
        {
            logWriter.WriteLine(DateTime.Now.ToString() + " Processing " + projDescr.plateId + " - " + projDescr.LaneCount + " lanes [DBId=" + projDescr.analysisId + "]...");
            logWriter.Flush();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            DateTime d = DateTime.Now;
            if (projDescr.layoutFile != "")
            {
                string layoutSrcPath = Path.Combine(Props.props.UploadsFolder, projDescr.layoutFile);
                if (File.Exists(layoutSrcPath))
                {
                    string layoutDestPath = projDescr.SampleLayoutPath;
                    try
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(layoutDestPath)))
                            Directory.CreateDirectory(Path.GetDirectoryName(layoutDestPath));
                        File.Copy(layoutSrcPath, layoutDestPath, true);
                        logWriter.WriteLine(DateTime.Now.ToString() + " cp " + layoutSrcPath + " -> " + layoutDestPath); logWriter.Flush();
                    }
                    catch (Exception e)
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " *** WARNING: Could not copy layout " + layoutSrcPath
                                                                    + " to " + layoutDestPath + ": " + e.Message);
                        logWriter.Flush();
                    }
                }
                else
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** WARNING: " + projDescr.plateId + " - layout does not exist: " + layoutSrcPath);
                    logWriter.Flush();
                }
            }
            if (projDescr.aligner != "") 
                Props.props.Aligner = projDescr.aligner;
            StrtReadMapper mapper = new StrtReadMapper();
            mapper.Process(projDescr, logWriter);
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            logWriter.WriteLine(DateTime.Now.ToString() + " ..." + projDescr.plateId + " done after " + ts + ".");
            logWriter.Flush();
        }

        private static void NotifyManager(ProjectDescription projDescr, List<string> results)
        {
            if (projDescr.managerEmails == "") return;
            bool success = (projDescr.status == ProjectDescription.STATUS_READY);
            string subject = (success)? "Results ready from STRT project " + projDescr.plateId :
                                        "Failure processing STRT project " + projDescr.plateId;
            StringBuilder sb = new StringBuilder();
            sb.Append("<html>");
            string toEmails = projDescr.managerEmails;
            if (success)
            {
                if (results.Count == 0)
                    sb.Append("<p>No new data were generated - the old results that exist are up-to-date.</p>");
                else
                {
                    sb.Append("<p>You can retrieve the results from the following location:</p>");
                    foreach (string link in results)
                        sb.Append(string.Format("<a href={0}>{0}</a><br />", link, link));
                }
            }
            else
            {
                toEmails = Props.props.FailureReportAndAnonDownloadEmail;
                sb.Append("<p>The data analysis failed!</p>");
                foreach (string msg in results)
                    sb.Append(msg);
                sb.Append("Please consult logfile " + logFile + " for technical info on the error.");
                sb.Append("<p>After fixing the error, you may need to re-activate the analysis in the Sanger DB (View the sample: Analysis results/Retry).</p>");
            }
            sb.Append("<p>Run parameters follow:</p>\n<code>");
            sb.Append("<br />\nBarcodeSet: " + projDescr.barcodeSet);
            sb.Append("<br />\nExtraction version: " + projDescr.extractionVersion);
            sb.Append("<br />\nAnnotation version: " + projDescr.annotationVersion + "<br />");
            foreach (ResultDescription rd in projDescr.resultDescriptions)
                sb.Append("<br />\nAligner index: " + rd.splcIndexVersion + " - Results: " + rd.resultFolder);
            sb.Append("\n</code>\n</html>");
            toEmails = toEmails.Replace(';', ','); // C# requires email addresses separated by ','
            string body = sb.ToString();
            EmailSender.SendMsg(toEmails, subject, body, true);
        }

        private static List<string> PublishResultsForDownload(ProjectDescription projDescr)
        {
            List<string> resultLinks = new List<string>();
            foreach (ResultDescription resultDescr in projDescr.resultDescriptions)
            {
                string docFileDest = Path.Combine(resultDescr.resultFolder, Path.GetFileName(Props.props.OutputDocFile));
                if (File.Exists(Props.props.OutputDocFile))
                {
                    if (File.Exists(docFileDest))
                        File.Delete(docFileDest);
                    File.Copy(Props.props.OutputDocFile, docFileDest);
                }
                string resultTarName = Path.GetFileName(resultDescr.resultFolder) + ".tar.gz";
                string tempTarGzPath = Path.Combine(Path.GetTempPath(), resultTarName);
                CompressResult(resultDescr.resultFolder, tempTarGzPath);
                string cmdArg = string.Format("-P {0} {1} {2}", Props.props.ResultDownloadScpPort, tempTarGzPath, Props.props.ResultDownloadUrl);
                Console.WriteLine("scp " + cmdArg);
                int cmdResult = CmdCaller.Run("scp", cmdArg);
                if (cmdResult == 0)
                {
                    string resultLink = Props.props.ResultDownloadFolderHttp + resultTarName;
                    resultLinks.Add(resultLink);
                    File.Delete(tempTarGzPath);
                }
                else
                {
                    resultLinks.Add(Path.GetFileName(resultDescr.resultFolder) + " could not be published on HTTP server - contact administrator!");
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: " + 
                                         Path.GetFileName(resultDescr.resultFolder) + " could not be published on HTTP server");
                    logWriter.Flush();
                }
            }
            return resultLinks;
        }

        private static void CompressResult(string resultFolder, string outputPath)
        {
            string cmdArg = string.Format("-acf {0} {1}", outputPath, resultFolder);
            Console.WriteLine("tar " + cmdArg);
            int cmdResult = CmdCaller.Run("tar", cmdArg);
        }
    }

}
