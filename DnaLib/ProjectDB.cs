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
    public class ProjectDB : IDB
    {
        private static string connectionString;

        public ProjectDB()
        {
            connectionString = Props.props.MySqlServerConnectionString;
        }

        private void IssueNonQuery(string sql)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
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
            string sql = "SELECT a.id, p.id AS pid, a.genome, a.transcript_db_version, a.transcript_variant, a.rpkm, a.readdir, a.emails, a.user, " +
                         " p.plateid, p.barcodeset, p.spikemolecules, p.species, p.layoutfile, a.status, a.aligner, a.comment," +
                         " r.illuminarunid AS runid, GROUP_CONCAT(l.laneno ORDER BY l.laneno) AS lanenos " +
                         "FROM {0}aaaanalysis a " + 
                         "LEFT JOIN {0}aaaproject p ON a.{0}aaaprojectid = p.id " +
                         "RIGHT JOIN {0}aaaanalysislane al ON a.id = al.{0}aaaanalysisid " +
                         "LEFT JOIN {0}aaalane l ON al.{0}aaalaneid = l.id " +
                         "LEFT JOIN {0}aaailluminarun r ON l.{0}aaailluminarunid = r.id " +
                         "WHERE a.status=\"{1}\" AND p.id NOT IN " +
                         "(SELECT p1.id FROM {0}aaaproject p1 JOIN {0}aaaanalysis a1 ON a1.{0}aaaprojectid=p1.id AND a1.status=\"{2}\") " +
                         "GROUP BY a.id, runid ORDER BY a.id DESC, p.plateid, runid;";
            sql = string.Format(sql, Props.props.DBPrefix, ProjectDescription.STATUS_INQUEUE, ProjectDescription.STATUS_PROCESSING);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            List<string> laneArgs = new List<string>();
            string dbanalysisid = "", dbplateid = "", plateId = "", barcodeset = "", defaultSpecies = "", layoutfile = "", plateStatus = "",
                    emails = "", defaultBuild = "", variant = "", aligner = "", rpkm = "", comment = "", user = "";
            int readdir = 1;
            int spikemolecules = Props.props.TotalNumberOfAddedSpikeMolecules;
            while (rdr.Read())
            {
                string nextAnalysisId = rdr["id"].ToString();
                if (dbanalysisid != "" && nextAnalysisId != dbanalysisid)
                {
                    pds.Add(new ProjectDescription(plateId, plateId, "", barcodeset, defaultSpecies, laneArgs, layoutfile, plateStatus, emails,
                                                   defaultBuild, variant, aligner, dbanalysisid, dbplateid, null, rpkm, spikemolecules, readdir, comment, user));
                    laneArgs = new List<string>();
                }
                dbanalysisid = nextAnalysisId;
                string laneArg = string.Format("{0}:{1}", rdr["runid"], rdr.GetString("lanenos").Replace(",", ""));
                laneArgs.Add(laneArg);
                dbplateid = rdr["pid"].ToString();
                plateId = rdr["plateid"].ToString();
                barcodeset = rdr["barcodeset"].ToString();
                defaultSpecies = rdr["species"].ToString();
                layoutfile = rdr["layoutfile"].ToString();
                plateStatus = rdr["status"].ToString();
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
            if (dbanalysisid != "")
                pds.Add(new ProjectDescription(plateId, plateId, "", barcodeset, defaultSpecies, laneArgs, layoutfile, plateStatus, emails,
                                               defaultBuild, variant, aligner, dbanalysisid, dbplateid, null, rpkm, spikemolecules, readdir, comment, user));
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
        /// <param name="projDescr"></param>
        /// <returns>true if the analysis was successfully 'caught' by this process</returns>
        public bool SecureStartAnalysis(ProjectDescription projDescr)
        {
            bool success = false;
            string sql = string.Format("SELECT * FROM {0}aaaanalysis WHERE id=\"{1}\" AND {0}aaaprojectid IN " +
                                       "(SELECT {0}aaaprojectid FROM {0}aaaanalysis a2 WHERE status IN (\"{2}\",\"{3}\",\"{4}\",\"{5}\") );",
                         Props.props.DBPrefix, projDescr.dbanalysisid,
                         ProjectDescription.STATUS_PROCESSING, ProjectDescription.STATUS_EXTRACTING,
                         ProjectDescription.STATUS_ALIGNING, ProjectDescription.STATUS_ANNOTATING);
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                bool projectAlreadyProcessing = rdr.HasRows;
                rdr.Close();
                if (!projectAlreadyProcessing)
                {
                    sql = string.Format("UPDATE {0}aaaanalysis SET status=\"{1}\", time=NOW() WHERE id=\"{2}\" AND status=\"{3}\";",
                     Props.props.DBPrefix, ProjectDescription.STATUS_EXTRACTING, projDescr.dbanalysisid, ProjectDescription.STATUS_INQUEUE);
                    cmd = new MySqlCommand(sql, conn);
                    int nRowsAffected = cmd.ExecuteNonQuery();
                    if (nRowsAffected > 0)
                    {
                        success = true;
                        projDescr.status = ProjectDescription.STATUS_EXTRACTING;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
            return success;
        }

        public void PublishResults(ProjectDescription pd)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql;
            MySqlCommand cmd;
            bool firstResult = true;
            foreach (ResultDescription rd in pd.ResultDescriptions)
            {
                if (firstResult)
                {
                    sql = string.Format("UPDATE {0}aaaanalysis " +
                            "SET extraction_version=\"{1}\", annotation_version=\"{2}\", genome=\"{3}\", transcript_db_version=\"{4}\", " +
                            "transcript_variant=\"{5}\", resultspath=\"{6}\", status=\"{7}\", time=NOW() WHERE id=\"{8}\" ",
                            Props.props.DBPrefix,
                            pd.extractionVersion, pd.annotationVersion, rd.build, rd.annotAndDate,
                            rd.variants, rd.resultFolder, pd.status, pd.dbanalysisid);
                }
                else
                {
                    sql = string.Format("INSERT INTO {0}aaaanalysis " +
                               "({0}aaaprojectid, extraction_version, annotation_version, genome, comment, emails, user, " +
                                "transcript_db_version, transcript_variant, lanecount, resultspath, status, rpkm, time) " +
                               "VALUES (\"{1}\", \"{2}\", \"{3}\", \"{4}\", \"{5}\", \"{6}\", \"{7}\", \"{8}\", \"{9}\", \"{10}\", \"{11}\", \"{12}\", \"{13}\", NOW());",
                               Props.props.DBPrefix,
                               pd.dbchipid, pd.extractionVersion, pd.annotationVersion, rd.build, pd.comment, pd.emails, pd.user,
                               rd.annotAndDate, rd.variants, pd.laneCount, rd.resultFolder, pd.status, pd.rpkm);
                }
                cmd = new MySqlCommand(sql, conn);
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
            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
            }
            conn.Close();
            return success;
        }

        public void UpdateRunStatus(string runId, string status, int runNo)
        { // Below SQL will update with status and runno if user has defined the run, else add a new run as
            // well as defining 8 new lanes by side-effect of a MySQL trigger
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
        private void UpdateRunCycles(string runId, int read, int cycles)
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

        public Cell GetCellFromChipWell(string chipid, string chipwell)
        {
            string whereClause = string.Format("LEFT JOIN {0}aaachip h ON h.id=c.{0}aaachipid WHERE h.chipid='{1}' AND c.chipwell='{2}'",
                                               Props.props.DBPrefix, chipid, chipwell);
            List<Cell> cells = GetCells(whereClause);
            return (cells.Count == 1)? cells[0] : null;
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
            string sql = "SELECT c.id, c.{0}aaachipid, c.chipwell, c.platewell, diameter, area, red, green, blue, c.valid, c.subwell, c.subbarcodeidx FROM {0}aaacell c {1}";
            sql = string.Format(sql, Props.props.DBPrefix, whereClause);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                bool valid = (rdr.GetInt32(9) == 1);
                string subwell = rdr.IsDBNull(10) ? "" : rdr.GetString(10);
                int subbarcodeidx = rdr.IsDBNull(11) ? 0 : rdr.GetInt32(11);
                Cell cell = new Cell(rdr.GetInt32(0), rdr.GetInt32(1), 0, rdr.GetString(2), rdr.GetString(3),
                                     rdr.GetDouble(4), rdr.GetDouble(5), rdr.GetInt32(6), rdr.GetInt32(7), rdr.GetInt32(8), valid, subwell, subbarcodeidx);
                cells.Add(cell);
            }
            conn.Close();
            return cells;
        }

        public Dictionary<string, int> GetWell2CellIdMapping(string plateid)
        {
            Dictionary<string, int> cellIdByPlateWell = GetCellIdByPlateWell(plateid);
            if (cellIdByPlateWell.Count == 0)
            {
                SetPlateWellToChipWell(plateid);
                cellIdByPlateWell = GetCellIdByPlateWell(plateid);
            }
            return cellIdByPlateWell;
        }

        private Dictionary<string, int> GetCellIdByPlateWell(string plateid)
        {
            Dictionary<string, int> cellIdByPlateWell = new Dictionary<string, int>();
            string sql = string.Format("SELECT c.platewell, c.id FROM {0}aaacell c LEFT JOIN {0}aaachip h ON c.{0}aaachipid=h.id " +
                     "JOIN {0}aaaproject p ON h.{0}aaaprojectid=p.id WHERE plateid='{1}' AND platewell != '' ORDER BY platewell",
                     Props.props.DBPrefix, plateid);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIdByPlateWell.Add(rdr.GetString(0), rdr.GetInt32(1));
            conn.Close();
            return cellIdByPlateWell;
        }

        private void SetPlateWellToChipWell(string projectId)
        {
            string sql = string.Format("UPDATE {0}aaachip h " +
                         " JOIN {0}aaaproject p ON h.{0}aaaprojectid=p.id JOIN {0}aaacell c ON c.{0}_aaachipid=h.id " +
                         " SET c.platewell=c.chipwell WHERE p.plateid='{1}'", Props.props.DBPrefix, projectId);
            IssueNonQuery(sql);
        }

        public void GetCellAnnotations(string plateId, out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            CellAnnotationsCall(string.Format("LEFT JOIN {0}aaachip h ON c.{0}aaachipid=h.id " +
                        "LEFT JOIN {0}aaaproject p ON h.{0}aaaprojectid=p.id WHERE p.plateid='{1}' ORDER BY c.platewell", Props.props.DBPrefix, plateId),
                out annotations, out annotationIndexes);
            if (annotations.Count == 0)
            {
                string chipid = plateId.Replace(C1Props.C1ProjectPrefix, "");
                CellAnnotationsCall(string.Format("WHERE c.{0}aaachipid='{1}' ORDER BY c.chipwell", Props.props.DBPrefix, chipid),
                    out annotations, out annotationIndexes);
                Console.WriteLine("WARNING: Plate " + plateId + " has not been properly loaded in DB. Assuming matching chip->plate wellIds.");
                //throw new SampleLayoutFileException("Can not extract any well/cell annotations for " + plateId + "  from C1 database.");
            }
        }

        private void CellAnnotationsCall(string chipOrProjectWhereSql,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            annotationIndexes = new Dictionary<string, int>();
            int i = 0;
            annotationIndexes["chip"] = i++;
            annotationIndexes["chipwell"] = i++;
            annotationIndexes["subwell"] = i++;
            annotationIndexes["species"] = i++;
            annotationIndexes["age"] = i++;
            annotationIndexes["sex"] = i++;
            annotationIndexes["strain"] = i++;
            annotationIndexes["tissue"] = i++;
            annotationIndexes["treatment"] = i++;
            annotationIndexes["animalid"] = i++;
            annotationIndexes["weight"] = i++;
            annotationIndexes["diameter"] = i++;
            annotationIndexes["area"] = i++;
            annotationIndexes["red"] = i++;
            annotationIndexes["blue"] = i++;
            annotationIndexes["green"] = i++;
            annotationIndexes["posinpatch"] = i++;
            annotationIndexes["Spikemolecules"] = i++;
            annotationIndexes["valid"] = i++;
            annotationIndexes["comments"] = i++;
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
                wellAnn[i++] = cell.subwell;
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
                wellAnn[i++] = cell.subwell;
                wellAnn[i++] = chip.spikemolecules.ToString();
                wellAnn[i++] = cell.valid? "Y" : "-";
                wellAnn[i++] = chip.comments;
                annotations[plateWell] = wellAnn;
            }
            string sqlPat = "SELECT c2.platewell, name, value FROM {0}aaacellannotation a LEFT JOIN {0}aaacell c2 ON a.{0}aaacellid=c2.id " +
                            "WHERE a.{0}aaacellid IN (SELECT c.id FROM {0}aaacell c {1})";
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
            string sql = "SELECT DISTINCT(ca.name) FROM {0}aaacellannotation ca WHERE {0}aaacellid IN (SELECT c.id FROM {0}aaacell c {1})";
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
        private Dictionary<int, Chip> GetChipsById(List<Cell> cells)
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

        private Chip GetChipById(int id)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT id, chipid, strtprotocol, datedissected, datecollected," +
                         " species, strain, donorid, age, sex, weight," +
                         " tissue, treatment, spikemolecules, {0}aaaprojectid, " +
                         " {0}aaaclientid, {0}aaacontactid, {0}aaamanagerid, comments FROM {0}aaachip WHERE id='{1}';";
            sql = string.Format(sql, Props.props.DBPrefix, id);
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

        public void InsertCellImage(CellImage ci)
        {
            int detectionValue = (ci.Detection == Detection.Yes) ? 1 : (ci.Detection == Detection.No) ? -1 : 0;
            string sql = "INSERT INTO {0}aaacellimage ({0}aaacellid, reporter, marker, detection, relativepath) " +
                               "VALUES ({1},'{2}','{3}','{4}','{5}') " +
                         "ON DUPLICATE KEY UPDATE marker='{3}',detection='{4}',relativepath='{5}'";
            sql = string.Format(sql, Props.props.DBPrefix, ci.jos_aaacellid, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void DeleteCell(Cell c)
        {
            string oldIdSql = string.Format("SELECT id FROM {0}aaacell WHERE {0}aaachipid='{1}' AND chipwell='{2}'",
                                             Props.props.DBPrefix, c.jos_aaachipid, c.chipwell);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(oldIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                int cellId = int.Parse(rdr["id"].ToString());
                rdr.Close();
                string delSql = string.Format("DELETE FROM {0}aaacellimage WHERE {0}aaacellid='{1}'", Props.props.DBPrefix, cellId);
                cmd = new MySqlCommand(delSql, conn);
                cmd.ExecuteNonQuery();
                delSql = string.Format("DELETE FROM {0}aaacellannotation WHERE {0}aaacellid='{1}'", Props.props.DBPrefix, cellId);
                cmd = new MySqlCommand(delSql, conn);
                cmd.ExecuteNonQuery();
                delSql = string.Format("DELETE FROM {0}aaacell WHERE id='{1}'", Props.props.DBPrefix, cellId);
                cmd = new MySqlCommand(delSql, conn);
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public void InsertOrUpdateCell(Cell c)
        {
            int validValue = c.valid ? 1 : 0;
            string subwell = (c.subwell == "") ? "NULL" : "'" + c.subwell + "'";
            DeleteCell(c);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string insSql = "INSERT INTO {0}aaacell ({0}aaachipid, chipwell, diameter, area, red, green, blue, valid, subwell, subbarcodeidx) " +
                            "VALUES ('{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}',{9},'{10}') ";
            insSql = string.Format(insSql, Props.props.DBPrefix, c.jos_aaachipid, c.chipwell, c.diameter, c.area,
                                c.red, c.green, c.blue, validValue, subwell, c.subbarcodeidx);
            MySqlCommand cmd = new MySqlCommand(insSql, conn);
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
                InsertCellImage(ci);
            }
            foreach (CellAnnotation ca in c.cellAnnotations)
            {
                ca.jos_aaacellid = cellId;
                InsertOrUpdateCellAnnotation(ca);
            }
        }

        public void InsertOrUpdateCellAnnotation(CellAnnotation ca)
        {
            string sql = "INSERT INTO {0}aaacellannotation ({0}aaacellid, name, value) VALUES ('{1}','{2}','{3}') " +
                         "ON DUPLICATE KEY UPDATE value='{3}';";
            sql = string.Format(sql, Props.props.DBPrefix, ca.jos_aaacellid, ca.Name, ca.Value);
            IssueNonQuery(sql);
        }

    }
}
