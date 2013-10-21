using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private readonly static string connectionString = "server=192.168.1.12;uid=cuser;pwd=3pmknHQyl;database=cells10k;Connect Timeout=300;Charset=utf8;";

        public C1DB()
        {
        }

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

        public IEnumerable<Transcript> IterTranscripts(int transcriptomeId)
        {
            string sql = string.Format("SELECT * FROM Transcript WHERE TranscriptomeID='{0}' ORDER BY TranscriptID", transcriptomeId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string uniqueName = rdr.GetString("GeneName");
                Match m = Regex.Match(uniqueName, "_v[0-9]+$");
                string geneName = (m.Success)? uniqueName.Substring(0, m.Index) : uniqueName;
                Transcript t = new Transcript(rdr.GetInt32("TranscriptID"), rdr.GetInt32("TranscriptomeID"), rdr.GetString("Name"),
                                              rdr.GetString("Type"), geneName, uniqueName, rdr.GetString("EntrezID"),
                                              rdr.GetString("Description"),
                                              rdr.GetString("Chromosome"), rdr.GetInt32("Start"), rdr.GetInt32("End"), 
                                              rdr.GetInt32("Length"), rdr.GetChar("Strand"), rdr.GetInt32("Extension5Prime"),
                                              rdr.GetString("ExonStarts"), rdr.GetString("ExonEnds"));
                yield return t;
            }
            conn.Close();
        }

        public List<string> GetAllPlateIds()
        {
            List<string> plateIds = new List<string>();
            MySqlConnection conn = new MySqlConnection(connectionString);
            string sql = "SELECT DISTINCT Plate FROM Cell";
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string file = rdr["Plate"].ToString();
                plateIds.Add(file);
            }
            rdr.Close();
            conn.Close();
            return plateIds;
        }

        public void InsertCellImage(CellImage ci)
        {
            int detectionValue = (ci.Detection == Detection.Yes) ? 1 : (ci.Detection == Detection.No) ? -1 : 0;
            string sql = "INSERT INTO CellImage (CellID, Reporter, Marker, Detection, RelativePath) " +
                               "VALUES ({0},'{1}','{2}','{3}','{4}')";
            sql = string.Format(sql, ci.CellID, ci.Reporter, ci.Marker, detectionValue, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void InsertCell(Cell c)
        {
            CultureInfo cult = new CultureInfo("sv-SE");
            string sql = "INSERT INTO Cell (Plate, Well, StrtProtocol, DateCollected, Species, Strain, Age, Sex, Tissue, " +
                                            "Treatment, Diameter, Area, PI, Operator, Comments, Red, Green, Blue) " +
                         "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}','{13}','{14}','{15}','{16}','{17}')";
            sql = string.Format(sql, c.Plate, c.Well, c.StrtProtocol, c.DateCollected.ToString(cult), c.Species, c.Strain, c.Age, c.Sex, c.Tissue,
                                     c.Treatment, c.Diameter, c.Area, c.PI, c.Operator, c.Comments, c.Red, c.Green, c.Blue);
            int cellId = InsertAndGetLastId(sql, "Cell");
            foreach (CellImage ci in c.cellImages)
            {
                ci.CellID = cellId;
                InsertCellImage(ci);
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
            string sql = "REPLACE INTO Transcript (TranscriptomeID, Name, Type, GeneName, EntrezID, Description, Chromosome, " +
                                                 "Start, End, Length, Strand, Extension5Prime, ExonStarts, ExonEnds) " +
                                "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}','{13}')";
            sql = string.Format(sql, t.TranscriptomeID, t.Name, t.Type, t.UniqueGeneName, t.EntrezID, description, t.Chromosome,
                                     t.Start, t.End, t.Length, t.Strand, t.Extension5Prime, t.ExonStarts, t.ExonEnds);
            int transcriptomeId = InsertAndGetLastId(sql, "Transcript");
            t.TranscriptID = transcriptomeId;
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
            //string sqlPat = "INSERT INTO Expression (CellID, TranscriptID, UniqueMolecules, UniqueReads, MaxMolecules, MaxReads) " +
            //                "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}')";
            string sqlPat = "REPLACE INTO Expression (CellID, TranscriptID, Molecules) VALUES ('{0}',{1}, {2})";
            foreach (Expression e in exprIterator)
            {
                //string sql = string.Format(sqlPat, e.CellID, e.TranscriptID, e.UniqueMolecules, e.UniqueReads, e.MaxMolecules, e.MaxReads);
                string sql = string.Format(sqlPat, e.CellID, e.TranscriptID, e.Molecules);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        private static string StripC1Indicator(string projectId)
        {
            return projectId.StartsWith(C1Props.C1ProjectPrefix) ? projectId.Substring(C1Props.C1ProjectPrefix.Length) : projectId;
        }

        public Dictionary<string, int> GetCellIdByWell(string projectId)
        {
            projectId = StripC1Indicator(projectId);
            Dictionary<string, int> cellIdByWell = new Dictionary<string, int>();
            string sql = string.Format("SELECT Well, CellID FROM Cell WHERE Plate='{0}' ORDER BY Well", projectId);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
                cellIdByWell.Add(rdr.GetString(0), rdr.GetInt32(1));
            conn.Close();
            return cellIdByWell;
        }

        private static List<string> GetCellAnnotationNames(string projectId)
        {
            projectId = StripC1Indicator(projectId);
            string sql = "SELECT DISTINCT(Name) FROM CellAnnotation WHERE CellID IN (SELECT CellID FROM Cell WHERE Plate='{0}')";
            sql = string.Format(sql, projectId);
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
        /// Read plate layout data from the database
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="annotations"></param>
        /// <param name="annotationIndexes"></param>
        public static void GetCellAnnotations(string projectId, 
            out Dictionary<string, string[]> annotations, out Dictionary<string, int> annotationIndexes)
        {
            projectId = StripC1Indicator(projectId);
            List<string> annotNames = GetCellAnnotationNames(projectId);
            annotationIndexes = new Dictionary<string, int>(7 + annotNames.Count);
            annotationIndexes["CellID"] = 0;
            annotationIndexes["Species"] = 1;
            annotationIndexes["Diameter"] = 2;
            annotationIndexes["Area"] = 3;
            annotationIndexes["Red"] = 4;
            annotationIndexes["Blue"] = 5;
            annotationIndexes["Green"] = 6;
            for (int i = 0; i < annotNames.Count; i++)
                annotationIndexes[annotNames[i]] = annotationIndexes.Count;
            annotations = new Dictionary<string, string[]>(96);
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sql = "SELECT Well, CellID, Species, Diameter, Area, Red, Blue, Green FROM Cell WHERE Plate='{0}'";
            sql = string.Format(sql, projectId);
            MySqlCommand cmd = new MySqlCommand(sql, conn);
            MySqlDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string[] wellAnn = new string[annotationIndexes.Count];
                wellAnn[0] = rdr.GetString(1);
                wellAnn[1] = rdr.GetString(2);
                wellAnn[2] = rdr.GetString(3);
                wellAnn[3] = rdr.GetString(4);
                wellAnn[4] = rdr.GetString(5);
                wellAnn[5] = rdr.GetString(6);
                wellAnn[6] = rdr.GetString(7);
                string well = rdr.GetString(0);
                annotations[well] = wellAnn;
            }
            rdr.Close();
            sql = "SELECT Well, Name, Value FROM CellAnnotation a LEFT JOIN Cell c ON a.CellID=c.CellID " +
                    "WHERE a.CellID IN (SELECT CellID FROM Cell WHERE Plate='{0}')";
            sql = string.Format(sql, projectId);
            cmd = new MySqlCommand(sql, conn);
            rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                string well = rdr.GetString(0);
                string name = rdr.GetString(1);
                string value = rdr.GetString(2);
                annotations[well][annotationIndexes[name]] = value;
            }
            rdr.Close();
            conn.Close();
        }

    }
}
