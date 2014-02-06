using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Mail;
using System.Diagnostics;
using System.Threading;
using Linnarsson.Utilities;
using Linnarsson.Dna;

namespace BkgFastQCopier
{
    public class Program
    {
        private static string logFile;
        private static bool keepRunning = true;

        static void Main(string[] args)
        {
            int minutesWait = 15; // Time between scans.
            string illuminaRunsFolder = Props.props.RunsFolder;
            string outputReadsFolder = Props.props.ReadsFolder;
            logFile = new FileInfo("BFQC_" + Process.GetCurrentProcess().Id + ".log").FullName;
            string specificRunFolder = null;

            try 
            {
                int i = 0;
                while (i < args.Length)
                {
                    string arg = args[i];
                    if (arg == "-i")
                        illuminaRunsFolder = args[++i];
                    else if (arg.StartsWith("-i"))
                        illuminaRunsFolder = arg.Substring(2);
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
                    else if (arg == "-o")
                        outputReadsFolder = args[++i];
                    else if (arg.StartsWith("-o"))
                        outputReadsFolder = arg.Substring(2);
                    else if (arg == "-t")
                        minutesWait = int.Parse(args[++i]);
                    else if (arg.StartsWith("-o"))
                        minutesWait = int.Parse(arg.Substring(2));
                    else if (arg == "--run")
                        specificRunFolder = args[++i];
                    else throw new ArgumentException();
                    i++;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("\nOptions:\n\n" +
                                  "-i <file>               - specify a non-standard Illumina runs folder\n" +
                                  "-o <file>               - specify a non-standard reads output folder\n" +
                                  "-l <file>               - specify a non-standard log file\n" +
                                  "-t <N>                  - specify a non-standard interval for scans in minutes\n" +
                                  "--run <folder>[:lane]   - copy only specified [lane of] run folder and then quit\n" +
                                  "Start using nohup to scan for new data every {0} minutes.\n", minutesWait);
                    return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find logfile {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            using (StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                string now = DateTime.Now.ToString();
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting BkgFastQCopier");
                logWriter.Flush();
                Console.WriteLine("BkgFastQCopier started at " + now + " and logging to " + logFile);
                ReadCopier readCopier = new ReadCopier(illuminaRunsFolder, outputReadsFolder, logWriter);
                if (specificRunFolder != null)
                {
                    int laneFrom = 1, laneTo = 8;
                    string laneTxt = "";
                    if (specificRunFolder.Contains(':'))
                    {
                        int colonIdx = specificRunFolder.IndexOf(':');
                        laneFrom = laneTo = int.Parse(specificRunFolder.Substring(colonIdx + 1));
                        specificRunFolder = specificRunFolder.Substring(0, colonIdx);
                        laneTxt = "lane " + laneFrom.ToString() + " of ";
                    }
                    Console.WriteLine("Copying data from " + laneTxt + specificRunFolder + " to " + outputReadsFolder);
                    int nFilesCopied = readCopier.SingleUseCopy(specificRunFolder, outputReadsFolder, laneFrom, laneTo);
                    Console.WriteLine("Created totally " + nFilesCopied.ToString() + " output fq files.");
                }
                else
                {
                    Console.WriteLine("Scans for new data every {0} minutes. Log output goes to {1}.", minutesWait, logFile);
                    KeepScanning(minutesWait, logWriter, readCopier);
                }
                logWriter.WriteLine("BkgFastQCopier quit at " + DateTime.Now.ToPathSafeString());
            }
        }

        private static void KeepScanning(int minutesWait, StreamWriter logWriter, ReadCopier readCopier)
        {
            int nExceptions = 0;
            while (nExceptions < 5 && Program.keepRunning)
            {
                try
                {
                    readCopier.Scan();
                }
                catch (Exception exp)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Exception in BkgFastQCopier: ***\n" + exp);
                    logWriter.Flush();
                }
                Thread.Sleep(1000 * 60 * minutesWait);
            }
            if (nExceptions > 0)
                ReportExceptionTermination(logFile);
        }

        private static void ReportExceptionTermination(string logFile)
        {
            string from = Props.props.ProjectDBProcessorNotifierEmailSender;
            string to = Props.props.FailureReportEmail;
            string smtp = "localhost";
            string subject = "BkgFastQCopier PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.";
            MailMessage message = new MailMessage(from, to, subject, body);
            message.IsBodyHtml = false;
            SmtpClient mailClient = new SmtpClient(smtp, 25);
            mailClient.Send(message);
        }



    }
}
