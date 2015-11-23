using System;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using System.Xml.Serialization;
using Linnarsson.Strt;
using Linnarsson.Utilities;
using MySql.Data.MySqlClient;

namespace ESCAF_BclToFq
{
    [Serializable]
    public sealed class ESCAFProps
    {
        [NonSerialized]
        public static string configFilename = "ESCAF_BclToFq.xml";
        [NonSerialized]
        public string ConnectionString = "server=127.0.0.1;uid=user;pwd=password;database=joomla;Connect Timeout=300;Charset=utf8;";

        public string DBPrefix = "sccf_"; // Table prefix use in CellDB
        public string LogFile = "ESCAF_BclToFq.log"; // Log output file
        public string FinishedRunFoldersFile = "processed_run_folder_names.txt"; // List of ready processed and transferred run folders
        public int scanInterval = 5; // Minutes between scans for new data
        public string RunsFolder = "/home/data/runs"; // Where Illumina run folders (or tarballs) are deposited
        public string RunFolderMatchPattern = "([0-9][0-9][0-9][0-9][0-9][0-9])_.+_([0-9]+)_[AB](.+XX)$";
        // Pattern that HiSeq run folders in RunsFolder should match. Groups: (date) (runno) (runid)
        public int minLastAccessMinutes = 10; // Min time in minutes since last access of a run folder before starting to process
        public string ReadsFolder = "/home/data/reads"; // Where .fq files for each lane are put
        public string[] scpDestinations = new string[] 
        { "sten@milou.uppmax.uu.se:reads", "hiseq@130.237.117.141:/data/reads" };
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
            SetConnectionStrings(props);
            return props;
        }

        private static void SetConnectionStrings(ESCAFProps props)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exeFilePath = Path.Combine(appDir, "SB.exe"); // The application that holds ConnectionString config
                Configuration config = ConfigurationManager.OpenExeConfiguration(exeFilePath);
                ConnectionStringsSection section = config.GetSection("connectionStrings") as ConnectionStringsSection;
                if (!section.SectionInformation.IsProtected)
                {
                    section.SectionInformation.ProtectSection("RsaProtectedConfigurationProvider");
                    config.Save(ConfigurationSaveMode.Full, true);
                }
                section.SectionInformation.UnprotectSection();
                ConnectionStringSettings settings = section.ConnectionStrings["SB.Properties.Settings.MainDBConnString"];
                if (settings != null)
                    props.ConnectionString = settings.ConnectionString;
            }
            catch (Exception)
            { }
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
        private static readonly string[] cyclecolnames = new string[] { "", "cycles", "indexcycles", "pairedcycles" };

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
                    foreach (string runDir in runDirs)
                    {
                        if (copiedRunDirs.Contains(runDir) ||  !Regex.IsMatch(runDir, ESCAFProps.props.RunFolderMatchPattern))
                            continue;
                        TimeSpan ts = DateTime.Now - Directory.GetLastAccessTime(runDir);
                        if (ts >= new TimeSpan(0, ESCAFProps.props.minLastAccessMinutes, 0))
                        {
                            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Processing " + runDir);
                            logWriter.Flush();
                            ProcessRun(runDir);
                            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Ready");
                            logWriter.Flush();
                            copiedRunDirs.Add(runDir);
                            string readRunLine = Path.GetFileName(runDir) + "\n";
                            File.AppendAllText(ESCAFProps.props.FinishedRunFoldersFile, readRunLine);
                        }
                    }
                }
                catch (Exception e)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Exception in ESCAF_BclToFq:\n" + e);
                    logWriter.Flush();
                }
                Thread.Sleep(1000 * 60 * ESCAFProps.props.scanInterval);
            }
        }

        private static void ProcessRun(string runFolderOrTgz)
        {
            string runFolder = UnpackIfNeeded(runFolderOrTgz);
            Match mr = Regex.Match(runFolder, ESCAFProps.props.RunFolderMatchPattern);
            string rundate = mr.Groups[1].Value;
            string runno = mr.Groups[2].Value;
            string runid = mr.Groups[3].Value;
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            CmdCaller c;
            try 
            {
                DBInsertIlluminaRun(runid, runno, rundate);
                ReadCopier readCopier = new ReadCopier(logWriter);
                if (!ESCAFProps.props.multiThreaded)
                {
                    readFileResults = readCopier.SingleUseCopy(runFolder, ESCAFProps.props.ReadsFolder, 1, 8, false);
                }
                else
                {
                    CopierStart start1 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 1, 2, false);
                    Thread thread1 = new Thread(readCopier.CopyRun);
                    thread1.Start(start1);
                    CopierStart start2 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 3, 4, false);
                    Thread thread2 = new Thread(readCopier.CopyRun);
                    thread2.Start(start2);
                    CopierStart start3 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 5, 6, false);
                    Thread thread3 = new Thread(readCopier.CopyRun);
                    thread3.Start(start3);
                    CopierStart start4 = new CopierStart(runFolder, ESCAFProps.props.ReadsFolder, 7, 8, false);
                    Thread thread4 = new Thread(readCopier.CopyRun);
                    thread4.Start(start4);
                    thread1.Join();
                    thread2.Join();
                    thread3.Join();
                    thread4.Join();
                    readFileResults.AddRange(start1.readFileResults);
                    readFileResults.AddRange(start2.readFileResults);
                    readFileResults.AddRange(start3.readFileResults);
                    readFileResults.AddRange(start4.readFileResults);
                }
                foreach (ReadFileResult r in readFileResults)
                {
                    foreach (string scpDest in ESCAFProps.props.scpDestinations)
                    {
                        string scpArg = string.Format("-p {0} {1}/{2}", r.PFPath, scpDest, Path.GetFileName(r.PFPath));
                        c = new CmdCaller("scp", scpArg);
                        if (c.ExitCode != 0) throw new Exception(scpArg + "\n" + c.StdError);
                        scpArg = string.Format("-p {0} {1}/nonPF/{2}", r.nonPFPath, scpDest, Path.GetFileName(r.nonPFPath));
                        c = new CmdCaller("scp", scpArg);
                        if (c.ExitCode != 0) throw new Exception(scpArg + "\n" + c.StdError);
                        scpArg = string.Format("-p {0} {1}/statistics/{2}", r.statsPath, scpDest, Path.GetFileName(r.statsPath));
                        c = new CmdCaller("scp", scpArg);
                        if (c.ExitCode != 0) throw new Exception(scpArg + "\n" + c.StdError);
                    }
                    DBUpdateLaneYield(runid, r);
                }
                DBUpdateRunStatus(runid, "copied");
            }
            catch (Exception)
            {
                DBUpdateRunStatus(runid, "copyfail");
                throw;
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
                }
                if (runFolder != runFolderOrTgz) clearDir(runFolder); // If tarball, delete unpacked
            }
        }

        private static void clearDir(string dirname)
        {
            DirectoryInfo dir = new DirectoryInfo(dirname);
            foreach (FileInfo fi in dir.GetFiles())
            {
                fi.Delete();
            }

            foreach (DirectoryInfo di in dir.GetDirectories())
            {
                clearDir(di.FullName);
                di.Delete();
            }
        }
        
        private static string UnpackIfNeeded(string runFolderOrTgz)
        {
            string runFolder = runFolderOrTgz;
            Match m = Regex.Match(runFolderOrTgz, "(.+)(\\.tar\\.gz|\\.tgz)$");
            if (m.Success)
            {
                runFolder = m.Groups[1].Value;
                CmdCaller cmdCaller = new CmdCaller("tar", "zxf " + runFolderOrTgz, true);
                if (cmdCaller.ExitCode != 0) throw new Exception(cmdCaller.StdError);
            }
            return runFolder;
        }

        private static string DBInsertIlluminaRun(string runid, string runno, string rundate)
        {
            string sql = string.Format("INSERT INTO #__aaailluminarun (status, runno, illuminarunid, rundate, time, user) " +
                                "VALUES ('copying', '{0}', '{1}', '{2}', NOW(), '{3}') " +
                                "ON DUPLICATE KEY UPDATE status='copying', runno='{0}', rundate='{2}'",
                                        runno, runid, rundate, "system");
            //logWriter.WriteLine(sql);
            IssueNonQuery(sql);
            return runid;
        }

        private static void DBUpdateRunStatus(string runid, string status)
        {
            string sql = string.Format("UPDATE #__aaailluminarun SET status='{0}', time=NOW() WHERE illuminarunid='{1}'",
                                       status, runid);
            //logWriter.WriteLine(sql);
            IssueNonQuery(sql);
        }

        private static void DBUpdateLaneYield(string runid, ReadFileResult r)
        {
            if (r.read == 1)
            {
                uint nReads = r.nPFReads + r.nNonPFReads;
                string sql = string.Format(string.Format("UPDATE #__aaalane SET yield='{0}', pfyield='{1}' WHERE laneno='{2}'" +
                              " AND #__aaailluminarunid=(SELECT id FROM #__aaailluminarun WHERE illuminarunid='{3}') ",
                                 nReads, r.nPFReads, r.lane, runid));
                //logWriter.WriteLine(sql);
                IssueNonQuery(sql);
            }
            if (r.lane == 1 && r.readLen >= 0)
            {
                string c = cyclecolnames[r.read];
                string sql = string.Format("UPDATE #__aaailluminarun SET {0}='{1}' WHERE illuminarunid='{2}'", c, r.readLen, runid);
                //logWriter.WriteLine(sql);
                IssueNonQuery(sql);
            }
        }

        private static void IssueNonQuery(string sql)
        {
            sql = sql.Replace("#__", ESCAFProps.props.DBPrefix);
            MySqlConnection conn = new MySqlConnection(ESCAFProps.props.ConnectionString);
            conn.Open();
            //Console.WriteLine(sql);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            conn.Close();
        }

    }
}
