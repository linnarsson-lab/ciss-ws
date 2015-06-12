using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Configuration;
using System.Diagnostics;
using System.Xml.Serialization;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace ESCAF_BclToFq
{
    [Serializable]
    public sealed class ESCAFProps
    {
        [NonSerialized]
        public static string configFilename = "ESCAF_BclToFq.xml";

        public string LogFile = "ESCAF_BclToFq.log"; // Log output file
        public int scanInterval = 5; // Minutes between scans for new data
        public string RunsFolder = "/home/data/runs"; // Where Illumina run folders (or tarballs) are deposited
        public string RunFolderMatchPattern = "[0-9][0-9][0-9][0-9][0-9][0-9]_.+XX"; // Pattern that HiSeq run folders in RunsFolder should match
        public int minLastAccessMinutes = 10; // Min time in minutes since last access of a run folder before starting to process
        public string ReadsFolder = "/home/data/reads"; // Where .fq files for each lane are put
        public string[] scpDestinations = new string[] 
        { "sten@milou.uppnex.uu.se:reads", "hiseq@130.237.117.141:/data/reads" };
        // scp destinations of resulting .fq files. The directory structure will be PF in top folder, and nonPF/ and statistics/ subfolders
        public bool clearData = true; // Remove all run data and local .fq files after successful processing and scp:ing
        public bool multiThreaded = false; // Use parallell threads for quick processing

        private static ESCAFProps Read()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string configFilePath = Path.Combine(appDir, configFilename);
            ESCAFProps props = null;
            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(ESCAFProps));
                System.IO.StreamReader sr = new System.IO.StreamReader(configFilename);
                props = (ESCAFProps)xs.Deserialize(sr);
                sr.Close();
            }
            catch (FileNotFoundException)
            {
                props = new ESCAFProps();
                XmlSerializer xs = new XmlSerializer(props.GetType());
                System.IO.StreamWriter sw = new System.IO.StreamWriter(configFilename);
                xs.Serialize(sw, props);
                sw.Close();
            }
            return props;
        }

        ESCAFProps() { }
    
        public static ESCAFProps props { get { return PropsHolder.instance; } }

        class PropsHolder
        {
            static PropsHolder() { }
            internal static readonly ESCAFProps instance = Read();
        }

    }

    public class Program
    {
        private static StreamWriter logWriter;

        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
                Console.WriteLine("This program scans for Illumina output folders (or tar balls) in Config.RunsFolder (" + 
                                     ESCAFProps.props.RunsFolder + "), and\n" +
                                  "extracts the reads from .bcl files into per-lane/read .fq files in the directory given by Config.ReadsFolder. (" +
                                     ESCAFProps.props.ReadsFolder + ")\n" +
                                  "On success, the .fq files are copied using scp into Config.scpDestinations, and if Config.clearData (" + 
                                     ESCAFProps.props.clearData + ") is true,\n" +
                                  "the intermediate files in ReadsFolder are deleted.\n" +
                                  "Setup configuration in " + ESCAFProps.configFilename + ".\n" +
                                  "Start using nohup and put in crontab for activation at each reboot.");
            if (!File.Exists(ESCAFProps.props.LogFile))
            {
                Console.WriteLine("Can not find logfile {0}. Creating it.", ESCAFProps.props.LogFile);
                File.Create(ESCAFProps.props.LogFile).Close();
            }
            using (logWriter = new StreamWriter(File.Open(ESCAFProps.props.LogFile, FileMode.Append)))
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Started");
                logWriter.Flush();
                Scan();
                logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Quit");
            }
        }

        private static void Scan()
        {
            List<string> copiedRunDirs = new List<string>();
            int nExceptions = 0;
            while (nExceptions < 5)
            {
                try
                {
                    string[] runDirs = Directory.GetDirectories(ESCAFProps.props.RunsFolder);
                    //Console.WriteLine("#runDirs={0}", runDirs.Length);
                    foreach (string runDir in runDirs)
                    {
                        if (copiedRunDirs.Contains(runDir) ||  !Regex.IsMatch(runDir, ESCAFProps.props.RunFolderMatchPattern))
                            continue;
                        TimeSpan ts = DateTime.Now - Directory.GetLastAccessTime(runDir);
                        Console.WriteLine("{0}: TimeSinceAccess={1}", runDir, ts);
                        if (ts >= new TimeSpan(0, ESCAFProps.props.minLastAccessMinutes, 0))
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Processing " + runDir);
                            logWriter.Flush();
                            ProcessRun(runDir);
                            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Ready");
                            logWriter.Flush();
                            copiedRunDirs.Add(runDir);
                        }
                    }
                }
                catch (Exception e)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Unhandled exception in ESCAF_BclToFq:\n" + e);
                    logWriter.Flush();
                }
                Thread.Sleep(1000 * 60 * ESCAFProps.props.scanInterval);
            }
        }

        private static void ProcessRun(string run)
        {
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            string runFolder = run;
            CmdCaller c;
            try
            {
                Match m = Regex.Match(run, "(.+)(\\.tar\\.gz|\\.tgz)$");
                if (m.Success)
                {
                    runFolder = m.Groups[1].Value;
                    c = new CmdCaller("tar", "zxf " + run, true);
                    if (c.ExitCode != 0) throw new Exception(c.StdError);
                }
                ReadCopier readCopier = new ReadCopier(logWriter);
                if (!ESCAFProps.props.multiThreaded)
                {
                    readFileResults = readCopier.SingleUseCopy(runFolder, ESCAFProps.props.ReadsFolder, 1, 8, false);
                }
                else
                {
                    CopierStart start1 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 1, 4);
                    Thread thread1 = new Thread(readCopier.CopyRun);
                    thread1.Start(start1);
                    CopierStart start2 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 5, 8);
                    Thread thread2 = new Thread(readCopier.CopyRun);
                    thread2.Start(start2);
                    thread1.Join();
                    thread2.Join();
                    readFileResults.AddRange(start1.readFileResults);
                    readFileResults.AddRange(start2.readFileResults);
                }
                foreach (ReadFileResult r in readFileResults)
                {
                    foreach (string scpDest in ESCAFProps.props.scpDestinations)
                    {
                        c = new CmdCaller("scp", string.Format("-p {0} {1}/{2}", r.PFPath, scpDest, Path.GetFileName(r.PFPath)));
                        if (c.ExitCode != 0) throw new Exception(c.StdError);
                        c = new CmdCaller("scp", string.Format("-p {0} {1}/nonPF/{2}", r.nonPFPath, scpDest, Path.GetFileName(r.nonPFPath)));
                        if (c.ExitCode != 0) throw new Exception(c.StdError);
                        c = new CmdCaller("scp", string.Format("-p {0} {1}/statistics/{2}", r.statsPath, scpDest, Path.GetFileName(r.statsPath)));
                        if (c.ExitCode != 0) throw new Exception(c.StdError);
                    }
                }
            }
            catch (Exception e)
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Exception in ESCAF_BclToFq:\n" + e);
                logWriter.Flush();
            }
            finally
            {
                if (ESCAFProps.props.scpDestinations.Length > 0 && ESCAFProps.props.clearData)
                {
                    foreach (ReadFileResult r in readFileResults)
                    {
                        File.Delete(r.PFPath);
                        File.Delete(r.nonPFPath);
                        File.Delete(r.statsPath);
                    }
                    if (runFolder != run) Directory.Delete(runFolder, true); // If tarball, delete unpacked
                }
            }
        }

    }
}
