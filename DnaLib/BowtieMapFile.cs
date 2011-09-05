using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public class BowtieMapFile
	{
        private Barcodes barcodes;
        private BarcodeMapper bm;

        public BowtieMapFile(Barcodes barcodes)
        {
            this.barcodes = barcodes;
            this.bm = new BarcodeMapper(barcodes);
        }

        public IEnumerable<BowtieMapRecord> Read(string file)
        {
            BowtieMapRecord recHolder = new BowtieMapRecord();
            string line;
            string[] arg;
            var reader = file.OpenRead();
            while (true)
            {
                recHolder.filePos = reader.BaseStream.Position;
                line = reader.ReadLine();
                if (line == null) break;
                arg = line.Split('\t');
                int p = arg[0].Length - barcodes.SeqLength;
                if (arg[0][p - 1] == '_')
                {
                    recHolder.ReadId = arg[0].Substring(0, p - 1);
                    recHolder.barcodeIdx = bm.GetBarcodeIdx(arg[0].Substring(p, barcodes.SeqLength));
                }
                else
                {
                    recHolder.ReadId = arg[0];
                    recHolder.barcodeIdx = bm.NOBARIdx;
                }
                recHolder.Strand = arg[1][0];
                recHolder.Chr = arg[2].StartsWith("chr") ? arg[2].Substring(3) : arg[2];
                recHolder.Position = int.Parse(arg[3]);
                recHolder.SeqLen = arg[4].Length;
                //recHolder.Sequence = arg[4];
                //recHolder.Qualities = ConvertQualitiesToIlluminaBase64(arg[5]);
                recHolder.AltMappings = int.Parse(arg[6]);
                recHolder.Mismatches = arg[7];
                arg = null;
                yield return recHolder;
            }
            reader.Close();
            yield break;
        }

        public IEnumerable<BowtieMapRecord[]> ReadBlocks(TextReader reader, int maxCount)
        {
            int nErronousRecords = 0;
            BowtieMapRecord[] recHolders = new BowtieMapRecord[maxCount + 1];
            for (int i = 0; i < recHolders.Length; i++)
                recHolders[i] = new BowtieMapRecord();
            string line;
            long filePos;
            string[] arg;
            int recIdx = 0;
            string lastReadId = "";
            while (true)
            {
                filePos = 0;//reader.BaseStream.Position;
                line = reader.ReadLine();
                if (line == null) break;
                if (lastReadId != "" && !line.StartsWith(lastReadId))
                {
                    recHolders[recIdx].Position = -1;
                    yield return recHolders;
                    recIdx = 0;
                    lastReadId = "";
                }
                arg = line.Split('\t');
                if (arg.Length < 8)
                {
                    if (nErronousRecords == 0)
                    { // Be kind with erronous or truncated records in the input file.
                        Console.Error.WriteLine("Detected erronous record in .map file: {0}", line);
                        nErronousRecords++;
                    }
                    continue;
                }
                int p = arg[0].Length - barcodes.SeqLength;
                if (p > 0 && arg[0][p - 1] == '_')
                {
                    recHolders[recIdx].ReadId = arg[0].Substring(0, p - 1);
                    recHolders[recIdx].barcodeIdx = bm.GetBarcodeIdx(arg[0].Substring(p, barcodes.SeqLength));
                }
                else
                {
                    recHolders[recIdx].ReadId = arg[0];
                    recHolders[recIdx].barcodeIdx = bm.NOBARIdx;
                }
                lastReadId = recHolders[recIdx].ReadId;
                recHolders[recIdx].filePos = filePos;
                recHolders[recIdx].Strand = arg[1][0];
                recHolders[recIdx].Chr = arg[2].StartsWith("chr") ? arg[2].Substring(3) : arg[2];
                recHolders[recIdx].Position = int.Parse(arg[3]);
                recHolders[recIdx].SeqLen = arg[4].Length;
                recHolders[recIdx].AltMappings = int.Parse(arg[6]);
                recHolders[recIdx].Mismatches = arg[7];
                recIdx++;
            }
            reader.Close();
            if (nErronousRecords > 0)
                Console.Error.WriteLine("Skipped totally {0} erronous records in .map file", nErronousRecords);
            yield break;
        }
        
		/// <summary>
		/// Convert a Sanger (33-based) quality string to an Illumina (64-based) string
		/// </summary>
		/// <param name="qs"></param>
		private static string ConvertQualitiesToIlluminaBase64(string qs)
		{
			char[] result = new char[qs.Length];
			for (int i = 0; i < qs.Length; i++)
			{
				int temp = (int)qs[i];
				result[i] = (char)(temp - 33 + 64); 
			}
			return result.ToString();
		}
	}

	public struct BowtieMapRecord
	{
        public string ReadId;
        public int barcodeIdx;
        public char Strand;
        public string Chr;
        public int Position;
        public int SeqLen;
        //public string Sequence;
        //public string Qualities;
        public string Mismatches;
        public long filePos;
        public int AltMappings;

        public BowtieMapRecord(string id, int bcIdx, char strand, string chr, int pos, int len, long fPos)
        {
            ReadId = id;
            barcodeIdx = bcIdx;
            Strand = strand;
            Chr = chr;
            Position = pos;
            SeqLen = len;
            filePos = fPos;
            AltMappings = 1;
            Mismatches = null;
        }
        public override string ToString()
        {
            return ReadId + "\tchr" + Chr + "\t" + Strand + "\t" + Position;
        }
	}
}
