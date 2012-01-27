﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;

namespace Linnarsson.Strt
{
    [Serializable()]
    public class ResultDescription
    {
        public string bowtieIndexVersion { get; set; }
        public string resultFolder { get; set; }
        public List<string> mapFileFolders { get; set; }
        public List<string> mapFilePaths { get; set; }

        public ResultDescription() { }
        public ResultDescription(List<string> mapFilePaths, string bowtieIndexVersion, string resultFolder)
        {
            this.mapFilePaths = mapFilePaths;
            this.bowtieIndexVersion = bowtieIndexVersion;
            this.resultFolder = resultFolder;
            this.mapFileFolders = new List<string>();
            foreach (string mapFilePath in mapFilePaths)
            {
                string mapFileFolder = Path.GetDirectoryName(mapFilePath);
                if (!mapFileFolders.Contains(mapFileFolder)) mapFileFolders.Add(mapFileFolder);
            }
        }
    }

    [Serializable()]
    public class ProjectDescription
    {
        public string projectName { get; set; }
        public string ProjectFolder { get { return Path.Combine(Props.props.ProjectsFolder, projectName); } }
        public string managerEmails { get; set; }
        public string[] runIdsLanes { get; set; }
        public int[] runNumbers { get; set; }
        public string barcodeSet { get; set; }
        public bool analyzeVariants { get; set; }
        public string extractionVersion { get; set; }
        public string annotationVersion { get; set; }
        public bool rpkm { get; set; }
        public string layoutFile { get; set; }
        public string defaultBuild { get; set; }
        public List<LaneInfo> extractionInfos { get; set; }
        public string analysisId { get; set; }
        public List<ResultDescription> resultDescriptions { get; set; }

        public string SampleLayoutPath { get { return Path.Combine(ProjectFolder, layoutFile); } }

        [XmlIgnoreAttribute]
        public string defaultSpecies;
        [XmlIgnoreAttribute]
        public string status;
        public static readonly string STATUS_INQUEUE = "inqueue";
        public static readonly string STATUS_PROCESSING = "processing";
        public static readonly string STATUS_READY = "ready";
        public static readonly string STATUS_FAILED = "failed";

        /// <summary>
        /// Default constructor needed only for serialization!!
        /// </summary>
        public ProjectDescription()
        { }

        public ProjectDescription(string projectName, string barcodesName, string defaultSpecies, List<string> laneInfos,
                          string layoutFile, string status, string emails, string defaultBuild, string variants, string analysisId, bool rpkm)
        {
            this.projectName = projectName;
            this.barcodeSet = barcodesName;
            this.defaultSpecies = defaultSpecies;
            this.runIdsLanes = laneInfos.ToArray();
            this.runNumbers = new int[runIdsLanes.Length];
            this.layoutFile = layoutFile;
            this.status = status;
            this.managerEmails = emails;
            this.defaultBuild = defaultBuild;
            this.analyzeVariants = (variants == "all");
            this.rpkm = rpkm;
            this.analysisId = analysisId;
            this.resultDescriptions = new List<ResultDescription>();
        }
        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\n", projectName, string.Join("|", runIdsLanes.ToArray()), barcodeSet, 
                                  defaultSpecies, ProjectFolder, layoutFile, status, managerEmails);
        }
        public int LaneCount
        {
            get
            {
                int n = 0;
                foreach (string laneArg in runIdsLanes)
                    n += laneArg.Split(':')[1].Length;
                return n;
            }
        }
    }

    [Serializable()]
    public class MailTaskDescription
    {
        public string id { get; set; }
        public string runNo { get; set; }
        public string laneNo { get; set; }
        public string email { get; set; }
        public string status { get; set; }

        public MailTaskDescription(string id, string runNo, string laneNo, string email, string status)
        {
            this.id = id;
            this.runNo = runNo;
            this.laneNo = laneNo;
            this.email = email;
            this.status = status;
        }
    }

    public class ProjectDBFileException : ApplicationException
    {
        public ProjectDBFileException(string msg)
            : base(msg)
        { }
    }
    
    public class ProjectDB
    {
        private readonly static string connectionString = "server=192.168.1.12;uid=cuser;pwd=3pmknHQyl;database=joomla;Connect Timeout=300;";

        public ProjectDB()
        {
        }

        private bool IssueNonQuery(string sql)
        {
            bool success = true;
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + ": " + ex.ToString());
                success = false;
            }
            conn.Close();
            return success;
        }

        private int nextInQueue = 0;
        public void ResetQueue()
        {
            nextInQueue = 0;
        }
        public ProjectDescription GetNextProjectInQueue()
        {
            ProjectDescription pd = null;
            List<ProjectDescription> queue = GetProjectDescriptions("WHERE a.status=\"" + ProjectDescription.STATUS_INQUEUE + "\"");
            if (nextInQueue < queue.Count)
            {
                pd = queue[nextInQueue++];
            }
            return pd;
        }

        private List<ProjectDescription> GetProjectDescriptions(string whereClause)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            List<ProjectDescription> pds = new List<ProjectDescription>();
            string sql = "SELECT a.id, a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.emails, " +
                         " p.plateid, p.barcodeset, p.species, p.layoutfile, a.status, " +
                         " r.illuminarunid AS runid, GROUP_CONCAT(l.laneno ORDER BY l.laneno) AS lanenos " +
                         "FROM jos_aaaanalysis a " + 
                         "LEFT JOIN jos_aaaproject p ON a.jos_aaaprojectid = p.id " +
                         "RIGHT JOIN jos_aaaanalysislane al ON a.id = al.jos_aaaanalysisid " +
                         "LEFT JOIN jos_aaalane l ON al.jos_aaalaneid = l.id " +
                         "LEFT JOIN jos_aaailluminarun r ON l.jos_aaailluminarunid = r.id " +
                         whereClause + 
                         " GROUP BY a.id, runid ORDER BY a.id, p.plateid, runid;";
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> laneInfos = new List<string>();
            string currAnalysisId = "", plateId = "", bcSet = "", defaultSpecies = "", layoutFile = "", plateStatus = "",
                    emails = "", defaultBuild = "", variant = "";
            bool rpkm = false;
            while (rdr.Read())
            {
                string analysisId = rdr["id"].ToString();
                if (currAnalysisId != "" && analysisId != currAnalysisId)
                {
                    pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                    emails, defaultBuild, variant, currAnalysisId, rpkm));
                    laneInfos = new List<string>();
                }
                currAnalysisId = analysisId;
                string laneInfo = rdr["runid"].ToString() + ":" + rdr.GetString("lanenos").Replace(",", "");
                laneInfos.Add(laneInfo);
                plateId = rdr["plateid"].ToString();
                bcSet = rdr["barcodeset"].ToString();
                defaultSpecies = rdr["species"].ToString();
                layoutFile = rdr["layoutfile"].ToString();
                plateStatus = rdr["status"].ToString();
                emails = rdr["emails"].ToString();
                defaultBuild = rdr["transcript_db_version"].ToString();
                variant = rdr["transcript_variant"].ToString();
                rpkm = (rdr["rpkm"].ToString() == "True");
            }
            if (currAnalysisId != "") pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                                        emails, defaultBuild, variant, currAnalysisId, rpkm));
            rdr.Close();
            conn.Close();
            return pds;
        }

        public void UpdateDB(ProjectDescription projDescr)
        {
            string sql = string.Format("UPDATE jos_aaaanalysis SET status=\"{0}\", time=NOW() WHERE id=\"{1}\";",
                                       projDescr.status, projDescr.analysisId);
            IssueNonQuery(sql);
        }

        public void PublishResults(ProjectDescription projDescr)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT jos_aaaprojectid, lanecount FROM jos_aaaanalysis WHERE id=\"{0}\";", projDescr.analysisId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            string projectId = rdr["jos_aaaprojectid"].ToString();
            string laneCount = rdr["lanecount"].ToString();
            rdr.Close();
            bool firstResult = true;
            foreach (ResultDescription resultDescr in projDescr.resultDescriptions)
            {
                int i = 3;
                int p = resultDescr.bowtieIndexVersion.IndexOf("chr");
                if (p == -1) { i = 1; p = resultDescr.bowtieIndexVersion.IndexOf("_"); } // Backward compability
                string genome = resultDescr.bowtieIndexVersion.Substring(0, p);
                string variants = (resultDescr.bowtieIndexVersion[p + i] == 'a') ? "all" : "single";
                string dbbuild = resultDescr.bowtieIndexVersion.Substring(p + i + 1);
                if (firstResult)
                {
                    sql = string.Format("UPDATE jos_aaaanalysis " +
                            "SET extraction_version=\"{0}\", annotation_version=\"{1}\", genome=\"{2}\", transcript_db_version=\"{3}\", " +
                            "transcript_variant=\"{4}\", resultspath=\"{5}\", status=\"{6}\", time=NOW() WHERE id=\"{7}\" ",
                            projDescr.extractionVersion, projDescr.annotationVersion, genome, dbbuild,
                            variants, resultDescr.resultFolder, projDescr.status, projDescr.analysisId);
                }
                else
                {
                    sql = string.Format("INSERT INTO jos_aaaanalysis " +
                                        "(jos_aaaprojectid, extraction_version, annotation_version, genome, " +
                                        "transcript_db_version, transcript_variant, lanecount, resultspath, status, time) " +
                                        "VALUES (\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", NOW());",
                                        projectId, projDescr.extractionVersion, projDescr.annotationVersion, genome,
                                        dbbuild, variants, laneCount, resultDescr.resultFolder, projDescr.status);
                }
                cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                firstResult = false;
            }
            foreach (LaneInfo extrInfo in projDescr.extractionInfos)
            {
                if (extrInfo.nReads == 0)
                    continue; // Has been extracted earlier - no data to update
                sql = string.Format(string.Format("UPDATE jos_aaalane SET yield=\"{0}\", pfyield=\"{1}\" WHERE laneno=\"{2}\" AND " + 
                                        "jos_aaailluminarunid= (SELECT id FROM jos_aaailluminarun WHERE illuminarunid=\"{3}\") ",
                                        extrInfo.nReads, extrInfo.nPFReads, extrInfo.laneNo, extrInfo.runId));
                Console.WriteLine(sql);
                cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        /// <summary>
        /// Sets the bcl copy/collection status of a run.
        /// </summary>
        /// <param name="runId">Either a run number for the old machine or a cell Id for the new</param>
        /// <param name="status"></param>
        /// <param name="runNo">A run number to set.</param>
        /// <param name="runDate">Date of the run (extracted from filename)</param>
        public void UpdateRunStatus(string runId, string status, int runNo, string runDate)
        { // Below SQL will update with status and runno if user has defined the run, else add a new run as
          // well as defining 8 new lanes by side-effect of a MySQL trigger
            string sql = string.Format("INSERT INTO jos_aaailluminarun (status, runno, illuminarunid, rundate) " +
                                       "VALUES ('{0}', '{1}', '{2}', '{3}') " +
                                       "ON DUPLICATE KEY UPDATE status='{0}', runno='{1}';",
                                       status, runNo, runId, runDate);
            IssueNonQuery(sql);
        }

        public void UpdateRunCycles(string runId, int cycles, int indexCycles, int pairedCycles)
        {
            string sql = string.Format("UPDATE jos_aaailluminarun SET cycles='{0}', indexcycles='{1}' " +
                                       "WHERE illuminarunid='{2}' AND cycles=0;",
                                       cycles + pairedCycles, indexCycles, runId);
            IssueNonQuery(sql);
        }

        public Dictionary<string, List<MailTaskDescription>> GetQueuedMailTasksByEmail()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            Dictionary<string, List<MailTaskDescription>> mds = new Dictionary<string, List<MailTaskDescription>>();
            string sql = "SELECT id, runno, laneno, email, status FROM jos_aaafqmailqueue WHERE status='inqueue' ORDER BY email";
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string email = rdr["email"].ToString();
                    MailTaskDescription md = new MailTaskDescription(rdr["id"].ToString(), rdr["runno"].ToString(), rdr["laneno"].ToString(),
                                                                     email, rdr["status"].ToString());
                    if (!mds.ContainsKey(email))
                        mds[email] = new List<MailTaskDescription>();
                    mds[email].Add(md);
                }
                rdr.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + ": " + ex.ToString());
            }
            conn.Close();
            return mds;
        }

        public void UpdateMailTaskStatus(string id, string status)
        {
            string sql = string.Format("UPDATE jos_aaafqmailqueue SET status='{0}', time=NOW() WHERE id='{1}'", status, id);
            IssueNonQuery(sql);
        }

        public bool AddToBackupQueue(string readFile, int priority)
        {
            string sql = string.Format("INSERT INTO jos_aaabackupqueue (path, status, priority) VALUES ('{0}', 'inqueue', '{1}')", readFile, priority);
            return IssueNonQuery(sql);
        }

        public bool SetBackupStatus(string readFile, string status)
        {
            string sql = string.Format("UPDATE jos_aaabackupqueue SET status='{0}' WHERE path='{1}'", status, readFile);
            return IssueNonQuery(sql);
        }

        public bool RemoveFileToBackup(string readFile)
        {
            string sql = string.Format("DELETE FROM jos_aaabackupqueue WHERE path='{0}'", readFile);
            return IssueNonQuery(sql);
        }

        public List<string> GetWaitingFilesToBackup()
        {
            List<string> waitingFiles = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            string sql = "SELECT path FROM jos_aaabackupqueue WHERE status='inqueue' ORDER BY priority, id";
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    string file = rdr["path"].ToString();
                    waitingFiles.Add(file);
                }
                rdr.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now.ToString() + ": " + ex.ToString());
            }
            conn.Close();
            return waitingFiles;
        }
    }
}
