using System;
using System.Collections.Generic;
using System.Text;
using Linnarsson.Mathematics;
using Linnarsson.Mathematics.SortSearch;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public enum DnaStrand { Forward, Reverse }

	public class IupacEncoding
	{
		public const byte GC = 6;
		public const byte AT = 9;
		public const byte Purine = 10;
		public const byte Pyrimidine = 5;
		public const byte Any = 15;

		private static byte[] iupacComplement = new byte[] {
			0,
			8,
			4,
			12,
			2,
			10,
			6,
			14,
			1,
			9,
			5,
			13,
			3,
			11,
			7,
			15
		};


		private static char[] iupac = new char[] {
			'-',	// 0000
			'T',	// 0001
			'G',	// 0010
			'K',	// 0011
			'C',	// 0100
			'Y',	// 0101
			'S',	// 0110
			'B',	// 0111
			'A',	// 1000
			'W',	// 1001
			'R',	// 1010
			'D',	// 1011
			'M',	// 1100
			'H',	// 1101
			'V',	// 1110
			'N'		// 1111
		};

		public static byte Complement(byte code)
		{
			if(code > 15) throw new ArgumentOutOfRangeException("IUPAC code must be 0-15");
			return iupacComplement[code];
		}

		public static char ToIupac(byte code)
		{
			if(code > 15) throw new ArgumentOutOfRangeException("IUPAC code must be in range 0 - 15");
			return iupac[code];
		}

		public static byte FromIupac(char nt)
		{
			switch(nt)
			{
				case 'A': return 8;
				case 'C': return 4;
				case 'G': return 2;
				case 'T': return 1;

				case 't': return 1;
				case 'g': return 2;
				case 'c': return 4;
				case 'a': return 8;
				case 'N': return 15;
				case 'n': return 15;

				case 'K': return 3;
				case 'Y': return 5;
				case 'S': return 6;
				case 'B': return 7;
				case 'W': return 9;
				case 'R': return 10;
				case 'D': return 11;
				case 'M': return 12;
				case 'H': return 13;
				case 'V': return 14;
				case '-': return 0;
				case ' ': return 0;
				case 'k': return 3;
				case 'y': return 5;
				case 's': return 6;
				case 'b': return 7;
				case 'w': return 9;
				case 'r': return 10;
				case 'd': return 11;
				case 'm': return 12;
				case 'h': return 13;
				case 'v': return 14;
			}
			throw new ArgumentOutOfRangeException("Illegal IUPAC character");
		}
	}

	public abstract class DnaSequence
	{
		public long Count { get; protected set; }


		/// <summary>
		/// Gets or sets the binary representation of the nucleotide at index, given as a binary representation (A = 1, C = 2, G = 4, T = 8).
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public abstract byte this[long index] { get; set; }

		/// <summary>
		/// Append a nucleotide, given as a binary representation (A = 1, C = 2, G = 4, T = 8)
		/// </summary>
		/// <param name="item"></param>
		public abstract void Append(byte item);

		/// <summary>
		/// Generate a compressed representation of the sequence
		/// </summary>
		/// <returns></returns>
		public Blob2 ToBlob2()
		{
			Blob2 result = new Blob2(this.Count);
			for (long i = 0; i < this.Count; i++)
			{
				result.Append(NtToIndex(this[i]));
			}
			return result;
		}

        public bool TryMatch(DnaSequence subsequence, long start, out long matchPos)
        {
            return TryMatch(subsequence, start, Count, out matchPos);
        }
		public bool TryMatch(DnaSequence subsequence, long start, long end, out long matchPos)
		{
			for(long ix = start; ix < end - subsequence.Count; ix++)
			{
				bool match = true;
				for(long j = 0; j < subsequence.Count; j++)
				{
					if(((~subsequence[j]) & this[ix + j]) != 0)
					{
						match = false;
						break;
					}
				}
				if(match)
				{
					matchPos = ix;
					return true;
				}
			}
			matchPos = 0;
			return false;
		}

        public long Match(DnaSequence subsequence, long start)
        {
            return Match(subsequence, start, Count);
        }
		public long Match(DnaSequence subsequence, long start, long end)
		{
			long matchPos = 0;
			if(!TryMatch(subsequence, start, end, out matchPos)) return -1;
			return matchPos;
		}

		public DnaSequence[] AllSingleSubstitutions()
		{
			int count = 0;
			DnaSequence[] result = new DnaSequence[3 * Count];

			for(long ix = 0; ix < Count; ix++)
			{
				foreach(byte nt in new byte[] { 1, 2, 4, 8 } )
				{
					if(nt == this[ix]) continue;
					if (this is ShortDnaSequence) result[count] = new ShortDnaSequence(this);
					else result[count] = new LongDnaSequence(this);
					result[count][ix] = nt;
					count++;
				}
			}
			return result;
		}

		/// <summary>
		/// Convert the sequence to an index, using two bits per nucleotide and higher bits for left-most nucleotides. 
		/// Useful only for non-degenerate sequences up to 32 bp, after which the index will be cyclic.
		/// </summary>
		/// <returns></returns>
		public ulong ToIndex()
		{
			ulong result = 0;
			for(long ix = 0; ix < Count; ix++)
			{
				result <<= 2;
				ulong nt = 0;
				if(this[ix] == 1) nt = 3; // T
				if(this[ix] == 2) nt = 2; // G
				if(this[ix] == 4) nt = 1; // C
				// if(this[ix] == 8) nt = 0; // A

				result |= nt;
			}
			return result;
		}

		/// <summary>
		/// Create a DNA sequence based on an index, using LSB bits up to the given length,
		/// and ignoring any remaining high-order bits.
		/// </summary>
		/// <param name="index"></param>
		/// <param name="length"></param>
		/// <returns></returns>
		public static DnaSequence FromIndex(ulong index, int length)
		{
			var buffer = new byte[length];
			for (int i = 0; i < length; i++)
			{
				byte nt = (byte)((index & (3ul << 2*i)) >> 2*i); // Represented as 0,1,2,3
				buffer[length - i - 1] = (byte)(1 << (3-nt)); // 0->8, 1->4, 2->2, 3->1  
			}
			return new ShortDnaSequence(buffer, length);
		}

		/// <summary>
		/// Get an index that encodes the k-mer of a given length at a given position,
		/// using two bits per nucleotide and higher bits for left-most nucleotides. 
		/// Useful only for non-degenerate sequences up to 32 bp, after which the index will be cyclic.
		/// </summary>
		/// <param name="start"></param>
		/// <param name="lenth"></param>
		/// <returns></returns>
		public ulong GetIndexAt(long start, int length)
		{
			ulong result = 0;
			for (long ix = start; ix < start + length && ix < Count; ix++)
			{
				result <<= 2;
				ulong nt = 0;
				if (this[ix] == 1) nt = 3; // T
				if (this[ix] == 2) nt = 2; // G
				if (this[ix] == 4) nt = 1; // C
				// if(this[ix] == 8) nt = 0; // A

				result |= nt;
			}
			return result;
		}

		/// <summary>
		/// Convert to a non-degenerate index; degenerates will be converted to A.
		/// NOTE: nt should be a number (0-7), not a character
		/// </summary>
		/// <param name="nt"></param>
		/// <returns></returns>
		public static byte NtToIndex(byte nt)
		{
			if(nt == 1) return 3; // T
			if(nt == 2) return 2; // G
			if(nt == 4) return 1; // C
			return 0; // A
		}

		public char GetNucleotide(long pos)
		{
			return IupacEncoding.ToIupac(this[pos]);
		}
        public void SetNucleotide(long pos, char nt)
        {
            this[pos] = IupacEncoding.FromIupac(nt);
        }

		/// <summary>
		/// Test for the equality of two DnaSequences (based on their sequence). If you
		/// intend to check for reference equality, use ReferenceEquals or ==.
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public bool Equals(DnaSequence s)
		{
			if(s == null) return false;
			if(Count != s.Count) return false;
			for(long ix = 0; ix < Count; ix++)
			{
				if(this[ix] != s[ix]) return false;
			}
			return true;
		}

		public override bool Equals(Object obj)
		{
			//Check for null and compare run-time types.
			if(obj == null || GetType() != obj.GetType()) return false;
			DnaSequence s = (DnaSequence)obj;
			if(Count != s.Count) return false;
			for(long ix = 0; ix < Count; ix++)
			{
				if(this[ix] != s[ix]) return false;
			}
			return true;
		}

		public override int GetHashCode()
		{
			uint hash = 0;
			for(long ix = 0; ix < Count; ix++)
			{
				hash = (hash << 1) ^ this[ix];
			}
			return (int)hash;
		}



		public void Append(DnaSequence s)
		{
			for(long ix = 0; ix < s.Count; ix++)
			{
				Append(s[ix]);
			}
		}

		public void Append(char nt)
		{
			byte code = IupacEncoding.FromIupac(nt);
			Append(code);
		}

		public void Append(string seq)
		{
			for(int ix = 0; ix < seq.Length; ix++)
			{
				Append(seq[ix]);
			}
		}

		public void Reverse()
		{
			byte temp;
			for(long ix = 0; ix < Count / 2; ix++)
			{
				temp = this[ix];
				this[ix] = this[Count - ix - 1];
				this[Count - ix - 1] = temp;
			}
		}

		public void Reverse(long start, long count)
		{
			byte temp;
			for(long ix = start; ix < count / 2; ix++)
			{
				temp = this[start + ix];
				this[start + ix] = this[start + count - ix - 1];
				this[start + count - ix - 1] = temp;
			}
		}

		public void Complement()
		{
			for(long ix = 0; ix < Count; ix++)
			{
				Complement(ix);
			}
		}
		public void Complement(long ix)
		{
			this[ix] = IupacEncoding.Complement(this[ix]);
		}

		public void Complement(long start, long count)
		{
			for(long ix = start; ix < Count && ix < start + count; ix++)
			{
				Complement(ix);
			}
		}

		public void RevComp()
		{
			Reverse();
			Complement();
		}

		public void RevComp(long start, long count)
		{
			Reverse(start, count);
			Complement(start, count);
		}

		public DnaSequence SubSequence(long start, long count)
		{
			DnaSequence s = null;
			if (count > 100000000) s = new LongDnaSequence();
			else s = new ShortDnaSequence();
			for(long ix = 0; ix < count; ix++)
			{
				if(start + ix >= this.Count) break;
				s.Append(this[start + ix]);
			}
			return s;
		}

		/// <summary>
		/// Count the number of occurrences of the given IUPAC pattern. The
		/// pattern is interpreted as a mask, and a match is counted whenever
		/// the sequence overlaps with the mask. For example IupacEncoding.GC (=6)
		/// matches any position that may have a G or C. Gaps in the sequence are
		/// never counted.
		/// </summary>
		/// <param name="iupacPattern"></param>
		/// <returns></returns>
		public long CountCases(byte iupacPattern)
		{
            return CountCases(iupacPattern, 0, Count);
        }
        public long CountCases(byte iupacPattern, long startPos, long endPos)
        {
			long cnt = 0;
			for(long ix = Math.Max(0, startPos); ix < Math.Min(Count, endPos); ix++)
			{
				if((this[ix] & iupacPattern) != 0) cnt++;
			}
			return cnt;
		}

        /// <summary>
        /// Count the number of occurrences of exactly the given IUPAC (degenerate) nt.
        /// </summary>
        /// <param name="nt">'A' or 'N', for example</param>
        /// <returns></returns>
        public long CountCases(char nt)
        {
            return CountCases(nt, 0, Count);
        }
        public long CountCases(char nt, long startPos, long endPos)
        {
            byte iupac = IupacEncoding.FromIupac(nt);
            long cnt = 0;
            for (long ix = Math.Max(0, startPos); ix < Math.Min(Count, endPos); ix++)
            {
                if (this[ix] == iupac) cnt++;
            }
            return cnt;
        }

		public long CountGaps()
		{
			long cnt = 0;
			for(long ix = 0; ix < Count; ix++)
			{
				if(this[ix] == 0) cnt++;
			}
			return cnt;
		}

		/// <summary>
		/// Returns a string representation of the compressed sequence,
		/// using the standard upper-case IUPAC encoding.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			StringBuilder sb = new StringBuilder((int)Count);
			for(long ix = 0; ix < Count; ix++)
			{
				sb.Append(IupacEncoding.ToIupac(this[ix]));
			}
			return sb.ToString();
		}

		/// <summary>
		/// Returns a non-degenerate gap-free uniformly random sequence
		/// </summary>
		/// <param name="length"></param>
		/// <returns></returns>
		public static DnaSequence Random(long length)
		{
			DnaSequence result = null;
			if (length > 100000000) result = new LongDnaSequence();
			else result = new ShortDnaSequence();
			while (length > 0)
			{
				int nt = (int)(MersenneTwister.Instance.NextUInt32() % 4);
				result.Append((byte)(1 << nt));
				length--;
			}
			return result;
		}

		/// <summary>
		/// Returns a non-degenerate gap-free biased random sequence
		/// </summary>
		/// <param name="length"></param>
		/// <param name="probabilities">Four probabilities, which must add up to 1.0, for the nucleotides A,C,G and T</param>
		/// <returns></returns>
		public static DnaSequence RandomBiased(int length, double[] probabilities)
		{
			DnaSequence result = DnaSequence.CreateEmpty(length);
			while(length > 0)
			{
				double p = MersenneTwister.Instance.NextDouble();
				for(int i = 0; i < 4; i++)
				{
					if(probabilities[i] > p) p -= probabilities[i];
					else
					{
						result.Append((byte)(1 << i));
						break;
					}
				}
				length--;
			}
			return result;
		}

		public static DnaSequence RandomMotif(DnaMotif motif)
		{
			DnaSequence result = DnaSequence.CreateEmpty(motif.Length);
			for (int i = 0; i < motif.Length; i++)
			{
			 	double p = MersenneTwister.Instance.NextDouble();
				for(byte nt = 0; nt < 4; nt++)
				{
					double prob = motif.Probability(i, nt);
					if(p > prob) p -= prob;
					else
					{
						result.Append((byte)(1 << nt));
						break;
					}
				}
			}
			return result;
		}

		public DnaSequence SubSequence(long start)
		{
			return SubSequence(start, Count - start);
		}
		/// <summary>
		/// Yield all subsequences of length k, with the indicated step
		/// </summary>
		/// <param name="k"></param>
		/// <param name="step"></param>
		/// <returns></returns>
		public IEnumerable<DnaSequence> AllSubSequences(long k, long step)
		{
			for (long i = 0; i < Count - k; i+=step)
			{
				yield return SubSequence(i, k);
			}
		}

		public IEnumerable<DnaSequence> AllSubSequences(long k)
		{
			for(long i = 0; i < Count - k; i++)
			{
				yield return SubSequence(i, k);
			}
		}

        public void FindPolyNSubSequence(char nt, bool findFirst, int minCount, int start,
                                         out int pos, out int count)
        {
            byte b = IupacEncoding.FromIupac(nt);
            int maxCount = 0;
            int maxCountPos = -1;
            pos = start;
            while (pos < Count - minCount)
            {
                if (this[pos] == b)
                {
                    int i = pos + 1;
                    while (i < Count && this[i] == b) i++;
                    count = i - pos;
                    if (findFirst && count >= minCount)
                        return;
                    if (count > maxCount)
                    {
                        maxCount = count;
                        maxCountPos = pos;
                    }
                    while (this[pos] == b) pos++;
                }
                pos++;
            }
            pos = maxCountPos;
            count = maxCount;
        }

        /// <summary>
        /// Find all nt-rich subsequences that have some maximum fraction of other nts.
        /// Will only find sequences containing at least a tandem pair of nt.
        /// </summary>
        /// <param name="nt">The selected enriched nucleotide</param>
        /// <param name="maxFractionOtherNts"></param>
        /// <returns>Pairs of start position, length for subsequences</returns>
        public List<Pair<int, int>> FindNtRichSubSequence(char nt, double maxFractionOtherNts, int minCount)
        {
            List<Pair<int, int>> result = new List<Pair<int, int>>();
            byte b = IupacEncoding.FromIupac(nt);
            int minDist = Math.Max(2, minCount);
            List<int> otherPos = new List<int>();
            for (int p = 0; p < Count; p++)
                if (this[p] != b) otherPos.Add(p);
            int i = 0;
            int fromPos = 0;
            while (i < otherPos.Count)
            {
                int dist = otherPos[i] - fromPos;
                if (dist >= minDist)
                {
                    int length = dist;
                    int bestLength = dist;
                    double other = 0.0;
                    double bestOther = 0.0;
                    int bestI = i;
                    int k = i - 1;
                    int j = i + 1;
                    while (j < otherPos.Count)
                    {
                        int nextDist = otherPos[j] - otherPos[j-1] - 1;
                        if ((other + 1.0) / maxFractionOtherNts > this.Count - fromPos)
                            break;
                        length += 1 + nextDist;
                        other += 1.0;
                        if (other / length <= maxFractionOtherNts)
                        {
                            bestOther = other;
                            bestLength = length;
                            bestI = j;
                        }
                        j += 1;
                    }
                    other = bestOther;
                    length = bestLength;
                    while (k > 0)
                    {
                        int prevDist = otherPos[k] - otherPos[k-1] - 1;
                        if ((other + 1.0) / maxFractionOtherNts > otherPos[bestI])
                            break;
                        length += 1 + prevDist;
                        other += 1.0;
                        if (other / length <= maxFractionOtherNts)
                        {
                            bestOther = other;
                            bestLength = length;
                            fromPos = otherPos[k-1] + 1;
                        }
                        k -= 1;
                    }
                    int toPos = otherPos[bestI];
                    while (this[fromPos] != b)
                    {
                        fromPos++;
                        bestOther--;
                        bestLength--;
                    }
                    while (this[toPos-1] != b)
                    {
                        toPos--;
                        bestOther--;
                        bestLength--;
                    }
                    if (bestLength >= minCount)
                        result.Add(new Pair<int,int>(fromPos, bestLength));
                    i = bestI;
                }
                fromPos = otherPos[i] + 1;
                i += 1;
            }
            return result;
        }

        /// <summary>
        /// Read and return the first sequence from a genbank or fasta formatted sequence file
        /// </summary>
        /// <param name="seqFile"></param>
        /// <returns></returns>
        public static DnaSequence FromFile(string seqFile)
        {
            DnaSequence seq = null;
            if (seqFile.IndexOf(".gbk") > 0)
            {
                GenbankFile records = GenbankFile.Load(seqFile);
                seq = records.Records[0].Sequence;
            }
            else
            {
                FastaFile records = FastaFile.Load(seqFile);
                seq = records.Records[0].Sequence;
            }
            return seq;
        }	

		/// <summary>
		/// Create a zero-length sequence capable of hold a desired max length. This will return
		/// either a ShortDnaSequence or a LongDnaSequence, depending on the desired length.
		/// </summary>
		/// <param name="maxLength">Anticipated max length</param>
		/// <returns></returns>
		public static DnaSequence CreateEmpty(long maxLength)
		{
			if (maxLength > 100000000) return new LongDnaSequence(maxLength);
			else return new ShortDnaSequence(maxLength);
		}
	}

	/// <summary>
	/// Represents a DNA sequence stored in uncompressed form, allowing degeneracy. Each nucleotide position
	/// is stored as a four-bit pattern in the order ACGT. For example, A is 0001, G is 0100 and N is 1111.
	/// The internal storage is a byte array, where only the lower four bits are used.
	/// The DnaSequence can grow as needed using the Append method, and the internal storage will double
	/// when necessary. This class is not suitable for sequences longer than about 2 Gbp (use LongDnaSequence instead), 
	/// since the internal array is limited to 32-bit adressing. However, for shorter DNA sequences, it has the
	/// advantage of using standard garbage collection, which prevents heap fragmentation.
	/// </summary>
	public class ShortDnaSequence : DnaSequence
	{
		List<byte> words;	// array of 8-bit words

		public ShortDnaSequence(string seq) : this()
		{
			for(int ix = 0; ix < seq.Length; ix++) this.Append(IupacEncoding.FromIupac(seq[ix]));
		}

		public ShortDnaSequence(DnaSequence seq) : this()
		{
			for(long ix = 0; ix < seq.Count; ix++) this.Append(seq[ix]);
		}

		public ShortDnaSequence() : this(20) { }
		public ShortDnaSequence(long capacity)
		{
			if (capacity > Int32.MaxValue) throw new OutOfMemoryException("Cannot allocate ShortDnaSequence with capacity > " + int.MaxValue);

			Count = 0;
			words = new List<byte>((int)capacity);
		}

		public ShortDnaSequence(byte[] buffer, int length) : this(length)
		{
			for (int i = 0; i < length; i++)
			{
				words.Add(buffer[i]);
			}
			Count = length;
		}

		/// <summary>
		/// Gets or sets the binary representation of the nucleotide at index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override byte this[long index]
		{
			get
			{
				return words[(int)index];
			}
			set
			{
				words[(int)index] = value;
			}
		}

		public override void Append(byte item)
		{
			words.Add(item);
			Count++;
		}
	}

	/// <summary>
	/// Represents a DNA sequence stored in compressed form, but allowing degeneracy. Each nucleotide position
	/// is stored as a four-bit pattern in the order ACGT. For example, A is 1000, G is 0010 and N is 1111.
	/// The internal storage is a raw byte array, supporting very long sequences on 64-bit machines.
	/// The DnaSequence can grow as needed using the Append method, and the internal storage will double
	/// when necessary. This class is NOT suitable for short sequences (use ShortDnaSequence instead), since
	/// the memory is not garbage collected, and may cause fragmentation of the heap.
	/// </summary>
	public unsafe class LongDnaSequence : DnaSequence
	{
		byte* words;	// array of 8-bit words
		private long Capacity;

		public LongDnaSequence(string seq) : this()
		{
			for(int ix = 0; ix < seq.Length; ix++) this.Append(IupacEncoding.FromIupac(seq[ix]));
		}

		public LongDnaSequence(DnaSequence seq) : this()
		{
			for(long ix = 0; ix < seq.Count; ix++) this.Append(seq[ix]);
		}

		public LongDnaSequence() : this(20) { }
		public LongDnaSequence(long capacity)
		{
			Count = 0;
			Capacity = capacity;
			words = (byte*)Marshal.AllocHGlobal((IntPtr)(Capacity / 2 + 1));
			if(words == null)
			{
				Console.Error.WriteLine("ERROR: AllocHGlobal returned null");
				throw new OutOfMemoryException("AllocHGlobal returned null");
			}
		}
		public LongDnaSequence(byte[] buffer, int length) : this(buffer.Length*2)
		{
			if (length != buffer.Length * 2 && length != buffer.Length * 2 - 1) throw new InvalidDataException("length must be exactly 2x buffer size or 2x buffer size minus one");
			for (int i = 0; i < buffer.Length; i++)
			{
				words[i] = buffer[i];
			}
			Count = length;
		}

		~LongDnaSequence()
		{
			Marshal.FreeHGlobal((IntPtr)words);
		}

		/// <summary>
		/// Gets or sets the binary representation of the nucleotide at index.
		/// </summary>
		/// <param name="index"></param>
		/// <returns></returns>
		public override byte this[long index]
		{
			get
			{
				// extract the two bits
				long offset = index >> 1;
				byte shift = (byte)((index % 2) * 4);
				byte mask = (byte)(15 << shift);

				return (byte)((words[offset] & mask) >> shift);
			}
			set
			{
				// no bounds or value checking here, for performance reasons

				// locate the two bits
				long offset = index >> 1;
				byte shift = (byte)((index % 2) * 4);
				byte mask = (byte)~(15 << shift);

				// put them in place
				*(words + offset) = (byte)((*(words + offset) & mask) | (value << shift));
			}
		}

     	public override void Append(byte item)
		{
			if(Capacity <= Count)
			{
				Capacity *= 2;
				var temp = (byte*)Marshal.AllocHGlobal((IntPtr)(Capacity / 2 + 1));
				if(temp == null)
				{
					Console.Error.WriteLine("ERROR: AllocHGlobal returned null");
					throw new OutOfMemoryException("AllocHGlobal returned null");
				}

				for(long i = 0; i < Count / 2 + 1; i++)
				{
					*(temp + i) = *(words + i);
				}
				Marshal.FreeHGlobal((IntPtr)words);
				words = temp;
			}
			this[Count++] = item;
		}

	

		#region Custom serialization

		public unsafe void Serialize(BinaryWriter writer)
		{
			writer.Write(Count);
			for(long i = 0; i < Count/2 + 1; i++)
			{
				writer.Write(*(words + i));
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="skip">If true, the sequence will not actually be loaded</param>
        /// <returns></returns>
		public static DnaSequence Deserialize(BinaryReader reader, bool skip)
		{
			long numItems = reader.ReadInt64();
            if (skip)
            {
                while (numItems > 0)
                {
                    reader.ReadByte();
                    numItems--;
                }
                return new LongDnaSequence();
            }

			LongDnaSequence result = new LongDnaSequence(numItems);
			result.Count = numItems;
			byte[] data = reader.ReadBytes((int)(numItems / 2 + 1));
			for(long i = 0; i < data.Length; i++)
			{
				*(result.words + i) = data[i];
			}
			return result;
		}

#endregion

	}
}
