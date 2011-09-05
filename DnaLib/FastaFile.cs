using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public class FastaFile
	{
		public List<FastaRecord> Records { get; set; }

		public FastaFile()
		{
			Records = new List<FastaRecord>();
		}

		public static IEnumerable<FastaRecord> Stream(string path)
		{
			StreamReader sr;
			if(Path.GetExtension(path) == ".gz")
			{
				FileStream fs = new FileStream(path, FileMode.Open);
				GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress);
				sr = new StreamReader(gzip);
			}
			else
			{
				sr = new StreamReader(path);
			}
			
			using(sr)
			{
				// read the first line of the first record
				string line = sr.ReadLine();
				while(true)
				{
					FastaRecord record = new FastaRecord();

					if(line == null)
					{
						sr.Close();
						yield break;
					}
					if(line.Trim() == "") continue; // we are forgiving about extra empty lines

					// Parse the header and extract any annotations
					if(!(line.StartsWith(">"))) throw new InvalidDataException("Fasta record does not start with '>'");
					line = line.Substring(1);
					record.HeaderLine = line;
					string[] items = line.Split(' ');
					if(items.Length > 0)
					{
						record.Identifier = items[0];
						if(items.Length > 1)
						{
							for(int ix = 1; ix < items.Length; ix++)
							{
								string[] pair = items[ix].Split('=');
								if(pair.Length == 2)
								{
									if(pair[0].StartsWith("/")) pair[0] = pair[0].Substring(1);
									record.Annotations[pair[0]] = pair[1];
								}
							}
						}
					}

					// Parse the sequence
					DnaSequence seq = new LongDnaSequence();
					while(true)
					{
						line = sr.ReadLine();
						if(line == null)
						{
							record.Sequence = seq;
							break;
						}
						if(line.StartsWith(">")) break;
						if(line.Trim() == "") continue; // we are forgiving about extra empty lines
						seq.Append(line.Trim());
					}
					record.Sequence = seq;
					yield return record;
				}
			}
		}

        /// <summary>
        /// Loads a fasta file into memory. Also accepts .gz files as input.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
		public static FastaFile Load(string path)
		{
			FastaFile result = new FastaFile();
			foreach(FastaRecord record in Stream(path))
			{
				result.Records.Add(record);
			}
			return result;
		}
        public static void Write(string path, List<FastaRecord> fastaRecords)
        {
            StreamWriter writer = path.OpenWrite();
            foreach (FastaRecord rec in fastaRecords)
                rec.ToFile(writer);
            writer.Close();
        }
    }


	public class FastaRecord
	{
        public static int WriteLineLength = 60;

        public Dictionary<string,string> Annotations { get; set; }
		public string HeaderLine { get; set; }
        public string Identifier { get; set; }
		public DnaSequence Sequence { get; set; }

		public FastaRecord()
		{
			Annotations = new Dictionary<string, string>();
		}

        public FastaRecord(string headerLine, DnaSequence sequence)
        {
            HeaderLine = headerLine;
            Sequence = sequence;
            Annotations = new Dictionary<string, string>();
        }

        public override string ToString()
        {
            StringBuilder s = new StringBuilder();
            s.Append(">" + HeaderLine + "\n");
            for (long i = 0; i < Sequence.Count; i += WriteLineLength)
                s.Append(Sequence.SubSequence(i, WriteLineLength).ToString() + "\n");
            return s.ToString();
        }

        /// <summary>
        /// Use for very long sequences where ToString may give MemoryOverflow
        /// </summary>
        /// <param name="writer"></param>
        public void ToFile(StreamWriter writer)
        {
            writer.WriteLine(">" + HeaderLine);
            for (long i = 0; i < Sequence.Count; i += WriteLineLength)
                writer.WriteLine(Sequence.SubSequence(i, WriteLineLength));
        }
    }

}
