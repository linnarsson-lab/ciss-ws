using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Linnarsson.Utilities;
using Linnarsson.Dna;

namespace BkgFastQCopier
{
    public class Program
    {
        private static bool keepRunning = true;

        static void Main(string[] args)
        {
            int minutesWait = 15; // Time between scans.
            string illuminaRunsFolder = Props.props.RunsFolder;
            string outputReadsFolder = Props.props.ReadsFolder;
            string logFile = new FileInfo("BFQC_" + Process.GetCurrentProcess().Id + ".log").FullName;

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
                    else throw new ArgumentException();
                }
            }
            catch (Exception)
            {
                    Console.WriteLine("\nOptions:\n\n" +
                                      "-i<file>    - specify a non-standard Illumina runs folder" +
                                      "-o<file>    - specify a non-standard reads output folder" +
                                      "-l<file>    - specify a non-standard log file\n\n" +
                                      "-t<N>       - specify a non-standard interval for scans in minutes\n\n" +
                                      "Start using nohup to scan forever every {0} minutes.\n" +
                                      "Log output goes to {1}.", minutesWait, logFile);
                    return;
            }
            if (!File.Exists(logFile))
            {
                Console.WriteLine("Can not find logfile {0}. Creating it.", logFile);
                File.Create(logFile).Close();
            }
            StreamWriter logWriter = new StreamWriter(File.Open(logFile, FileMode.Append));
            string now = DateTime.Now.ToString();
            logWriter.WriteLine("Starting BkgFastQCopier at " + now);
            logWriter.Flush();
            Console.WriteLine("BkgFastQCopier started at " + now + " and logging to " + logFile);

            ReadCopier readCopier = new ReadCopier(illuminaRunsFolder, outputReadsFolder, logWriter);
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
                    logWriter.WriteLine("*** ERROR: Exception in BkgFastQCopier: ***\n" + exp);
                    logWriter.Flush();
                }
                Thread.Sleep(1000 * 60 * minutesWait);
            }
            logWriter.WriteLine("BkgFastQCopier quit at " + DateTime.Now.ToPathSafeString());
            logWriter.Close();
        }

    }
}
