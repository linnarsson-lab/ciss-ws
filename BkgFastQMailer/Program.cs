using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Net.Mail;
using Linnarsson.Strt;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace BkgFastQMailer
{
    public class Program
    {
        private static bool keepRunning = true;

        static void Main(string[] args)
        {
            int minutesWait = 15; // Time between scans.
            string readsFolder = Props.props.ReadsFolder;
            string logFile = new FileInfo("BFQM_" + Process.GetCurrentProcess().Id + ".log").FullName;

            try
            {
                int i = 0;
                while (i < args.Length)
                {
                    string arg = args[i];
                    if (arg == "-i")
                        readsFolder = args[++i];
                    else if (arg.StartsWith("-i"))
                        readsFolder = arg.Substring(2);
                    else if (arg == "-l")
                        logFile = args[++i];
                    else if (arg.StartsWith("-l"))
                        logFile = arg.Substring(2);
                    else if (arg == "-t")
                        minutesWait = int.Parse(args[++i]);
                    else if (arg.StartsWith("-o"))
                        minutesWait = int.Parse(arg.Substring(2));
                    else throw new ArgumentException();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("\nOptions:\n\n" +
                                  "-i<file>    - specify a non-standard reads folder" +
                                  "-l<file>    - specify a non-standard log file\n\n" +
                                  "-t<N>       - specify a non-standard interval for scans in minutes\n\n" +
                                  "Start using nohup to scan forever every {0} minutes.\n" +
                                  "Log output goes to {1}.", minutesWait, logFile);
                return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            using (StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                string now = DateTime.Now.ToString();
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting BkgFastQMailer");
                logWriter.Flush();
                Console.WriteLine("BkgFastQMailer started at " + now + " and logging to " + logFile);

                ReadMailer readMailer = new ReadMailer(readsFolder, logWriter);
                int nExceptions = 0;
                while (nExceptions < 5 && Program.keepRunning)
                {
                    try
                    {
                        readMailer.Scan();
                    }
                    catch (Exception exp)
                    {
                        nExceptions++;
                        logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Exception in BkgFastQMailer: ***\n" + exp);
                        logWriter.Flush();
                    }
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                logWriter.WriteLine(DateTime.Now.ToString() + " BkgFastQMailer quit");
                if (nExceptions > 0)
                    ReportExceptionTermination(logFile);
            }
        }

        private static void ReportExceptionTermination(string logFile)
        {
            string from = Props.props.ProjectDBProcessorNotifierEmailSender;
            string to = Props.props.FailureReportEmail;
            string smtp = "localhost";
            string subject = "BkgFastQMailer PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.";
            MailMessage message = new MailMessage(from, to, subject, body);
            message.IsBodyHtml = false;
            SmtpClient mailClient = new SmtpClient(smtp, 25);
            mailClient.Send(message);
        }


    }
}
