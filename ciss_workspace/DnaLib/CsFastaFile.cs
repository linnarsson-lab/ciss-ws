using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using Linnarsson.Mathematics.SortSearch;

namespace Linnarsson.Dna
{
	public class CsFastaFile
	{
		public List<CsFastaRecord> Records { get; set; }

		public CsFastaFile()
		{
			Records = new List<CsFastaRecord>();
		}

		public static IEnumerable<CsFastaRecord> Stream(string path)
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
				while(line.StartsWith("#")) line = sr.ReadLine(); // skip commments
				while(true)
				{
					CsFastaRecord record = new CsFastaRecord();

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
					StringBuilder seq = new StringBuilder();
					while(true)
					{
						line = sr.ReadLine();
						if(line == null) break;
						if(line.StartsWith(">")) break;
						if(line.Trim() == "") continue; // we are forgiving about extra empty lines
						seq.Append(line.Trim());
					}
					record.FirstBase = seq[0];
					record.ColorSequence = seq.ToString(1, seq.Length - 1);
					yield return record;
				}
			}
		}

		public static CsFastaFile Load(string path)
		{
			CsFastaFile result = new CsFastaFile();
			foreach(CsFastaRecord record in Stream(path))
			{
				result.Records.Add(record);
			}
			return result;
		}
	}

	public class CsFastaRecord
	{
		public Dictionary<string, string> Annotations { get; set; }
		public string HeaderLine { get; set; }
		public string Identifier { get; set; }
		public char FirstBase { get; set; }
		public string ColorSequence { get; set; }

		public CsFastaRecord()
		{
			Annotations = new Dictionary<string, string>();
		}
	}

}
