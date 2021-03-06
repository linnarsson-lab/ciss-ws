﻿using System;
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
using Linnarsson.Dna;
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
        public string RunFolderMatchPattern = "^([0-9][0-9][0-9][0-9][0-9][0-9])_.+_([0-9]+)_[AB](.+)$";
        // Pattern that HiSeq run folders in RunsFolder should match. Groups: (date) (runno) (runid)
        public int minLastAccessMinutes = 10; // Min time in minutes since last access of a run folder before starting to process
        public string ReadsFolder = "/home/data/reads"; // Where .fq files for each lane are put
        public string[] scpDestinations = new string[] 
        { "sten@milou.uppmax.uu.se:reads", "hiseq@130.237.117.141:/data/reads" };
        // scp destinations of resulting .fq files. The directory structure will be PF in top folder, and nonPF/ and statistics/ subfolders
        public bool clearData = true; // Remove all run data and local .fq files after successful processing and scp:ing
        public bool multiThreaded = false; // Use parallell threads for quick processing
        public int MaxBclToFqErrors = 50; // Max No of errors before quitting

        private static ESCAFProps Read()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
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
                logWriter.AutoFlush = true;
                logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Started");
                Scan();
                logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Quit");
            }
        }

        private static void Scan()
        {
			List<string> copiedRunDirs = new List<string>();
			using (StreamReader r = new StreamReader (ESCAFProps.props.FinishedRunFoldersFile)) {
				string line = r.ReadLine();
				while (line != null) {
					if (line.Trim ().Length > 5)
						copiedRunDirs.Add (line.Trim ());
					line = r.ReadLine();
				}
			}
			logWriter.WriteLine(string.Format("{0} INFO: {1} already processed rundirs found in {2}", 
			                                  DateTime.Now.ToString(), copiedRunDirs.Count, ESCAFProps.props.FinishedRunFoldersFile));
            int nExceptions = 0;
            while (nExceptions < ESCAFProps.props.MaxBclToFqErrors)
            {
                try
                {
                    string[] runDirs = Directory.GetDirectories(ESCAFProps.props.RunsFolder);
                    foreach (string runDir in runDirs)
                    {
                        string runDirName = Path.GetFileName(runDir);
						bool m = Regex.IsMatch(runDirName, ESCAFProps.props.RunFolderMatchPattern);
						if (! m )
							Console.WriteLine("No match for {0}", runDirName);
                        if (copiedRunDirs.Contains(runDirName) || ! m)
                            continue;
                        TimeSpan ts = DateTime.Now - Directory.GetLastAccessTime(runDir);
                        if (ts < new TimeSpan(0, ESCAFProps.props.minLastAccessMinutes, 0)) {
							Console.WriteLine("Too recent access of {0}", runDirName);
                            continue;
						}
						if (ReadyToProcess(runDir))
                        {
                            bool allCopied = IsChromiumSample(runDir)? OnlyDoScp(runDir) : ProcessRunWithReads(runDir);
                            if (!allCopied)
                                continue;
                            logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Ready");
                            copiedRunDirs.Add(runDirName);
                            string readRunLine = Path.GetFileName(runDir) + "\n";
                            File.AppendAllText(ESCAFProps.props.FinishedRunFoldersFile, readRunLine);
                        }
                    }
                }
                catch (Exception e)
                {
                    nExceptions++;
                    logWriter.WriteLine(DateTime.Now.ToString() + " ERROR: Exception in ESCAF_BclToFq:\n" + e);
                }
                Thread.Sleep(1000 * 60 * ESCAFProps.props.scanInterval);
            }
        }

		private static bool IsChromiumSample(string runDir) {
			string tenXFilePath = Path.Combine(runDir, "Basecalling_Netcopy_complete_Read4.txt");
			if (File.Exists (tenXFilePath)) {
				return true;
			}
			string runInfoPath = Path.Combine (runDir, "RunInfo.xml");
			string runinfoxml = runInfoPath.OpenRead ().ReadToEnd ();
			return Regex.IsMatch (runinfoxml, " <Read Number=\"3\" NumCycles=\"98\" IsIndexedRead=\"N\" />");
		}

		private static bool ReadyToProcess(string runDir) {
			string runInfoPath = Path.Combine (runDir, "RunInfo.xml");
			string readyFilePath = Path.Combine(runDir, "Basecalling_Netcopy_complete.txt");
			string rtaPath = Path.Combine(runDir, "RTAComplete.txt");
			return (File.Exists (runInfoPath) && File.Exists (readyFilePath) && File.Exists (rtaPath));
		}

		private static bool ProcessRunWithReads(string runFolderOrTgz)
        {
			logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Processing " + runFolderOrTgz + " into reads");
            string runFolder = UnpackIfNeeded(runFolderOrTgz);
            Match mr = Regex.Match(runFolder, ESCAFProps.props.RunFolderMatchPattern);
            string runDate = mr.Groups[1].Value;
            string runNo = mr.Groups[2].Value;
            string runId = mr.Groups[3].Value;
            List<ReadFileResult> readFileResults = new List<ReadFileResult>();
            CmdCaller c;
            try 
            {
                DBInsertIlluminaRun(runId, runNo, runDate);
                ReadCopier readCopier = new ReadCopier(logWriter, null);
                ReadCopierStatus status;
                if (ESCAFProps.props.multiThreaded)
                    readFileResults = readCopier.ParallelCopy(runFolder, ESCAFProps.props.ReadsFolder, out status);
                else
                    readFileResults = readCopier.SerialCopy(runFolder, ESCAFProps.props.ReadsFolder, 1, 8, false, out status);
                if (readFileResults.Count > 0)
                {
                    List<string> scpErrors = new List<string>();
                    int nCopied = 0;
                    foreach (ReadFileResult r in readFileResults)
                    {
                        DBUpdateLaneYield(runId, r);
                        foreach (string scpDest in ESCAFProps.props.scpDestinations)
                        {
                            string scpArg = string.Format("-p {0} {1}/statistics/{2}", r.statsPath, scpDest, Path.GetFileName(r.statsPath));
                            c = new CmdCaller("scp", scpArg);
                            if (c.ExitCode != 0) scpErrors.Add(scpArg + "\n    " + c.StdError);
                            scpArg = string.Format("-p {0} {1}/{2}", r.PFPath, scpDest, Path.GetFileName(r.PFPath));
                            c = new CmdCaller("scp", scpArg);
                            if (c.ExitCode != 0) scpErrors.Add(scpArg + "\n    " + c.StdError);
                            scpArg = string.Format("-p {0} {1}/nonPF/{2}", r.nonPFPath, scpDest, Path.GetFileName(r.nonPFPath));
                            c = new CmdCaller("scp", scpArg);
                            if (c.ExitCode != 0) scpErrors.Add(scpArg + "\n    " + c.StdError);
                        }
                        nCopied++;
                    }
                    logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Mirrored " + nCopied + "/" + readFileResults.Count.ToString()
                                       + " fq files to  " + string.Join(" & ", ESCAFProps.props.scpDestinations) + "\n");
                    if (scpErrors.Count > 0)
                    {
                        string scpErrString = string.Join("\n", scpErrors.ToArray());
                        logWriter.WriteLine("*** Errors during mirroring: ***\n" + scpErrString + "\n");
                        throw new Exception(scpErrString);
                    }
                }
                if (status == ReadCopierStatus.SOMEREADMISSING)
                    return false;
                if (status == ReadCopierStatus.SOMEREADFAILED) 
                    throw new Exception("ERROR copying " + runFolderOrTgz + ". Check logfile for more info.");
                DBUpdateRunStatus(runId, "copied");
            }
            catch (Exception)
            {
                DBUpdateRunStatus(runId, "copyfail");
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
            return true;
        }

		/// <summary>
		/// This only scp's the run folder a .tgz to destinations. For use with 10Xgenomics samples.
		/// </summary>
		/// <param name="runDir">Run dir.</param>
		private static bool OnlyDoScp(string runDir)
		{
			logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copying " + runDir + " to backup/storage");
			Match mr = Regex.Match(runDir, ESCAFProps.props.RunFolderMatchPattern);
			string runDate = mr.Groups[1].Value;
			string runNo = mr.Groups[2].Value;
			string runId = mr.Groups[3].Value;
			CmdCaller c;
			try 
			{
				DBInsertIlluminaRun(runId, runNo, runDate);
				string tgzFile = runDir + ".tgz";
				CmdCaller cmdCaller = new CmdCaller("tar", "zcf " + tgzFile + " " + runDir, true);
				if (cmdCaller.ExitCode != 0) throw new Exception(cmdCaller.StdError);
				List<string> scpErrors = new List<string>();
				foreach (string scpDest in ESCAFProps.props.scpDestinations)
				{
					string scpArg = string.Format("{0} {1}/{0}", tgzFile, scpDest);
					c = new CmdCaller("scp", scpArg);
					if (c.ExitCode != 0) scpErrors.Add(scpArg + "\n    " + c.StdError);
				}
				logWriter.WriteLine(DateTime.Now.ToString() + " INFO: Copied " + runDir
				                    + " to  " + string.Join(" & ", ESCAFProps.props.scpDestinations) + "\n");
				if (scpErrors.Count > 0)
				{
					string scpErrString = string.Join("\n", scpErrors.ToArray());
					logWriter.WriteLine("*** Errors during copying: ***\n" + scpErrString + "\n");
					throw new Exception(scpErrString);
				}
				DBUpdateRunStatus(runId, "copied");
				File.Delete(tgzFile);
			}
			catch (Exception)
			{
				DBUpdateRunStatus(runId, "copyfail");
				throw;
			}
			return true;
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
