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
        public static readonly int SortedAnalysisWindowSize = 10000000;
        /// <summary>
        /// Maximum number of mappings that will be kept for a multiread
        /// </summary>
        public static readonly int MaxNStoredMappings = 500;

        protected MultiReadMappings mrm;

        /// <summary>
        /// Find the correct reader for the file of mapped reads. Will handle .map, .bam, and .sam files
        /// </summary>
        /// <param name="file"></param>
        /// <param name="barcodes"></param>
        /// <returns></returns>
        public static MapFile GetMapFile(string file, Barcodes barcodes)
        {
            int maxNMappings = Math.Max(MaxNStoredMappings, Props.props.MaxAlternativeMappings);
            if (file.EndsWith(".map"))
                return new BowtieMapFile(maxNMappings, barcodes);
            if (file.EndsWith(".bam") || file.EndsWith(".sbam"))
                return new BamMapFile(barcodes, SortedAnalysisWindowSize);
            if (file.EndsWith(".sam"))
                return new SamMapFile(maxNMappings, barcodes);
            return null;
        }

        public MapFile(int maxNMappings, Barcodes barcodes)
        {
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
        /// Return number of multiread mappings from the "XM:n" field of a sam/bam file, or 0 if field not found
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="startField"></param>
        /// <returns></returns>
        protected static int ParseXMField(string[] fields, int startField)
        {
            for (int i = startField; i < fields.Length; i++)
                if (fields[i].StartsWith("XM:i:"))
                    return int.Parse(fields[i].Substring(5));
            return 0;
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
            using (StreamReader reader = file.OpenRead())
            {
                int pos, nOtherMappings;
                string line = reader.ReadLine();
                if (line == null) yield break;
                string[] fields = line.Split('\t');
                if (fields.Length < 8)
                    throw new FormatException("Too few columns in first line of map(?) file " + file);
                string combinedReadId = fields[0];
                nOtherMappings = int.Parse(fields[6]);
                mrm.Init(combinedReadId, fields[4], fields[5], fields[1][0], nOtherMappings + 1);
                while (line != null)
                {
                    fields = line.Split('\t');
                    if (fields.Length < 8)
                        Console.WriteLine("Error: Too few columns (Is the file truncated?) in {0} at line:\n{1} ", file, line);
                    else
                    {
                        char strand = fields[1][0];
                        if (!line.StartsWith(combinedReadId))
                        {
                            yield return mrm;
                            combinedReadId = fields[0];
                            if (!int.TryParse(fields[6], out nOtherMappings))
                                Console.WriteLine("Error parsing int in field 6 (AltMappings) of {0}:\n{1}", file, line);
                            mrm.Init(combinedReadId, fields[4], fields[5], strand, nOtherMappings + 1);
                        }
                        if (!int.TryParse(fields[3], out pos))
                            Console.WriteLine("Error parsing int in field 3 (Position) of {0}:\n{1}", file, line);
                        mrm.AddMapping(fields[2], strand, pos, fields[7]);
                    }
                    try
                    {
                        line = reader.ReadLine();
                    }
                    catch (OutOfMemoryException)
                    {
                        throw new OutOfMemoryException("Out of memory reading " + file + ". Do you have wrong line endings in the file?");
                    }
                }
            }
            yield return mrm;
            yield break;
        }

        public override IEnumerable<MultiReadMappings> SingleMappings(string file)
        {
            using (StreamReader reader = file.OpenRead())
            {
                string line = reader.ReadLine();
                if (line == null) yield break;
                string[] fields = line.Split('\t');
                if (fields.Length < 8)
                    throw new FormatException("Too few columns in first line of map(?) file " + file);
                while (line != null)
                {
                    fields = line.Split('\t');
                    if (fields.Length < 8)
                        throw new FormatException("Too few columns (Is the file truncated?) in " + file + " at line:\n" + line);
                    char strand = fields[1][0];
                    int nOtherMappings = int.Parse(fields[6]);
                    mrm.Init(fields[0], fields[4], fields[5], strand, nOtherMappings + 1);
                    mrm.AddMapping(fields[2], strand, int.Parse(fields[3]), fields[7]);
                    yield return mrm;
                    try
                    {
                        line = reader.ReadLine();
                    }
                    catch (OutOfMemoryException)
                    {
                        throw new OutOfMemoryException("Out of memory reading " + file + ". Do you have wrong line endings in the file?");
                    }
                }
            }
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

        public static void ParseFileItem(BamAlignedRead a, ref MultiReadMappings mrm)
        {
            string chr = (a.Chromosome.StartsWith("chr")) ? a.Chromosome.Substring(3) : a.Chromosome;
            char strand = (a.Strand == DnaStrand.Forward) ? '+' : '-';
            int nMappings = ParseXMField(a.ExtraFields, 0);
            mrm.Init(a.QueryName, a.QuerySequence.ToString(), a.QueryQuality, strand, nMappings);
            if (a.Position > 0) // 0 in SAM file indicates no alignment
                mrm.AddMapping(chr, strand, a.Position - 1, "");
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
                        int nMappings = ParseXMField(fields, 11);
                        char strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                        mrm.Init(fields[0], fields[9], fields[10], strand, nMappings);
                        int pos = int.Parse(fields[3]);
                        if (pos > 0) // 0 in SAM file indicates no alignment
                        {
                            mrm.AddMapping(fields[2], strand, pos - 1, "");
                            yield return mrm;
                        }
                    }
                }
            }
            yield break;
        }

    }

    public class SamMapFile : MapFile
    {
        public SamMapFile(int maxNMappings, Barcodes barcodes)
            : base(maxNMappings, barcodes)
        {
        }

        public override IEnumerable<MultiReadMappings> MultiMappings(string file)
        {
            using (StreamReader reader = new StreamReader(file))
            {
                string line = reader.ReadLine();
                while (line != null && line.StartsWith("@"))
                    line = reader.ReadLine();
                if (line == null)
                    yield break;
                string[] fields = line.Split('\t');
                if (fields.Length < 11)
                    throw new FormatException("Too few columns in first alignment line of sam(?) file " + file);
                string combinedReadId = fields[0];
                char strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                int nMappings = ParseXMField(fields, 11);
                mrm.Init(fields[0], fields[9], fields[10], strand, nMappings);
                while (line != null)
                {
                    fields = line.Split('\t');
                    if (!line.StartsWith(combinedReadId))
                    {
                        yield return mrm;
                        combinedReadId = fields[0];
                        strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                        nMappings = ParseXMField(fields, 11);
                        mrm.Init(fields[0], fields[9], fields[10], strand, nMappings);
                    }
                    int pos = int.Parse(fields[3]);
                    if (pos > 0) // 0 in SAM file indicates no alignment
                        mrm.AddMapping(fields[2], strand, pos - 1, "");
                    line = reader.ReadLine();
                }
                yield return mrm;
            }
        }

        public override IEnumerable<MultiReadMappings> SingleMappings(string file)
        {
            using (StreamReader reader = new StreamReader(file))
            {
                string line = reader.ReadLine();
                while (line != null && line.StartsWith("@"))
                    line = reader.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Split('\t');
                    char strand = ((BamFlags)int.Parse(fields[1]) & BamFlags.QueryStrand) == 0 ? '+' : '-';
                    int nMappings = ParseXMField(fields, 11);
                    mrm.Init(fields[0], fields[9], fields[10], strand, nMappings);
                    int pos = int.Parse(fields[3]);
                    if (pos > 0) // 0 in SAM file indicates no alignment
                    {
                        mrm.AddMapping(fields[2], strand, pos - 1, "");
                        yield return mrm;
                    }
                    line = reader.ReadLine();
                }
            }
        }

    }

    public class Mismatch
    {
        /// <summary>
        /// Position within the chr of the mismatch
        /// </summary>
        public int posInChr;
        /// <summary>
        /// Position relative to the start of the aligned read, in chr direction
        /// </summary>
        public byte relPosInChrDir;
        /// <summary>
        /// The reference chr seq nucleotide
        /// </summary>
        public char refNtInChrDir;
        /// <summary>
        /// The alternative nucleotide on chr as told by the read
        /// </summary>
        public char ntInChrDir;

        public Mismatch(int posInChr, byte relPosInChrDir, char refNtInChrDir, char ntInChrDir)
        {
            this.posInChr = posInChr;
            this.relPosInChrDir = relPosInChrDir;
            this.refNtInChrDir = refNtInChrDir;
            this.ntInChrDir = ntInChrDir;
        }

        public override string ToString()
        {
            return "Mismatch(PosInChr=" + posInChr + ", RelPos=" + relPosInChrDir + ", RefNt=" + refNtInChrDir + ", AltNt=" + ntInChrDir + ")";
        }
    }

    public class MultiReadMapping
    {
        private MultiReadMappings parent;

        public string Chr;
        public char Strand;
        /// <summary>
        /// Hit start position
        /// </summary>
        public int Position;
        public string Mismatches;

        /// <summary>
        /// For even AverageReadLen, HitMidPos is to the right of the midpoint
        /// </summary>
        public int HitMidPos { get { return Position + MappedTagItem.AverageReadLen / 2; } }
        public string ReadId { get { return parent.ReadId; } }
        public int BcIdx { get { return parent.BcIdx; } }
        public int UMIIdx { get { return parent.UMIIdx; } }
        public int SeqLen { get { return parent.SeqLen; } }
        public bool HasMultipleMappings { get { return parent.HasMultipleMappings; } }
        public bool HasMismatches { get { return Mismatches != ""; } }

        public MultiReadMapping(MultiReadMappings parent)
        {
            this.parent = parent;
        }

        public override string ToString()
        {
            return "MultiReadMapping: Chr=" + Chr + Strand + " Pos=" + Position + " HitMidPos=" + HitMidPos + " Mismatches=" + Mismatches;
        }

        public int NMismatches { get { return Mismatches.Split(',').Length; } }

        public IEnumerable<Mismatch> IterMismatches(int minPhredAsciiVal)
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
                byte relPosInReadDir = byte.Parse(snp.Substring(0, p));
                if (parent.GetQuality(relPosInReadDir) >= minPhredAsciiVal)
                {
                    byte relPosInChrDir = (Strand == '+') ? relPosInReadDir : (byte)(parent.SeqLen - 1 - relPosInReadDir);
                    yield return new Mismatch(Position + relPosInChrDir, relPosInChrDir, snp[p + 1], snp[p + 3]);
                }
            }
        }
        public bool Contains(int posOnChr, int margin)
        {
            return (posOnChr >= (Position + margin) && posOnChr < (Position + SeqLen - margin));
        }

        public char GetQuality(int posOnChr)
        {
            return parent.GetQualityByAlignmentPos(posOnChr - Position);
        }

    }

    public class MultiReadMappings
    {
        private Barcodes Barcodes;

        public string CombinedReadId; // Includes Barcode and UMI
        public string ReadId;
        public int BcIdx;
        public int UMIIdx = 0;
        public int SeqLen;
        public string Sequence { get; private set; }
        public string QualityString { get; private set; }
        public char QualityDir { get; private set; }
        public char GetQuality(int relPosInRead)
        {
            return QualityString[(QualityDir == '+')? relPosInRead : QualityString.Length - 1 - relPosInRead];
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="relPosInAlignment"></param>
        /// <returns>If outside read, returns the lowest possible ascii value in phred strings: '!'</returns>
        public char GetQualityByAlignmentPos(int relPosInAlignment)
        {
            if (relPosInAlignment < 0 || relPosInAlignment >= QualityString.Length) return '!';
            return QualityString[relPosInAlignment];
        }

        /// <summary>
        /// 1+value from 2nd last column of map file, or "XM:i" value in sam/bam file, or 0 if these vaules are unavailable
        /// </summary>
        private int m_NMultiMappings;
        /// <summary>
        /// Number of different mappings the read has (or, for extreme multireads, the maximum number analyzed during alignment)
        /// </summary>
        public int NMappings { get { return (m_NMultiMappings > 0) ? m_NMultiMappings : MappingsIdx; } }

        /// <summary>
        /// true if multiple mappings are available, or if the mapping file indicated that alternative mappings existed
        /// </summary>
        public bool HasMultipleMappings { get { return m_NMultiMappings > 1 || MappingsIdx > 1; } }

        /// <summary>
        /// Used as index to next free in Mappings array
        /// </summary>
        public int MappingsIdx;
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
            sb.Append("MultiReadMappings: ReadID=" + ReadId + " BcIdx=" + BcIdx + " UMIIdx=" + UMIIdx);
            sb.Append("\n      Mappings.Length=" + Mappings.Length + " MappingsIdx=" + MappingsIdx + " HasAltMappings=" + HasMultipleMappings);
            foreach (MultiReadMapping m in IterMappings())
                sb.Append("\n    " + m.ToString());
            return sb.ToString();
        }

        public string ToMapfileLines()
        {
            StringBuilder sb = new StringBuilder();
            foreach (MultiReadMapping m in IterMappings())
            {
                sb.Append(CombinedReadId + "\t");
                sb.Append(m.Strand + "\t");
                sb.Append("chr" + m.Chr + "\t");
                sb.Append(m.Position + "\t");
                sb.Append(Sequence + "\t");
                sb.Append(QualityString + "\t");
                sb.Append((NMappings - 1) + "\t");
                sb.Append(m.Mismatches + "\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="combinedReadId"></param>
        /// <param name="seq"></param>
        /// <param name="qualityString"></param>
        /// <param name="qualityDirection"></param>
        /// <param name="nMappings">Number of mappings found for a multiread, or 1+maximum allowed during alignment when more than that</param>
        public void Init(string combinedReadId, string seq, string qualityString, char qualityDirection, int nMappings)
        {
            CombinedReadId = combinedReadId;
            ReadId = Barcodes.StripBcAndUMIFromReadId(combinedReadId, out BcIdx, out UMIIdx);
            Sequence = seq;
            SeqLen = seq.Length;
            m_NMultiMappings = nMappings;
            this.QualityDir = qualityDirection;
            this.QualityString = qualityString;
            MappingsIdx = 0;
        }

        /// <summary>
        /// Add one (alternative) alignment for the (multi)read
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="pos">0-based positions</param>
        /// <param name="mismatches"></param>
        public void AddMapping(string chr, char strand, int pos, string mismatches)
        {
            if (MappingsIdx < Mappings.Length)
            { 
                int idx = MappingsIdx;
                Mappings[idx].Chr = chr.StartsWith("chr") ? chr.Substring(3) : chr;
                Mappings[idx].Strand = strand;
                Mappings[idx].Position = pos;
                Mappings[idx].Mismatches = mismatches;
                MappingsIdx++;
            }
        }

        public MultiReadMapping this[int idx]
        {
            get { return (idx < MappingsIdx)? Mappings[idx] : null; }
        }

        public IEnumerable<MultiReadMapping> IterMappings()
        {
            for (int idx = 0; idx < MappingsIdx; idx++)
                yield return Mappings[idx];
        }

    }
}
