using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using C1;

namespace C1FillExprBlobs
{
    enum ZeroStorage { StoreAll, StoreNull, StoreNothing };

    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono C1FillExprBlobs.exe TRANSCRIPTOME_ID\n\n" +
                                  "Options:\n" +
                                  "--null         Store NULL instead of 0-array when all data is 0.\n" +
                                  "--skip0        Do not store anything when all data is 0.\n");
            }
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
        }

    }
}
