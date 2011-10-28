﻿using System;
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
        static void Main(string[] args)
        {
            int minutesWait = 5; // Time between scans to wait for new data to appear in queue.
            int maxExceptions = 20; // Max number of exceptions before giving up.
            string logFile = new FileInfo("PDBP_" + Process.GetCurrentProcess().Id + ".log").FullName;
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
                Console.WriteLine("Usage:  nohup mono ProjectDBProcessor.exe [options] &");
                Console.WriteLine("Options: -t N   scan for new data every N  minutes.");
                Console.WriteLine("         -e N   exit if more than N exceptions occured.");
                return;
            }
            if (!File.Exists(logFile))
            {
                File.Create(logFile).Close();
            }
            StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append));
            string now = DateTime.Now.ToString();
            logWriter.WriteLine("ProjectDBProcessor started at " + now +
                                " ScanInterval=" + minutesWait + " minutes. Max#Exceptions=" + maxExceptions);
            logWriter.Flush();
            Console.WriteLine("ProjectDBProcessor started " + now + " and logging to " + logFile);

            ProjectDB projectDB = new ProjectDB();
            Run(minutesWait, maxExceptions, logWriter, projectDB);
        }

        private static void Run(int minutesWait, int maxExceptions, StreamWriter logWriter, ProjectDB projectDB)
        { 
            int nExceptions = 0;
            while (nExceptions < maxExceptions)
            {
                try
                {
                    ScanDB(projectDB, logWriter);
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                catch (Exception exp)
                {
                    nExceptions++;
                    logWriter.WriteLine("*** ERROR: ProjectDBProcessor ***\n" + exp);
                    logWriter.Flush();
                }
            }
            logWriter.WriteLine("ProjectDBProcessor quit at " + DateTime.Now.ToString());
        }

        private static bool CheckAllReadsCollected(ref ProjectDescription projDescr)
        {
            List<int> runNos = new List<int>();
            foreach (string laneArg in projDescr.runIdsLanes)
            {
                string[] parts = laneArg.Split(':');
                int runNo = PathHandler.CheckReadsCollected(parts[0], parts[1]);
                if (runNo == -1) return false;
                runNos.Add(runNo);
            }
            projDescr.runNumbers = runNos.ToArray();
            return true;
        }


        private static void ScanDB(ProjectDB projectDB, StreamWriter logWriter)
        {
            ProjectDescription pd = projectDB.GetNextProjectInQueue();
            while (pd != null)
            {
                if (CheckAllReadsCollected(ref pd))
                {
                    pd.status = ProjectDescription.STATUS_PROCESSING;
                    projectDB.UpdateDB(pd);
                    List<string> results = new List<string>();
                    try
                    { 
                        ProcessItem(pd, logWriter);
                        results = PublishResultsForDownload(pd);
                        pd.status = ProjectDescription.STATUS_DONE;
                        projectDB.PublishResults(pd);
                    }
                    catch (Exception e)
                    {
                        pd.status = ProjectDescription.STATUS_FAILED;
                        pd.managerEmails = Props.props.FailureReportEmail;
                        logWriter.WriteLine("*** ERROR: ProjectDBProcessor processing " + pd.projectName + " ***\n" + e);
                        logWriter.Flush();
                        results.Add(e.ToString());
                    }
                    NotifyManager(pd, results);
                    projectDB.UpdateDB(pd);
                    logWriter.WriteLine(pd.projectName + "[analysisId=" + pd.analysisId + "] finished with status " + pd.status);
                    Console.WriteLine(pd.projectName + "[analysisId=" + pd.analysisId + "] finished with status " + pd.status);
                    logWriter.Flush();
                }
                pd = projectDB.GetNextProjectInQueue();
            }
        }

        private static void ProcessItem(ProjectDescription projDescr, StreamWriter logWriter)
        {
            logWriter.WriteLine("Processing " + projDescr.projectName + " - " + projDescr.LaneCount + " lanes [DBId=" + projDescr.analysisId + "]...");
            logWriter.Flush();
            DateTime d = DateTime.Now;
            if (projDescr.layoutFile != "")
            {
                string layoutSrcPath = Path.Combine(Props.props.UploadsFolder, projDescr.layoutFile);
                if (File.Exists(layoutSrcPath))
                {
                    string layoutDestPath = Path.Combine(projDescr.ProjectFolder, projDescr.layoutFile);
                    logWriter.WriteLine("cp " + layoutSrcPath + " -> " + layoutDestPath);
                    if (!Directory.Exists(Path.GetDirectoryName(layoutDestPath)))
                        Directory.CreateDirectory(Path.GetDirectoryName(layoutDestPath));
                    if (File.Exists(layoutDestPath))
                        File.Delete(layoutDestPath);
                    File.Copy(layoutSrcPath, layoutDestPath, true);
                }
                else
                    logWriter.WriteLine("*** WARNING: " + projDescr.projectName + " - layout file does not exist: " + layoutSrcPath);
            }
            Props.props.BarcodesName = projDescr.barcodeSet;
            StrtReadMapper mapper = new StrtReadMapper(Props.props);
            mapper.Process(projDescr);
            logWriter.WriteLine("..." + projDescr.projectName + " done after " + DateTime.Now.Subtract(d) + ".");
            logWriter.Flush();
        }

        private static void NotifyManager(ProjectDescription projDescr, List<string> results)
        {
            if (projDescr.managerEmails == "") return;
            string from = "peter.lonnerberg@ki.se";
            string smtp = "localhost";
            bool success = (projDescr.status == ProjectDescription.STATUS_DONE);
            string subject = (success)? "Results ready from STRT project " + projDescr.projectName :
                                        "Failure processing STRT project " + projDescr.projectName;
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
                string cmdArg = string.Format("-P 9952 {0} {1}", tempTarGzPath, Props.props.ResultDownloadUrl);
                Console.WriteLine("scp " + cmdArg);
                int cmdResult = CmdCaller.Run("scp", cmdArg);
                if (cmdResult == 0)
                {
                    string resultLink = Props.props.ResultDownloadFolderHttp + resultTarName;
                    resultLinks.Add(resultLink);
                    File.Delete(tempTarGzPath);
                }
                else
                    resultLinks.Add(tempTarGzPath);
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
