using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using Linnarsson.Utilities;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;


namespace Linnarsson.Dna
{
    [Flags]
    public enum BamFlags
    {
        PairedInSequencing = 0x0001,
        MappedInPair = 0x0002,
        IsUnmapped = 0x0004,
        MateIsUnmapped = 0x0008,
        QueryStrand = 0x0010,
        MateStrand = 0x0020,
        IsFirstInPair = 0x0040,
        IsSecondInPair = 0x0080,
        NotPrimary = 0x0100,
        FailedPlatformQC = 0x0200,
        IsDuplicate = 0x0400
    }

    public class BamAlignedRead
    {
        public string Chromosome { get; set; }
        /// <summary>
        /// One-based position
        /// </summary>
        public int Position { get; set; }
        public DnaStrand Strand
        {
            get
            {
                return (Flags & BamFlags.QueryStrand) == 0 ? DnaStrand.Forward : DnaStrand.Reverse;
            }
        }
        public DnaStrand MateStrand
        {
            get
            {
                return (Flags & BamFlags.MateStrand) == 0 ? DnaStrand.Forward : DnaStrand.Reverse;
            }
        }
        public bool IsMatePair
        {
            get
            {
                return (Flags & BamFlags.PairedInSequencing) == 0 ? false : true;
            }
        }
        public bool IsMappedMatePair
        {
            get
            {
                return (Flags & BamFlags.MappedInPair) == 0 ? false : true;
            }
        }

        public bool IsMapped
        {
            get
            {
                return (Flags & BamFlags.IsUnmapped) == 0 ? true : false;
            }
        }

        /// <summary>
        /// The number of this read in a pair (1 = first in pair, 2 = second in pair, 0 = unknown)
        /// </summary>
        public int NumberInPair
        {
            get
            {
                return ((Flags & BamFlags.IsFirstInPair) == 0) ? (((Flags & BamFlags.IsSecondInPair) == 0) ? 0 : 2) : 1;
            }
        }

        public byte MappingQuality { get; set; }
        public BamFlags Flags { get; set; }

        public string QueryName { get; set; }
        public DnaSequence QuerySequence { get; set; }
        //public uint[] Cigar { get; set; }
        //public sbyte[] QueryQuality { get; set; }
        public string Cigar { get; set; }

        /// <summary>
        /// Quality string representing phred scores, encoded using base 33
        /// </summary>
        public string QueryQuality { get; set; }

        public string MateChromosome { get; set; }
        public int MatePosition { get; set; }
        public int MateDistance { get; set; }
        public string[] ExtraFields { get; set; }

        public override string ToString()
        {
            return QueryName + "\t" + Chromosome + "\t" + Strand + "\t" + Position + "\t" + QuerySequence.Count + "\t" + ExtraFields;
        }

    }

    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    //struct bam1_t
    //{
    //    // bam1_core_t
    //    public int tid;
    //    public int pos;
    //    public ushort bin;
    //    public byte qual;
    //    public byte l_qname;
    //    public ushort flag;
    //    public ushort n_cigar;
    //    public int l_qseq;
    //    public int mtid;
    //    public int mpos;
    //    public int isize;

    //    // bam1_t
    //    public int l_aux;
    //    public int data_len;
    //    public int m_data;
    //    public byte[] data;
    //}

    //[StructLayout(LayoutKind.Sequential)]
    //struct bam_header_t
    //{ 
    //    public int n_targets; 
    //    public string[] target_name; 
    //    public int[] target_len; 
    //    IntPtr hash;
    //    IntPtr rg2lib; 
    //    int l_text; 
    //    string text; 
    //}

    /// <summary>
    /// Provides read-only access to an indexed BAM file
    /// </summary>
    public class BamFile
    {
        public string BamFileName { get; set; }
        public string[] Chromosomes { get; set; }
        public int[] ChromosomeLengths { get; set; }

        public int GetChromosomeLength(string chr)
        {
            int i = Array.IndexOf(Chromosomes, chr);
            if (i >= 0)
                return ChromosomeLengths[i];
            return 0;
        }
        /// <summary>
        /// Open a BAM file for random access. BAM file must be indexed (i.e. have a companion BAI file). 
        /// 'samtools' must be available in the PATH.
        /// </summary>
        /// <param name="filename">Full path to the BAM file (including ".bam")</param>
        public BamFile(string filename)
        {
            BamFileName = filename;
            if (!File.Exists(filename)) throw new FileNotFoundException(filename + " does not exist");
            if (!File.Exists(filename + ".bai")) throw new FileNotFoundException(filename + ".bai does not exist");

            // Load the header
            string hdr = samtools("view", "-H " + filename);
            List<string> chrs = new List<string>();
            List<int> lens = new List<int>();
            foreach (string line in hdr.Split('\n', '\r'))
            {
                if (line.StartsWith("@SQ"))
                {
                    var match = Regex.Match(line, "^@SQ\tSN:(?<chr>.*)\tLN:(?<len>[-0-9]+).*$");
                    chrs.Add(match.Groups["chr"].Captures[0].Value);
                    lens.Add(int.Parse(match.Groups["len"].Captures[0].Value));
                }
            }
            ChromosomeLengths = lens.ToArray();
            Chromosomes = chrs.ToArray();
        }

        string samtools(string cmd, string args)
        {
            Process cmdProcess = new Process();
            ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
            cmdStartInfo.FileName = "samtools";

            cmdStartInfo.RedirectStandardError = true;
            cmdStartInfo.RedirectStandardOutput = true;
            cmdStartInfo.RedirectStandardInput = false;
            cmdStartInfo.UseShellExecute = false;
            cmdStartInfo.CreateNoWindow = true;

            cmdStartInfo.Arguments = cmd + " " + args;

            cmdProcess.EnableRaisingEvents = true;
            cmdProcess.StartInfo = cmdStartInfo;
            cmdProcess.Start();

            // Collect stdout then wait for exit
            string stdout = cmdProcess.StandardOutput.ReadToEnd();
            cmdProcess.WaitForExit();
            if (cmdProcess.ExitCode != 0) throw new IOException("Failed to run 'samtools " + cmd + " " + args);
            cmdProcess.Close();
            return stdout.ToString();
        }

        /// <summary>
        /// Return all alignments for a given region.
        /// </summary>
        /// <param name="chromosome">Name of the chromosome, like 'chr12'</param>
        /// <param name="start">One-based start position</param>
        /// <param name="end">One-based end position</param>
        /// <returns></returns>
        public List<BamAlignedRead> Fetch(string chromosome, int start, int end)
        {
            List<BamAlignedRead> result = new List<BamAlignedRead>();
            string alignments = null;

            try
            {
                string samArg = BamFileName + " " + chromosome + ":" + start + "-" + end;
                alignments = samtools("view", samArg);
            }
            catch (IOException io)
            {
                Console.WriteLine(io.Message);
                return result;
            }
            string[] lines = alignments.Split('\n', '\r');
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) break;
                //Console.WriteLine("2.2");
                string[] fields = line.Split('\t');
                //Console.WriteLine(line);
                //Console.WriteLine("fields = " + fields.Length);
                try
                {
                    BamAlignedRead a = new BamAlignedRead
                    {
                        Chromosome = fields[2],
                        Flags = (BamFlags)int.Parse(fields[1]),
                        MappingQuality = byte.Parse(fields[4]),
                        Position = int.Parse(fields[3]),
                        MateChromosome = fields[6],
                        MatePosition = int.Parse(fields[7]),
                        MateDistance = int.Parse(fields[8]),
                        QuerySequence = new ShortDnaSequence(fields[9]),
                        QueryName = fields[0],
                        QueryQuality = fields[10],
                        Cigar = fields[5]
                    };
                    a.ExtraFields = new string[fields.Length - 11];
                    Array.ConstrainedCopy(fields, 11, a.ExtraFields, 0, fields.Length - 11);
                    result.Add(a);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e + " BamLine=" + line);
                }
            }
            return result;
        }

        public IEnumerable<string> IterLines(string chromosome, int start, int end)
        {
            string alignments = null;
            try
            {
                string samArg = BamFileName + " " + chromosome + ":" + start + "-" + end;
                alignments = samtools("view", samArg);
            }
            catch (IOException io)
            {
                Console.WriteLine(io.Message);
                yield break;
            }
            string[] lines = alignments.Split('\n', '\r');
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line)) yield break;
                yield return line;
            }
        }

        /// <summary>
        /// Return all alignments for a given region. 
        /// </summary>
        /// <param name="chromosome">Name of the chromosome, like 'chr12'</param>
        /// <param name="start">One-based start position</param>
        /// <param name="end">One-based end position</param>
        /// <returns></returns>
        public List<BamAlignedRead> FetchFaster(string chromosome, int start, int end)
        {
            Console.WriteLine("3.1");

            List<BamAlignedRead> result = new List<BamAlignedRead>();
            Process cmdProcess = new Process();
            ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
            cmdStartInfo.FileName = "samtools";

            cmdStartInfo.RedirectStandardError = true;
            cmdStartInfo.RedirectStandardOutput = true;
            cmdStartInfo.RedirectStandardInput = false;
            cmdStartInfo.UseShellExecute = false;
            cmdStartInfo.CreateNoWindow = true;

            cmdStartInfo.Arguments = "view -u " + BamFileName + " " + chromosome + ":" + start + "-" + end;

            cmdProcess.EnableRaisingEvents = true;
            cmdProcess.StartInfo = cmdStartInfo;
            cmdProcess.Start();

            // Read each alignment (binary)
            var br = new BinaryReader(cmdProcess.StandardOutput.BaseStream);


            // Failed approach: this causes StandardOutput to buffer the first part of the output, so that it can't be read from BaseStream
            // while (!cmdProcess.StandardOutput.EndOfStream)

            // Working approach:
            // For why, see http://stackoverflow.com/questions/4143281/capturing-binary-output-from-process-standardoutput
            Console.WriteLine("initialized");
            try
            {
                while (true)
                {
                    // Consume the initial, undocumented BAM data (including 'magic' in the spec)
                    char[] gttt = br.ReadChars(22);
                    foreach (char item in gttt)
                    {
                       Console.WriteLine(item);          
                    }
                    // br.ReadString();// .ReadChars(4);
                    // Skip past the header
                    int header_len = br.ReadInt32();
                    Console.WriteLine("header_len " + header_len);
                    br.ReadChars(header_len);
                    int n_ref = br.ReadInt32();
                    Console.WriteLine("n_ref " + n_ref);
                    for (int i = 0; i < n_ref; i++)
                    {
                        int l_name = br.ReadInt32();
                        // Console.WriteLine("l_name " + l_name);
//                        br.ReadBytes(l_name);
                        //                        Console.WriteLine(ASCIIEncoding.ASCII.GetString(br.ReadBytes(l_name)));    //         RE 20120126
                        Console.WriteLine("n " + i + "  " + l_name + " " + ASCIIEncoding.ASCII.GetString(br.ReadBytes(l_name)) + "     l " + br.ReadInt32());    //         RE 20120126
                       
                    }
//                    if (cmdProcess.StandardOutput.EndOfStream) break;           RE 20120126
                    // Read the binary BAM record
                    byte[] block_size = br.ReadBytes(40);				// 4
//                    Console.WriteLine("block_size " + block_size);
//                    byte[] theblock = br.ReadBytes(block_size);
                    for (int i = 0; i < 36; i++)
                    {
 //                       Console.WriteLine("FW" + BitConverter.ToInt32(block_size, i));
 //                       Console.WriteLine(BitConverter.ToInt32(INT2LE(BitConverter.ToInt32(block_size, i))));
                        byte[] reverted = new byte[4];
                        reverted[0] = block_size[i+3];
                        reverted[1] = block_size[i+2];
                        reverted[2] = block_size[i+1];
                        reverted[3] = block_size[i];
                        Console.WriteLine(BitConverter.ToInt32(block_size, i) + "\t" + BitConverter.ToInt32(reverted, 0) + "\t" + 
             BitConverter.ToInt16(reverted, 1) + "\t" + BitConverter.ToInt16(reverted, 2) + "\t" + BitConverter.ToString(reverted) + "\t" + 
             BitConverter.ToChar(block_size, i)); 
                    }

                    int rID = br.ReadInt16();						// 8
                    GetBytesInt32(rID);
                    Console.WriteLine("rID " + rID);
                    int pos = br.ReadInt32();						// 12
                    Console.WriteLine("pos " + pos);
                    ushort bin = br.ReadUInt16();					// 14
                    Console.WriteLine("bin " + bin);
                    GetBytesUInt32(bin);
//                    uint bin_mq_nl = br.ReadUInt32();
//                    ushort bin = (byte)(bin_mq_nl >> 16);
//                    byte mapQual = (byte)((bin_mq_nl >> 8) & 255);
//                    byte[] bmn = BitConverter.GetBytes(bin_mq_nl);
//                    byte read_name_len = (byte)(bin_mq_nl);				// 16
           
                    byte mapQual = br.ReadByte();					// 15
Console.WriteLine("mapqual " + mapQual);
                    byte read_name_len = br.ReadByte();				// 16
Console.WriteLine("rnl " + read_name_len);

                    ushort flag = br.ReadUInt16();					// 18
                    ushort cigar_len = br.ReadUInt16();				// 20
Console.WriteLine("cigar_len " + cigar_len);
                    int read_len = br.ReadInt32();					// 24
Console.WriteLine("read_len " + read_len);
                    int mate_rID = br.ReadInt32();					// 28
                    int mate_pos = br.ReadInt32();					// 32
                    int ins_size = br.ReadInt32();					// 36
                    byte[] read_name = br.ReadBytes(read_name_len);	// 36 + read_name_len
                    char[] cigar = new char[cigar_len * 2];				// 36 + read_name_len + 4*cigar_len
                    Console.WriteLine("HIT");
                    for (int i = 0; i < cigar_len; i += 2)
                    {
                        var temp = br.ReadUInt32();
//                        cigar[i] = (char)('0' + (temp >> 4));	// op length
//                        cigar[i + 1] = (char)("MIMDNSHP=X"[(int)(temp & 15)]);
                    }
                    Console.WriteLine("H " + ASCIIEncoding.ASCII.GetString(read_name) + "  pos " + pos);

                    // BAM uses the same encoding as we do
                    byte[] seq = br.ReadBytes((read_len + 1) / 2);	// 36 + read_name_len + 4*cigar_len + (read_len + 1)/2
                    byte[] qual = new byte[read_len];				// 36 + read_name_len + 4*cigar_len + (read_len + 1)/2 + read_len
                    for (int i = 0; i < read_len; i++)
                    {
                        qual[i] = br.ReadByte();
                        if (qual[i] == 0xFF) qual[i] = 0;	// We encode 0xFF (no quality) as zero quality
                    }
 //                   var qstring = FastQRecord.StringFromQualities(qual, 33);               RE 20120126

                    int count = 36 + read_name_len + 4 * cigar_len + (read_len + 1) / 2 + read_len;
                    Console.WriteLine("count " + count);
                    Console.WriteLine("blocksize " + block_size);
//                    br.ReadBytes((int)(block_size - count - 4));  // Subtract 4 since block_size itself is not included*/
                    // Make a BamAlignedRead
//                    byte mappingquality = theblock[9];
//                    int position = (theblock[4] << 24) | (theblock[5] << 16) | (theblock[6] << 8) | theblock[7];
                    BamAlignedRead a = new BamAlignedRead
                    {
                        Chromosome = null, //Chromosomes[rID],
                        Flags = 0, //(BamFlags)flag,
                        MappingQuality = mapQual,
                        Position = pos, //pos,
                        MateChromosome = null, //Chromosomes[mate_rID],
                        MatePosition = 0, //mate_pos,
                        MateDistance = 0, //ins_size,
                        QuerySequence = null, //new ShortDnaSequence(seq, read_len),
                        QueryName = null, //ASCIIEncoding.ASCII.GetString(read_name, 0, read_name_len - 1),
                        QueryQuality = null, //qstring,            RE 20120126
                        Cigar = null //new string(cigar)
                    };
//                    Console.WriteLine(a.QuerySequence);
                    Console.WriteLine(a.Position + " " + a.MappingQuality);
                    result.Add(a);
                }
            }
            catch (EndOfStreamException)
            {
                Console.WriteLine("EXCEPTION");
            }
            cmdProcess.WaitForExit();
            if (cmdProcess.ExitCode != 0) throw new IOException("Failed to run 'samtools view -u " + BamFileName + " " + chromosome + ":" + start + "-" + end);
            cmdProcess.Close();

            return result;
        }
        byte[] INT2LE(int data)
        {
            byte[] b = new byte[4];
            b[0] = (byte)data;
            b[1] = (byte)(((uint)data >> 8) & 0xFF);
            b[2] = (byte)(((uint)data >> 16) & 0xFF);
            b[3] = (byte)(((uint)data >> 24) & 0xFF);
            return b;
        }
        int LE2INT(byte[] data)
        {
            return (data[3] << 24) | (data[2] << 16) | (data[1] << 8) | data[0];
        }

        // Convert a uint argument to a byte array and display it.
        public static void GetBytesUInt32(uint argument)
        {
            const string formatter = "{0,16}{1,20}";
            byte[] byteArray = BitConverter.GetBytes(argument);
            Console.WriteLine(formatter, argument,
                BitConverter.ToString(byteArray));
        }
        public static void GetBytesInt32(int argument)
        {
            const string formatter = "{0,16}{1,20}";
            byte[] byteArray = BitConverter.GetBytes(argument);
            Console.WriteLine(formatter, argument,
                BitConverter.ToString(byteArray));
        }


        }
}

/*	/// <summary>
	/// Read-only access to a SAM or BAM file. Requires the samtools DLL. Doesn't work 
	/// because it doesn't find the DLL even though the DLL is there.
	/// </summary>
	public class SamBamFile_deprecated
	{
		/// <summary>
		/// True if the format is BAM; false if it is SAM
		/// </summary>
		public bool IsBamFile { get; set; }

		public string[] Chromosomes { get; set; }
		public int[] ChromosomeLengths { get; set; }

		private IntPtr BamIndex;
		private IntPtr FileHandle;

		public delegate void BamFetchCallback(IntPtr alignment, IntPtr data);

#region samtools
		[DllImport("libbam.so")]
		private static extern IntPtr samopen(string fileName, string mode, IntPtr aux);

		[DllImport("libbam.so")]
		private static extern void sam_close(IntPtr file);

		[DllImport("libbam.so")]
		private static extern IntPtr sam_header_read(IntPtr file);

		[DllImport("libbam.so")]
		private static extern IntPtr bam_header_read(IntPtr file);

		[DllImport("libbam.so")]
		private static extern void bam_header_destroy(IntPtr header);

		[DllImport("libbam.so")]
		private static extern IntPtr bam_index_load(string indexFileName);

		[DllImport("libbam.so")]
		private static extern void bam_index_destroy(IntPtr index);

		[DllImport("libbam.so")]
		private static extern int bam_fetch(IntPtr bamFileHandle, IntPtr bamIndexFileHandle, int chrId, int begin, int end, IntPtr data, BamFetchCallback callback); 
#endregion

		/// <summary>
		/// Open a SAM/BAM file for easy access. BAM file may be indexed (i.e. have a companion BAI file). 
		/// </summary>
		/// <param name="filename">Full path to the SAM/BAM file (including ".sam"/".bam")</param>
		public SamBamFile(string filename)
		{
			if (filename.EndsWith(".bam")) IsBamFile = true;
			else if (filename.EndsWith(".sam")) IsBamFile = false;
			else throw new NotSupportedException("File must be either .bam or .sam");

			if(!File.Exists(filename)) throw new FileNotFoundException(filename + " does not exist");
			FileHandle = samopen(filename, IsBamFile ? "rb" : "r", IntPtr.Zero);

			if (IsBamFile)
			{
				// Try to load the corresponding index file
				string baiFile = Path.ChangeExtension(filename, ".bai");
				if(File.Exists(baiFile))
				{
					BamIndex = bam_index_load(baiFile);
					if (BamIndex == null) throw new FileNotFoundException("Failed to open .bai index file");
				}
			}

			// Load the header
			IntPtr temp = IsBamFile ? bam_header_read(FileHandle) : sam_header_read(FileHandle);
			if (temp != IntPtr.Zero)
			{
				bam_header_t hdr = (bam_header_t)Marshal.PtrToStructure(temp, typeof(bam_header_t));
				Chromosomes = new string[hdr.n_targets];
				ChromosomeLengths = new int[hdr.n_targets];
				for (int i = 0; i < hdr.n_targets; i++)
				{
					Chromosomes[i] = hdr.target_name[i];
					ChromosomeLengths[i] = hdr.target_len[i];
				}
				bam_header_destroy(temp);
			}
		}

		public void Close()
		{
			if (BamIndex != null) bam_index_destroy(BamIndex);
			if (FileHandle != IntPtr.Zero)
			{
				sam_close(FileHandle);
			}
		}

		/// <summary>
		/// Return a stream of alignments for a given region; will only work for BAM files
		/// that are sorted and indexed (i.e. which have a valid BAI file).
		/// </summary>
		/// <param name="chromosome"></param>
		/// <param name="start">Zero-based start position</param>
		/// <param name="end">Zero-based end position</param>
		/// <returns></returns>
		public List<BamAlignedRead> Fetch(string chromosome, int start, int end)
		{
			int chrID = -1;
			for (int i = 0; i < Chromosomes.Length; i++)
			{
				if (Chromosomes[i] == chromosome) chrID = i;
			}
			if (chrID == -1) throw new InvalidOperationException("Chromosome '" + chromosome + "' not found");

			List<BamAlignedRead> result = new List<BamAlignedRead>();

			// Use callback function inside lambda to get the alignments
			bam_fetch(this.FileHandle, this.BamIndex, chrID, start, end, IntPtr.Zero,
				(IntPtr algn_ptr, IntPtr data) =>
				{
					bam1_t a = (bam1_t)Marshal.PtrToStructure(algn_ptr, typeof(bam1_t));
					result.Add(parseRawAlignment(a));
				});
			return result;
		}

		private BamAlignedRead parseRawAlignment(bam1_t a)
		{
			BamAlignedRead alignment = new BamAlignedRead
			{
				Chromosome = a.tid >= 0 ? Chromosomes[a.tid] : null,
				Flags = (BamFlags)a.flag,
				MappingQuality = a.qual,
				Position = a.pos,
				MateChromosome = a.mtid >= 0 ? Chromosomes[a.mtid] : null,
				MatePosition = a.mpos,
				MateDistance = a.isize,
				Query = new DnaSequence(),
				QueryName = ASCIIEncoding.ASCII.GetString(a.data, a.n_cigar, a.l_qname - 1), // Avoid trailing \0
				QueryQuality = new byte[a.l_qseq],
				CigarOps = new char[a.n_cigar],
				CigarOpLengths = new byte[a.n_cigar]
			};
			// Copy the quality scores
			Array.Copy(a.data, a.n_cigar + a.l_qname + (a.l_qseq + 1) / 2, alignment.QueryQuality, 0, a.l_qseq);
			// Copy the query sequence
			for (int i = 0; i < a.l_qseq; i++)
			{
				if (i % 2 == 0) alignment.Query.Append((byte)((a.data[a.n_cigar + a.l_qname + (i / 2)] & 0xF0) >> 4));
				else alignment.Query.Append((byte)(a.data[a.n_cigar + a.l_qname + (i / 2)] & 0x0F));
			}
			// Copy the Cigar ops
			for (int i = 0; i < a.n_cigar; i++)
			{
				alignment.CigarOps[i] = "MIDNSHP"[a.data[i] & 0x0F];
				alignment.CigarOpLengths[i] = (byte)((a.data[i] & 0xF0) >> 4);
			}
			return alignment;
		}
	*/
