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

        public BowtieMapFile(Barcodes barcodes)
        {
            this.barcodes = barcodes;
        }

        public IEnumerable<ReadMapping> Read(string file)
        {
            ReadMapping recHolder = new ReadMapping();
            var reader = file.OpenRead();
            string line = reader.ReadLine();
            string[] fields = line.Split('\t');
            while (line != null)
            {
                fields = line.Split('\t');
                ParseFields(ref recHolder, fields);
                barcodes.ExtractBarcodesFromReadId(ref recHolder.ReadId, out recHolder.barcodeIdx, out recHolder.randomBcIdx);
                fields = null;
                yield return recHolder;
                line = reader.ReadLine();
            }
            reader.Close();
            yield break;
        }

        private void ParseFields(ref ReadMapping recHolder, string[] fields)
        {
            recHolder.Strand = fields[1][0];
            recHolder.Chr = fields[2].StartsWith("chr") ? fields[2].Substring(3) : fields[2];
            recHolder.Position = int.Parse(fields[3]);
            recHolder.SeqLen = fields[4].Length;
            recHolder.AltMappings = int.Parse(fields[6]);
            recHolder.Mismatches = fields[7];
        }

        /// <summary>
        /// Generate sets of alternative alignments for every read in the file.
        /// N.B.: Only the first record in each set will get barcode and readId set!
        /// </summary>
        /// <param name="file"></param>
        /// <param name="maxCount"></param>
        /// <returns></returns>
        public IEnumerable<ReadMapping[]> ReadBlocks(string file, int maxCount)
        {
            ReadMapping[] recHolders = new ReadMapping[maxCount + 1];
            for (int i = 0; i < recHolders.Length; i++)
                recHolders[i] = new ReadMapping();
            StreamReader reader = file.OpenRead();
            string line = reader.ReadLine();
            string[] fields = line.Split('\t');
            if (fields.Length < 8)
                throw new FormatException("Too few columns in input bowtie map file " + file);
            int recIdx = 0;
            while (line != null)
            {
                if (!line.StartsWith(fields[0]))
                {
                    barcodes.ExtractBarcodesFromReadId(ref recHolders[0].ReadId, out recHolders[0].barcodeIdx, out recHolders[0].randomBcIdx);
                    recHolders[recIdx].Position = -1;
                    yield return recHolders;
                    recIdx = 0;
                }
                fields = line.Split('\t');
                ParseFields(ref recHolders[recIdx], fields);
                recIdx++;
                line = reader.ReadLine();
            }
            reader.Close();
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

	public struct ReadMapping
	{
        public string ReadId;
        public int barcodeIdx;
        public int randomBcIdx;
        public char Strand;
        public string Chr;
        public int Position;
        public int SeqLen;
        public string Mismatches;
        public int AltMappings;

        public ReadMapping(string id, int bcIdx, char strand, string chr, int pos, int len, int rndBcIdx)
        {
            ReadId = id;
            barcodeIdx = bcIdx;
            randomBcIdx = rndBcIdx;
            Strand = strand;
            Chr = chr;
            Position = pos;
            SeqLen = len;
            AltMappings = 1;
            Mismatches = null;
        }
        public override string ToString()
        {
            return ReadId + " BcIdx=" + barcodeIdx + " RndTagIdx=" + randomBcIdx + " Chr=" + Chr + Strand + ":" + Position + " Alt=" + AltMappings;
        }

        public static ReadMapping FromBamAlignedRead(BamAlignedRead a, Barcodes barcodes)
        {
            ReadMapping recHolder = new ReadMapping();
            recHolder.ReadId = a.QueryName;
            recHolder.Strand = (a.Strand == DnaStrand.Forward) ? '+' : '-';
            recHolder.Chr = (a.Chromosome.StartsWith("chr")) ? a.Chromosome.Substring(3) : a.Chromosome;
            recHolder.Position = a.Position;
            recHolder.SeqLen = (int)a.QuerySequence.Count;
            recHolder.AltMappings = 0;
            barcodes.ExtractBarcodesFromReadId(ref recHolder.ReadId, out recHolder.barcodeIdx, out recHolder.randomBcIdx);
            foreach (string x in a.ExtraFields)
                if (x.StartsWith("XM:i:"))
                {
                    recHolder.AltMappings = int.Parse(x.Substring(5));
                    break;
                }
            recHolder.Mismatches = ""; // Do not handle the mismatches for BAM input - need seq to get substitution bases.
            return recHolder;
        }

    }
}
