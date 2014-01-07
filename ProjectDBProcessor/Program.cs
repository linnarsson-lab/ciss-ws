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
using System.Net.Mail;
using tar_cs;

namespace ProjectDBProcessor
{
    class Program
    {
        private static string logFile;
        private static StreamWriter logWriter;
        private static ProjectDB projectDB;

        static void Main(string[] args)
        {
            int minutesWait = 5; // Time between scans to wait for new data to appear in queue.
            int maxExceptions = 20; // Max number of exceptions before giving up.
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
                    if (arg == "-e")
                        maxExceptions = int.Parse(args[++i]);
                    else if (arg.StartsWith("-e"))
                        maxExceptions = int.Parse(arg.Substring(2));
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
                    else throw new ArgumentException();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Typical usage:  nohup mono ProjectDBProcessor.exe [options] > PDBP.out &");
                Console.WriteLine("Options: -t N   scan for new data every N  minutes.");
                Console.WriteLine("         -e N   exit if more than N exceptions occured.");
                Console.WriteLine("         -l F   log to file F instead of default logfile.");
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
            string from = Props.props.ProjectDBProcessorNotifierEmailSender;
            string to = Props.props.FailureReportEmail;
            string smtp = "localhost";
            string subject = "BkgBackuper PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.";
            MailMessage message = new MailMessage(from, to, subject, body);
            message.IsBodyHtml = false;
            SmtpClient mailClient = new SmtpClient(smtp, 25);
            mailClient.Send(message);
        }

        private static bool CheckAllReadsCollected(ref ProjectDescription projDescr)
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
            return true;
        }

        private static void ScanDB()
        {
            projectDB.ResetQueue();
            ProjectDescription pd = projectDB.GetNextProjectInQueue();
            while (pd != null)
            {
                if (CheckAllReadsCollected(ref pd))
                {
                    if (HandleDBTask(pd))
                        projectDB.ResetQueue();
                }
                pd = projectDB.GetNextProjectInQueue();
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
            bool result = false;
            projDescr.status = ProjectDescription.STATUS_PROCESSING;
            int nRowsAffected = projectDB.UpdateAnalysisStatus(projDescr, ProjectDescription.STATUS_INQUEUE);
            if (nRowsAffected == 0) return false;
            List<string> results = new List<string>();
            try
            {
                ProcessItem(projDescr);
                results = PublishResultsForDownload(projDescr);
                projDescr.status = ProjectDescription.STATUS_READY;
                projectDB.PublishResults(projDescr);
                result = true;
            }
            catch (Exception e)
            {
                projDescr.managerEmails = Props.props.FailureReportEmail;
                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: ProjectDBProcessor processing " + projDescr.plateId + " ***\n" + e);
                logWriter.Flush();
                results.Add(e.ToString());
                if (e.Message.Contains("Sharing violation"))
                    projDescr.status = ProjectDescription.STATUS_INQUEUE;
                else
                    projDescr.status = ProjectDescription.STATUS_FAILED;
            }
            NotifyManager(projDescr, results);
            logWriter.WriteLine(DateTime.Now.ToString() + " " + projDescr.plateId + "[analysisId=" + projDescr.analysisId + "] finished with status " + projDescr.status);
            logWriter.Flush();
            projectDB.UpdateAnalysisStatus(projDescr);
            return result;
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
                    logWriter.WriteLine(DateTime.Now.ToString() + " cp " + layoutSrcPath + " -> " + layoutDestPath); logWriter.Flush();
                    if (!Directory.Exists(Path.GetDirectoryName(layoutDestPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(layoutDestPath));
                    if (File.Exists(layoutDestPath))
                        File.Delete(layoutDestPath);
                    File.Copy(layoutSrcPath, layoutDestPath, true);
                }
                else
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** WARNING: " + projDescr.plateId + " - layout file does not exist: " + layoutSrcPath);
                    logWriter.Flush();
                }
            }
            StrtReadMapper mapper = new StrtReadMapper(Props.props);
            mapper.Process(projDescr, logWriter);
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            logWriter.WriteLine(DateTime.Now.ToString() + " ..." + projDescr.plateId + " done after " + ts + ".");
            logWriter.Flush();
        }

        private static void NotifyManager(ProjectDescription projDescr, List<string> results)
        {
            if (projDescr.managerEmails == "") return;
            string from = Props.props.ProjectDBProcessorNotifierEmailSender;
            string smtp = "localhost";
            bool success = (projDescr.status == ProjectDescription.STATUS_READY);
            string subject = (success)? "Results ready from STRT project " + projDescr.plateId :
                                        "Failure processing STRT project " + projDescr.plateId;
            StringBuilder sb = new StringBuilder();
            sb.Append("<html>");
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
                sb.Append("<p>The data analysis failed!</p>");
                foreach (string msg in results)
                    sb.Append(msg);
            }
            sb.Append("<p>Run parameters follow:</p>\n<code>");
            sb.Append("<br />\nBarcodeSet: " + projDescr.barcodeSet);
            sb.Append("<br />\nExtraction version: " + projDescr.extractionVersion);
            sb.Append("<br />\nAnnotation version: " + projDescr.annotationVersion + "<br />");
            foreach (ResultDescription rd in projDescr.resultDescriptions)
                sb.Append("<br />\nBowtie index: " + rd.bowtieIndexVersion + " - Results: " + rd.resultFolder);
            sb.Append("\n</code>\n</html>");
            string toEmails = projDescr.managerEmails.Replace(';', ','); // C# requires email addresses separated by ','
            MailMessage message = new MailMessage(from, toEmails, subject, sb.ToString());
            message.IsBodyHtml = true;
            SmtpClient mailClient = new SmtpClient(smtp, 25);
            mailClient.Send(message);
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
                    logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: " + 
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
