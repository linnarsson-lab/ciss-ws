﻿using System;
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
        private static string readsFolder;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            illuminaRunsFolder = Props.props.RunsFolder;
            readsFolder = Props.props.ReadsFolder;
            logFile = new FileInfo("BFQC_" + Process.GetCurrentProcess().Id + ".log").FullName;
            string specificRunFolder = null;
            bool forceOverwrite = false;

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
                        readsFolder = args[++i];
                    else if (arg.StartsWith("-o"))
                        readsFolder = arg.Substring(2);
                    else if (arg == "-t")
                        minutesWait = int.Parse(args[++i]);
                    else if (arg.StartsWith("-o"))
                        minutesWait = int.Parse(arg.Substring(2));
                    else if (arg == "--run")
                        specificRunFolder = args[++i];
                    else if (arg == "-f")
                        forceOverwrite = true;
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
                                  "-o <file>               - specify a non-standard reads output folder (default=" + readsFolder + ")\n" +
                                  "-l <file>               - specify a non-standard log file\n" +
                                  "-t <N>                  - specify a non-standard interval for scans in minutes (default=" + minutesWait + ")\n" +
                                  "--run <folder>[:lane]   - copy only specified [lane(s) of, as 'n', or 'n-n'] run folder and then quit\n" +
                                  "-f                      - used with --run forces overwrite also of existing fastq files\n" +
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
                logWriter.AutoFlush = true;
                string now = DateTime.Now.ToString();
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting BkgFastQCopier");
                Console.WriteLine("BkgFastQCopier started at " + now + " and logging to " + logFile);
                if (specificRunFolder != null)
                    specificRunFolder = CopySpecificRunFolder(specificRunFolder, forceOverwrite);
                else
                    KeepScanning();
                logWriter.WriteLine("BkgFastQCopier quit at " + DateTime.Now.ToPathSafeString());
            }
        }

        private static string CopySpecificRunFolder(string specificRunFolder, bool forceOverwrite)
        {
            int laneFrom = 1, laneTo = 8;
            string laneTxt = "";
            if (specificRunFolder.Contains(':'))
            {
                int colonIdx = specificRunFolder.IndexOf(':');
                string laneRange = specificRunFolder.Substring(colonIdx + 1);
                int hyphenIdx = laneRange.IndexOf('-');
                if (hyphenIdx == -1)
                    laneFrom = laneTo = int.Parse(laneRange);
                else if (hyphenIdx == 0)
                    laneTo = int.Parse(laneRange);
                else if (hyphenIdx == laneRange.Length - 1)
                    laneFrom = int.Parse(laneRange);
                else
                {
                    laneFrom = int.Parse(laneRange.Substring(0, hyphenIdx));
                    laneTo = int.Parse(laneRange.Substring(hyphenIdx + 1));
                }
                specificRunFolder = specificRunFolder.Substring(0, colonIdx);
                laneTxt = string.Format("lane {0}-{1} of ", laneFrom, laneTo);
            }
            Console.WriteLine("Copying data from " + laneTxt + specificRunFolder + " to " + readsFolder);
            ReadCopierStatus copyStatus;
            ReadCopier readCopier = new ReadCopier(logWriter);
            int nFilesCopied = readCopier.SerialCopy(specificRunFolder, readsFolder, laneFrom, laneTo, forceOverwrite, out copyStatus).Count;
            Console.WriteLine("Created totally " + nFilesCopied.ToString() + " output fq files.");
            return specificRunFolder;
        }

        private static void KeepScanning()
        {
            Console.WriteLine("Scans for new data every {0} minutes. Log output goes to {1}.", minutesWait, logFile);
            int nExceptions = 0;
            while (nExceptions < 5)
            {
                try
                {
                    Scan(illuminaRunsFolder, readsFolder);
                }
                catch (Exception exp)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Exception in BkgFastQCopier: ***\n" + exp);
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


        /// <summary>
        /// Copies all data in runsFolder for which fq files are missing into fq files in readsFolder.
        /// </summary>
        /// <param name="runsFolder"></param>
        /// <param name="readsFolder"></param>
        public static void Scan(string runsFolder, string readsFolder)
        {
            AssertOutputFolders(readsFolder);
            IDB projectDB = DBFactory.GetProjectDB();
            string[] runFolderNames = Directory.GetDirectories(runsFolder);
            foreach (string runFolder in runFolderNames)
            {
                int runNo;
                string runId, runDate;
                if (ReadCopier.ParseRunFolderName(runFolder, out runNo, out runId, out runDate))
                {
                    string readyFilePath = Path.Combine(runFolder, Props.props.IlluminaRunReadyFilename);
                    string callFolder = Path.Combine(runFolder, PathHandler.MakeRunDataSubPath());
                    bool readyFileExists = File.Exists(readyFilePath);
                    bool callFolderExists = Directory.Exists(callFolder);
                    if (readyFileExists && callFolderExists)
                    {
                        if (projectDB.SecureStartRunCopy(runId, runNo, runDate))
                        {
                            ReadCopier readCopier = new ReadCopier(logWriter);
                            ReadCopierStatus status;
                            if (Props.props.ParallellFastqCopy)
                                readCopier.ParallelCopy(runFolder, readsFolder, out status);
                            else
                                readCopier.SerialCopy(runFolder, readsFolder, 1, 8, false, out status);
                            string runStatus = (status == ReadCopierStatus.ALLREADSREADY) ? "copied"
                                : ((status == ReadCopierStatus.SOMEREADFAILED) ? "copyfail": "copying");
                            projectDB.UpdateRunStatus(runId, runStatus, runNo);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Make the read, non past filter, and statistics subfolders if they don't exist
        /// </summary>
        /// <param name="readsFolder"></param>
        private static void AssertOutputFolders(string readsFolder)
        {
            if (!File.Exists(readsFolder))
            {
                Directory.CreateDirectory(readsFolder);
                Directory.CreateDirectory(Path.Combine(readsFolder, PathHandler.nonPFReadsSubFolder));
                Directory.CreateDirectory(Path.Combine(readsFolder, PathHandler.readStatsSubFolder));
            }
        }

    }
}
