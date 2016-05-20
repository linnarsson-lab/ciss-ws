using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using MySql.Data;
using Linnarsson.Mathematics;

namespace Linnarsson.C1
{
    /// <summary>
    /// Calls related to expression data
    /// </summary>
    public class C1DB : Linnarsson.C1.IExpressionDB
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

        private bool IssueNonQuery(string sql)
        {
            bool success = true;
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
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
            }
            return success;
        }

        private int InsertAndGetLastId(string sql, string tableName)
        {
            int lastId = -1;
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                cmd.ExecuteNonQuery();
                string lastIdSql = string.Format("SELECT MAX({0}ID) FROM {0}", tableName);
                cmd = new MySqlCommand(lastIdSql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                if (rdr.Read())
                    lastId = rdr.GetInt32(0);
            }
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
                string sql = string.Format("SELECT * FROM Transcriptome WHERE Name='{0}' ORDER BY BuildDate DESC LIMIT 1", buildVarAnnot);
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
            catch (MySqlException)
            {
                Console.WriteLine("Warning: No MySQL Database for " + buildVarAnnot + " available.");
            }
            return t;
        }

        public IEnumerable<Transcript> IterTranscriptsFromDB(int transcriptomeId)
        {
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = string.Format("SELECT * FROM Transcript WHERE TranscriptomeID='{0}' AND Chromosome='CTRL' ORDER BY Start", transcriptomeId);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    yield return MakeTranscriptFromDBReader(rdr);
                sql = string.Format("SELECT * FROM Transcript WHERE TranscriptomeID='{0}' AND Chromosome!='CTRL' " +
                                    "AND LEFT(GeneName,2)!='r_' ORDER BY Chromosome, Start", transcriptomeId);
                rdr.Close();
                cmd = new MySqlCommand(sql, conn);
                rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    yield return MakeTranscriptFromDBReader(rdr);
                rdr.Close();
            }
        }

        public Dictionary<string, int> GetRepeatNamesToTranscriptIdsMap(string buildVarAnnot)
        {
            Dictionary<string, int> mapping = new Dictionary<string, int>();
            Transcriptome c1Trome = GetTranscriptome(buildVarAnnot);
            if (c1Trome == null)
                return mapping;
            string sql = string.Format("SELECT GeneName, TranscriptID FROM Transcript WHERE TranscriptomeID='{0}' AND Type='repeat';", 
                                       c1Trome.TranscriptomeID.Value);
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    mapping[rdr.GetString(0)] = rdr.GetInt32(1);
                rdr.Close();
            }
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
                                          rdr.GetString("ExonStarts"), rdr.GetString("ExonEnds"), rdr.GetString("StartToCloseCutSite"));
            return t;
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

        public void InsertChromosomePos(int transcriptomeID, string chrId, int startPos, int endPos)
        {
            string sql = "INSERT INTO WigChrom (TranscriptomeID, Chromosome, GenomeStartPos, GenomeEndPos) " +
                                 "VALUES ('{0}','{1}','{2}','{3}')";
            sql = string.Format(sql, transcriptomeID, chrId, startPos, endPos);
            IssueNonQuery(sql);
        }

        public void InsertChrWiggle(IEnumerator<Pair<int, int>> wiggle, int cellID, int transcriptomeID, string chr, char strand)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            string sql = string.Format("SELECT GenomeStartPos,GenomeEndPos FROM WigChrom WHERE TranscriptomeID={0} and Chromosome='{1}'", transcriptomeID, chr);
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                if (!rdr.Read()) return;
                int genomeStartPos = rdr.GetInt32(0);
                int genomeEndPos = rdr.GetInt32(1);
                rdr.Close();
                sql = string.Format("DELETE FROM Wig WHERE CellID={0} AND GenomePos>={1} and GenomePos<{2}", cellID, genomeStartPos, genomeEndPos);
                using (MySqlCommand dc = new MySqlCommand(sql, conn))
                    dc.ExecuteNonQuery();
                List<string> valueItems = new List<string>(1000);
                while (wiggle.MoveNext())
                    if (wiggle.Current.Second > 0)
                    {
                        valueItems.Add(string.Format("({0},{1},{2})", cellID, wiggle.Current.First + genomeStartPos, wiggle.Current.Second * strandSign));
                        if (valueItems.Count == 1000)
                        {
                            sql = "INSERT INTO Wig (CellID,GenomePos,MolCount) VALUES " + string.Join(",", valueItems.ToArray());
                            using (MySqlCommand c = new MySqlCommand(sql, conn))
                                c.ExecuteNonQuery();
                            valueItems.Clear();
                        }
                    }
                if (valueItems.Count > 0)
                {
                    sql = "INSERT INTO Wig (CellID,GenomePos,MolCount) VALUES " + string.Join(",", valueItems.ToArray());
                    using (MySqlCommand c = new MySqlCommand(sql, conn))
                        c.ExecuteNonQuery();
                }
            }
        }

        public void InsertTranscript(Transcript t)
        {
            string description = MySqlHelper.EscapeString(t.Description);
            string sql = "INSERT INTO Transcript (TranscriptomeID, Name, Type, GeneName, EntrezID, Description, Chromosome, " +
                                    "Start, End, Length, Strand, Extension5Prime, ExonStarts, ExonEnds, " + 
                                    "ExprBlobIdx, StartToCloseCutSite) " +
                                    "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}'," +
                                    "'{7}','{8}','{9}','{10}','{11}','{12}','{13}'," +
                                    "'{14}','{15}')";
            sql = string.Format(sql, t.TranscriptomeID, t.Name, t.Type, t.UniqueGeneName, t.EntrezID, description, t.Chromosome,
                                     t.Start, t.End, t.Length, t.Strand, t.Extension5Prime, t.ExonStarts, t.ExonEnds,
                                     t.ExprBlobIdx, t.StartToCloseCutSites);
            int newTranscriptId = InsertAndGetLastId(sql, "Transcript");
            t.TranscriptID = newTranscriptId;
            foreach (TranscriptAnnotation ta in t.TranscriptAnnotations)
            {
                ta.TranscriptID = t.TranscriptID.Value;
                InsertTranscriptAnnotation(ta);
            }
        }

        public bool UpdateTranscriptAnnotations(Transcript t)
        {
            int transcriptId = -1;
            string sql = "SELECT TranscriptID FROM Transcript WHERE TranscriptomeID='{0}' AND Type='{1}' " +
                              "AND GeneName='{2}' AND EntrezID='{3}' AND Chromosome='{4}'";
            sql = string.Format(sql, t.TranscriptomeID, t.Type, t.UniqueGeneName, t.EntrezID, t.Chromosome);
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                MySqlDataReader rdr = cmd.ExecuteReader();
                if (rdr.Read())
                {
                    transcriptId = int.Parse(rdr["TranscriptId"].ToString());
                    if (rdr.Read())
                        transcriptId = -1;
                }
            }
            if (transcriptId > -1)
            {
                IssueNonQuery("DELETE FROM TranscriptAnnotation WHERE TranscriptID=" + transcriptId);
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
            string sql = "REPLACE INTO TranscriptAnnotation (TranscriptID, Source, Value, Description) " +
                         "VALUES ('{0}','{1}','{2}','{3}')";
            sql = string.Format(sql, ta.TranscriptID, ta.Source, value, description);
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
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sql = string.Format("REPLACE INTO AnalysisSetup (Plate, Path, Genome, Parameters) VALUES ('{0}','{1}','{2}','{3}')",
                                           projectId, resultFolder, bowtieIndex, parameters);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
        }

        public int InsertExprBlobs(IEnumerable<ExprBlob> exprBlobIterator, bool mols, string aligner)
        {
            string table = mols ? "Expr" : "Read";
            int n = 0, maxId = 0, minId = int.MaxValue;
            using (MySqlConnection conn = new MySqlConnection(connectionString))
            {
                conn.Open();
                string sqlPat = "REPLACE INTO " + table + "Blob (CellID, TranscriptomeID, Aligner, Data) VALUES ('{0}',{1},'{2}', ?BLOBDATA)";
                foreach (ExprBlob exprBlob in exprBlobIterator)
                {
                    string sql = string.Format(sqlPat, exprBlob.jos_aaacellid, exprBlob.TranscriptomeID, aligner);
                    MySqlCommand cmd = new MySqlCommand(sql, conn);
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("?BLOBDATA", exprBlob.Blob);
                    cmd.ExecuteNonQuery();
                    n += 1;
                    maxId = Math.Max(int.Parse(exprBlob.jos_aaacellid), maxId);
                    minId = Math.Min(int.Parse(exprBlob.jos_aaacellid), minId);
                }
            }
            return n;
        }
    }
}
