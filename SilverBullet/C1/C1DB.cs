﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
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
        /// <returns>null if no match exists in database</returns>
        public Transcriptome GetTranscriptome(string buildVarAnnot)
        {
            Transcriptome t = null;
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
                Transcript t = new Transcript(rdr.GetInt32("TranscriptID"), rdr.GetInt32("TranscriptomeID"), rdr.GetString("Name"),
                                              rdr.GetString("Type"), rdr.GetString("GeneName"), rdr.GetString("Description"),
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
            string sql = "INSERT INTO CellImage (CellID, Reporter, Marker, Detection, RelativePath) " +
                               "VALUES ({0},'{1}','{2}','{3}','{4}')";
            sql = string.Format(sql, ci.CellID, ci.Reporter, ci.Marker, ci.Detection, ci.RelativePath);
            IssueNonQuery(sql);
        }

        public void InsertCell(Cell c)
        {
            CultureInfo cult = new CultureInfo("sv-SE"); 
            string sql = "INSERT INTO Cell (Plate, Well, StrtProtocol, DateCollected, Species, " +
                                            "Strain, Age, Sex, Tissue, Treatment, Diameter, Area, PI, Operator, Comments) " +
                               "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}','{13}','{14}')";
            sql = string.Format(sql, c.Plate, c.Well, c.StrtProtocol, c.DateCollected.ToString(cult), c.Species,
                                     c.Strain, c.Age, c.Sex, c.Tissue,
                                     c.Treatment, c.Diameter, c.Area, c.PI, c.Operator, c.Comments);
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
            string sql = "INSERT INTO Transcript (TranscriptomeID, Name, Type, GeneName, Description, Chromosome, " +
                                                 "Start, End, Length, Strand, Extension5Prime, ExonStarts, ExonEnds) " +
                                "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}')";
            sql = string.Format(sql, t.TranscriptomeID, t.Name, t.Type, t.GeneName, description, t.Chromosome,
                                     t.Start, t.End, t.Length, t.Strand, t.Extension5Prime, t.ExonStarts, t.ExonEnds);
            int transcriptomeId = InsertAndGetLastId(sql, "Transcript");
            t.TranscriptID = transcriptomeId;
        }

        public void InsertExpressions(IEnumerable<Expression> exprIterator)
        {
            MySqlConnection conn = new MySqlConnection(connectionString);
            conn.Open();
            string sqlPat = "INSERT INTO Expression (CellID, TranscriptID, UniqueMolecules, UniqueReads, MaxMolecules, MaxReads) " +
                            "VALUES ('{0}','{1}','{2}','{3}','{4}','{5}')";
            foreach (Expression e in exprIterator)
            {
                string sql = string.Format(sqlPat, e.CellID, e.TranscriptID, e.UniqueMolecules, e.UniqueReads, e.MaxMolecules, e.MaxReads);
                MySqlCommand cmd = new MySqlCommand(sql, conn);
                if (test)
                    Console.WriteLine(sql);
                else
                    cmd.ExecuteNonQuery();
            }
            conn.Close();
        }

        public Dictionary<string, int> GetCellIdByWell(string projectId)
        {
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

    }
}
