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
    class Program
    {
        static int minutesWait = 10;
        static string backupDest = "hiseq@130.237.142.75:/mnt/davidson/hiseq/data_reads/";
        static int startHour = 17;
        static int stopHour = 8;
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
                Console.WriteLine("\nOptions:\n\n" +
                                  "-i<file>     - specify a non-standard reads folder\n" +
                                  "-l<file>     - specify a non-standard log file\n" +
                                  "-o<scp_dest> - specify a non-standard destination\n" +
                                  "--on <int>   - specify a non-standard start hour (default=" + startHour + ")\n" +
                                  "--off <int>  - specify a non-standard stop hour (default=" + stopHour + ")\n" +
                                  "Put in crontab for starting at every reboot.\n" +
                                  "Destination defaults to " + backupDest +
                                  "\nLogfile defaults to a name with Pid included like: " + logFile);
                return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append));
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
            logWriter.Close();
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
                            DateTime startTime = DateTime.Now;
                            new ProjectDB().SetBackupStatus(readFile, "copying");
                            string cmdArg = string.Format("{0} {1}", readFile, backupDest);
                            logWriter.WriteLine(DateTime.Now.ToString() + " scp " + cmdArg);
                            logWriter.Flush();
                            int cmdResult = CmdCaller.Run("scp", cmdArg);
                            if (cmdResult == 0)
                            {
                                new ProjectDB().SetBackupStatus(readFile, "copied");
                                TimeSpan timeTaken = DateTime.Now.Subtract(startTime);
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
