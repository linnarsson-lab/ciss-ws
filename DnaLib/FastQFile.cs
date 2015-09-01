using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public class FastQFile
	{
		/// <summary>
		/// Attempt to automatically detect if the quality score base is 64 or 33 by examining a number of records.
        /// Return 0 if both bases are possible.
		/// Will throw an exception if the file is incompatible with both 33 and 64 base.
		/// </summary>
		/// <param name="path">The input file</param>
		/// <param name="numberOfRecordsToExamine">Maximum number of records to examine before deciding</param>
		/// <returns>33, 64, or 0 if both bases work</returns>
		public static byte AutoDetectQualityScoreBase(string path, int numberOfRecordsToExamine)
		{
            byte qualBase = 0;
			int count = 0;
			try
			{
				foreach (var rec in FastQFile.Stream(path, 64))
                    if (count++ > numberOfRecordsToExamine) break;
				qualBase = 64;
			}
			catch (InvalidDataException) { }
            count = 0;
			try
			{
				foreach (var rec in FastQFile.Stream(path, 33))
                    if (count++ > numberOfRecordsToExamine) break;
                if (qualBase == 64) return 0;
			}
            catch (InvalidDataException) { }
            if (qualBase != 0) return qualBase;
            throw new InvalidDataException("Failed to autodetect quality score base, trying both 64 and 33");
		}

		public List<FastQRecord> Records { get; private set; }

		public FastQFile()
		{
			Records = new List<FastQRecord>();
		}

		/// <summary>
		/// Stream through a "FASTQ", "qseq.txt", "sequence.txt" or plain "FASTA" file.
		/// Will only yield reads that passed the Illumina filter (last column = "1").
		/// An exception is thrown if invalid quality scores are encountered (outside 0 - 40)
		/// </summary>
		/// <param name="path"></param>
		/// <param name="qualityScoreBase">The quality score base in the ASCII encoding (typically 64 or 33; use AutoDetectQualityScoreBase() to find out)</param>
		/// <returns></returns>
        public static IEnumerable<FastQRecord> Stream(string path, byte qualityScoreBase)
        {
            return Stream(path, qualityScoreBase, false);
        }

		/// <summary>
		/// Stream through a "FASTQ", "qseq.txt", "sequence.txt" or plain "FASTA" file.
		/// An exception is thrown if invalid quality scores are encountered (outside 0 - 40)
		/// </summary>
		/// <param name="path"></param>
		/// <param name="qualityScoreBase">The quality score base in the ASCII encoding (typically 64 or 33; use AutoDetectQualityScoreBase() to find out)</param>
		/// <param name="includeNonPFRecords">If true, records that failed Illumina chasitity filter will be included</param>
		/// <returns></returns>
		public static IEnumerable<FastQRecord> Stream(string path, byte qualityScoreBase, bool includeNonPFRecords)
		{
            var sr = new LineReader(path);
            string line, seq, qs;
			while(true)
			{
                try
                {

                    line = sr.ReadLine();
                    while (line == "")
                        line = sr.ReadLine();
                }
                catch (OutOfMemoryException)
                {
                    throw new OutOfMemoryException("Out of memory reading " + path + ". Do you have wrong line endings in the file?");
                }
				if (line == null)
				{
					sr.Close();
					yield break;
				}
				if (line.StartsWith("@")) // Classical .fastq file
				{
					string hdr = line.Substring(1);
                    try
                    {
                        // Parse the sequence
                        seq = sr.ReadLine();
                        sr.ReadLine();
                        qs = sr.ReadLine();
                    }
                    catch (OutOfMemoryException)
                    {
                        throw new OutOfMemoryException("Out of memory reading " + path + ". Do you have wrong line endings in the file?");
                    }
                    if (qs == null)
                        yield break;
					var fqr = new FastQRecord(hdr, seq, FastQRecord.QualitiesFromString(qs, qualityScoreBase));
					if(fqr.IsValid()) yield return fqr;
					else
					{
						// attempt to recover
						int recoverLen = 0;
						while(true)
						{
							string temp = sr.PeekLine();
							if(temp == null) break;
							if(temp.Length > 0 && temp[0] == '@') break;
							sr.ReadLine();
							recoverLen++;
						}
						Console.Error.WriteLine(recoverLen.ToString() + " lines skipped while recovering from malformed record. Next line:" + sr.PeekLine());
					}
				}
                else if (line.StartsWith(">")) // Standard Fasta file
                {
                    string hdr = line.Substring(1);
                    try
                    {
                        seq = sr.ReadLine();
                        string temp = sr.PeekLine();
                        while (temp != null && !temp.StartsWith(">"))
                        {
                            seq += sr.ReadLine();
                            temp = sr.PeekLine();
                        }
                    }
                    catch (OutOfMemoryException)
                    {
                        throw new OutOfMemoryException("Out of memory reading " + path + ". Do you have wrong line endings in the file?");
                    }
                    byte[] fakeQs = new byte[seq.Length];
                    yield return new FastQRecord(hdr, seq, fakeQs);
                }
                else
				{
					string[] items = line.Split('\t');
					if (items.Length == 11) // Illumina 'qseq.txt'
					{
						string hdr = items[0] + ":" + items[2] + ":" + items[3] + ":" + items[4] + ":" + items[5] + "#" + items[6] + "/" + items[7];
						seq = items[8].Replace('.', 'N');
						string qual = items[9];
                        bool passedFilter = (items[10] == "1");
						if (includeNonPFRecords || passedFilter)
						{
							var fqr = new FastQRecord(hdr, seq, FastQRecord.QualitiesFromString(qual, qualityScoreBase), passedFilter);
							if(fqr.IsValid()) yield return fqr;
						}
					}
                    else // Illumina sequence.txt.gz
					{
						items = line.Split(':');
                        string hdr = items[0] + ":" + items[1] + ":" + items[2] + ":" + items[3] + ":" + items[4];
						yield return new FastQRecord(hdr, items[5], FastQRecord.QualitiesFromString(items[6], qualityScoreBase));
					}
				}
			}
		}

		public static FastQFile Load(string path, byte qualityScoreBase)
		{
			FastQFile result = new FastQFile();
			foreach (FastQRecord record in Stream(path, qualityScoreBase))
			{
				result.Records.Add(record);
			}
			return result;
		}
	}

	public class FastQRecord
	{
        public string Header { get; set; }
		public string Sequence { get; private set; }

        /// <summary>
		/// Qualities as phred score
		/// </summary>
		public byte[] Qualities { get; private set; }
        public bool PassedFilter { get; set; }

		public FastQRecord(string hdr, string seq, byte[] phredQualities) : this(hdr, seq, phredQualities, true) { }

		public FastQRecord(string hdr, string seq, byte[] phredQualities, bool passedFilter)
		{
			Header = hdr;
			Sequence = seq;
			Qualities = phredQualities;
			PassedFilter = passedFilter;
		}

        /// <summary>
        /// Make a trimmed version of another FastQRecord
        /// </summary>
        /// <param name="oldRec"></param>
        /// <param name="start"></param>
        /// <param name="len"></param>
        public FastQRecord(FastQRecord oldRec, int start, int len, string newHeader)
        {
            Header = newHeader;
            Sequence = oldRec.Sequence.Substring(start, len);
            Qualities = new byte[len];
            Array.Copy(oldRec.Qualities, start, Qualities, 0, len);
            PassedFilter = oldRec.PassedFilter;
        }

		/// <summary>
		/// Return the ASCII-encoded quality string, using the indicated base
		/// See http://en.wikipedia.org/wiki/FASTQ_format
		/// </summary>
		/// <returns></returns>
		public string QualitiesAsString(byte qualityScoreBase)
		{
			StringBuilder sb = new StringBuilder(Sequence.Length);
			for (int i = 0; i < Sequence.Length; i++)
			{
				sb.Append((char)(Qualities[i] + qualityScoreBase));
			}
			return sb.ToString();
		}

		/// <summary>
		/// Return the phred scores for a given quality string
		/// See http://en.wikipedia.org/wiki/FASTQ_format
		/// This method will throw an exception if qualities outside the 0 - 40 interval are encountered.
        /// CHANGE TO 0 - 63 due to factual higher values. /Peter
		/// </summary>
		/// <param name="seq"></param>
		/// <param name="qualityScoreBase"></param>
		/// <returns></returns>
		public static byte[] QualitiesFromString(string qs, byte qualityScoreBase)
		{
			byte[] quals = new byte[qs.Length];
			for (int i = 0; i < qs.Length; i++)
			{
				var temp = qs[i] - qualityScoreBase;
                quals[i] = (byte)temp;
                if (temp < 0 || temp > 63) throw new InvalidDataException("Phred quality score outside expected range (0 - 63): "
                                                                          + temp + " at pos " + i + " in " + qs);
			}
			return quals;
		}
        public static byte[] QualitiesFromCharArray(char[] qs, byte qualityScoreBase)
        {
            byte[] quals = new byte[qs.Length];
            for (int i = 0; i < qs.Length; i++)
            {
                var temp = qs[i] - qualityScoreBase;
                quals[i] = (byte)temp;
                if (temp < 0 || temp > 63) throw new InvalidDataException("Phred quality score outside expected range (0 - 63): "
                                                                          + temp + " at pos " + i + " in " + qs);
            }
            return quals;
        }
        /// <summary>
		/// Make a quality string from an array of phred scores
		/// </summary>
		/// <param name="qualities"></param>
		/// <param name="qualityScoreBase"></param>
		/// <returns></returns>
		public static string StringFromQualities(byte[] qualities, byte qualityScoreBase)
		{
			StringBuilder result = new StringBuilder(qualities.Length);
			for (int i = 0; i < qualities.Length; i++)
			{
				result.Append((char)qualityScoreBase + qualities[i]);
			}
			return result.ToString();
		}
		/// <summary>
		/// Convert a Phred quality score to an error probability
		/// See http://en.wikipedia.org/wiki/Phred_quality_score
		/// </summary>
		/// <param name="phredQual"></param>
		/// <returns></returns>
		public static double QualityToProbability(int phredQual)
		{
			return Math.Pow(10, -phredQual / 10.0);
		}

		/// <summary>
		/// Convert an error probability to a Phred quality score
		/// See http://en.wikipedia.org/wiki/Phred_quality_score
		/// </summary>
		/// <param name="prob"></param>
		/// <returns></returns>
		public static int ProbabilityToQuality(double prob)
		{
			return (int)(-10 * Math.Log10(prob));
		}

        /// <summary>
        /// Replace Sequence and Qualities by a subsection of the read
        /// </summary>
        /// <param name="start">First position of replacement</param>
        /// <param name="len">Length to keep</param>
        public void Trim(int start, int len)
        {
           byte[] newQualities = new byte[len];
           Array.Copy(Qualities, start, newQualities, 0, len);
           Qualities = newQualities;
           Sequence = Sequence.Substring(start, len);
        }


        /// <summary>
        /// Insert a sequence and corresponding qualities into the record
        /// </summary>
        /// <param name="pos">Insertion position in record</param>
        /// <param name="insertSeq">Sequence to insert</param>
        /// <param name="insertQualities">If longer than insertSeq, only uses the same length from start</param>
        public void Insert(int pos, string insertSeq, byte[] insertQualities)
        {
            if (insertSeq.Length > insertQualities.Length)
                throw new FormatException("FastQRecord.Insert(" + pos + ", " + insertSeq + ",[ " + insertQualities.Length +
                                          "]): Qualities must be at least of sequence length!");
            Sequence = Sequence.Substring(0, pos) + insertSeq + Sequence.Substring(pos);
            byte[] newQualities = new byte[insertSeq.Length + Qualities.Length];
            Array.Copy(Qualities, 0, newQualities, 0, pos);
            Array.Copy(insertQualities, 0, newQualities, pos, insertSeq.Length);
            Array.Copy(Qualities, pos, newQualities, pos + insertSeq.Length, Qualities.Length - pos);
            Qualities = newQualities;
        }

        public bool IsValid()
        {
            if (Sequence == null || Qualities == null || Header == null) return false;
            if (Sequence.Length != Qualities.Length) return false;
            foreach (char c in Sequence) if ("ACGTN".IndexOf(c) < 0) return false;
			for (int i = 0; i < Qualities.Length; i++)
			{
				if (Qualities[i] < 0) return false;
			}
            return true;
        }

		/// <summary>
		/// Get the record as a fastq-formatted string (without newline)
		/// </summary>
		/// <returns></returns>
		public string ToString(byte qualityScoreBase)
		{
			return "@" + Header + Environment.NewLine +
				Sequence + Environment.NewLine +
				"+" + Environment.NewLine +
				QualitiesAsString(qualityScoreBase);
		}

		/// <summary>
		/// WARNING: this method is not implemented, by design, and throws an exception on every invocation. 
		/// Use ToString(byte qualityScoreBase) instead.
		/// </summary>
		/// <returns></returns>
		[Obsolete("Use ToString(byte qualityScoreBase) instead", true)]
		public new string ToString()
		{
			throw new NotImplementedException("FastQFile.ToString() is not implemented by design - use ToString(byte qualityScoreBase) instead!");
		}
    }

}
