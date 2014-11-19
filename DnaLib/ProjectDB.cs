using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using C1;

namespace Linnarsson.Dna
{
    [Serializable()]
    public class ResultDescription
    {
        public string bowtieIndexVersion { get; set; }
        public string resultFolder { get; set; }
        public List<string> mapFileFolders { get; set; }

        public ResultDescription() { }
        public ResultDescription(List<string> mapFilePaths, string bowtieIndexVersion, string resultFolder)
        {
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
        public string plateId { get; set; }
        public string ProjectFolder { get { return Path.Combine(Props.props.ProjectsFolder, plateId); } }
        public string managerEmails { get; set; }
        public string[] runIdsLanes { get; set; }
        public int[] runNumbers { get; set; }
        public string barcodeSet { get; set; }
        public int SpikeMoleculeCount { get; set; }
        public bool analyzeVariants { get; set; }
        public string extractionVersion { get; set; }
        public string annotationVersion { get; set; }
        public bool rpkm { get; set; }
        public int readDirection { get; set; }
        public string layoutFile { get; set; }
        public string build { get; set; }
        public string annotation { get; set; }
        public string variant { get; set; }
        public int gene5PrimeExtension { get; set; }
        public List<LaneInfo> laneInfos { get; set; }
        public string analysisId { get; set; }
        public List<ResultDescription> resultDescriptions { get; set; }

        [XmlIgnoreAttribute]
        public bool DirectionalReads { get { return readDirection != 0; } }
        [XmlIgnoreAttribute]
        public bool SenseStrandIsSequenced { get { return readDirection == 1; } }

        [XmlIgnoreAttribute]
        public string SampleLayoutPath { get { return Path.Combine(ProjectFolder, layoutFile); } }

        [XmlIgnoreAttribute]
        public string defaultBuild { get; set; }
        [XmlIgnoreAttribute]
        public string defaultSpecies;
        [XmlIgnoreAttribute]
        public string status;
        public static readonly string STATUS_INQUEUE = "inqueue";
        public static readonly string STATUS_PROCESSING = "processing";
        public static readonly string STATUS_READY = "ready";
        public static readonly string STATUS_FAILED = "failed";

        public string title { get; set; }
        public DateTime productionDate { get; set; }
        public string tissue { get; set; }
        public string description { get; set; }
        public string protocol { get; set; }
        public string comment { get; set; }
        public string plateReference { get; set; }
        public string sampleType { get; set; }
        public string collectionMethod { get; set; }
        public string labBookPage { get; set; }
        public int jos_aaacontactid { get; set; }
        public int jos_aaamanagerid { get; set; }
        public int jos_aaaclientid { get; set; }

        [XmlIgnoreAttribute]
        public int nSeqCycles { get; set; }
        [XmlIgnoreAttribute]
        public int nIdxCycles { get; set; }
        [XmlIgnoreAttribute]
        public int nPairedCycles { get; set; }
        [XmlIgnoreAttribute]
        public string seqPrimer { get; set; }
        [XmlIgnoreAttribute]
        public string idxPrimer { get; set; }
        [XmlIgnoreAttribute]
        public string pairedPrimer { get; set; }

        /// <summary>
        /// Default constructor needed only for serialization!!
        /// </summary>
        public ProjectDescription()
        { }

        /// <summary>
        /// Constructor when starting analysis of projects in database
        /// </summary>
        public ProjectDescription(string plateId, string barcodesName, string defaultSpecies, List<string> laneInfos,
                          string layoutFile, string status, string emails, string defaultBuild, string variants, string analysisId,
                          bool rpkm, int spikeMoleculeCount, int readdir)
        {
            this.plateId = plateId;
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
            this.readDirection = readdir;
            this.SpikeMoleculeCount = spikeMoleculeCount;
            this.analysisId = analysisId;
            this.resultDescriptions = new List<ResultDescription>();
        }

        /// <summary>
        /// Constructor for inserting new projects
        /// </summary>
        public ProjectDescription(int jos_aaacontactid, int jos_aaamanagerid, int jos_aaaclientid, 
                    string title, DateTime productiondate, string plateid, string platereference, string species,
                    string tissue, string sampletype, string collectionmethod, string description, string protocol,
                    string barcodeSet, string labbookpage, string layoutFile, string comment, int spikeMoleculeCount)
        {
            this.jos_aaacontactid = jos_aaacontactid;
            this.jos_aaamanagerid = jos_aaamanagerid;
            this.jos_aaaclientid = jos_aaaclientid;
            this.title = title;
            this.productionDate = productiondate;
            this.plateId = plateid;
            this.plateReference = platereference;
            this.defaultSpecies = species;
            this.tissue = tissue;
            this.sampleType = sampletype;
            this.collectionMethod = collectionmethod;
            this.description = description;
            this.protocol = protocol;
            this.barcodeSet = barcodeSet;
            this.labBookPage = labbookpage;
            this.layoutFile = layoutFile;
            this.comment = comment;
            this.SpikeMoleculeCount = spikeMoleculeCount;
            this.status = STATUS_INQUEUE;
        }

        public void SetGenomeData(StrtGenome genome)
        {
            build = genome.Build;
            variant = genome.GeneVariants ? "all" : "single";
            annotation = genome.Annotation;
            gene5PrimeExtension = Props.props.GeneFeature5PrimeExtension;
        }

        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\n", plateId, string.Join("|", runIdsLanes.ToArray()), barcodeSet, 
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

    public class Person
    {
        public int id { get; set; }
        public string name { get; set; }
        public string first { get; private set; }
        public string last { get; private set; }
        public string initials { get { return string.Join("", Array.ConvertAll(name.Split(' '), n => n[0].ToString())); } }
        public Person(int id, string name)
        {
            this.id = id;
            this.name = name;
            string[] parts = name.Split(' ');
            first = parts[0];
            last = parts[parts.Length - 1];
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
        private static string connectionString;

        public ProjectDB()
        {
            connectionString = Props.props.MySqlServerConnectionString;
        }

        private int IssueNonQuery(string sql)
        {
            int nRowsAffected = 0;
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                nRowsAffected = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
            return nRowsAffected;
        }

        /// <summary>
        /// Get all projects for every barcode set that have been queued for analysis in given lane.
        /// </summary>
        /// <param name="runNo"></param>
        /// <param name="lane"></param>
        /// <returns>dictonary from each barcodeset to the its projects</returns>
        public List<Pair<string, string>> GetBarcodeSetsAndProjects(int runNo, int lane)
        {
            string sql = "SELECT p.plateid, barcodeset FROM jos_aaaproject p " +
                         " LEFT JOIN jos_aaaanalysis a ON a.jos_aaaprojectid = p.id " +
                         " LEFT JOIN jos_aaaanalysislane al ON al.jos_aaaanalysisid = a.id " +
                         " LEFT JOIN jos_aaalane l ON l.id = al.jos_aaalaneid " +
                         " LEFT JOIN jos_aaailluminarun r ON r.id = l.jos_aaailluminarunid " +
                         " WHERE r.runno = {0} AND laneno = {1}";
            sql = string.Format(sql, runNo, lane);
            List<Pair<string, string>> projectsByBc = new List<Pair<string,string>>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string projectId = rdr["plateid"].ToString();
                string barcodeSetName = rdr["barcodeset"].ToString();
                projectsByBc.Add(new Pair<string, string>(barcodeSetName, projectId));
            }
            rdr.Close();
            conn.Close();
            return projectsByBc;
        }

        private int nextInQueue = 0;
        public void ResetQueue()
        {
            nextInQueue = 0;
        }
        public ProjectDescription GetNextProjectInQueue()
        {
            ProjectDescription pd = null;
            List<ProjectDescription> queue = GetProjectDescriptions("WHERE a.status=\"" + ProjectDescription.STATUS_INQUEUE + "\"" +
                " AND p.id NOT IN (SELECT p1.id FROM jos_aaaproject p1 JOIN jos_aaaanalysis a1 ON a1.jos_aaaprojectid=p1.id AND a1.status=\"processing\")");
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
            string sql = "SELECT a.id, a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.readdir, a.emails, " +
                         " p.plateid, p.barcodeset, p.spikemolecules, p.species, p.layoutfile, a.status, " +
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
            int readdir = 1;
            int spikeMolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
            while (rdr.Read())
            {
                string analysisId = rdr["id"].ToString();
                if (currAnalysisId != "" && analysisId != currAnalysisId)
                {
                    pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                    emails, defaultBuild, variant, currAnalysisId, rpkm, spikeMolecules, readdir));
                    laneInfos = new List<string>();
                }
                currAnalysisId = analysisId;
                string laneInfo = string.Format("{0}:{1}", rdr["runid"], rdr.GetString("lanenos").Replace(",", ""));
                laneInfos.Add(laneInfo);
                plateId = rdr["plateid"].ToString();
                bcSet = rdr["barcodeset"].ToString();
                defaultSpecies = rdr["species"].ToString();
                layoutFile = rdr["layoutfile"].ToString();
                plateStatus = rdr["status"].ToString();
                emails = rdr["emails"].ToString();
                defaultBuild = rdr["transcript_db_version"].ToString();
                variant = rdr["transcript_variant"].ToString();
                spikeMolecules = int.Parse(rdr["spikemolecules"].ToString());
                rpkm = (rdr["rpkm"].ToString() == "True");
                readdir = rdr.GetInt32("readdir");
            }
            if (currAnalysisId != "") pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                             emails, defaultBuild, variant, currAnalysisId, rpkm, spikeMolecules, readdir));
            rdr.Close();
            conn.Close();
            return pds;
        }

        public int UpdateAnalysisStatus(ProjectDescription projDescr)
        {
            return UpdateAnalysisStatus(projDescr, "");
        }
        public int UpdateAnalysisStatus(ProjectDescription projDescr, string reqPreviousStatus)
        {
            string reqSql = (reqPreviousStatus == "")? "": string.Format(" AND status=\"{0}\"", reqPreviousStatus);
            string sql = string.Format("UPDATE jos_aaaanalysis SET status=\"{0}\", time=NOW() WHERE id=\"{1}\" {2};",
                                       projDescr.status, projDescr.analysisId, reqSql);
            return IssueNonQuery(sql);
        }

        public void PublishResults(ProjectDescription projDescr)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT jos_aaaprojectid, lanecount, comment, emails, user FROM jos_aaaanalysis WHERE id=\"{0}\";", projDescr.analysisId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            string projectId = rdr["jos_aaaprojectid"].ToString();
            string laneCount = rdr["lanecount"].ToString();
            string comment = rdr["comment"].ToString();
            string emails = rdr["emails"].ToString();
            string user = rdr["user"].ToString();
            string isRpkm = (projDescr.rpkm) ? "1" : "0";
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
                               "(jos_aaaprojectid, extraction_version, annotation_version, genome, comment, emails, user, " +
                                "transcript_db_version, transcript_variant, lanecount, resultspath, status, rpkm, time) " +
                               "VALUES (\"{0}\", \"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\", NOW());",
                               projectId, projDescr.extractionVersion, projDescr.annotationVersion, genome, comment, emails, user,
                               dbbuild, variants, laneCount, resultDescr.resultFolder, projDescr.status, isRpkm);
                }
                cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                firstResult = false;
            }
            foreach (LaneInfo laneInfo in projDescr.laneInfos)
            {
                if (laneInfo.nValidReads == 0)
                    continue; // Has been extracted earlier - no data to update
                sql = string.Format(string.Format("UPDATE jos_aaalane SET strtyield=\"{0}\" WHERE laneno=\"{1}\" AND " + 
                                        "jos_aaailluminarunid= (SELECT id FROM jos_aaailluminarun WHERE illuminarunid=\"{2}\") ",
                                        laneInfo.nValidReads, laneInfo.laneNo, laneInfo.illuminaRunId));
                cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        /// <summary>
        /// Updates the database with info on number of total and passed filter reads for a lane
        /// </summary>
        /// <param name="runId">The (8 character long) plate id</param>
        /// <param name="nReads">Total number of reads</param>
        /// <param name="nPFReads">Number of reads passed Illumina filter</param>
        /// <param name="lane">Lane number</param>
        public void SetIlluminaYield(string runId, uint nReads, uint nPFReads, int lane)
        {
            string sql = string.Format(string.Format("UPDATE jos_aaalane SET yield=\"{0}\", pfyield=\"{1}\" WHERE laneno=\"{2}\" AND " +
                                    "jos_aaailluminarunid= (SELECT id FROM jos_aaailluminarun WHERE illuminarunid=\"{3}\") ",
                                    nReads, nPFReads, lane, runId));
            IssueNonQuery(sql);
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
            string sql = string.Format("INSERT INTO jos_aaailluminarun (status, runno, illuminarunid, rundate, time, user) " +
                                       "VALUES ('{0}', '{1}', '{2}', '{3}', NOW(), '{4}') " +
                                       "ON DUPLICATE KEY UPDATE status='{0}', runno='{1}';",
                                       status, runNo, runId, runDate, "system");
            IssueNonQuery(sql);
        }

        /// <summary>
        /// Updates the actual cycle numbers for the illumina run.
        /// If a specific reads's cycle value is -1 no update is performed.
        /// Also sets the planned cycle numbers for matching batches where these values are not set before in DB.
        /// </summary>
        /// <param name="runId"></param>
        /// <param name="cycles">Use -1 to indicate that this value should not be updated</param>
        /// <param name="indexCycles">Use -1 to indicate that this value should not be updated</param>
        /// <param name="pairedCycles">Use -1 to indicate that this value should not be updated</param>
        public void UpdateRunCycles(string runId, int cycles, int indexCycles, int pairedCycles)
        {
            string sql = string.Format("UPDATE jos_aaailluminarun SET cycles=IFNULL(cycles, IF('{0}'>=0,'{0}',cycles)), " +
                                        "indexcycles=IFNULL(indexcycles, IF('{1}'>=0,'{1}',indexcycles)), " +
                                        "pairedcycles=IFNULL(pairedcycles, IF('{2}'>=0,'{2}',pairedcycles)) " +
                                       "WHERE illuminarunid='{3}';",
                                       cycles, indexCycles, pairedCycles, runId);
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
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
            return mds;
        }

        public void UpdateMailTaskStatus(string id, string status)
        {
            string sql = string.Format("UPDATE jos_aaafqmailqueue SET status='{0}', time=NOW() WHERE id='{1}'", status, id);
            IssueNonQuery(sql);
        }

        public void AddToBackupQueue(string readFile, int priority)
        {
            string sql = string.Format("INSERT IGNORE INTO jos_aaabackupqueue (path, status, priority, time) VALUES ('{0}', 'inqueue', '{1}', NOW())", readFile, priority);
            IssueNonQuery(sql);
        }

        public void SetBackupStatus(string readFile, string status)
        {
            string sql = string.Format("UPDATE jos_aaabackupqueue SET status='{0}', time=NOW() WHERE path='{1}'", status, readFile);
            IssueNonQuery(sql);
        }

        public void RemoveFileToBackup(string readFile)
        {
            string sql = string.Format("DELETE FROM jos_aaabackupqueue WHERE path='{0}'", readFile);
            IssueNonQuery(sql);
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
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
            return waitingFiles;
        }

        /// <summary>
        /// Obtain a list of string values from a single column of a table
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        private List<string> GetStrings(string sql)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> result = new List<string>();
            while (rdr.Read())
                result.Add(rdr.GetString(0));
            rdr.Close();
            conn.Close();
            return result;
        }

        /// <summary>
        /// Find selected column from project table where some (other) column matches an SQL "LIKE" pattern.
        /// </summary>
        /// <param name="likeCol">The column to match</param>
        /// <param name="likeFilter">The LIKE-style filter</param>
        /// <param name="resultCol">The column to return</param>
        /// <returns></returns>
        public List<string> GetProjectColumn(string likeCol, string likeFilter, string resultCol)
        {
            string sql = string.Format("SELECT {0} FROM jos_aaaproject WHERE {1} LIKE '{2}'", resultCol, likeCol, likeFilter);
            return GetStrings(sql);
        }

        public string TryGetPrimerId(string primername)
        {
            string result = "NULL";
            if (primername == null || primername == "")
                return result;
            string sql = string.Format("SELECT id FROM jos_aaasequencingprimer WHERE primername='{0}'", primername);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            if (rdr.HasRows)
            {
                rdr.Read();
                result = rdr.GetInt32(0).ToString();
            }
            rdr.Close();
            conn.Close();
            return result;
        }

        public void InsertOrUpdateProject(ProjectDescription pd)
        {
            if (pd.SpikeMoleculeCount == 0)
                pd.SpikeMoleculeCount = Props.props.TotalNumberOfAddedSpikeMolecules;
            CultureInfo cult = new CultureInfo("sv-SE");
            string checkSql = "SELECT count(distinct(p.plateid)) AS nprojects, count(a.id) AS nresults FROM jos_aaaproject p " +
                              "LEFT JOIN jos_aaaanalysis a ON a.jos_aaaprojectid=p.id WHERE p.plateid='{0}'";
            string sql = string.Format(checkSql, pd.plateId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            bool projectExists = rdr.GetInt32(0) > 0;
            bool hasResults = rdr.GetInt32(1) > 0;
            rdr.Close();
            conn.Close();
            string seqPrimerId = TryGetPrimerId(pd.seqPrimer);
            string idxPrimerId = TryGetPrimerId(pd.idxPrimer);
            string pairedPrimerId = TryGetPrimerId(pd.pairedPrimer);
            if (hasResults || projectExists) // MAYBE HAS TO BE MORE CAREFUL IF THERE ARE RESULTS?
                sql = "UPDATE jos_aaaproject SET jos_aaacontactid='{0}', jos_aaamanagerid='{1}', jos_aaaclientid='{2}', " +
                "title='{3}', productiondate='{4}', platereference='{6}', species='{7}', tissue='{8}', " +
                "sampletype='{9}', collectionmethod='{10}', description='{14}', " +
                "protocol='{15}', barcodeset='{16}', labbookpage='{17}', " +
                "layoutfile='{18}', comment='{20}', user='{21}', spikemolecules='{22}', time=NOW(), " +
                "plannedseqcycles='{23}', plannedidxcycles='{24}', plannedpairedcycles='{25}', " +
                "seqprimerid={26}, idxprimerid={27}, pairedprimerid={28} " +
                "WHERE plateid='{5}'";
            else
                sql = "INSERT INTO jos_aaaproject (jos_aaacontactid, jos_aaamanagerid, jos_aaaclientid, " +
                "title, productiondate, plateid, platereference, species, tissue, sampletype, " +
                "collectionmethod, weightconcentration, fragmentlength, molarconcentration, description, " +
                "protocol, barcodeset, labbookpage, layoutfile, status, comment, user, spikemolecules, time, " +
                "plannedseqcycles, plannedidxcycles, plannedpairedcycles, seqprimerid, idxprimerid, pairedprimerid" +
                ") VALUES('{0}','{1}','{2}', " +
                         "'{3}','{4}','{5}','{6}','{7}','{8}','{9}'," +
                         "'{10}','{11}','{12}','{13}','{14}', " +
                         "'{15}','{16}','{17}','{18}','{19}','{20}','{21}','{22}', NOW(), " +
                         "'{23}','{24}','{25}', {26}, {27}, {28})";
            sql = string.Format(sql, pd.jos_aaacontactid, pd.jos_aaamanagerid, pd.jos_aaaclientid,
                 pd.title, pd.productionDate.ToString(cult), pd.plateId, pd.plateReference, pd.defaultSpecies, pd.tissue, pd.sampleType,
                 pd.collectionMethod, 0, 0, 0, pd.description,
                 pd.protocol, pd.barcodeSet, pd.labBookPage, pd.layoutFile, pd.status, pd.comment, Environment.UserName, pd.SpikeMoleculeCount,
                 pd.nSeqCycles, pd.nIdxCycles, pd.nPairedCycles, seqPrimerId, idxPrimerId, pairedPrimerId);
            IssueNonQuery(sql);
        }

        public void SetChipsProjectId(string plateid, List<Chip> chips)
        {
            int dbProjId = GetPlateInsertId(plateid);
            string chipids = string.Join("','", chips.ConvertAll(c => c.chipid).ToArray());
            string sql = string.Format("UPDATE jos_aaachip SET jos_aaaprojectid={0} WHERE chipid IN ('{1}')", dbProjId, chipids);
            Console.WriteLine(sql);
            IssueNonQuery(sql);
        }

        public int GetPlateInsertId(string plateId)
        {
            return GetInsertId(string.Format("SELECT id FROM jos_aaaproject WHERE plateid='{0}'", plateId));
        }
        private int GetInsertId(string sql)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int insertId = rdr.GetInt32(0);
            rdr.Close();
            conn.Close();
            return insertId;
        }

        public Cell GetCellFromChipWell(string chipid, string chipwell)
        {
            string whereClause = string.Format("LEFT JOIN jos_aaachip h ON h.id=jos_aaachipid WHERE h.chipid='{0}' AND chipwell='{1}'",
                                               chipid, chipwell);
            List<Cell> cells = GetCells(whereClause);
            return (cells.Count == 1)? cells[0] : null;
        }
        public List<Cell> GetCellsOfChip(string chipid)
        {
            string whereClause = string.Format("LEFT JOIN jos_aaachip h ON h.id=jos_aaachipid WHERE h.chipid='{0}' ORDER BY chipwell", chipid);
            return GetCells(whereClause);
        }
        public List<Cell> GetCells(string whereClause)
        {
            List<Cell> cells = new List<Cell>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT c.id, jos_aaachipid, chipwell, platewell, diameter, area, red, green, blue, valid FROM jos_aaacell c {0}";
            sql = string.Format(sql, whereClause);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                bool valid = (rdr.GetInt32(9) == 1);
                Cell cell = new Cell(rdr.GetInt32(0), rdr.GetInt32(1), rdr.GetString(2), rdr.GetString(3),
                                     rdr.GetDouble(4), rdr.GetDouble(5), rdr.GetInt32(6), rdr.GetInt32(7), rdr.GetInt32(8), valid);
                cells.Add(cell);
            }
            conn.Close();
            return cells;
        }

        public Dictionary<string, int> GetCellIdByPlateWell(string projectId)
        {
            Dictionary<string, int> cellIdByPlateWell = new Dictionary<string, int>();
            string sql = string.Format("SELECT platewell, c.id FROM jos_aaacell c LEFT JOIN jos_aaachip h ON c.jos_aaachipid=h.id " +
                                 "JOIN jos_aaaproject p ON h.jos_aaaprojectid=p.id WHERE plateid='{0}' ORDER BY platewell", projectId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIdByPlateWell.Add(rdr.GetString(0), rdr.GetInt32(1));
            conn.Close();
            return cellIdByPlateWell;
        }

        public void UpdatePlateWellOfCells(List<Cell> plateOrderedCells)
        {
            string sqlPat = "UPDATE jos_aaacell SET platewell='{0}' WHERE id='{1}'";
            foreach (Cell c in plateOrderedCells)
            {
                string sql = string.Format(sqlPat, c.platewell, c.id);
                IssueNonQuery(sql);
            }
        }

        public void GetCellAnnotationsByPlate(string projectId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("LEFT JOIN jos_aaachip h ON jos_aaachipid=h.id " +
                        "LEFT JOIN jos_aaaproject p ON h.jos_aaaprojectid=p.id WHERE p.plateid='{0}' ORDER BY platewell", projectId),
                out annotations, out annotationIndexes);
        }
        public void GetCellAnnotationsByChip(string chipId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("WHERE jos_aaachipid='{0}' ORDER BY chipwell", chipId),
                out annotations, out annotationIndexes);
        }
        public void GetCellAnnotations(string chipOrProjectWhereSql,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            annotationIndexes = new Dictionary<string, int>();
            int i = 0;
            annotationIndexes["Chip"] = i++;
            annotationIndexes["ChipWell"] = i++;
            annotationIndexes["Species"] = i++;
            annotationIndexes["Age"] = i++;
            annotationIndexes["Sex"] = i++;
            annotationIndexes["Tissue"] = i++;
            annotationIndexes["Treatment"] = i++;
            annotationIndexes["DonorID"] = i++;
            annotationIndexes["Weight"] = i++;
            annotationIndexes["Diameter"] = i++;
            annotationIndexes["Area"] = i++;
            annotationIndexes["Red"] = i++;
            annotationIndexes["Blue"] = i++;
            annotationIndexes["Green"] = i++;
            annotationIndexes["SpikeMolecules"] = i++;
            annotationIndexes["Valid"] = i++;
            annotationIndexes["Comments"] = i++;
            List<string> extraAnnotNames = GetCellAnnotationNames(chipOrProjectWhereSql);
            for (i = 0; i < extraAnnotNames.Count; i++)
                annotationIndexes[extraAnnotNames[i]] = annotationIndexes.Count;
            annotations = new Dictionary<string, string[]>(96);
            List<Cell> plateCells = GetCells(chipOrProjectWhereSql);
            Dictionary<int, Chip> chipsById = GetChipsById(plateCells);
            foreach (Cell cell in plateCells)
            {
                Chip chip = chipsById[cell.jos_aaachipid];
                string[] wellAnn = new string[annotationIndexes.Count];
                string plateWell = cell.platewell;
                i = 0;
                wellAnn[i++] = chip.chipid;
                wellAnn[i++] = cell.chipwell;
                wellAnn[i++] = chip.species;
                wellAnn[i++] = chip.age;
                wellAnn[i++] = chip.sex;
                wellAnn[i++] = chip.tissue;
                wellAnn[i++] = chip.treatment;
                wellAnn[i++] = chip.donorid;
                wellAnn[i++] = chip.weight;
                wellAnn[i++] = cell.diameter.ToString();
                wellAnn[i++] = cell.area.ToString();
                wellAnn[i++] = cell.red.ToString();
                wellAnn[i++] = cell.blue.ToString();
                wellAnn[i++] = cell.green.ToString();
                wellAnn[i++] = chip.spikemolecules.ToString();
                wellAnn[i++] = cell.valid? "Y" : "-";
                wellAnn[i++] = chip.comments;
                annotations[plateWell] = wellAnn;
            }
            string sqlPat = "SELECT c.platewell, name, value FROM jos_aaacellannotation a LEFT JOIN jos_aaacell c ON a.jos_aaacellid=c.id " +
                    string.Format("WHERE a.jos_aaacellid IN (SELECT jos_aaacell.id FROM jos_aaacell {0})", chipOrProjectWhereSql);
            string sql = string.Format(sqlPat, chipOrProjectWhereSql);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string plateWell = rdr.GetString(0);
                string name = rdr.GetString(1);
                string value = rdr.GetString(2);
                annotations[plateWell][annotationIndexes[name]] = value;
            }
            rdr.Close();
            conn.Close();
        }

        private static List<string> GetCellAnnotationNames(string chipOrProjectWhereSql)
        {
            string sql = "SELECT DISTINCT(name) FROM jos_aaacellannotation WHERE jos_aaacellid IN (SELECT jos_aaacell.id FROM jos_aaacell {0})";
            sql = string.Format(sql, chipOrProjectWhereSql);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> annotNames = new List<string>();
            while (rdr.Read())
                annotNames.Add(rdr.GetString(0));
            rdr.Close();
            conn.Close();
            return annotNames;
        }

        public Dictionary<int, Chip> GetChipsById(List<Cell> cells)
        {
            Dictionary<int, Chip> chipsById = new Dictionary<int, Chip>();
            foreach (Cell cell in cells)
            {
                if (!chipsById.ContainsKey(cell.jos_aaachipid))
                {
                    Chip chip = GetChipById(cell.jos_aaachipid);
                    chipsById[cell.jos_aaachipid] = chip;
                }
            }
            return chipsById;
        }

        public List<string> GetCellIds()
        {
            List<string> cellIds = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand("SELECT id FROM jos_aaacell", conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIds.Add(rdr.GetString(0));
            rdr.Close();
            conn.Close();
            return cellIds;
        }

        public List<string> GetLoadedChips()
        {
            List<string> loadedChips = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT DISTINCT(chipid) FROM jos_aaachip";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                loadedChips.Add(rdr.GetString(0));
            conn.Close();
            return loadedChips;
        }

        public Chip GetChipByChipId(string chipid)
        {
            return GetChip("WHERE chipid='" + chipid + "'");
        }
        public Chip GetChipById(int id)
        {
            return GetChip("WHERE id='" + id + "'");
        }
        public Chip GetChip(string whereClause)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT id, chipid, strtprotocol, datedissected, datecollected," +
                         " species, strain, donorid, age, sex, weight," +
                         " tissue, treatment, spikemolecules, jos_aaaprojectid, " +
                         " jos_aaaclientid, jos_aaacontactid, jos_aaamanagerid, comments FROM jos_aaachip {0};";
            sql = string.Format(sql, whereClause);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            Chip chip = null;
            if (rdr.Read())
            {
                int? jos_aaaprojectid = null;
                if (rdr[14] != DBNull.Value) jos_aaaprojectid = rdr.GetInt32(14);
                chip = new Chip(rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2), rdr.GetDateTime(3), rdr.GetDateTime(4),
                                rdr.GetString(5), rdr.GetString(6), rdr.GetString(7), rdr.GetString(8), rdr.GetString(9), rdr.GetString(10),
                                rdr.GetString(11), rdr.GetString(12), rdr.GetInt32(13), jos_aaaprojectid,
                                rdr.GetInt32(15), rdr.GetInt32(16), rdr.GetInt32(17), rdr.GetString(18));
                chip.cells = GetCellsOfChip(chip.chipid);
            }
            conn.Close();
            return chip;
        }

        public void InsertOrUpdateCellImage(CellImage ci)
        {
            int detectionValue = (ci.Detection == Detection.Yes) ? 1 : (ci.Detection == Detection.No) ? -1 : 0;
            string sql = "INSERT INTO jos_aaacellimage (jos_aaacellid, reporter, marker, detection, relativepath) " +
                               "VALUES ({0},'{1}','{2}','{3}','{4}') " +
                         "ON DUPLICATE KEY UPDATE marker='{2}',detection='{3}',relativepath='{4}';";
            sql = string.Format(sql, ci.CellID, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void InsertOrUpdateCell(Cell c)
        {
            int validValue = c.valid ? 1 : 0;
            string sql = "INSERT INTO jos_aaacell (jos_aaachipid, chipwell, diameter, area, red, green, blue, valid) " +
                         "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}') " +
                         "ON DUPLICATE KEY UPDATE diameter='{2}',area='{3}',red='{4}',green='{5}',blue='{6}',valid='{7}'";
            sql = string.Format(sql, c.jos_aaachipid, c.chipwell, c.diameter, c.area, c.red, c.green, c.blue, validValue);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            string lastIdSql = string.Format("SELECT id FROM jos_aaacell WHERE jos_aaachipid='{0}' AND chipwell='{1}'",
                                             c.jos_aaachipid, c.chipwell);
            cmd = new MySqlCommand(lastIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int cellId = int.Parse(rdr["id"].ToString());
            conn.Close();
            foreach (CellImage ci in c.cellImages)
            {
                ci.CellID = cellId;
                InsertOrUpdateCellImage(ci);
            }
            foreach (CellAnnotation ca in c.cellAnnotations)
            {
                ca.CellID = cellId;
                InsertOrUpdateCellAnnotation(ca);
            }
        }

        public void InsertOrUpdateCellAnnotation(CellAnnotation ca)
        {
            string sql = "INSERT INTO jos_aaacellannotation (id, name, value) " +
                               "VALUES ({0},'{1}','{2}') " +
                         "ON DUPLICATE KEY UPDATE value='{2}';";
            sql = string.Format(sql, ca.CellID, ca.Name, ca.Value);
            IssueNonQuery(sql);
        }

    }
}
