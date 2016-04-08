using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Linnarsson.C1;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Replacement for ProjectDB() for use with the updated joomla3 database (in a Cloud DB+Processor setting)
    /// </summary>
    public class SampleChipDB : Linnarsson.Dna.IDB, Linnarsson.C1.IExpressionDB
    {
        private static string connectionString;

        public SampleChipDB()
        {
            connectionString = Props.props.MySqlServerConnectionString;
        }

        /// <summary>
        /// Handle Exception:s upstream (write to logfile and/or email operator)
        /// </summary>
        /// <param name="sql"></param>
        private void IssueNonQuery(string sql)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        private int InsertAndGetLastId(string sql, string tableName)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            string lastIdSql = string.Format("SELECT MAX(id) AS LastId FROM {0}", tableName);
            cmd = new MySqlCommand(lastIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int lastId = int.Parse(rdr["LastId"].ToString());
            conn.Close();
            return lastId;
        }

        private int nextInQueue = 0;
        public void ResetQueue()
        {
            nextInQueue = 0;
        }
        public ProjectDescription GetNextProjectInQueue()
        {
            ProjectDescription pd = null;
            List<ProjectDescription> queue = GetProjectDescriptions();
            if (nextInQueue < queue.Count)
            {
                pd = queue[nextInQueue++];
            }
            return pd;
        }

        private List<ProjectDescription> GetProjectDescriptions()
        {
            List<ProjectDescription> pds = new List<ProjectDescription>();
            string sql = "SELECT a.id, CONCAT_WS('_', h.chipid, s.name) AS analysisname, a.comment, a.user," + 
                         " a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.readdir, a.emails, " +
                         " chipid, s.name AS sample, mh.barcodeset, h.spikemolecules, s.species, h.layoutfile, a.status, a.aligner, " +
                         " r.illuminarunid AS runid, GROUP_CONCAT(l.laneno ORDER BY l.laneno) AS lanenos " +
                         "FROM {0}aaaanalysis a " +
                         "LEFT JOIN {0}aaachip h ON a.{0}aaachipid = h.id " +
                         "LEFT JOIN {0}aaasample s ON a.{0}aaasampleid = s.id " +
                         "JOIN sccf_aaamixchip mh ON mh.sccf_aaachipid = h.id " +
                         "RIGHT JOIN {0}aaaanalysislane al ON a.id = al.{0}aaaanalysisid " +
                         "LEFT JOIN {0}aaalane l ON al.{0}aaalaneid = l.id " +
                         "LEFT JOIN {0}aaailluminarun r ON l.{0}aaailluminarunid = r.id " +
                         "WHERE a.status=\"{1}\" " +
                         "AND h.id NOT IN (SELECT {0}aaachipid FROM {0}aaaanalysis WHERE status=\"{2}\") " +
                         "GROUP BY a.id, runid ORDER BY a.id DESC, runid";
            sql = string.Format(sql, Props.props.DBPrefix, ProjectDescription.STATUS_INQUEUE, ProjectDescription.STATUS_PROCESSING);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> laneInfos = new List<string>();
            string analysisId = "", analysisname = "", chipid = "", sample = "", barcodeset = "", defaultSpecies = "", user = "",
                   layoutfile = "", status = "", emails = "", defaultBuild = "", variant = "", aligner = "", rpkm = "", comment = "";
            int readdir = 1;
            int spikemolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
            while (rdr.Read())
            {
                string nextAnalysisId = rdr["id"].ToString();
                if (analysisId != "" && nextAnalysisId != analysisId)
                {
                    pds.Add(new ProjectDescription(analysisname, chipid, sample, barcodeset, defaultSpecies, laneInfos, layoutfile, status, emails,
                                                  defaultBuild, variant, aligner, analysisId, rpkm, spikemolecules, readdir, comment, user));
                    laneInfos = new List<string>();
                }
                analysisId = nextAnalysisId;
                string laneInfo = string.Format("{0}:{1}", rdr["runid"], rdr.GetString("lanenos").Replace(",", ""));
                laneInfos.Add(laneInfo);
                analysisname = rdr["analysisname"].ToString();
                chipid = rdr["chipid"].ToString();
                sample = rdr["sample"].ToString();
                barcodeset = rdr["barcodeset"].ToString();
                defaultSpecies = rdr["species"].ToString();
                layoutfile = rdr["layoutfile"].ToString();
                status = rdr["status"].ToString();
                aligner = rdr["aligner"].ToString();
                emails = rdr["emails"].ToString();
                defaultBuild = rdr["transcript_db_version"].ToString();
                variant = rdr["transcript_variant"].ToString();
                spikemolecules = int.Parse(rdr["spikemolecules"].ToString());
                rpkm = rdr["rpkm"].ToString();
                readdir = rdr.GetInt32("readdir");
                comment = rdr.GetString("comment");
                user = rdr.GetString("user");
            }
            if (analysisId != "") 
                pds.Add(new ProjectDescription(analysisname, chipid, sample, barcodeset, defaultSpecies, laneInfos, layoutfile, status, emails,
                                               defaultBuild, variant, aligner, analysisId, rpkm, spikemolecules, readdir, comment, user));
            rdr.Close();
            conn.Close();
            return pds;
        }

        public void UpdateAnalysisStatus(string analysisId, string status)
        {
            string sql = string.Format("UPDATE {0}aaaanalysis SET status=\"{1}\", time=NOW() WHERE id=\"{2}\";",
                                       Props.props.DBPrefix, status, analysisId);
            IssueNonQuery(sql);
        }

        /// <summary>
        /// Check that no parallel process just started the analysis, then change status to 'extracting'
        /// </summary>
        /// <param name="pd"></param>
        /// <returns>true if the analysis was successfully 'caught' by this process</returns>
        public bool SecureStartAnalysis(ProjectDescription pd)
        {
            bool success = false;
            string sql = string.Format("SELECT * FROM {0}aaaanalysis WHERE id=\"{1}\" AND {0}aaachipid IN " +
                                       "(SELECT {0}aaachipid FROM {0}aaaanalysis a2 WHERE status IN (\"{2}\",\"{3}\",\"{4}\",\"{5}\") );",
                         Props.props.DBPrefix, pd.analysisid, 
                         ProjectDescription.STATUS_PROCESSING, ProjectDescription.STATUS_EXTRACTING,
                         ProjectDescription.STATUS_ALIGNING, ProjectDescription.STATUS_ANNOTATING);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            bool projectAlreadyProcessing = rdr.HasRows;
            rdr.Close();
            if (!projectAlreadyProcessing)
            {
                sql = string.Format("UPDATE {0}aaaanalysis SET status=\"{1}\", time=NOW() WHERE id=\"{2}\" AND status=\"{3}\"",
                            Props.props.DBPrefix, ProjectDescription.STATUS_EXTRACTING, pd.analysisid, ProjectDescription.STATUS_INQUEUE);
                cmd = new MySqlCommand(sql, conn);
                int nRowsAffected = cmd.ExecuteNonQuery();
                if (nRowsAffected > 0)
                {
                    success = true;
                    pd.status = ProjectDescription.STATUS_EXTRACTING;
                }
            }
            conn.Close();
            return success;
        }

        public void PublishResults(ProjectDescription pd)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql;
            bool firstResult = true;
            foreach (ResultDescription resultDescr in pd.resultDescriptions)
            {
                if (firstResult)
                {
                    sql = string.Format("UPDATE {0}aaaanalysis " +
                            "SET extraction_version=\"{1}\", annotation_version=\"{2}\", genome=\"{3}\", transcript_db_version=\"{4}\", " +
                            "transcript_variant=\"{5}\", resultspath=\"{6}\", status=\"{7}\", time=NOW() WHERE id=\"{8}\" ",
                            Props.props.DBPrefix,
                            pd.extractionVersion, pd.annotationVersion, resultDescr.build, resultDescr.annotAndDate,
                            resultDescr.variants, resultDescr.resultFolder, pd.status, pd.analysisid);
                }
                else
                {
                    sql = string.Format("INSERT INTO {0}aaaanalysis " +
                               "({0}aaachipid, {0}aaasampleid, extraction_version, aligner, annotation_version, genome, comment, emails, user, " +
                                "transcript_db_version, transcript_variant, lanecount, resultspath, status, rpkm, time) " +
                               "VALUES (\"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", \"{14}\", \"{15}\", NOW());",
                               Props.props.DBPrefix, pd.plateid, pd.sample, pd.extractionVersion, pd.aligner,
                               pd.annotationVersion, resultDescr.build, pd.comment, pd.emails, pd.user,
                               resultDescr.annotAndDate, resultDescr.variants, pd.laneCount, resultDescr.resultFolder, pd.status, pd.rpkm);
                }
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                firstResult = false;
            }
            foreach (LaneInfo laneInfo in pd.laneInfos)
            {
                if (laneInfo.nValidReads == 0)
                    continue; // Has been extracted earlier - no data to update
                sql = string.Format(string.Format("UPDATE {0}aaalane SET strtyield=\"{1}\" WHERE laneno=\"{2}\" AND " +
                                        "{0}aaailluminarunid= (SELECT id FROM {0}aaailluminarun WHERE illuminarunid=\"{3}\") ",
                                        Props.props.DBPrefix, laneInfo.nValidReads, laneInfo.laneNo, laneInfo.illuminaRunId));
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        /// <summary>
        /// Updates the database with info on number of total and passed filter reads for a lane
        /// </summary>
        /// <param name="runId">The (8 character long) Illumina flowcell id</param>
        /// <param name="nReads">Total number of reads</param>
        /// <param name="nPFReads">Number of reads passed Illumina filter</param>
        /// <param name="lane">Lane number</param>
        public void SetIlluminaYield(string runId, uint nReads, uint nPFReads, int lane)
        {
            string sql = string.Format(string.Format("UPDATE {0}aaalane SET yield=\"{1}\", pfyield=\"{2}\" WHERE laneno=\"{3}\" AND " +
                                    "{0}aaailluminarunid= (SELECT id FROM {0}aaailluminarun WHERE illuminarunid=\"{4}\") ",
                                    Props.props.DBPrefix, nReads, nPFReads, lane, runId));
            IssueNonQuery(sql);
        }

        /// <summary>
        /// Start the bcl copy/collection of a run. Checks that no other process is already copying the run.
        /// </summary>
        /// <param name="runId">Either a run number for the old machine or a cell Id for the new</param>
        /// <param name="runNo">A run number to set.</param>
        /// <param name="runDate">Date of the run (extracted from filename)</param>
        /// <returns>true if no other process is copying the run and the status update/insert was successful</returns>
        public bool SecureStartRunCopy(string runId, int runNo, string runDate)
        {
            bool success = false;
            string sql = string.Format("SELECT * FROM {0}aaailluminarun WHERE illuminarunid='{1}' AND status='copying';",
                                       Props.props.DBPrefix, runId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            bool alreadyCopying = rdr.HasRows;
            rdr.Close();
            if (!alreadyCopying)
            {
                // Below SQL will update with status and runno if user has defined the run, else add a new run as
                // well as defining 8 new lanes by side-effect of a MySQL trigger
                sql = string.Format("INSERT INTO {0}aaailluminarun (status, runno, illuminarunid, rundate, time, user) " +
                                    "VALUES ('copying', '{1}', '{2}', '{3}', NOW(), '{4}') " +
                                    "ON DUPLICATE KEY UPDATE status='copying', runno='{1}';",
                                            Props.props.DBPrefix, runNo, runId, runDate, "system");
                cmd = new MySqlCommand(sql, conn);
                int nRowsAffected = cmd.ExecuteNonQuery();
                if (nRowsAffected > 0)
                    success = true;
            }
            conn.Close();
            return success;
        }

        /// <summary>
        /// Update with status and runno if user has defined the run, else add a new run as
        /// well as defining 8 new lanes by side-effect of a MySQL trigger
        /// </summary>
        /// <param name="runId"></param>
        /// <param name="status"></param>
        /// <param name="runNo"></param>
        public void UpdateRunStatus(string runId, string status, int runNo)
        {
            string sql = string.Format("UPDATE {0}aaailluminarun SET status='{1}', runno='{2}', time=NOW() WHERE illuminarunid='{3}';",
                                       Props.props.DBPrefix, status, runNo, runId);
            IssueNonQuery(sql);
        }

        public void ReportReadFileResult(string runId, int read, ReadFileResult r)
        {
            AddToBackupQueue(r.PFPath, 10);
            if (r.read == 1)
                SetIlluminaYield(runId, r.nReads, r.nPFReads, r.lane);
            UpdateRunCycles(runId, read, (int)r.readLen);
        }

        /// <summary>
        /// Updates the actual cycle numbers for the illumina run.
        /// If a specific reads's cycle value is -1 no update is performed.
        /// </summary>
        /// <param name="runId"></param>
        /// <param name="cycles">Use -1 to indicate that this value should not be updated</param>
        public void UpdateRunCycles(string runId, int read, int cycles)
        {
            string col = (read == 1) ? "cycles" : ((read == 2) ? "indexcycles" : "pairedcycles");
            string sql = string.Format("UPDATE {0}aaailluminarun SET {1}=IFNULL({1}, IF('{2}'>=0,'{2}',{1})) WHERE illuminarunid='{3}';",
                                       Props.props.DBPrefix, col, cycles, runId);
            IssueNonQuery(sql);
        }

        public Dictionary<string, List<MailTaskDescription>> GetQueuedMailTasksByEmail()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            Dictionary<string, List<MailTaskDescription>> mds = new Dictionary<string, List<MailTaskDescription>>();
            string sql = "SELECT id, runno, laneno, email, status FROM {0}aaafqmailqueue WHERE status='inqueue' ORDER BY email";
            sql = string.Format(sql, Props.props.DBPrefix);
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
            conn.Close();
            return mds;
        }

        public void UpdateMailTaskStatus(string id, string status)
        {
            string sql = string.Format("UPDATE {0}aaafqmailqueue SET status='{1}', time=NOW() WHERE id='{2}'",
                Props.props.DBPrefix, status, id);
            IssueNonQuery(sql);
        }

        public void AddToBackupQueue(string readFile, int priority)
        {
            string sql = string.Format("INSERT IGNORE INTO {0}aaabackupqueue (path, status, priority, time) VALUES ('{1}', 'inqueue', '{2}', NOW())",
                Props.props.DBPrefix, readFile, priority);
            IssueNonQuery(sql);
        }

        public void SetBackupStatus(string readFile, string status)
        {
            string sql = string.Format("UPDATE {0}aaabackupqueue SET status='{1}', time=NOW() WHERE path='{2}'",
                Props.props.DBPrefix, status, readFile);
            IssueNonQuery(sql);
        }

        public List<string> GetWaitingFilesToBackup()
        {
            List<string> waitingFiles = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            string sql = string.Format("SELECT path FROM {0}aaabackupqueue WHERE status='inqueue' OR status='missing' ORDER BY priority, id",
                Props.props.DBPrefix);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string file = rdr["path"].ToString();
                waitingFiles.Add(file);
            }
            rdr.Close();
            conn.Close();
            return waitingFiles;
        }

        public Cell GetCellFromChipWell(string chipid, string chipwell)
        {
            string whereClause = string.Format("LEFT JOIN {0}aaachip h ON h.id=c.{0}aaachipid WHERE h.chipid='{1}' AND c.chipwell='{2}'",
                                               Props.props.DBPrefix, chipid, chipwell);
            List<Cell> cells = GetCells(whereClause);
            return (cells.Count == 1) ? cells[0] : null;
        }
        private List<Cell> GetCellsOfChip(string chipid)
        {
            string whereClause = string.Format("LEFT JOIN {0}aaachip h ON h.id=c.{0}aaachipid WHERE h.chipid='{1}' ORDER BY c.chipwell",
                                               Props.props.DBPrefix, chipid);
            return GetCells(whereClause);
        }
        private List<Cell> GetCells(string whereClause)
        {
            List<Cell> cells = new List<Cell>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT c.id, c.{0}aaachipid, c.{0}aaasampleid, c.chipwell, diameter, area, red, green, blue, c.valid FROM {0}aaacell c {1}";
            sql = string.Format(sql, Props.props.DBPrefix, whereClause);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                bool valid = (rdr.GetInt32(9) == 1);
                Cell cell = new Cell(rdr.GetInt32(0), rdr.GetInt32(1), rdr.GetInt32(2), rdr.GetString(3), rdr.GetString(3),
                                     rdr.GetDouble(4), rdr.GetDouble(5), rdr.GetInt32(6), rdr.GetInt32(7), rdr.GetInt32(8), valid);
                cells.Add(cell);
            }
            conn.Close();
            return cells;
        }

        public Dictionary<string, int> GetWell2CellIdMapping(string chipid)
        {
            Dictionary<string, int> well2CellId = new Dictionary<string, int>();
            string sql = "SELECT chipwell, id FROM {0}aaacell WHERE {0}aaachipid IN (SELECT id FROM {0}aaachip WHERE chipid='{1}') ORDER BY chipwell";
            sql = string.Format(sql, Props.props.DBPrefix, chipid);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                well2CellId.Add(rdr.GetString(0), rdr.GetInt32(1));
            conn.Close();
            return well2CellId;
        }

        public void GetCellAnnotations(string chipid, out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT DISTINCT(name) FROM {0}aaacellannotation WHERE {0}aaacellid IN (SELECT id FROM {0}aaacell WHERE chipid='{1}')";
            sql = string.Format(sql, Props.props.DBPrefix, chipid);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> extraAnnotNames = new List<string>();
            while (rdr.Read())
                extraAnnotNames.Add(rdr.GetString(0));
            rdr.Close();
            sql = "SELECT c.chipwell, h.chipid, diameter, area, green, red, blue, c.valid, " +
                " h.protocol, h.spikemolecules, h.datecollected, h.comment, " +
                " s.name AS sample, s.datedissected, s.species, s.strain, s.animalid, s.age, s.sex, s.weight, s.tissue, s.treatment " +
                "FROM {0}aaacell c JOIN {0}aaachip h ON c.{0}aaachipid = h.id " +
                "JOIN {0}aaasample s ON c.{0}aaasampleid=s.id WHERE c.chipid='{1}' ORDER BY c.chipwell";
            sql = string.Format(sql, Props.props.DBPrefix, chipid);
            annotationIndexes = new Dictionary<string, int>();
            annotations = new Dictionary<string, string[]>(96);
            cmd = new MySqlCommand(sql, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                if (annotationIndexes.Count == 0)
                {
                    for (int i = 0; i < rdr.FieldCount; i++)
                        annotationIndexes[rdr.GetName(i)] = i;
                    foreach (string annotName in extraAnnotNames)
                        annotationIndexes[annotName] = annotationIndexes.Count;
                }
                string[] wellAnn = new string[annotationIndexes.Count];
                for (int i = 0; i < rdr.FieldCount; i++)
                    wellAnn[i] = rdr.GetString(i);
                string chipwell = rdr.GetString(0);
                annotations[chipwell] = wellAnn;
            }
            rdr.Close();
            string sqlPat = "SELECT c.chipwell, name, value FROM {0}aaacellannotation a LEFT JOIN {0}aaacell c ON a.{0}aaacellid=c.id " +
                            "WHERE a.{0}aaacellid IN (SELECT id FROM {0}aaacell WHERE chipid='{1}')";
            sql = string.Format(sqlPat, Props.props.DBPrefix, chipid);
            cmd = new MySqlCommand(sql, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string chipwell = rdr.GetString(0);
                string annotName = rdr.GetString(1);
                string value = rdr.GetString(2);
                annotations[chipwell][annotationIndexes[annotName]] = value;
            }
            rdr.Close();
            conn.Close();
        }

        public void InsertOrUpdateCellImage(CellImage ci)
        {
            int detectionValue = (ci.Detection == Detection.Yes) ? 1 : (ci.Detection == Detection.No) ? -1 : 0;
            string sql = "INSERT INTO {0}aaacellimage ({0}aaacellid, reporter, marker, detection, relativepath) " +
                               "VALUES ({1},'{2}','{3}','{4}','{5}') " +
                         "ON DUPLICATE KEY UPDATE marker='{3}',detection='{4}',relativepath='{5}';";
            sql = string.Format(sql, Props.props.DBPrefix, ci.jos_aaacellid, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void InsertOrUpdateCell(Cell c)
        {
            int validValue = c.valid ? 1 : 0;
            string sql = "INSERT INTO {0}aaacell ({0}aaachipid, chipwell, diameter, area, red, green, blue, valid) " +
                         "VALUES ('{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}') " +
                         "ON DUPLICATE KEY UPDATE diameter='{3}',area='{4}',red='{5}',green='{6}',blue='{7}',valid='{8}'";
            sql = string.Format(sql, Props.props.DBPrefix, c.jos_aaachipid, c.chipwell, c.diameter, c.area,
                                c.red, c.green, c.blue, validValue);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            string lastIdSql = string.Format("SELECT id FROM {0}aaacell WHERE {0}aaachipid='{1}' AND chipwell='{2}'",
                                             Props.props.DBPrefix, c.jos_aaachipid, c.chipwell);
            cmd = new MySqlCommand(lastIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int cellId = int.Parse(rdr["id"].ToString());
            conn.Close();
            foreach (CellImage ci in c.cellImages)
            {
                ci.jos_aaacellid = cellId;
                InsertOrUpdateCellImage(ci);
            }
            foreach (CellAnnotation ca in c.cellAnnotations)
            {
                ca.jos_aaacellid = cellId;
                InsertOrUpdateCellAnnotation(ca);
            }
        }

        public void InsertOrUpdateCellAnnotation(CellAnnotation ca)
        {
            string sql = "INSERT INTO {0}aaacellannotation (id, name, value) " +
                               "VALUES ({1},'{2}','{3}') " +
                         "ON DUPLICATE KEY UPDATE value='{2}';";
            sql = string.Format(sql, Props.props.DBPrefix, ca.jos_aaacellid, ca.Name, ca.Value);
            IssueNonQuery(sql);
        }

        public int GetIdOfChip(string chipid)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT id FROM {0}aaachip WHERE chipid='{1}'", Props.props.DBPrefix, chipid);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            int result = -1;
            if (rdr.HasRows)
            {
                rdr.Read();
                result = rdr.GetInt32(0);
            }
            rdr.Close();
            conn.Close();
            return result;
        }

        #region Stuff from C1DB for use in unified database:

        /// <summary>
        /// Returns most up-to-date Transcriptome data for the specified genome data
        /// </summary>
        /// <param name="buildVarAnnot">e.g. "mm10_sUCSC"</param>
        /// <returns>null if no match exists in database, or can not connect</returns>
        public Transcriptome GetTranscriptome(string buildVarAnnot)
        {
            Transcriptome t = null;
            string sql = string.Format("SELECT * FROM {0}aaatranscriptome WHERE name ='{1}' ORDER BY builddate DESC LIMIT 1",
                Props.props.DBPrefix, buildVarAnnot);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read()) t = new Transcriptome(rdr.GetInt32("id"), rdr.GetString("name"), rdr.GetString("organism"),
                                rdr.GetString("source"), rdr.GetString("genomefolder"), rdr.GetString("description"),
                                rdr.GetDateTime("builddate"), rdr.GetString("builderversion"), rdr.GetDateTime("analysisdate"),
                                rdr.GetString("annotationversion"));
            conn.Close();
            return t;
        }

        public IEnumerable<Transcript> IterTranscriptsFromDB(int transcriptomeId)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT * FROM {0}aaatranscript WHERE {0}aaatranscriptomeid='{1}' AND chromosome='CTRL' ORDER BY start",
                Props.props.DBPrefix, transcriptomeId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                yield return MakeTranscriptFromDBReader(rdr);
            sql = string.Format("SELECT * FROM {0}aaatranscript WHERE {0}aaatranscriptomeid='{1}' AND chromosome!='CTRL' " +
                                "AND LEFT(genename,2)!='r_' ORDER BY chromosome, start", Props.props.DBPrefix, transcriptomeId);
            rdr.Close();
            cmd = new MySqlCommand(sql, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
                yield return MakeTranscriptFromDBReader(rdr);
            rdr.Close();
            conn.Close();
        }

        public Dictionary<string, int> GetRepeatNamesToTranscriptIdsMap(string buildVarAnnot)
        {
            Dictionary<string, int> mapping = new Dictionary<string, int>();
            Transcriptome c1Trome = GetTranscriptome(buildVarAnnot);
            if (c1Trome == null)
                return mapping;
            string sql = string.Format("SELECT genename, id FROM {0}aaatranscript WHERE {0}aaatranscriptomeid='{1}' AND type='repeat';",
                                       Props.props.DBPrefix, c1Trome.TranscriptomeID.Value);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                mapping[rdr.GetString(0)] = rdr.GetInt32(1);
            rdr.Close();
            conn.Close();
            return mapping;
        }

        private static Transcript MakeTranscriptFromDBReader(MySqlDataReader rdr)
        {
            string uniqueName = rdr.GetString("genename");
            Match m = Regex.Match(uniqueName, "_v[0-9]+$");
            string geneName = (m.Success) ? uniqueName.Substring(0, m.Index) : uniqueName;
            Transcript t = new Transcript(rdr.GetInt32("id"), rdr.GetInt32("{0}aaatranscriptomeid"), rdr.GetInt32("exprblobidx"),
                                          rdr.GetString("name"), rdr.GetString("type"), geneName, uniqueName,
                                          rdr.GetString("entrezid"), rdr.GetString("description"),
                                          rdr.GetString("chromosome"), rdr.GetInt32("start"), rdr.GetInt32("end"),
                                          rdr.GetInt32("length"), rdr.GetChar("strand"), rdr.GetInt32("extension5prime"),
                                          rdr.GetString("exonstarts"), rdr.GetString("exonends"), rdr.GetString("starttoclosecutsite"));
            return t;
        }

        public void InsertTranscriptome(Transcriptome t)
        {
            CultureInfo cult = new CultureInfo("sv-SE");
            string sql = "INSERT INTO {9}aaatranscriptome (name, organism, source, genomefolder, description, " +
                                                    "builddate, builderversion, annotationversion, analysisdate) " +
                                 "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}')";
            sql = string.Format(sql, t.Name, t.Organism, t.Source, t.GenomeFolder, t.Description,
                    t.BuildDate.ToString(cult), t.BuilderVersion, t.AnnotationVersion, t.AnalysisDate.ToString(cult), Props.props.DBPrefix);
            int transcriptomeId = InsertAndGetLastId(sql, Props.props.DBPrefix + "aaatranscriptome");
            t.TranscriptomeID = transcriptomeId;
        }

        public void InsertChromosomePos(int transcriptomeId, string chrId, int startPos, int endPos)
        {
            string sql = "INSERT INTO {0}aaawigchrom ({0}aaatranscriptomeid, chromosome, genome_startpos, genome_endpos) " +
                                 "VALUES ('{0}','{1}','{2}','{3}')";
            sql = string.Format(sql, transcriptomeId, chrId, startPos, endPos);
            IssueNonQuery(sql);
        }

        public void InsertWig(int transcriptomeId, string chrId, int chrPos, int cellId, int count)
        {
            string sql = "INSERT INTO {0}aaawig ({0}aaacellid, genome_pos, molcount) " +
                         "SELECT {1}, {2} + genome_startpos, {3} FROM {0}aaawigchrom where {0}aaatranscriptomeid={4} and chromosome={5}";
            sql = string.Format(sql, Props.props.DBPrefix, cellId, chrPos, count, transcriptomeId, chrId);
            IssueNonQuery(sql);
        }

        public void InsertTranscript(Transcript t)
        {
            string description = MySqlHelper.EscapeString(t.Description);
            string sql = "INSERT INTO {16}aaatranscript ({16}aaatranscriptomeid, name, type, genename, entrezid, description, chromosome, " +
                                    "start, end, length, strand, extension5prime, exonstarts, exonends, " +
                                    "exprblobidx, starttoclosecutsite) " +
                                    "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}'," +
                                    "'{7}','{8}','{9}','{10}','{11}','{12}','{13}'," +
                                    "'{14}','{15}')";
            sql = string.Format(sql, t.TranscriptomeID, t.Name, t.Type, t.UniqueGeneName, t.EntrezID, description, t.Chromosome,
                                     t.Start, t.End, t.Length, t.Strand, t.Extension5Prime, t.ExonStarts, t.ExonEnds,
                                     t.ExprBlobIdx, t.StartToCloseCutSites, Props.props.DBPrefix);
            int newTranscriptId = InsertAndGetLastId(sql, Props.props.DBPrefix + "aaatranscript");
            t.TranscriptID = newTranscriptId;
            foreach (TranscriptAnnotation ta in t.TranscriptAnnotations)
            {
                ta.TranscriptID = t.TranscriptID.Value;
                InsertTranscriptAnnotation(ta);
            }
        }

        public bool UpdateTranscriptAnnotations(Transcript t)
        {
            string sql = "SELECT id FROM {0}aaatranscript WHERE {0}aaatranscriptomeid='{1}' AND type='{2}' " +
                              "AND genename='{3}' AND entrezid='{4}' AND chromosome='{5}'";
            sql = string.Format(sql, Props.props.DBPrefix, t.TranscriptomeID, t.Type, t.UniqueGeneName, t.EntrezID, t.Chromosome);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            int transcriptId = -1;
            if (rdr.Read())
            {
                transcriptId = int.Parse(rdr["id"].ToString());
                if (rdr.Read())
                    transcriptId = -1;
            }
            conn.Close();
            if (transcriptId > -1)
            {
                IssueNonQuery(string.Format("DELETE FROM transcriptannotation WHERE {0}aaatranscriptid={1}",
                    Props.props.DBPrefix, transcriptId));
                foreach (TranscriptAnnotation ta in t.TranscriptAnnotations)
                {
                    ta.TranscriptID = transcriptId;
                    InsertTranscriptAnnotation(ta);
                }
            }
            return (transcriptId > -1);
        }

        public void InsertTranscriptAnnotation(TranscriptAnnotation ta)
        {
            string description = MySqlHelper.EscapeString(ta.Description);
            string value = MySqlHelper.EscapeString(ta.Value);
            string sql = "REPLACE INTO {0}aaatranscriptannotation ({0}aaatranscriptid, source, value, description) " +
                         "VALUES ('{1}','{2}','{3}','{4}')";
            sql = string.Format(sql, Props.props.DBPrefix, ta.TranscriptID, ta.Source, value, description);
            IssueNonQuery(sql);
        }

        public void InsertAnalysisSetup(string analysisname, string bowtieIndex, string resultFolder, string parameters)
        {
            string sql = string.Format("REPLACE INTO {0}aaaanalysissetup (plateid, path, genome, parameters) VALUES ('{1}','{2}','{3}','{4}')",
                                      Props.props.DBPrefix, analysisname, resultFolder, bowtieIndex, parameters);
            IssueNonQuery(sql);
        }

        public void InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator, bool mols, string aligner)
        {
            string table = mols ? "expr" : "read";
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            int n = 0, maxId = 0, minId = int.MaxValue;
            string sqlPat = "REPLACE INTO {0}aaa" + table + "blob ({0}aaacellid, {0}aaatranscriptomeid, aligner, data) VALUES ('{1}',{2},'{3}', ?BLOBDATA)";
            foreach (ExprBlob exprBlob in exprBlobIterator)
            {
                string sql = string.Format(sqlPat, Props.props.DBPrefix, exprBlob.jos_aaacellid, exprBlob.TranscriptomeID, aligner);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?BLOBDATA", exprBlob.Blob);
                cmd.ExecuteNonQuery();
                n += 1;
                maxId = Math.Max(int.Parse(exprBlob.jos_aaacellid), maxId);
                minId = Math.Min(int.Parse(exprBlob.jos_aaacellid), minId);
            }
            Console.WriteLine("Inserted {0} ExprBlobs with cellid {1} - {2}", n, minId, maxId);
            conn.Close();
        }

        #endregion
    }
}
