using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using MySql.Data;

namespace C1
{
    public class C1DB
    {
        /// <summary>
        /// set to true to run without executing any inserts to database
        /// </summary>
        private readonly bool test = false;

        private static string connectionString;

        public C1DB()
        {
            connectionString = C1Props.props.MySQlConnectionString;
        }

        /// <summary>
        /// Make sure a standard numerical ID has the form NNNNNNN-NNN, else all "-" are removed
        /// </summary>
        /// <param name="chipId"></param>
        /// <returns></returns>
        public static string StandardizeChipId(string chipId)
        {
            chipId = chipId.Replace("-", "");
            int junk;
            int last3Pos = chipId.Length - 3;
            if (int.TryParse(chipId.Substring(last3Pos, 3), out junk))
                chipId = chipId.Substring(0, last3Pos) + "-" + chipId.Substring(last3Pos);
            return chipId;
        }

        private bool IssueNonQuery(string sql)
        {
            bool success = true;
            MySqlConnection conn = new MySqlConnection(connectionString);
            try
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: {1}", DateTime.Now, ex);
                success = false;
            }
            conn.Close();
            return success;
        }

        private int InsertAndGetLastId(string sql, string tableName)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            if (test)
                Console.WriteLine(sql);
            else
                cmd.ExecuteNonQuery();
            string lastIdSql = string.Format("SELECT MAX({0}ID) AS LastId FROM {0}", tableName);
            cmd = new MySqlCommand(lastIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int lastId = int.Parse(rdr["LastId"].ToString());
            conn.Close();
            return lastId;
        }

        /// <summary>
        /// Returns Transcriptome data for the specified genome data
        /// </summary>
        /// <param name="buildVarAnnot">e.g. "mm10_sUCSC"</param>
        /// <returns>null if no match exists in database, or can not connect</returns>
        public Transcriptome GetTranscriptome(string buildVarAnnot)
        {
            Transcriptome t = null;
            try
            {
                string sql = string.Format("SELECT * FROM Transcriptome WHERE Name ='{0}'", buildVarAnnot);
                MySqlConnection conn = new MySqlConnection(connectionString);
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                if (rdr.Read()) t = new Transcriptome(rdr.GetInt32("TranscriptomeID"), rdr.GetString("Name"), rdr.GetString("Organism"),
                                                      rdr.GetString("Source"), rdr.GetString("GenomeFolder"), rdr.GetString("Description"),
                                                      rdr.GetDateTime("BuildDate"), rdr.GetString("BuilderVersion"), rdr.GetDateTime("AnalysisDate"),
                                                      rdr.GetString("AnnotationVersion"));
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
            string sql = string.Format("SELECT * FROM Transcript WHERE TranscriptomeID='{0}' AND Chromosome='CTRL' ORDER BY Start", transcriptomeId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                yield return MakeTranscriptFromDBReader(rdr);
            sql = string.Format("SELECT * FROM Transcript WHERE TranscriptomeID='{0}' AND Chromosome!='CTRL' AND LEFT(GeneName,2)!='r_' ORDER BY Chromosome, Start", transcriptomeId);
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
            string sql = string.Format("SELECT GeneName, TranscriptID FROM Transcript WHERE TranscriptomeID='{0}' AND LEFT(GeneName,2)='r_';", 
                                       c1Trome.TranscriptomeID.Value);
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
            string uniqueName = rdr.GetString("GeneName");
            Match m = Regex.Match(uniqueName, "_v[0-9]+$");
            string geneName = (m.Success) ? uniqueName.Substring(0, m.Index) : uniqueName;
            Transcript t = new Transcript(rdr.GetInt32("TranscriptID"), rdr.GetInt32("TranscriptomeID"), rdr.GetInt32("ExprBlobIdx"),
                                          rdr.GetString("Name"), rdr.GetString("Type"), geneName, uniqueName,
                                          rdr.GetString("EntrezID"), rdr.GetString("Description"),
                                          rdr.GetString("Chromosome"), rdr.GetInt32("Start"), rdr.GetInt32("End"),
                                          rdr.GetInt32("Length"), rdr.GetChar("Strand"), rdr.GetInt32("Extension5Prime"),
                                          rdr.GetString("ExonStarts"), rdr.GetString("ExonEnds"));
            return t;
        }

        public void InsertOrUpdateCellImage(CellImage ci)
        {
            int detectionValue = (ci.Detection == Detection.Yes) ? 1 : (ci.Detection == Detection.No) ? -1 : 0;
            string sql = "INSERT INTO CellImage (CellID, Reporter, Marker, Detection, RelativePath) " +
                               "VALUES ({0},'{1}','{2}','{3}','{4}') " +
                         "ON DUPLICATE KEY UPDATE Marker='{2}',Detection='{3}',RelativePath='{4}';";
            sql = string.Format(sql, ci.CellID, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void InsertOrUpdateCell(Cell c)
        {
            CultureInfo cult = new CultureInfo("sv-SE");
            string sql = "INSERT INTO Cell (Chip, ChipWell, StrtProtocol, DateDissected, DateCollected, " +
                                           "Species, Strain, DonorID, Age, Sex, Tissue, Treatment, " +
                                           "Diameter, Area, PI, Operator, Scientist, Comments, " + 
                                           "Red, Green, Blue, Weight) " +
                         "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}'," +
                                 "'{12}','{13}','{14}','{15}','{16}','{17}','{18}','{19}','{20}','{21}') " +
                         "ON DUPLICATE KEY UPDATE StrtProtocol='{2}', DateDissected='{3}', DateCollected='{4}'," +
                            "Species='{5}',Strain='{6}',DonorID='{7}',Age='{8}',Sex='{9}',Tissue='{10}',Treatment='{11}'," +
                            "Diameter='{12}',Area='{13}',PI='{14}',Operator='{15}',Scientist='{16}',Comments='{17}'," +
                            "Red='{18}',Green='{19}',Blue='{20}',Weight='{21}';";
            sql = string.Format(sql, c.Chip, c.ChipWell, c.StrtProtocol, c.DateDissected.ToString(cult), c.DateCollected.ToString(cult),
                                     c.Species, c.Strain, c.DonorID, c.Age, c.Sex, c.Tissue, c.Treatment,
                                     c.Diameter, c.Area, c.PI, c.Operator, c.Scientist, c.Comments, 
                                     c.Red, c.Green, c.Blue, c.Weight);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            if (test)
                Console.WriteLine(sql);
            else
                cmd.ExecuteNonQuery();
            string lastIdSql = string.Format("SELECT CellID FROM Cell WHERE Chip='{0}' AND ChipWell='{1}';", c.Chip, c.ChipWell);
            cmd = new MySqlCommand(lastIdSql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            rdr.Read();
            int cellId = int.Parse(rdr["CellID"].ToString());
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
            string sql = "INSERT INTO CellAnnotation (CellID, Name, Value) " +
                               "VALUES ({0},'{1}','{2}') " +
                         "ON DUPLICATE KEY UPDATE Value='{2}';";
            sql = string.Format(sql, ca.CellID, ca.Name, ca.Value);
            IssueNonQuery(sql);
        }

        public void UpdateDBCellSeqPlateWell(List<Cell> cells)
        {
            string sqlPat = "UPDATE Cell SET Plate='{0}', PlateWell='{1}' WHERE CellID='{2}'";
            foreach (Cell c in cells)
            {
                string sql = string.Format(sqlPat, c.Plate, c.PlateWell, c.CellID);
                IssueNonQuery(sql);
            }
        }

        public void InsertTranscriptome(Transcriptome t)
        {
            CultureInfo cult = new CultureInfo("sv-SE");
            string sql = "INSERT INTO Transcriptome (Name, Organism, Source, GenomeFolder, Description, " +
                                                    "BuildDate, BuilderVersion, AnnotationVersion, AnalysisDate) " +
                                 "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}')";
            sql = string.Format(sql, t.Name, t.Organism, t.Source, t.GenomeFolder, t.Description, 
                                     t.BuildDate.ToString(cult), t.BuilderVersion, t.AnnotationVersion, t.AnalysisDate.ToString(cult));
            int transcriptomeId = InsertAndGetLastId(sql, "Transcriptome");
            t.TranscriptomeID = transcriptomeId;
        }

        public void InsertTranscript(Transcript t)
        {
            string description = MySqlHelper.EscapeString(t.Description);
            string sql = "INSERT INTO Transcript (TranscriptomeID, Name, Type, GeneName, EntrezID, Description, Chromosome, " +
                                    "Start, End, Length, Strand, Extension5Prime, ExonStarts, ExonEnds, " + 
                                    "ExprBlobIdx, StartToCloseSiteSite) " +
                                    "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}'," +
                                    "'{7}','{8}','{9}','{10}','{11}','{12}','{13}'," +
                                    "'{14}','{15}')";
            sql = string.Format(sql, t.TranscriptomeID, t.Name, t.Type, t.UniqueGeneName, t.EntrezID, description, t.Chromosome,
                                     t.Start, t.End, t.Length, t.Strand, t.Extension5Prime, t.ExonStarts, t.ExonEnds,
                                     t.ExprBlobIdx, t.StartToCloseCutSite);
            int newTranscriptId = InsertAndGetLastId(sql, "Transcript");
            t.TranscriptID = newTranscriptId;
            foreach (TranscriptAnnotation ta in t.TranscriptAnnotations)
            {
                ta.TranscriptID = t.TranscriptID.Value;
                InsertTranscriptAnnotation(ta);
            }
        }

        public void InsertTranscriptAnnotation(TranscriptAnnotation ta)
        {
            string description = MySqlHelper.EscapeString(ta.Description);
            string sql = "REPLACE INTO TranscriptAnnotation (TranscriptID, Source, Value, Description) " +
                         "VALUES ('{0}','{1}','{2}','{3}')";
            sql = string.Format(sql, ta.TranscriptID, ta.Source, ta.Value, description);
            IssueNonQuery(sql);
        }

        public void InsertExpressions(IEnumerable<Expression> exprIterator)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sqlPat = "REPLACE INTO Expression (CellID, TranscriptID, Molecules) VALUES ('{0}',{1}, {2})";
            foreach (Expression e in exprIterator)
            {
                string sql = string.Format(sqlPat, e.CellID, e.TranscriptID, e.Molecules);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public IEnumerable<Expression> IterExpressions(string cellId, int transcriptomeId)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("SELECT TranscriptID, Molecules FROM Expression WHERE CellID={0} AND " +
                "TranscriptID IN (SELECT TranscriptID FROM Transcript WHERE TranscriptomeID={1}) ORDER BY TranscriptID", cellId, transcriptomeId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                yield return new Expression(cellId, rdr.GetInt32(0), 0, 0, 0, rdr.GetInt32(1));
            rdr.Close();
            conn.Close();
        }

        public void InsertAnalysisSetup(string plate, string bowtieIndex, string resultFolder, string parameters)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = string.Format("REPLACE INTO AnalysisSetup (Plate, Path, Genome, Parameters) VALUES ('{0}','{1}','{2}','{3}')",
                                       plate, resultFolder, bowtieIndex, parameters);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            if (test)
                Console.WriteLine(sql);
            else
                cmd.ExecuteNonQuery();
            conn.Close();
        }

        public void InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sqlPat = "REPLACE INTO ExprBlob (CellID, TranscriptomeID, Data) VALUES ('{0}',{1}, ?BLOBDATA)";
            foreach (ExprBlob exprBlob in exprBlobIterator)
            {
                string sql = string.Format(sqlPat, exprBlob.CellID, exprBlob.TranscriptomeID);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("?BLOBDATA", exprBlob.Blob);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public void InsertExprBlob(ExprBlob exprBlob, bool blobIsZeroStoreNULL)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sqlPat = "REPLACE INTO ExprBlob (CellID, TranscriptomeID, Data) VALUES ('{0}',{1}, {2})";
            string sql = string.Format(sqlPat, exprBlob.CellID, exprBlob.TranscriptomeID, blobIsZeroStoreNULL? "NULL" : "?BLOBDATA");
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            cmd.CommandText = sql;
            if (!blobIsZeroStoreNULL)
                cmd.Parameters.AddWithValue("?BLOBDATA", exprBlob.Blob);
            if (test)
                Console.WriteLine(sql);
            else
                cmd.ExecuteNonQuery();
            conn.Close();
        }

        public Dictionary<string, int> GetCellIdByPlateWell(string projectId)
        {
            Dictionary<string, int> cellIdByPlateWell = new Dictionary<string, int>();
            string sql = string.Format("SELECT PlateWell, CellID FROM Cell WHERE Plate='{0}' ORDER BY PlateWell", projectId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIdByPlateWell.Add(rdr.GetString(0), rdr.GetInt32(1));
            conn.Close();
            return cellIdByPlateWell;
        }

        public List<string> GetCellIds()
        {
            List<string> cellIds = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand("SELECT CellID FROM Cell", conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIds.Add(rdr.GetString(0));
            rdr.Close();
            conn.Close();
            return cellIds;
        }

        private static List<string> GetCellAnnotationNames(string chipOrProjectWhereSql)
        {
            string sql = "SELECT DISTINCT(Name) FROM CellAnnotation WHERE CellID IN (SELECT CellID FROM Cell {0})";
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

        public Cell GetCellFromChipWell(string chip, string chipWell)
        {
            string whereClause = string.Format("WHERE Chip='{0}' AND ChipWell='{1}'", chip, chipWell);
            return GetCells(whereClause)[0];
        }
        public List<Cell> GetCellsOfChip(string chip)
        {
            string whereClause = string.Format("WHERE Chip='{0}' ORDER BY ChipWell", chip);
            return GetCells(whereClause);
        }
        public List<Cell> GetCells(string whereClause)
        {
            List<Cell> cells = new List<Cell>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT CellID, Chip, ChipWell, Plate, PlateWell, StrtProtocol, DateDissected, DateCollected, Species, " +
                         "Strain, DonorID, Age, Sex, Tissue, Treatment, Diameter, Area, PI, Operator, Scientist, Comments, " +
                         "Red, Green, Blue, Weight, SpikeMolecules " +
                         "FROM Cell {0}";
            sql = string.Format(sql, whereClause);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                Cell cell = new Cell(rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2), rdr.GetString(3), rdr.GetString(4),
                                     rdr.GetString(5), rdr.GetDateTime(6), rdr.GetDateTime(7), rdr.GetString(8),
                                     rdr.GetString(9), rdr.GetString(10), rdr.GetString(11), rdr.GetString(12),
                                     rdr.GetString(13), rdr.GetString(14), rdr.GetDouble(15), rdr.GetDouble(16),
                                     rdr.GetString(17), rdr.GetString(18), rdr.GetString(19), rdr.GetString(20),
                                     rdr.GetInt32(21), rdr.GetInt32(22), rdr.GetInt32(23), rdr.GetString(24), rdr.GetInt32(25));
                cells.Add(cell);
            }
            conn.Close();
            return cells;
        }

        public void GetCellAnnotationsByPlate(string projectId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("WHERE Plate='{0}' ORDER BY PlateWell", projectId),
                out annotations, out annotationIndexes);
        }
        public void GetCellAnnotationsByChip(string chipId,
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            GetCellAnnotations(string.Format("WHERE Chip='{0}' ORDER BY ChipWell", chipId),
                out annotations, out annotationIndexes);
        }

        /// <summary>
        /// Read plate layout data from the database
        /// </summary>
        /// <param name="chipOrProjectWhereSql"></param>
        /// <param name="annotations"></param>
        /// <param name="annotationIndexes"></param>
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
            annotationIndexes["Comments"] = i++;
            List<string> extraAnnotNames = GetCellAnnotationNames(chipOrProjectWhereSql);
            for (i = 0; i < extraAnnotNames.Count; i++)
                annotationIndexes[extraAnnotNames[i]] = annotationIndexes.Count;
            annotations = new Dictionary<string, string[]>(96);
            foreach (Cell cell in GetCells(chipOrProjectWhereSql))
            {
                string[] wellAnn = new string[annotationIndexes.Count];
                string plateWell = cell.PlateWell;
                i = 0;
                wellAnn[i++] = cell.Chip;
                wellAnn[i++] = cell.ChipWell;
                wellAnn[i++] = cell.Species;
                wellAnn[i++] = cell.Age;
                wellAnn[i++] = cell.Sex;
                wellAnn[i++] = cell.Tissue;
                wellAnn[i++] = cell.Treatment;
                wellAnn[i++] = cell.DonorID;
                wellAnn[i++] = cell.Weight;
                wellAnn[i++] = cell.Diameter.ToString();
                wellAnn[i++] = cell.Area.ToString();
                wellAnn[i++] = cell.Red.ToString();
                wellAnn[i++] = cell.Blue.ToString();
                wellAnn[i++] = cell.Green.ToString();
                wellAnn[i++] = cell.Comments;
                annotations[plateWell] = wellAnn;
            }
            string sqlPat = "SELECT c.PlateWell, Name, Value FROM CellAnnotation a LEFT JOIN Cell c ON a.CellID=c.CellID " +
                    string.Format("WHERE a.CellID IN (SELECT CellID FROM Cell {0})", chipOrProjectWhereSql);
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

        /// <summary>
        /// Return Chip names of all chips in the C1 database
        /// </summary>
        /// <returns></returns>
        public List<string> GetLoadedChips()
        {
            List<string> loadedChips = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT DISTINCT(Chip) FROM Cell";
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                loadedChips.Add(rdr.GetString(0));
            conn.Close();
            return loadedChips;
        }

        public void InsertOrUpdateCellAnnotation(int cellID, string key, string value)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "REPLACE INTO CellAnnotation (CellID, Name, Value) VALUES ('{0}','{1}','{2}')";
            sql = string.Format(sql, cellID, key, value);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            if (test)
                Console.WriteLine(sql);
            else
                cmd.ExecuteNonQuery();
            conn.Close();
        }
    }
}
