using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Linnarsson.Strt;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace BkgBackuper
{
    /// <summary>
    /// Handles the copying of fastq read files to a backup server.
    /// It works as a nohup service, that at regular intervals scans the backup table in the database for new tasks,
    /// and copies them using scp.
    /// The copying can be limited to off-office hours to reduce bandwidth consumption.
    /// </summary>
    class Program
    {
        static int minutesWait = 10;
        static string backupDest = Props.props.BackupDestinationFolder;
        static int startHour;
        static int stopHour;
        static int nExceptions = 0;
        static string readsFolder = Props.props.ReadsFolder;
        static double currentBytesPerHour = 7.0E+9;

        static void Main(string[] args)
        {
            startHour = Props.props.BkgBackuperStartHour;
            stopHour = Props.props.BkgBackuperStopHour;
            string logFile = new FileInfo("BBUP_" + Process.GetCurrentProcess().Id + ".log").FullName;
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
                    else if (arg == "-o")
                        backupDest = args[++i];
                    else if (arg.StartsWith("-o"))
                        backupDest = arg.Substring(2);
                    else if (arg == "--on")
                        startHour = int.Parse(args[++i]);
                    else if (arg == "--off")
                        stopHour = int.Parse(args[++i]);
                    else throw new ArgumentException("OptionError");
                    i++;
                }
                if (startHour <= stopHour)
                    throw new ArgumentException("Start hour has to be after stop hour!");
            }
            catch (Exception e)
            {
                if (!e.Message.Equals("OptionError"))
                    Console.WriteLine(e);
                Console.WriteLine("\nThis program regularly (every " + minutesWait + " minutes) scans the database backupqueue " +
                                  "table for new reads files to be copied to the backup server. Copying is performed " +
                                  "using scp only at night between a specified start and stop hour to avoid clogging networks. " +
                                  "Files are read from the directory defined by ReadsFolder in SilverBullet config file and copied to " + 
                                  "the remote address (user@server:/directory) given by property BackupDestinationFolder." +
                                  "Correct function requires the automatic ssh-login has been setup. " +
                                  "\nOptions:\n\n" +
                                  "-i<file>     - specify a non-standard reads folder (default=" + readsFolder + ")\n" +
                                  "-l<file>     - specify a non-standard log file\n" +
                                  "-o<scp_dest> - specify a non-standard destination\n" +
                                  "--on <int>   - specify a non-standard start hour (24-h base, default=" + startHour + ")\n" +
                                  "--off <int>  - specify a non-standard stop hour (24-h base, less than start hour, default=" + stopHour + ")\n" +
                                  "Should be started as nohup and put in crontab for starting at every reboot.\n" +
                                  "Destination defaults to " + backupDest +
                                  "\nLogfile defaults to a name with Pid included like: " + logFile);
                return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            using (StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append)))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " Starting BkgBackuper");
                logWriter.Flush();
                Console.WriteLine("BkgBackuper started at " + DateTime.Now.ToString() + " and logging to " + logFile);

                while (nExceptions < 30)
                {
                    bool canCopy = true;
                    while (TimeOfDayOK() && canCopy)
                    {
                        canCopy = TryCopy(logWriter);
                    }
                    Thread.Sleep(1000 * 60 * minutesWait);
                }
                logWriter.WriteLine(DateTime.Now.ToString() + "BkgBackuper quit");
                if (nExceptions > 0)
                    ReportExceptionTermination(logFile);
            }
        }

        private static void ReportExceptionTermination(string logFile)
        {
            string subject = "BkgBackuper PID=" + Process.GetCurrentProcess().Id + " quit with exceptions.";
            string body = "Please consult logfile " + logFile + " for more info on the errors.";
            EmailSender.ReportFailureToAdmin(subject, body, true);
        }

        private static bool TimeOfDayOK()
        {
            DateTime now = DateTime.Now;
            return (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday ||
                       now.Hour >= startHour || now.Hour < stopHour);
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool triedSomeCopy = false;
            List<string> readFiles = new ProjectDB().GetWaitingFilesToBackup();
            if (readFiles.Count > 0)
            {
                foreach (string readFile in readFiles)
                {
                    if (!File.Exists(readFile))
                    {
                        new ProjectDB().SetBackupStatus(readFile, "missing");
                        continue;
                    }
                    int hoursLeft = GetHoursLeft();
                    long maxLenLeft =  (long)Math.Floor(currentBytesPerHour * hoursLeft);
                    int maxMbLeft = (int)Math.Floor(maxLenLeft / Math.Pow(2.0, 20));
                    long fileLen = new FileInfo(readFile).Length;
                    int fileMb = (int)Math.Floor(fileLen / Math.Pow(2.0, 20));
                    if (fileLen < maxLenLeft)
                    {
                        triedSomeCopy = true;
                        try
                        {
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            new ProjectDB().SetBackupStatus(readFile, "copying");
                            string cmdArg = string.Format("{0} {1}", readFile, backupDest);
                            logWriter.WriteLine(DateTime.Now.ToString() + " scp " + cmdArg);
                            logWriter.Flush();
                            CmdCaller cmd = new CmdCaller("scp", cmdArg);
                            if (cmd.ExitCode == 0)
                            {
                                new ProjectDB().SetBackupStatus(readFile, "copied");
                                sw.Stop();
                                TimeSpan timeTaken = sw.Elapsed;
                                if (fileLen > 100000)
                                {
                                    currentBytesPerHour = fileLen / timeTaken.TotalHours;
                                    logWriter.WriteLine(DateTime.Now.ToString() + " ...speed: " + currentBytesPerHour / Math.Pow(2.0, 30) + " Gbytes/hour");
                                }
                                else
                                {
                                    logWriter.WriteLine(DateTime.Now.ToString() + " ...file was empty.");
                                }
                                logWriter.Flush();
                            }
                            else
                            {
                                new ProjectDB().SetBackupStatus(readFile, "failed");
                                logWriter.WriteLine("{0} ERROR: scp {1} failed - Exit code: {2} {3}", 
                                                    DateTime.Now.ToString(), cmdArg, cmd.ExitCode, cmd.StdError);
                                logWriter.Flush();
                            }
                        }
                        catch (Exception exp)
                        {
                            new ProjectDB().SetBackupStatus(readFile, "inqueue");
                            logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Exception in BkgBackuper: ***\n" + exp);
                            logWriter.Flush();
                            nExceptions++;
                        }
                    }
                }
            }
            return triedSomeCopy;
        }

        public static int GetHoursLeft()
        {
            int hoursLeft = 0;
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday) hoursLeft = 24;
            else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Hour >= startHour) hoursLeft = 48;
            if (DateTime.Now.Hour < stopHour)
                hoursLeft += stopHour - DateTime.Now.Hour;
            else if (DateTime.Now.Hour >= startHour)
                hoursLeft += stopHour + (24 - DateTime.Now.Hour);
            return hoursLeft;
        }
    }
}
