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
        private StreamReader reader;
        private MultiReadMappings mrm;

        public BowtieMapFile(string file, int maxNMappings, Barcodes barcodes)
        {
            reader = new StreamReader(file);
            this.barcodes = barcodes;
            mrm = new MultiReadMappings(maxNMappings, barcodes);
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

        public IEnumerable<MultiReadMappings> MultiMappings()
        {
            string line = reader.ReadLine();
            if (line == null) yield break;
            string[] fields = line.Split('\t');
            if (fields.Length < 8)
                throw new FormatException("Too few columns in input bowtie map file");
            mrm.Init(fields[0], fields[4].Length, int.Parse(fields[6]));
            while (line != null)
            {
                fields = line.Split('\t');
                if (!line.StartsWith(mrm.ReadId))
                {
                    yield return mrm;
                    mrm.Init(fields[0], fields[4].Length, int.Parse(fields[6]));
                }
                mrm.AddMapping(fields[2], fields[1][0], int.Parse(fields[3]), fields[7]);
                line = reader.ReadLine();
            }
            reader.Close();
            yield return mrm;
            yield break;
        }
	}

    public class MultiReadMapping
    {
        public string Chr;
        public char Strand;
        public int Position;
        public string Mismatches;
    }
    public class MultiReadMappings
    {
        private static Barcodes Barcodes;

        public string ReadId;
        public int BarcodeIdx;
        public int RandomBcIdx;
        public int SeqLen;
        public int AltMappings;
        public int NMappings;
        private MultiReadMapping[] Mappings;
        public MultiReadMappings(int maxNMappings, Barcodes barcodes)
        {
            Mappings = new MultiReadMapping[maxNMappings];
            for (int i = 0; i < maxNMappings; i++)
                Mappings[i] = new MultiReadMapping();
            Barcodes = barcodes;
        }
        public void Init(string combinedReadId, int seqLen, int altMappings)
        {
            ReadId = Barcodes.ExtractBarcodesFromReadId(combinedReadId, out BarcodeIdx, out RandomBcIdx);
            SeqLen = seqLen;
            AltMappings = altMappings;
            NMappings = 0;
        }
        public void AddMapping(string chr, char strand, int pos, string mismatches)
        {
            if (NMappings < Mappings.Length)
            {
                Mappings[NMappings].Chr = chr.StartsWith("chr") ? chr.Substring(3) : chr;
                Mappings[NMappings].Strand = strand;
                Mappings[NMappings].Position = pos;
                Mappings[NMappings].Mismatches = mismatches;
                NMappings++;
            }
        }
        public MultiReadMapping this[int idx]
        {
            get { return (idx < NMappings)? Mappings[idx] : null; }
        }

        public IEnumerable<MultiReadMapping> ValidMappings()
        {
            for (int idx = 0; idx < NMappings; idx++)
                yield return Mappings[idx];
            yield break;
        }

        public void FromBamAlignedRead(BamAlignedRead a)
        {
            NMappings = 1;
            Mappings[0].Chr = (a.Chromosome.StartsWith("chr")) ? a.Chromosome.Substring(3) : a.Chromosome;
            Mappings[0].Strand = (a.Strand == DnaStrand.Forward) ? '+' : '-';
            Mappings[0].Position = a.Position - 1; // SAM data are 1-based - "samtools view" delivers SAM-style data from BAM files
            SeqLen = (int)a.QuerySequence.Count;
            AltMappings = 0;
            ReadId = Barcodes.ExtractBarcodesFromReadId(a.QueryName, out BarcodeIdx, out RandomBcIdx);
            foreach (string x in a.ExtraFields)
                if (x.StartsWith("XM:i:"))
                {
                    AltMappings = int.Parse(x.Substring(5));
                    break;
                }
            Mappings[0].Mismatches = ""; // Do not handle the mismatches for BAM input - need seq to get substitution bases.
        }

        public void InitSingleMapping(string readId, string chr, char strand, int position, int seqLen, int altMappings, string mismatches)
        {
            ReadId = readId;
            SeqLen = seqLen;
            AltMappings = altMappings;
            NMappings = 1;
            Mappings[0].Chr = chr;
            Mappings[0].Strand = strand;
            Mappings[0].Mismatches = mismatches;
        }
    }
}
