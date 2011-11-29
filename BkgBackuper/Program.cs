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
        static int minutesWait = 15;
        static string backupDest = "hiseq@130.237.142.75:/mnt/davidson/hiseq/data_reads/";
        static int startHour = 19;
        static int stopHour = 7;
        static string readsFolder = Props.props.ReadsFolder;
        static double currentBytesPerHour = 10.0E+9;

        static void Main(string[] args)
        {
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
                                  "Put in crontab for starting every evening on weekdays.\n" +
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
            logWriter.WriteLine("Starting BkgBackuper at " + DateTime.Now.ToString());
            logWriter.Flush();
            Console.WriteLine("BkgBackuper started at " + DateTime.Now.ToString() + " and logging to " + logFile);

            DateTime now = DateTime.Now;
            while (now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday ||
                   now.Hour > startHour || now.Hour < stopHour)
            {
                bool didSomeCopy = TryCopy(logWriter);
                if (!didSomeCopy)
                    Thread.Sleep(1000 * 60 * minutesWait);
                now = DateTime.Now;
            }
            logWriter.WriteLine("BkgBackuper quit at " + DateTime.Now.ToString());
            logWriter.Close();
        }

        private static bool TryCopy(StreamWriter logWriter)
        {
            bool triedSomeCopy = false;
            List<string> readFiles = new ProjectDB().GetWaitingFilesToBackup();
            if (readFiles.Count > 0)
            {
                foreach (string readFile in readFiles)
                {
                    long fileLen = new FileInfo(readFile).Length;
                    long maxLenLeft = GetMaxBytesLeft();
                    Console.WriteLine("Testing " + readFile + " size=" + fileLen + " against maxSize=" + maxLenLeft);
                    if (fileLen < maxLenLeft)
                    {
                        triedSomeCopy = true;
                        try
                        {
                            DateTime startTime = DateTime.Now;
                            new ProjectDB().SetBackupStatus(readFile, "copying");
                            string cmdArg = string.Format("{0} {1}", readFile, backupDest);
                            logWriter.WriteLine(DateTime.Now.ToString() + ": scp " + cmdArg);
                            logWriter.Flush();
                            int cmdResult = CmdCaller.Run("scp", cmdArg);
                            if (cmdResult == 0)
                            {
                                new ProjectDB().RemoveFileToBackup(readFile);
                                TimeSpan timeTaken = DateTime.Now.Subtract(startTime);
                                currentBytesPerHour = fileLen / timeTaken.TotalHours;
                                logWriter.WriteLine("...speed: " + currentBytesPerHour / Math.Pow(2.0, 20) + " Gbytes/hour");
                                logWriter.Flush();
                            }
                        }
                        catch (Exception exp)
                        {
                            new ProjectDB().SetBackupStatus(readFile, "inqueue");
                            logWriter.WriteLine("*** ERROR: Exception in BkgBackuper: ***\n" + exp);
                            logWriter.Flush();
                        }
                    }
                }
            }
            return triedSomeCopy;
        }

        public static long GetMaxBytesLeft()
        {
            double hoursLeft = 0.0;
            if (DateTime.Now.DayOfWeek == DayOfWeek.Saturday) hoursLeft = 24.0;
            else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Hour > 16) hoursLeft = 48.0;
            if (DateTime.Now.Hour < stopHour) hoursLeft += DateTime.Now.Hour;
            else hoursLeft += stopHour + (24 - DateTime.Now.Hour);
            return (long)Math.Round(currentBytesPerHour * hoursLeft);
        }
    }
}
