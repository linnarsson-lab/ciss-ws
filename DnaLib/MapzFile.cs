using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using System.Text.RegularExpressions;

namespace Linnarsson.Dna
{
    public class ChrCodes
    {
        private static readonly string[] specials = new string[] { "X", "Y", "Z", "W",
                                                                   "UCSC", "VEGA", "ENSEMBL", "UNUSED1", "UNUSED2", "UNUSED3" };
        private static readonly int specialsOffset = 10;

        public static int ToCode(string chr)
        {
            string chrId = chr.StartsWith("chr") ? chr.Substring(3) : chr;
            int code = Array.IndexOf(specials, chrId);
            if (code > -1)
                return code;
            if (int.TryParse(chrId, out code) && code + specialsOffset < 128)
                return code + specialsOffset;
            return -1;
        }

        public static string FromCode(int code)
        {
            if (code == -1)
                throw new ArgumentOutOfRangeException("Valid ChrCodes are between 0 and 127");
            if (code < specialsOffset)
                return specials[code];
            return (code - specialsOffset).ToString();
        }
    }

    public class MapzFileWriter : IDisposable
    {
        public static readonly string FileType = "MapzV001";

        private static readonly string readIdPattern = "(.+)T([0-9]+)_C([0-9]+)_";

        private string outfile;
        private BinaryWriter writer;
        private bool hasCommonBarcode;

        public MapzFileWriter(string outfile, bool hasCommonBarcode)
        {
            this.outfile = outfile;
            this.hasCommonBarcode = hasCommonBarcode;
        }

        /// <summary>
        /// --- File header: ---
        /// Byte    Content
        /// 0       File type and version string: MapzV001
        /// 8       hasCommonBarcode (bool)
        /// 9       common barcode or 0 (byte)
        /// 10      length of readId head (byte)
        /// 11      readId head (h bytes)
        /// --- for each following read: ---
        /// 0       T number (ushort)
        /// 2       C number (uint)
        /// 6       rnd label index or 0 (short)
        /// (8)     barcode index if hasCommonBarcode==false (byte)
        /// 8/9     length l of read and quality string direction dlllllll where d = 1/0 for +/- (byte)
        /// 9/10    quality string (l bytes)
        /// 9/10+l  number of alternative mappings (short)
        /// --- for each of n alternative mappings of the read: ---
        /// 0       128 + chrCode or length i (max 127) of chrId (byte)
        /// (1)       chrId (without 'chr') if previous byte was max 127 (i bytes)
        /// 1+i     strand + mismatch indicator + position on chromosome smpppppp... where s=1/0 for +/- and m=1 if mismatches (uint)
        /// (5+i)   number m of mismatches if any (byte)
        /// --- for each mismatch: ---
        /// 0       Iupac codes for Reference  and Actual nucleotides rrrraaaa (byte)
        /// 1       position of mismatch in read (byte)
        /// 
        /// </summary>
        /// <param name="mrms"></param>
        public void Write(MultiReadMappings mrms)
        {
            Match m = Regex.Match(mrms.ReadId, readIdPattern);
            ushort T = ushort.Parse(m.Groups[1].Value);
            uint C = uint.Parse(m.Groups[2].Value);
            if (writer == null)
            {
                writer = new BinaryWriter(File.Open(outfile, FileMode.Create), Encoding.UTF8);
                writer.Write(FileType);
                writer.Write(hasCommonBarcode);
                writer.Write(hasCommonBarcode? (byte)mrms.BarcodeIdx : (byte)0);
                writer.Write((byte)m.Groups[0].Length);
                writer.Write(m.Groups[0].Value);
            }
            writer.Write((ushort)T);
            writer.Write((uint)C);
            writer.Write((ushort)mrms.RandomBcIdx);
            if (!hasCommonBarcode)
                writer.Write((byte)mrms.BarcodeIdx);
            int qDirBit = (mrms.qualityDir == '+') ? 128 : 0;
            if (mrms.qualityString.Length > 127)
                throw new FormatException("Can not compress sequences > 127 nts");
            writer.Write((byte)(qDirBit | mrms.SeqLen));
            writer.Write(mrms.qualityString);
            writer.Write((short)mrms.AltMappings);
            foreach (MultiReadMapping mrm in mrms.Mappings)
            {
                int chrCode = ChrCodes.ToCode(mrm.Chr);
                if (chrCode > -1)
                {
                    writer.Write((byte)(128 | chrCode));
                }
                else
                {
                    int len = Math.Min(mrm.Chr.Length, 127);
                    writer.Write((byte)len);
                    writer.Write(mrm.Chr.Substring(0, len));
                }
                int strandBit = (mrm.Strand == '+') ? 1 : 0;
                int hasMmBit = (mrm.HasMismatches) ? 1 : 0;
                writer.Write((uint)(mrm.Position | (strandBit << 31) | (hasMmBit << 30)));
                if (mrm.HasMismatches)
                {
                    writer.Write((byte)mrm.NMismatches);
                    writer.Write(mrm.CodedMismatches);
                }
            }
        }

        public void Close()
        {
            if (writer != null)
                writer.Close();
        }
        void IDisposable.Dispose()
        {
            Close();
        }
    }

    public class MapzFileReader : IDisposable
    {
        private readonly string readIdTailPattern = "{0}T{1}_C{2}_{3}";

        private BinaryReader reader;
        private string idHead;
        private Barcodes barcodes;
        private bool hasCommonBarcodes;
        private int commonBarcodeIdx;

        private MultiReadMappings item;

        public MapzFileReader(string infile, Barcodes barcodes)
        {
            this.barcodes = barcodes;
            reader = new BinaryReader(File.Open(infile, FileMode.Open), Encoding.UTF8);
            if (!string.Equals(reader.ReadChars(7).ToString(), MapzFileWriter.FileType))
                throw new IOException("Input file is not a " + MapzFileWriter.FileType + " file.");
            hasCommonBarcodes = reader.ReadBoolean();
            commonBarcodeIdx = reader.ReadByte();
            idHead = reader.ReadChars(reader.ReadByte()).ToString();
            item = new MultiReadMappings(100, barcodes);
        }

        public MultiReadMappings ReadNext()
        {
            ushort T;
            try
            {
                T = reader.ReadUInt16();
            }
            catch (IOException)
            {
                reader.Close();
                return null;
            }
            uint C = reader.ReadUInt32();
            uint rndTagIdx = reader.ReadUInt32();
            int barcodeIdx = hasCommonBarcodes? commonBarcodeIdx : reader.ReadByte();
            int qlByte = reader.ReadByte();
            char qualityDir = ((qlByte & 128) == 128) ? '+' : '-';
            int seqLen = qlByte & 127;
            string qualityString = reader.ReadChars(seqLen).ToString();
            string readId = string.Format(readIdTailPattern, idHead, T, C, barcodeIdx, rndTagIdx);
            int altMappings = (int)reader.ReadUInt32();
            MultiReadMappings mrms = new MultiReadMappings(100, barcodes);
            mrms.Init(readId, seqLen, qualityString, qualityDir, altMappings);
            for (int i = 0; i < altMappings; i++)
            {
                byte chrByte = reader.ReadByte();
                string chrId = (chrByte > 127) ? ChrCodes.FromCode(chrByte & 127) : reader.ReadChars(chrByte).ToString();
                uint smp = reader.ReadUInt32();
                char strand = ((smp >> 31) == 1) ? '+' : '-';
                int pos = (int)(smp & (2 << 30 - 1));
                byte nmm = reader.ReadByte();
                bool hasMismatches = (smp >> 30) == 1;
                if (hasMismatches)
                {
                    int nmismatches = reader.ReadByte();
                    byte[] codedMismatches = reader.ReadBytes(nmismatches * 2);
                    mrms.AddMapping(chrId, strand, pos, codedMismatches);
                }
            }
            return item;
        }

        void IDisposable.Dispose()
        {
            if (reader != null)
                reader.Close();
        }
    }

}
