using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Globalization;
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
        private static IDB db;
        private static Dictionary<string, string> lastMsgByProject = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            //Console.WriteLine("Testing decimal point: " + (1.0d / 4.0d).ToString());
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

                db = DBFactory.GetProjectDB();
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
        /// <param name="pd"></param>
        /// <returns>false if some fq file is missing, or there was an error</returns>
        private static bool CheckAllReadsCollected(ref ProjectDescription pd)
        {
            try
            {
                char reqReadNo = Barcodes.GetBarcodes(pd.barcodeset).HighestNeededReadNo;
                foreach (string laneArg in pd.laneArgs)
                {
                    string[] parts = laneArg.Split(':');
                    int runNo = PathHandler.CheckReadsCollected(parts[0], parts[1], reqReadNo);
                    if (runNo == -1) return false;
                }
            }
            catch (Exception e)
            {
                List<string> messages = new List<string>();
                if (HandleError(pd, messages , e, true))
                    NotifyManager(pd, messages);
                return false;
            }
            return true;
        }

        private static void ScanDB()
        {
            db.ResetQueue();
            ProjectDescription pd = db.GetNextProjectInQueue();
            while (pd != null)
            {
                if (CheckAllReadsCollected(ref pd))
                {
                    if (HandleDBTask(pd))
                        db.ResetQueue();
                }
                pd = db.GetNextProjectInQueue();
            }
        }

        /// <summary>
        /// Will return false if an error occured that may be solved by trying again later, in which
        /// case status is reset to 'inqueue'
        /// </summary>
        /// <param name="pd"></param>
        /// <returns></returns>
        private static bool HandleDBTask(ProjectDescription pd)
        {
            bool success = false;
            bool notifyManager = true;
            if (! db.SecureStartAnalysis(pd)) return false;
            logWriter.WriteLine(DateTime.Now.ToString() + " Processing " + pd.analysisname + " - " + pd.LaneCount + " lanes [DBId=" + pd.dbanalysisid + "]...");
            logWriter.Flush();
            List<string> messages = new List<string>();
            try
            {
                if (pd.layoutfile != "")
                    CopyLayoutFile(pd);
                if (pd.aligner != "")
                    Props.props.Aligner = pd.aligner;
                ProcessSteps(pd);
                messages = PublishResultsForDownload(pd);
                pd.status = ProjectDescription.STATUS_READY;
                db.PublishResults(pd);
                success = true;
                lastMsgByProject.Remove(pd.analysisname);
            }
            catch (BarcodeFileException e)
            {
                notifyManager = HandleError(pd, messages, e, false);
            }
            catch (SampleLayoutFileException e)
            {
                notifyManager = HandleError(pd, messages, e, false);
            }
            catch (Exception e)
            {
                bool recoverable = e.Message.Contains("Sharing violation");
                notifyManager = HandleError(pd, messages, e, recoverable);
            }
            if (notifyManager)
                NotifyManager(pd, messages);
            db.UpdateAnalysisStatus(pd.dbanalysisid, pd.status);
            logWriter.WriteLine(DateTime.Now.ToString() + " " + pd.analysisname + "[DBId=" + pd.dbanalysisid + "] finished with status " + pd.status);
            logWriter.Flush();
            return success;
        }

        private static bool HandleError(ProjectDescription pd, List<string> messages, Exception e, bool recoverable)
        {
            pd.emails += ";" + Props.props.FailureReportAndAnonDownloadEmail;
            logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: ProjectDBProcessor processing " + pd.analysisname + " ***\n" + e);
            logWriter.Flush();
            Console.WriteLine("\n===============" + pd.analysisname + " finished with errors: ==============\n" + e + 
                            "\n=============== check details in " + logFile  + " ================\n");
            string errorMsg = e.Message;
            pd.status = recoverable ? ProjectDescription.STATUS_INQUEUE : ProjectDescription.STATUS_FAILED;
            if (lastMsgByProject.ContainsKey(pd.analysisname) && lastMsgByProject[pd.analysisname].Equals(errorMsg))
                return false;
            messages.Add(errorMsg);
            lastMsgByProject[pd.analysisname] = errorMsg;
            return true;
        }

        private static void CopyLayoutFile(ProjectDescription pd)
        {
            string layoutSrcPath = Path.Combine(Props.props.UploadsFolder, pd.layoutfile);
            if (File.Exists(layoutSrcPath))
            {
                string layoutDestPath = pd.layoutpath;
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
                logWriter.WriteLine(DateTime.Now.ToString() + " *** WARNING: " + pd.analysisname + " - layout does not exist: " + layoutSrcPath);
                logWriter.Flush();
            }
        }

        public static void ProcessSteps(ProjectDescription pd)
        {
            StrtReadMapper mapper = new StrtReadMapper();
            Console.WriteLine("StrtReadMapper.Process(" + pd.analysisname + ")");
            Props.props.BarcodesName = pd.barcodeset;
            Props.props.TotalNumberOfAddedSpikeMolecules = pd.spikemolecules;
            if (!pd.analysisname.StartsWith("C1-"))
                Props.props.InsertCellDBData = false;
            logWriter.WriteLine("{0} Extracting {1} lanes with barcodes {2}...", DateTime.Now, pd.laneArgs.Length, pd.barcodeset);
            logWriter.Flush();
            pd.extractionVersion = StrtReadMapper.EXTRACTION_VERSION;
            pd.laneInfos = mapper.Extract(pd.ProjectFolder, pd.laneArgs.ToList(), null);
            Props.props.UseRPKM = pd.UseRPKM;
            Props.props.DirectionalReads = pd.DirectionalReads;
            Props.props.SenseStrandIsSequenced = pd.SenseStrandIsSequenced;
            pd.annotationVersion = StrtReadMapper.ANNOTATION_VERSION;
            string[] speciesArgs = Props.props.Barcodes.ParsePlateLayout(pd.plateid, pd.layoutpath);
            if (speciesArgs.Length == 0) speciesArgs = new string[] { pd.defaultSpecies };
            foreach (string speciesArg in speciesArgs)
            {
                StrtGenome genome = StrtGenome.GetGenome(speciesArg, pd.defaultVariants, pd.defaultBuild, true);
                pd.genome = genome;
                logWriter.WriteLine("{0} Aligning to {1}...", DateTime.Now, genome.BuildVarAnnot);
                logWriter.Flush();
                pd.status = ProjectDescription.STATUS_ALIGNING;
                db.UpdateAnalysisStatus(pd.dbanalysisid, pd.status);
                mapper.CreateAlignments(pd.genome, pd.laneInfos, null);
                List<string> mapFiles = LaneInfo.RetrieveAllMapFilePaths(pd.laneInfos);
                logWriter.WriteLine("{0} Annotating {1} alignment files...", DateTime.Now, mapFiles.Count);
                logWriter.WriteLine("{0} setting: TrVariants={1} Gene5'Extensions={4} #SpikeMols={5} DirectionalReads={2} RPKM={3}",
                                    DateTime.Now, pd.variant, pd.DirectionalReads, pd.UseRPKM,
                                    Props.props.GeneFeature5PrimeExtension, pd.spikemolecules);
                logWriter.Flush();
                pd.status = ProjectDescription.STATUS_ANNOTATING;
                db.UpdateAnalysisStatus(pd.dbanalysisid, pd.status);
                ResultDescription rd = mapper.ProcessAnnotation(pd.genome, pd.ProjectFolder, pd.analysisname, null, null, mapFiles);
                pd.AddResultDescription(rd);
                System.Xml.Serialization.XmlSerializer x = new System.Xml.Serialization.XmlSerializer(pd.GetType());
                using (StreamWriter writer = new StreamWriter(Path.Combine(rd.resultFolder, "ProjectConfig.xml")))
                    x.Serialize(writer, pd);
                logWriter.WriteLine("{0} Results stored in {1}.", DateTime.Now, rd.resultFolder);
                logWriter.Flush();
            }
        }


        private static void NotifyManager(ProjectDescription pd, List<string> results)
        {
            if (pd.emails == "") return;
            bool success = (pd.status == ProjectDescription.STATUS_READY);
            string subject = (success)? "Results ready from STRT project " + pd.analysisname :
                                        "Failure processing STRT project " + pd.analysisname;
            StringBuilder sb = new StringBuilder();
            sb.Append("<html>");
            string toEmails = pd.emails;
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
            sb.Append("<br />\nBarcodeSet: " + pd.barcodeset);
            sb.Append("<br />\nExtraction version: " + pd.extractionVersion);
            sb.Append("<br />\nAnnotation version: " + pd.annotationVersion + "<br />");
            foreach (ResultDescription rd in pd.ResultDescriptions)
                sb.Append("<br />\nAligner index: " + rd.splcIndexVersion + " - Results: " + rd.resultFolder);
            sb.Append("\n</code>\n</html>");
            toEmails = toEmails.Replace(';', ','); // C# requires email addresses separated by ','
            string body = sb.ToString();
            EmailSender.SendMsg(toEmails, subject, body, true);
        }

        private static List<string> PublishResultsForDownload(ProjectDescription pd)
        {
            List<string> resultLinks = new List<string>();
            foreach (ResultDescription rd in pd.ResultDescriptions)
            {
                string docFileDest = Path.Combine(rd.resultFolder, Path.GetFileName(Props.props.OutputDocFile));
                if (File.Exists(Props.props.OutputDocFile))
                {
                    if (File.Exists(docFileDest))
                        File.Delete(docFileDest);
                    File.Copy(Props.props.OutputDocFile, docFileDest);
                }
                string resultTarName = Path.GetFileName(rd.resultFolder) + ".tar.gz";
                string tempTarGzPath = Path.Combine(Path.GetTempPath(), resultTarName);
                CompressResult(rd.resultFolder, tempTarGzPath);
            string cpCmd, cmdArg;
            if (Props.props.ResultUrlIsMounted)
            {
                cpCmd = "cp";
                string destPath = Path.Combine(Props.props.ResultDownloadUrl, resultTarName);
                cmdArg = tempTarGzPath + " " + destPath;
            }
            else
            {
                cpCmd = "scp";
                cmdArg = string.Format("-P {0} {1} {2}", Props.props.ResultDownloadScpPort, tempTarGzPath, Props.props.ResultDownloadUrl);
            }
                Console.WriteLine(cpCmd + " " + cmdArg);
                int cmdResult = CmdCaller.Run(cpCmd, cmdArg);
                if (cmdResult == 0)
                {
                    string resultLink = Props.props.ResultDownloadFolderHttp + resultTarName;
                    resultLinks.Add(resultLink);
                    File.Delete(tempTarGzPath);
                }
                else
                {
                    resultLinks.Add(Path.GetFileName(rd.resultFolder) + " could not be published on HTTP server - contact administrator!");
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: " + 
                                         Path.GetFileName(rd.resultFolder) + " could not be published on HTTP server:\n" + 
                                         cpCmd + " " + cmdArg);
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
