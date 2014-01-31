using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using C1;

namespace Linnarsson.Strt
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
        public string layoutFile { get; set; }
        public string build { get; set; }
        public string annotation { get; set; }
        public string variant { get; set; }
        public int gene5PrimeExtension { get; set; }
        public List<LaneInfo> laneInfos { get; set; }
        public string analysisId { get; set; }
        public List<ResultDescription> resultDescriptions { get; set; }

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
        public string contact { get; set; }
        public string manager { get; set; }
        public string client { get; set; }

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
                          bool rpkm, int spikeMoleculeCount)
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
            this.SpikeMoleculeCount = spikeMoleculeCount;
            this.analysisId = analysisId;
            this.resultDescriptions = new List<ResultDescription>();
        }

        /// <summary>
        /// Constructor for inserting new projects
        /// </summary>
        public ProjectDescription(string contact, string manager, string client, 
                    string title, DateTime productiondate, string plateid, string platereference, string species,
                    string tissue, string sampletype, string collectionmethod, string description, string protocol,
                    string barcodeSet, string labbookpage, string layoutFile, string comment, int spikeMoleculeCount)
        {
            this.contact = contact;
            this.manager = manager;
            this.client = client;
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
        public Person(int id, string name)
        {
            this.id = id;
            this.name = name;
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
            connectionString = string.Format("server={0};uid=cuser;pwd=3pmknHQyl;database=joomla;Connect Timeout=300;Charset=utf8;",
                               Props.props.MySqlServerIP);
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

        /// <summary>
        /// Get all projects for every barcode set that have been queued for analysis in given lane.
        /// </summary>
        /// <param name="runNo"></param>
        /// <param name="laneNo"></param>
        /// <returns>dictonary from each barcodeset to the its projects</returns>
        public Dictionary<string, List<string>> GetProjectsByBarcodeSets(string runNo, string laneNo)
        {
            string sql = "SELECT p.plateid, barcodeset FROM jos_aaaproject p " +
                         "LEFT JOIN jos_aaaanalysis a ON a.jos_aaaprojectid = p.id" +
                         "LEFT JOIN jos_aaaanalysislane al ON al.jos_aaaanalysisid = a.id " +
                         "LEFT JOIN jos_aaalane l ON l.id = al.jos_aaalaneid " +
                         "LEFT JOIN jos_aaailluminarun r ON r.id = l.jos_aaailluminarunid " +
                         "WHERE r.runno = {0} AND laneno = {1}";
            sql = string.Format(sql, runNo, laneNo);
            Dictionary<string, List<string>> projectsByBc = new Dictionary<string, List<string>>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string projectId = rdr["plateid"].ToString();
                string barcodeSetName = rdr["barcodeset"].ToString();
                if (!projectsByBc.ContainsKey(barcodeSetName))
                    projectsByBc[barcodeSetName] = new List<string>();
                projectsByBc[barcodeSetName].Add(projectId);
            }
            rdr.Close();
            conn.Close();
            return projectsByBc;
        }

        private List<ProjectDescription> GetProjectDescriptions(string whereClause)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            List<ProjectDescription> pds = new List<ProjectDescription>();
            string sql = "SELECT a.id, a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.emails, " +
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
            int spikeMolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
            while (rdr.Read())
            {
                string analysisId = rdr["id"].ToString();
                if (currAnalysisId != "" && analysisId != currAnalysisId)
                {
                    pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                    emails, defaultBuild, variant, currAnalysisId, rpkm, spikeMolecules));
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
            }
            if (currAnalysisId != "") pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus,
                                                                        emails, defaultBuild, variant, currAnalysisId, rpkm, spikeMolecules));
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

        private static IEnumerable<Person> IterDBPersons(string table, string field)
        {
            string sql = string.Format("SELECT id, {0} FROM {1}", field, table);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                int id = rdr.GetInt32(0);
                string name = rdr.GetString(1).Trim().ToLower();
                yield return new Person(id, name);
            }
            rdr.Close();
            conn.Close();
            yield break;
        }

        private static Person defaultPerson = new Person(1, "?");
        public Person TryGetPerson(string table, string field, string person)
        {
            return TryGetPerson(table, field, person, defaultPerson);
        }
        /// <summary>
        /// Try to get a unique person from table by matching a (partial) name/initials with a table column
        /// </summary>
        /// <param name="table"></param>
        /// <param name="field"></param>
        /// <param name="person">Full or partial person's name</param>
        /// <param name="failPerson">Return value if no unique match was found</param>
        /// <returns></returns>
        public Person TryGetPerson(string table, string field, string person, Person failPerson)
        {
            List<Person> exactMatch = new List<Person>();
            List<Person> lastMatch = new List<Person>();
            List<Person> firstMatch = new List<Person>();
            List<Person> initialsMatch = new List<Person>();
            person = person.Trim().ToLower();
            string last = person, first = person, initials = "#.#";
            string[] parts = person.Split(' ');
            if (parts.Length >= 2)
            {
                first = parts[0];
                last = parts[parts.Length - 1];
                initials = string.Join("", Array.ConvertAll(parts, n => n[0].ToString()));
            }
            foreach (Person p in IterDBPersons(table, field))
            {
                if (p.name == person) exactMatch.Add(p);
                if (p.name.StartsWith(first)) firstMatch.Add(p);
                if (p.name.EndsWith(last)) lastMatch.Add(p);
                string nameInitials = string.Join("", Array.ConvertAll(p.name.Split(' '), n => n[0].ToString()));
                if (person == nameInitials || initials == p.name) initialsMatch.Add(p);
            }
            return (exactMatch.Count == 1) ? exactMatch[0] : (lastMatch.Count == 1) ? lastMatch[0] :
                (firstMatch.Count == 1) ? firstMatch[0] : (initialsMatch.Count == 1) ? initialsMatch[0] : failPerson;
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
            int contactId = TryGetPerson("jos_aaacontact", "contactperson", pd.contact).id;
            int managerId = TryGetPerson("jos_aaamanager", "person", pd.manager).id;
            int clientId = TryGetPerson("jos_aaaclient", "principalinvestigator", pd.client).id;
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
            sql = string.Format(sql, contactId, managerId, clientId,
                 pd.title, pd.productionDate.ToString(cult), pd.plateId, pd.plateReference, pd.defaultSpecies, pd.tissue, pd.sampleType,
                 pd.collectionMethod, 0, 0, 0, pd.description,
                 pd.protocol, pd.barcodeSet, pd.labBookPage, pd.layoutFile, pd.status, pd.comment, Environment.UserName, pd.SpikeMoleculeCount,
                 pd.nSeqCycles, pd.nIdxCycles, pd.nPairedCycles, seqPrimerId, idxPrimerId, pairedPrimerId);
            IssueNonQuery(sql);
        }

        private static int GetLastInsertId(string table)
        {
            string sql = "SELECT MAX(id) FROM " + table;
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int lastInsertId = rdr.GetInt32(0);
            rdr.Close();
            conn.Close();
            return lastInsertId;
        }

        /*
         * public void AutoStartC1Analyses(string newlyCopiedRunId)
                {
                    string sql = "SELECT p.plateid, b.jos_aaaprojectid AS projectid, l.laneno, l.id AS laneid, a.id AS analysisid," +
                                  " p.barcodeset, p.species FROM jos_aaalane l JOIN jos_aaailluminarun r ON l.jos_aaailluminarunid=r.id" +
                                  " LEFT JOIN jos_aaaanalysis a ON a.jos_aaaprojectid=b.jos_aaaprojectid" +
                                  " JOIN jos_aaaproject p ON p.id=l.jos_aaaprojectid1 OR p.id=l.jos_aaaprojectid2" +
                                  " WHERE r.illuminarunid='{0}' AND a.id IS NULL AND mid(p.plateid, 1, 3)='{1}';";
                    sql = string.Format(sql, newlyCopiedRunId, C1Props.C1ProjectPrefix);
                    MySqlConnection conn = new MySqlConnection(connectionString);
                    conn.Open();
                    MySqlCommand cmd = new MySqlCommand(sql, conn);
                    MySqlDataReader rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        string projectId = rdr["projectid"].ToString();
                        string laneId = rdr["laneid"].ToString();
                        string species = rdr["species"].ToString();
                        string barcodes = rdr["barcodeset"].ToString();
                        string asql = "INSERT INTO jos_aaaanalysis " +
                                      "(projectid, transcript_db_version, transcript_variant, rpkm, emails, status, lanecount, comment, time, user) " +
                                      "VALUES ('{0}', '{1}', '{2}', 0, '{3}', 'inqueue', 1, 'autoanalysis', NOW(), 'system');";
                        asql = string.Format(asql, projectId, C1Props.props.AutoAnalysisBuild, C1Props.props.AutoAnalysisBuildVariants,
                                             C1Props.props.AutoAnalysisMailRecepients);
                        IssueNonQuery(asql);
                        int analysisId = GetLastInsertId("jos_aaaanalysis");
                        string lsql = "INSERT INTO jos_aaaanalysislane (jos_aaaanalysisid, jos_aaalaneid) VALUES ('{0}', '{1}');";
                        lsql = string.Format(lsql, analysisId, laneId);
                        IssueNonQuery(lsql);
                    }
                    rdr.Close();
                    conn.Close();

                }
        */
    }
}
