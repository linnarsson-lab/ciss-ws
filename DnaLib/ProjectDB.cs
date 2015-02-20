﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using C1;

namespace Linnarsson.Dna
{
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
            string sql = "SELECT p.plateid, barcodeset FROM {0}aaaproject p " +
                         " LEFT JOIN {0}aaaanalysis a ON a.{0}aaaprojectid = p.id " +
                         " LEFT JOIN {0}aaaanalysislane al ON al.{0}aaaanalysisid = a.id " +
                         " LEFT JOIN {0}aaalane l ON l.id = al.{0}aaalaneid " +
                         " LEFT JOIN {0}aaailluminarun r ON r.id = l.{0}aaailluminarunid " +
                         " WHERE r.runno = {1} AND laneno = {2}";
            sql = string.Format(sql, Props.props.DBPrefix, runNo, lane);
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
        public ProjectDescription GetNextProjectInQueue(bool reverseSort)
        {
            ProjectDescription pd = null;
            string subSelect = string.Format("(SELECT p1.id FROM {0}aaaproject p1 JOIN {0}aaaanalysis a1 ON a1.{0}aaaprojectid=p1.id AND a1.status=\"processing\")", Props.props.DBPrefix);
            List<ProjectDescription> queue = GetProjectDescriptions("WHERE a.status=\"" + ProjectDescription.STATUS_INQUEUE + "\"" +
                " AND p.id NOT IN " + subSelect, reverseSort);
            if (nextInQueue < queue.Count)
            {
                pd = queue[nextInQueue++];
            }
            return pd;
        }

        private List<ProjectDescription> GetProjectDescriptions(string whereClause, bool reverseSort)
        {
            string sortStr = reverseSort ? " DESC" : "";
            MySqlConnection conn = new MySqlConnection(connectionString);
            List<ProjectDescription> pds = new List<ProjectDescription>();
            string sql = "SELECT a.id, a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.readdir, a.emails, " +
                         " p.plateid, p.barcodeset, p.spikemolecules, p.species, p.layoutfile, a.status, a.aligner, " +
                         " r.illuminarunid AS runid, GROUP_CONCAT(l.laneno ORDER BY l.laneno) AS lanenos " +
                         "FROM {0}aaaanalysis a " + 
                         "LEFT JOIN {0}aaaproject p ON a.{0}aaaprojectid = p.id " +
                         "RIGHT JOIN {0}aaaanalysislane al ON a.id = al.{0}aaaanalysisid " +
                         "LEFT JOIN {0}aaalane l ON al.{0}aaalaneid = l.id " +
                         "LEFT JOIN {0}aaailluminarun r ON l.{0}aaailluminarunid = r.id " +
                         whereClause + 
                         " GROUP BY a.id, runid ORDER BY a.id" + sortStr + ", p.plateid, runid;";
            sql = string.Format(sql, Props.props.DBPrefix);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> laneInfos = new List<string>();
            string currAnalysisId = "", plateId = "", bcSet = "", defaultSpecies = "", layoutFile = "", plateStatus = "",
                    emails = "", defaultBuild = "", variant = "", aligner = "";
            bool rpkm = false;
            int readdir = 1;
            int spikeMolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
            while (rdr.Read())
            {
                string analysisId = rdr["id"].ToString();
                if (currAnalysisId != "" && analysisId != currAnalysisId)
                {
                    pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies, laneInfos, layoutFile, plateStatus, emails,
                                                   defaultBuild, variant, aligner, currAnalysisId, rpkm, spikeMolecules, readdir));
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
                aligner = rdr["aligner"].ToString();
                emails = rdr["emails"].ToString();
                defaultBuild = rdr["transcript_db_version"].ToString();
                variant = rdr["transcript_variant"].ToString();
                spikeMolecules = int.Parse(rdr["spikemolecules"].ToString());
                rpkm = (rdr["rpkm"].ToString() == "True");
                readdir = rdr.GetInt32("readdir");
            }
            if (currAnalysisId != "") pds.Add(new ProjectDescription(plateId, bcSet, defaultSpecies,
                                                 laneInfos, layoutFile, plateStatus, emails,
                                                 defaultBuild, variant, aligner, currAnalysisId, rpkm, spikeMolecules, readdir));
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
            string sql = string.Format("UPDATE {0}aaaanalysis SET status=\"{1}\", time=NOW() WHERE id=\"{2}\" {3};",
                                       Props.props.DBPrefix, projDescr.status, projDescr.analysisId, reqSql);
            return IssueNonQuery(sql);
        }

        public void PublishResults(ProjectDescription projDescr)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT {0}aaaprojectid, lanecount, comment, emails, user FROM {0}aaaanalysis WHERE id=\"{1}\";",
                                        Props.props.DBPrefix, projDescr.analysisId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            string projectId = rdr[Props.props.DBPrefix + "aaaprojectid"].ToString();
            string laneCount = rdr["lanecount"].ToString();
            string comment = rdr["comment"].ToString();
            string emails = rdr["emails"].ToString();
            string user = rdr["user"].ToString();
            string isRpkm = (projDescr.rpkm) ? "1" : "0";
            rdr.Close();
            bool firstResult = true;
            foreach (ResultDescription resultDescr in projDescr.resultDescriptions)
            {
                if (firstResult)
                {
                    sql = string.Format("UPDATE {0}aaaanalysis " +
                            "SET extraction_version=\"{1}\", annotation_version=\"{2}\", genome=\"{3}\", transcript_db_version=\"{4}\", " +
                            "transcript_variant=\"{5}\", resultspath=\"{6}\", status=\"{7}\", time=NOW() WHERE id=\"{8}\" ",
                            Props.props.DBPrefix,
                            projDescr.extractionVersion, projDescr.annotationVersion, resultDescr.build, resultDescr.annotAndDate,
                            resultDescr.variants, resultDescr.resultFolder, projDescr.status, projDescr.analysisId);
                }
                else
                {
                    sql = string.Format("INSERT INTO {0}aaaanalysis " +
                               "({0}aaaprojectid, extraction_version, annotation_version, genome, comment, emails, user, " +
                                "transcript_db_version, transcript_variant, lanecount, resultspath, status, rpkm, time) " +
                               "VALUES (\"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", NOW());",
                               Props.props.DBPrefix,
                               projectId, projDescr.extractionVersion, projDescr.annotationVersion, resultDescr.build, comment, emails, user,
                               resultDescr.annotAndDate, resultDescr.variants, laneCount, resultDescr.resultFolder, projDescr.status, isRpkm);
                }
                cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                firstResult = false;
            }
            foreach (LaneInfo laneInfo in projDescr.laneInfos)
            {
                if (laneInfo.nValidReads == 0)
                    continue; // Has been extracted earlier - no data to update
                sql = string.Format(string.Format("UPDATE {0}aaalane SET strtyield=\"{1}\" WHERE laneno=\"{2}\" AND " + 
                                        "{0}aaailluminarunid= (SELECT id FROM {0}aaailluminarun WHERE illuminarunid=\"{3}\") ",
                                        Props.props.DBPrefix, laneInfo.nValidReads, laneInfo.laneNo, laneInfo.illuminaRunId));
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
            string sql = string.Format(string.Format("UPDATE {0}aaalane SET yield=\"{1}\", pfyield=\"{2}\" WHERE laneno=\"{3}\" AND " +
                                    "{0}aaailluminarunid= (SELECT id FROM {0}aaailluminarun WHERE illuminarunid=\"{4}\") ",
                                    Props.props.DBPrefix, nReads, nPFReads, lane, runId));
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
            string sql = string.Format("INSERT INTO {0}aaailluminarun (status, runno, illuminarunid, rundate, time, user) " +
                                       "VALUES ('{1}', '{2}', '{3}', '{4}', NOW(), '{5}') " +
                                       "ON DUPLICATE KEY UPDATE status='{1}', runno='{2}';",
                                       Props.props.DBPrefix, status, runNo, runId, runDate, "system");
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
            string sql = string.Format("UPDATE {0}aaailluminarun SET cycles=IFNULL(cycles, IF('{1}'>=0,'{1}',cycles)), " +
                                        "indexcycles=IFNULL(indexcycles, IF('{2}'>=0,'{2}',indexcycles)), " +
                                        "pairedcycles=IFNULL(pairedcycles, IF('{3}'>=0,'{3}',pairedcycles)) " +
                                       "WHERE illuminarunid='{4}';",
                                       Props.props.DBPrefix, cycles, indexCycles, pairedCycles, runId);
            IssueNonQuery(sql);
        }

        public Dictionary<string, List<MailTaskDescription>> GetQueuedMailTasksByEmail()
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            Dictionary<string, List<MailTaskDescription>> mds = new Dictionary<string, List<MailTaskDescription>>();
            string sql = "SELECT id, runno, laneno, email, status FROM {0}aaafqmailqueue WHERE status='inqueue' ORDER BY email";
            sql = string.Format(sql, Props.props.DBPrefix);
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

        public void RemoveFileToBackup(string readFile)
        {
            string sql = string.Format("DELETE FROM {0}aaabackupqueue WHERE path='{1}'", Props.props.DBPrefix, readFile);
            IssueNonQuery(sql);
        }

        public List<string> GetWaitingFilesToBackup()
        {
            List<string> waitingFiles = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            string sql = string.Format("SELECT path FROM {0}aaabackupqueue WHERE status='inqueue' OR status='missing' ORDER BY priority, id",
                Props.props.DBPrefix);
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
            string sql = string.Format("SELECT {1} FROM {0}aaaproject WHERE {2} LIKE '{3}' AND status!='cancelled'",
                Props.props.DBPrefix, resultCol, likeCol, likeFilter);
            return GetStrings(sql);
        }

        public string TryGetPrimerId(string primername)
        {
            string result = "NULL";
            if (primername == null || primername == "")
                return result;
            string sql = string.Format("SELECT id FROM {0}aaasequencingprimer WHERE primername='{1}'", Props.props.DBPrefix, primername);
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
            string checkSql = "SELECT count(distinct(p.plateid)) AS nprojects, count(a.id) AS nresults FROM {0}aaaproject p " +
                              "LEFT JOIN {0}aaaanalysis a ON a.{0}aaaprojectid=p.id WHERE p.plateid='{1}'";
            string sql = string.Format(checkSql, Props.props.DBPrefix, pd.plateId);
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
                sql = "UPDATE {29}aaaproject SET {29}aaacontactid='{0}', {29}aaamanagerid='{1}', {29}aaaclientid='{2}', " +
                "title='{3}', productiondate='{4}', platereference='{6}', species='{7}', tissue='{8}', " +
                "sampletype='{9}', collectionmethod='{10}', description='{14}', " +
                "protocol='{15}', barcodeset='{16}', labbookpage='{17}', " +
                "layoutfile='{18}', comment='{20}', user='{21}', spikemolecules='{22}', time=NOW(), " +
                "plannedseqcycles='{23}', plannedidxcycles='{24}', plannedpairedcycles='{25}', " +
                "seqprimerid={26}, idxprimerid={27}, pairedprimerid={28} " +
                "WHERE plateid='{5}'";
            else
                sql = "INSERT INTO {29}aaaproject ({29}aaacontactid, {29}aaamanagerid, {29}aaaclientid, " +
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
                 pd.nSeqCycles, pd.nIdxCycles, pd.nPairedCycles, seqPrimerId, idxPrimerId, pairedPrimerId, Props.props.DBPrefix);
            IssueNonQuery(sql);
        }

        /// <summary>
        /// Set the DB projectid of the chip (chips if it is a mix plate) of a sequencing plate
        /// </summary>
        /// <param name="plateid">The plate identifier ("Lxxx" or "C1-xxxxxxx-xxx")</param>
        /// <param name="chips">THe (combined) chip id(s) (xxxxxxx-xxx)</param>
        public void SetChipsProjectId(string plateid, List<Chip> chips)
        {
            int dbProjId = GetPlateInsertId(plateid);
            string chipids = string.Join("','", chips.ConvertAll(c => c.chipid).ToArray());
            string sql = string.Format("UPDATE {0}aaachip SET {0}aaaprojectid={1} WHERE chipid IN ('{2}')", 
                Props.props.DBPrefix, dbProjId, chipids);
            Console.WriteLine(sql);
            IssueNonQuery(sql);
        }

        /// <summary>
        /// Get the DB id of a textual plate identifier
        /// </summary>
        /// <param name="plateId">"Lxxx" or "C1-xxxxxxx-xxx"</param>
        /// <returns></returns>
        public int GetPlateInsertId(string plateId)
        {
            return GetInsertId(string.Format("SELECT id FROM {0}aaaproject WHERE plateid='{1}'", Props.props.DBPrefix, plateId));
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
            string whereClause = string.Format("LEFT JOIN {0}aaachip h ON h.id={0}aaachipid WHERE h.chipid='{1}' AND chipwell='{2}'",
                                               Props.props.DBPrefix, chipid, chipwell);
            List<Cell> cells = GetCells(whereClause);
            return (cells.Count == 1)? cells[0] : null;
        }
        public List<Cell> GetCellsOfChip(string chipid)
        {
            string whereClause = string.Format("LEFT JOIN {0}aaachip h ON h.id={0}aaachipid WHERE h.chipid='{1}' ORDER BY chipwell",
                Props.props.DBPrefix, chipid);
            return GetCells(whereClause);
        }
        public List<Cell> GetCells(string whereClause)
        {
            List<Cell> cells = new List<Cell>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT c.id, {0}aaachipid, chipwell, platewell, diameter, area, red, green, blue, valid FROM {0}aaacell c {1}";
            sql = string.Format(sql, Props.props.DBPrefix, whereClause);
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
            string sql = string.Format("SELECT platewell, c.id FROM {0}aaacell c LEFT JOIN {0}aaachip h ON c.{0}aaachipid=h.id " +
                     "JOIN {0}aaaproject p ON h.{0}aaaprojectid=p.id WHERE plateid='{1}' AND platewell != '' ORDER BY platewell",
                     Props.props.DBPrefix, projectId);
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
            string sqlPat = "UPDATE {0}aaacell SET platewell='{1}' WHERE id='{2}'";
            foreach (Cell c in plateOrderedCells)
            {
                string sql = string.Format(sqlPat, Props.props.DBPrefix, c.platewell, c.id);
                IssueNonQuery(sql);
            }
        }

        public void GetCellAnnotationsByPlate(string projectId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("LEFT JOIN {0}aaachip h ON {0}aaachipid=h.id " +
                        "LEFT JOIN {0}aaaproject p ON h.{0}aaaprojectid=p.id WHERE p.plateid='{1}' ORDER BY platewell", Props.props.DBPrefix, projectId),
                out annotations, out annotationIndexes);
        }
        public void GetCellAnnotationsByChip(string chipId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("WHERE {0}aaachipid='{1}' ORDER BY chipwell", Props.props.DBPrefix, chipId),
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
            annotationIndexes["Strain"] = i++;
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
                string plateWell = cell.platewell;
                if (plateWell == "")
                    continue;
                Chip chip = chipsById[cell.jos_aaachipid];
                string[] wellAnn = new string[annotationIndexes.Count];
                i = 0;
                wellAnn[i++] = chip.chipid;
                wellAnn[i++] = cell.chipwell;
                wellAnn[i++] = chip.species;
                wellAnn[i++] = chip.age;
                wellAnn[i++] = chip.sex;
                wellAnn[i++] = chip.strain;
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
            string sqlPat = "SELECT c.platewell, name, value FROM {0}aaacellannotation a LEFT JOIN {0}aaacell c ON a.{0}aaacellid=c.id " +
                            "WHERE a.{0}aaacellid IN (SELECT {0}aaacell.id FROM {0}aaacell {1})";
            string sql = string.Format(sqlPat, Props.props.DBPrefix, chipOrProjectWhereSql);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string plateWell = rdr.GetString(0);
                if (plateWell == "")
                    continue;
                string name = rdr.GetString(1);
                string value = rdr.GetString(2);
                annotations[plateWell][annotationIndexes[name]] = value;
            }
            rdr.Close();
            conn.Close();
        }

        private static List<string> GetCellAnnotationNames(string chipOrProjectWhereSql)
        {
            string sql = "SELECT DISTINCT(name) FROM {0}aaacellannotation WHERE {0}aaacellid IN (SELECT {0}aaacell.id FROM {0}aaacell {1})";
            sql = string.Format(sql, Props.props.DBPrefix, chipOrProjectWhereSql);
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

        /// <summary>
        /// Collect Chip metadata mapped by the DBId of each Chip represented among the given cells
        /// </summary>
        /// <param name="cells">Cells to collect Chip metadata for</param>
        /// <returns></returns>
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
            MySqlCommand cmd = new MySqlCommand(string.Format("SELECT id FROM {0}aaacell", Props.props.DBPrefix), conn);
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
            string sql = string.Format("SELECT DISTINCT(chipid) FROM {0}aaachip", Props.props.DBPrefix);
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
                         " tissue, treatment, spikemolecules, {0}aaaprojectid, " +
                         " {0}aaaclientid, {0}aaacontactid, {0}aaamanagerid, comments FROM {0}aaachip {1};";
            sql = string.Format(sql, Props.props.DBPrefix, whereClause);
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
            string sql = "INSERT INTO {0}aaacellimage ({0}aaacellid, reporter, marker, detection, relativepath) " +
                               "VALUES ({1},'{2}','{3}','{4}','{5}') " +
                         "ON DUPLICATE KEY UPDATE marker='{3}',detection='{4}',relativepath='{5}';";
            sql = string.Format(sql, Props.props.DBPrefix, ci.CellID, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
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
            string sql = "INSERT INTO {0}aaacellannotation (id, name, value) " +
                               "VALUES ({1},'{2}','{3}') " +
                         "ON DUPLICATE KEY UPDATE value='{2}';";
            sql = string.Format(sql, Props.props.DBPrefix, ca.CellID, ca.Name, ca.Value);
            IssueNonQuery(sql);
        }

        #region Stuff from C1DB for use in unified database:

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

        /// <summary>
        /// Returns most up-to-date Transcriptome data for the specified genome data
        /// </summary>
        /// <param name="buildVarAnnot">e.g. "mm10_sUCSC"</param>
        /// <returns>null if no match exists in database, or can not connect</returns>
        public Transcriptome GetTranscriptome(string buildVarAnnot)
        {
            Transcriptome t = null;
            try
            {
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
            }
            catch (MySqlException e)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, e);
            }
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
            try
            {
                IssueNonQuery(sql);
            }
            catch (Exception)
            {
                Console.WriteLine("InsertTranscriptAnnotation ERROR: " + ta.ToString());
            }
        }

        public void InsertAnalysisSetup(string projectId, string bowtieIndex, string resultFolder, string parameters)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("REPLACE INTO {0}aaaanalysissetup (plateid, path, genome, parameters) VALUES ('{1}','{2}','{3}','{4}')",
                                      Props.props.DBPrefix, projectId, resultFolder, bowtieIndex, parameters);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        public void InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            int n = 0, maxId = 0, minId = int.MaxValue;
            string sqlPat = "REPLACE INTO {0}aaaexprblob ({0}aaacellid, {0}aaatranscriptomeid, data) VALUES ('{1}',{2}, ?BLOBDATA)";
            foreach (ExprBlob exprBlob in exprBlobIterator)
            {
                string sql = string.Format(sqlPat, Props.props.DBPrefix, exprBlob.CellID, exprBlob.TranscriptomeID);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?BLOBDATA", exprBlob.Blob);
                cmd.ExecuteNonQuery();
                n += 1;
                maxId = Math.Max(int.Parse(exprBlob.CellID), maxId);
                minId = Math.Min(int.Parse(exprBlob.CellID), minId);
            }
            Console.WriteLine("Inserted {0} ExprBlobs with CellIDs {1} - {2}", n, minId, maxId);
            conn.Close();
        }

        #endregion
    }
}
