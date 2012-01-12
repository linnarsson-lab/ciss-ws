﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public abstract class MapFile
    {
        public static readonly int SortedAnalysisWindowSize = 10000000;
        public static readonly int MaxNMappings = 500;

        protected Barcodes barcodes;
        protected MultiReadMappings mrm;

        public static MapFile GetMapFile(string file, Barcodes barcodes)
        {
            if (file.EndsWith(".map"))
                return new BowtieMapFile(MaxNMappings, barcodes);
            if (file.EndsWith(".bam") || file.EndsWith(".sbam"))
                return new BamMapFile(barcodes, SortedAnalysisWindowSize);
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
            mrm.Init(combinedReadId, fields[4].Length, fields[5], fields[1][0], int.Parse(fields[6]));
            while (line != null)
            {
                fields = line.Split('\t');
                char strand = fields[1][0];
                if (!line.StartsWith(combinedReadId))
                {
                    yield return mrm;
                    combinedReadId = fields[0];
                    mrm.Init(combinedReadId, fields[4].Length, fields[5], strand, int.Parse(fields[6]));
                }
                mrm.AddMapping(fields[2], strand, int.Parse(fields[3]), fields[7]);
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
                char strand = fields[1][0];
                mrm.Init(fields[0], fields[4].Length, fields[5], strand, int.Parse(fields[6]));
                mrm.AddMapping(fields[2], strand, int.Parse(fields[3]), fields[7]);
                yield return mrm;
                line = reader.ReadLine();
            }
            reader.Close();
            yield break;
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
                        char strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                        mrm.Init(fields[0], fields[9].Length, fields[10], strand, altMappings);
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
            mrm.Init(a.QueryName, (int)a.QuerySequence.Count, a.QueryQuality, strand, 0);
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

    public struct Mismatch
    {
        public int relPosInChrDir;
        public char refNtInChrDir;
        public char ntInChrDir;

        public Mismatch(int relPosInChrDir, char refNtInChrDir, char ntInChrDir)
        {
            this.relPosInChrDir = relPosInChrDir;
            this.refNtInChrDir = refNtInChrDir;
            this.ntInChrDir = ntInChrDir;
        }
    }

    public class MultiReadMapping
    {
        private MultiReadMappings parent;

        public string Chr;
        public char Strand;
        public int Position;
        public string Mismatches;

        public int HitMidPos { get { return Position + parent.SeqLen / 2; } }
        public string ReadId { get { return parent.ReadId; } }
        public int BcIdx { get { return parent.BarcodeIdx; } }
        public int RndTagIdx { get { return parent.RandomBcIdx; } }
        public int SeqLen { get { return parent.SeqLen; } }
        public bool HasAltMappings { get { return parent.HasAltMappings; } }
        public bool HasMismatches { get { return Mismatches != ""; } }

        public MultiReadMapping(MultiReadMappings parent)
        {
            this.parent = parent;
        }
        public void Copy(MultiReadMapping other)
        {
            Chr = other.Chr;
            Strand = other.Strand;
            Position = other.Position;
            Mismatches = other.Mismatches;
            parent = other.parent;
        }

        public override string ToString()
        {
            return "MultiReadMapping: Chr=" + Chr + Strand + " Pos=" + Position + " HitMidPos=" + HitMidPos + " Mismatches=" + Mismatches;
        }

        public IEnumerable<Mismatch> IterMismatches()
        {
            return IterMismatches(0);
        }
        public IEnumerable<Mismatch> IterMismatches(int minPhredScore)
        {
            if (!HasMismatches) yield break;
            foreach (string snp in Mismatches.Split(','))
            {
                int p = snp.IndexOf(':');
                if (p == -1)
                {
                    Console.WriteLine("Strange mismatches in mapping:\n" + ToString());
                    continue;
                }
                int posInRead = int.Parse(snp.Substring(0, p));
                if (parent.GetQuality(posInRead) >= minPhredScore)
                {
                    int relPos = (Strand == '+') ? posInRead : parent.SeqLen - 1 - posInRead;
                    yield return new Mismatch(posInRead, snp[p + 1], snp[p + 3]);
                }
            }
        }

    }

    public class MultiReadMappings
    {
        private Barcodes Barcodes;

        public string ReadId;
        public int BarcodeIdx;
        public int RandomBcIdx = 0;
        public int SeqLen;
        private string qualityString;
        private char qualityDir;
        public char GetQuality(int posInRead)
        {
            return qualityString[(qualityDir == '+')? posInRead : qualityString.Length - 1 - posInRead];
        }
        public int AltMappings;
        public bool HasAltMappings { get { return AltMappings >= 1 || NMappings > 1; } }
        public int NMappings;
        private MultiReadMapping[] Mappings;

        public MultiReadMappings(int maxNMappings, Barcodes barcodes)
        {
            Mappings = new MultiReadMapping[maxNMappings];
            for (int i = 0; i < maxNMappings; i++)
                Mappings[i] = new MultiReadMapping(this);
            Barcodes = barcodes;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("MultiReadMappings: ReadID=" + ReadId + " BcIdx=" + BarcodeIdx + " RndTagIdx=" + RandomBcIdx);
            sb.Append("\n      Mappings.Length=" + Mappings.Length + " NMappings=" + NMappings + " HasAltMappings=" + HasAltMappings);
            foreach (MultiReadMapping m in IterMappings())
                sb.Append("\n    " + m.ToString());
            return sb.ToString();
        }

        public void Init(string combinedReadId, int seqLen, string qualityString, char qualityDirection, int altMappings)
        {
            ReadId = Barcodes.ExtractBarcodesFromReadId(combinedReadId, out BarcodeIdx, out RandomBcIdx);
            SeqLen = seqLen;
            AltMappings = altMappings;
            NMappings = 0;
            this.qualityDir = qualityDirection;
            this.qualityString = qualityString;
        }
        public void AddMapping(string chr, char strand, int pos, string mismatches)
        {
            if (NMappings < Mappings.Length)
            { 
                int idx = NMappings;
                /* // Keep mappings ordered by position, irrespective of chr or strand.
                while (idx > 0)
                {
                    if (Mappings[idx - 1].Position <= pos)
                        break;
                    Mappings[idx].Copy(Mappings[idx - 1]);
                    idx--;
                }*/
                Mappings[idx].Chr = chr.StartsWith("chr") ? chr.Substring(3) : chr;
                Mappings[idx].Strand = strand;
                Mappings[idx].Position = pos;
                Mappings[idx].Mismatches = mismatches;
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
        }

    }
}
