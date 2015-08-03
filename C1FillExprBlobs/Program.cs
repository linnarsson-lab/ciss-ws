using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.C1;

namespace C1FillExprBlobs
{
    enum ZeroStorage { StoreAll, StoreNull, StoreNothing };

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("DEPRECATED");
                /*Usage:\nmono C1FillExprBlobs.exe TRANSCRIPTOME_ID\n\n" +
                                      "Options:\n" +
                                      "--null         Store NULL instead of 0-array when all data is 0.\n" +
                                      "--skip0        Do not store anything when all data is 0.\n");*/
            }
        }
            /*            }
                        else
                        {
                            ZeroStorage zeroStorage = ZeroStorage.StoreAll;
                            int transcriptomeID = 0;
                            foreach (string arg in args)
                            {
                                if (arg == "--null") zeroStorage = ZeroStorage.StoreNull;
                                else if (arg == "--skip0") zeroStorage = ZeroStorage.StoreNothing;
                                else transcriptomeID = int.Parse(arg);
                            }
                            new C1FillExprBlobs().Insert(transcriptomeID, zeroStorage);
                        }
                    }
                }
             ********** FROM C1DB.cs:
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
            ************

                class C1FillExprBlobs
                {
                    public void Insert(int transcriptomeID, ZeroStorage zeroStorage)
                    {
                        C1DB db = new C1DB();
                        ProjectDB pdb = new ProjectDB();
                        Dictionary<int, int> trIDToBlobIdx = new Dictionary<int, int>();
                        foreach (Transcript t in db.IterTranscriptsFromDB(transcriptomeID))
                            trIDToBlobIdx[t.TranscriptID.Value] = t.ExprBlobIdx;
                        int nTranscripts = trIDToBlobIdx.Count;
                        ExprBlob exprBlob = new ExprBlob(nTranscripts);
                        List<string> cellIds = pdb.GetCellIds();
                        int nCells = 0, nInsCells = 0;
                        foreach (string cellId in cellIds)
                        {
                            nCells++;
                            Console.Write(".");
                            exprBlob.ClearBlob();
                            int nValues = 0;
                            int totalMols = 0;
                            foreach (Expression e in db.IterExpressions(cellId, transcriptomeID))
                            {
                                nValues++;
                                exprBlob.SetBlobValue(trIDToBlobIdx[e.TranscriptID], e.Molecules);
                                totalMols += e.Molecules;
                            }
                            if (nValues != nTranscripts)
                            {
                                if (nValues > 0)
                                    Console.WriteLine("\nError: Got {0} values for cell {1}, expected {2}.", nValues, cellId, nTranscripts);
                            }
                            else if (totalMols == 0 && zeroStorage == ZeroStorage.StoreNothing)
                            {
                                Console.WriteLine("\nSkipping cell {0}, total expression = {1}", cellId, totalMols);
                            }
                            else
                            {
                                exprBlob.CellID = cellId;
                                exprBlob.TranscriptomeID = transcriptomeID;
                                db.InsertExprBlob(exprBlob, zeroStorage == ZeroStorage.StoreNull);
                                nInsCells++;
                            }
                        }
                        Console.WriteLine("\nInserted blob data for {0} out of {1} cells.", nInsCells, nCells);
                    }*/

    }
}
