using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using Linnarsson.Strt;

namespace BkgFastQCopier
{
    public class Program
    {
        private static string logFile;
        private static StreamWriter logWriter;
        private static int minutesWait = 15;
        private static string illuminaRunsFolder;
        private static string outputReadsFolder;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            illuminaRunsFolder = Props.props.RunsFolder;
            outputReadsFolder = Props.props.ReadsFolder;
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
                Console.WriteLine("\nThis program regularly scans for new data in the Illumina output folder, " +
                                  "defined by property RunsFolder in SilverBullet config file, and extracts the reads from " +
                                  ".bcl/.qseq files into per-lane .fq files in the directory given by propery ReadsFolder. " +
                                  "Checks that an Illumina run is finished by looking for the file defined by property IlluminaRunReadyFilename (" +
                                  Props.props.IlluminaRunReadyFilename + ") in each run folder, and copies reads when either the output (past filter) .fq " +
                                  "or the output statistics file is missing. To re-extract data, either delete the statistics file, or use option --run. " +
                                  "\nOptions:\n\n" +
                                  "-i <file>               - specify a non-standard Illumina runs folder (default=" + illuminaRunsFolder + ")\n" +
                                  "-o <file>               - specify a non-standard reads output folder (default=" + outputReadsFolder + ")\n" +
                                  "-l <file>               - specify a non-standard log file\n" +
                                  "-t <N>                  - specify a non-standard interval for scans in minutes (default=" + minutesWait + ")\n" +
                                  "--run <folder>[:lane]   - copy only specified [lane of] run folder and then quit\n" +
                                  "Start using nohup and put in crontab for activation at each reboot.");
                    return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find logfile {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            using (logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                string now = DateTime.Now.ToString();
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting BkgFastQCopier");
                logWriter.Flush();
                Console.WriteLine("BkgFastQCopier started at " + now + " and logging to " + logFile);
                ReadCopier readCopier = new ReadCopier(logWriter);
                if (specificRunFolder != null)
                    specificRunFolder = CopyOneRun(specificRunFolder, readCopier);
                else
                    KeepScanning(readCopier);
                logWriter.WriteLine("BkgFastQCopier quit at " + DateTime.Now.ToPathSafeString());
            }
        }

        private static string CopyOneRun(string specificRunFolder, ReadCopier readCopier)
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
            int nFilesCopied = readCopier.SingleUseCopy(specificRunFolder, outputReadsFolder, laneFrom, laneTo).Count;
            Console.WriteLine("Created totally " + nFilesCopied.ToString() + " output fq files."); return specificRunFolder;
        }

        private static void KeepScanning(ReadCopier readCopier)
        {
            Console.WriteLine("Scans for new data every {0} minutes. Log output goes to {1}.", minutesWait, logFile);
            int nExceptions = 0;
            while (nExceptions < 5)
            {
                try
                {
                    readCopier.Scan(illuminaRunsFolder, outputReadsFolder);
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
            string subject = "BkgFastQCopier PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.";
            EmailSender.ReportFailureToAdmin(subject, body, false);
        }



    }
}
