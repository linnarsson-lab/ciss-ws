using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using C1;

namespace C1FillExprBlobs
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
            {
                Console.WriteLine("Usage:\nmono C1FillExprBlobs.exe TRANSCRIPTOME_ID\n\n");
            }
            else
            {
                int transcriptomeID = int.Parse(args[0]);
                new C1FillExprBlobs().InsertAll(transcriptomeID);
            }
        }
    }

    class C1FillExprBlobs
    {
        public void InsertAll(int transcriptomeID)
        {
            C1DB db = new C1DB();
            Dictionary<int, int> trIDToBlobIdx = new Dictionary<int, int>();
            foreach (Transcript t in db.IterTranscriptsFromDB(transcriptomeID))
                trIDToBlobIdx[t.TranscriptID.Value] = t.ExprBlobIdx;
            int nTranscripts = trIDToBlobIdx.Count;
            ExprBlob exprBlob = new ExprBlob(nTranscripts);
            List<string> cellIds = db.GetCellIds();
            int nCells = 0, nInsCells = 0;
            foreach (string cellId in cellIds)
            {
                nCells++;
                Console.Write(".");
                exprBlob.ClearBlob();
                int nValues = 0;
                foreach (Expression e in db.IterExpressions(cellId, transcriptomeID))
                {
                    nValues++;
                    exprBlob.SetBlobValue(trIDToBlobIdx[e.TranscriptID], e.Molecules);
                }
                if (nValues != nTranscripts)
                {
                    if (nValues > 0)
                        Console.WriteLine("\nError: Got {0} values, expected {1}.", nValues, nTranscripts);
                }
                else
                {
                    exprBlob.CellID = cellId;
                    exprBlob.TranscriptomeID = transcriptomeID;
                    db.InsertExprBlob(exprBlob);
                    nInsCells++;
                }
            }
            Console.WriteLine("\nInserted blob data for {0} out of {1} cells.", nInsCells, nCells);
        }

    }
}
