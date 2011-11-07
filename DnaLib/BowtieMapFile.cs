using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public abstract class MapFile
    {
        protected bool m_RedundantSecondaryMappings = false;
        public bool RedundantSecondaryMappings { get { return m_RedundantSecondaryMappings; } }

        public int MaxNMAppings = 20;
        protected Barcodes barcodes;
        protected MultiReadMappings mrm;

        public static MapFile GetMapFile(string file, int maxNMappings, Barcodes barcodes)
        {
            if (file.EndsWith(".smap"))
                return new SortedMapFile(maxNMappings, barcodes);
            if (file.EndsWith(".map"))
                return new BowtieMapFile(maxNMappings, barcodes);
            if (file.EndsWith(".bam") || file.EndsWith(".sbam"))
                return new BamMapFile(barcodes, RandomTagFilter.SortedAnalysisWindowSize);
            return null;
        }

        public MapFile(int maxNMappings, Barcodes barcodes)
        {
            this.barcodes = barcodes;
            mrm = new MultiReadMappings(maxNMappings, barcodes);
        }

        /// <summary>
        /// Convert a Sanger (33-based) quality string to an Illumina (64-based) string
        /// </summary>
        /// <param name="qs"></param>
        protected static string ConvertQualitiesToIlluminaBase64(string qs)
        {
            char[] result = new char[qs.Length];
            for (int i = 0; i < qs.Length; i++)
            {
                int temp = (int)qs[i];
                result[i] = (char)(temp - 33 + 64);
            }
            return result.ToString();
        }

        /// <summary>
        /// Iterates through mapped reads of file.
        /// Every multiread will be combined into one result object containing alternative mappings
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public abstract IEnumerable<MultiReadMappings> MultiMappings(string file);
        /// <summary>
        /// Iterates through all mappings of file.
        /// A multiread will generate one result with the same readId for every alternative mapping
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public abstract IEnumerable<MultiReadMappings> SingleMappings(string file);
    }

    public class BowtieMapFile : MapFile
	{
        public BowtieMapFile(int maxNMappings, Barcodes barcodes)
            : base(maxNMappings, barcodes)
        { }
        
        public override IEnumerable<MultiReadMappings> MultiMappings(string file)
        {
            StreamReader reader = new StreamReader(file);
            string line = reader.ReadLine();
            if (line == null) yield break;
            string[] fields = line.Split('\t');
            if (fields.Length < 8)
                throw new FormatException("Too few columns in input bowtie map file");
            string combinedReadId = fields[0];
            mrm.Init(combinedReadId, fields[4].Length, int.Parse(fields[6]));
            while (line != null)
            {
                fields = line.Split('\t');
                if (!line.StartsWith(combinedReadId))
                {
                    yield return mrm;
                    combinedReadId = fields[0];
                    mrm.Init(combinedReadId, fields[4].Length, int.Parse(fields[6]));
                }
                mrm.AddMapping(fields[2], fields[1][0], int.Parse(fields[3]), fields[7]);
                line = reader.ReadLine();
            }
            reader.Close();
            yield return mrm;
            yield break;
        }

        public override IEnumerable<MultiReadMappings> SingleMappings(string file)
        {
            StreamReader reader = new StreamReader(file);
            string line = reader.ReadLine();
            if (line == null) yield break;
            string[] fields = line.Split('\t');
            if (fields.Length < 8)
                throw new FormatException("Too few columns in input bowtie map file");
            while (line != null)
            {
                fields = line.Split('\t');
                mrm.Init(fields[0], fields[4].Length, int.Parse(fields[6]));
                mrm.AddMapping(fields[2], fields[1][0], int.Parse(fields[3]), fields[7]);
                yield return mrm;
                line = reader.ReadLine();
            }
            reader.Close();
            yield break;
        }

        public static void ParseFileItem(string linesBlock, ref MultiReadMappings mrm)
        {
            string[] lines = linesBlock.Split('\n');
            string[] fields = lines[0].Split('\t');
            mrm.Init(fields[0], fields[4].Length, int.Parse(fields[6]));
            for (int lIdx = 0; lIdx < lines.Length; lIdx++)
            {
                fields = lines[lIdx].Split('\t');
                mrm.AddMapping(fields[2], fields[1][0], int.Parse(fields[3]), fields[7]);
            }
        }
	}

    public class SortedMapFile : MapFile
    {
        public SortedMapFile(int maxNMappings, Barcodes barcodes)
            : base(maxNMappings, barcodes)
        { 
            m_RedundantSecondaryMappings = true;
        }

        public override IEnumerable<MultiReadMappings> MultiMappings(string file)
        {
            StreamReader reader = new StreamReader(file);
            string line = reader.ReadLine();
            if (line == null) yield break;
            string[] fields = line.Split('\t');
            if (fields.Length < 7)
                throw new FormatException("Too few columns in input sorted map file");
            while (line != null)
            {
                ParseFileItem(line, ref mrm);
                yield return mrm;
                line = reader.ReadLine();
            }
            reader.Close();
            yield break;
        }

        public override IEnumerable<MultiReadMappings> SingleMappings(string file)
        {
            throw new NotImplementedException("Not meaningful for this filetype");
        }

        public static void ParseFileItem(string line, ref MultiReadMappings mrm)
        {
            // ReadId \t BcIdx \t RndBcIdx \t SeqLen \t MappingNo \t AltMappings [\t ChrId/Strand/Pos/Mismatches ] +
            string[] fields = line.Split('\t');
            mrm.Init(fields[0], int.Parse(fields[1]), int.Parse(fields[2]), int.Parse(fields[3]),
                     int.Parse(fields[4]), int.Parse(fields[5]));
            for (int fIdx = 6; fIdx < fields.Length; fIdx++)
            {
                string[] m = fields[fIdx].Split('/');
                mrm.AddMapping(m[0], m[1][0], int.Parse(m[2]), m[3]);
            }
        }

        public static string ToFileItem(MultiReadMappings mrm)
        {
            string line = mrm.ReadId + "\t" + mrm.BarcodeIdx + "\t" + mrm.RandomBcIdx + "\t" + mrm.SeqLen + "\t" + 
                          mrm.MappingNumber + "\t" + mrm.AltMappings;
            for (int mIdx = 0; mIdx < mrm.NMappings; mIdx++)
                line += "\t" + mrm[mIdx].Chr + "/" + mrm[mIdx].Strand + "/" + mrm[mIdx].Position + "/" + mrm[mIdx].Mismatches;
            return line;
        }
    }

    public class BamMapFile : MapFile
    {
        private int bamFileWindowSize;

        public BamMapFile(Barcodes barcodes, int readFetchWindowSize)
            : base(1, barcodes)
        {
            bamFileWindowSize = readFetchWindowSize;
        }

        public override IEnumerable<MultiReadMappings> MultiMappings(string file)
        {
            BamFile bamf = new BamFile(file);
            foreach (string chrName in bamf.Chromosomes)
            {
                for (int windowStart = 0; windowStart < bamf.GetChromosomeLength(chrName); windowStart += bamFileWindowSize)
                {
                    List<BamAlignedRead> bamReads = bamf.Fetch(chrName, windowStart, windowStart + bamFileWindowSize);
                    foreach (BamAlignedRead a in bamReads)
                    {
                        ParseFileItem(a, ref mrm);
                        yield return mrm;
                    }
                }
            }
        }

        public override IEnumerable<MultiReadMappings> SingleMappings(string file)
        {
            BamFile bamf = new BamFile(file);
            foreach (string chrName in bamf.Chromosomes)
            {
                for (int windowStart = 0; windowStart < bamf.GetChromosomeLength(chrName); windowStart += bamFileWindowSize)
                {
                    foreach (string line in bamf.IterLines(chrName, windowStart, windowStart + bamFileWindowSize))
                    {
                        string[] fields = line.Split('\t');
                        int altMappings = 0;
                        for (int i = 11; i < fields.Length; i++)
                        {
                            if (fields[i].StartsWith("XM:i:"))
                            {
                                altMappings = int.Parse(fields[i].Substring(5));
                                break;
                            }
                        }
                        mrm.Init(fields[0], fields[9].Length, altMappings);
                        char strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                        mrm.AddMapping(fields[2], strand, int.Parse(fields[3]), "");
                        yield return mrm;
                    }
                }
            }
            yield break;
        }

        public static void ParseFileItem(BamAlignedRead a, ref MultiReadMappings mrm)
        {
            string chr = (a.Chromosome.StartsWith("chr")) ? a.Chromosome.Substring(3) : a.Chromosome;
            char strand = (a.Strand == DnaStrand.Forward) ? '+' : '-';
            //int altMappings = ParseAltMappings(a.ExtraFields);
            mrm.Init(a.QueryName, (int)a.QuerySequence.Count, 0);
            mrm.AddMapping(chr, strand, a.Position - 1, "");
        }

        private static int ParseAltMappings(string[] extraFields)
        {
            foreach (string x in extraFields)
                if (x.StartsWith("XM:i:"))
                    return int.Parse(x.Substring(5));
            return 0;
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
        private Barcodes Barcodes;

        public string ReadId;
        public int BarcodeIdx;
        public int RandomBcIdx = 0;
        public int SeqLen;
        public int AltMappings;
        public int MappingNumber;
        public bool IsFirstMapping { get { return MappingNumber == 0; } }
        public int NMappings;
        private MultiReadMapping[] Mappings;

        public MultiReadMappings(int maxNMappings, Barcodes barcodes)
        {
            Mappings = new MultiReadMapping[maxNMappings];
            for (int i = 0; i < maxNMappings; i++)
                Mappings[i] = new MultiReadMapping();
            Barcodes = barcodes;
        }
        public void Init(string readId, int bcIdx, int randomBcIdx, int seqLen, int mappingNumber, int altMappings)
        {
            ReadId = readId;
            BarcodeIdx = bcIdx;
            RandomBcIdx = randomBcIdx;
            SeqLen = seqLen;
            MappingNumber = mappingNumber;
            AltMappings = altMappings;
        }
        public void Init(string combinedReadId, int seqLen, int altMappings)
        {
            ReadId = Barcodes.ExtractBarcodesFromReadId(combinedReadId, out BarcodeIdx, out RandomBcIdx);
            SeqLen = seqLen;
            AltMappings = altMappings;
            NMappings = 0;
            MappingNumber = 0;
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

        public IEnumerable<MultiReadMapping> IterMappings()
        {
            for (int idx = 0; idx < NMappings; idx++)
                yield return Mappings[idx];
            yield break;
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
