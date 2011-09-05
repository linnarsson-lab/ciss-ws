using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	/// <summary>
	/// Rudimentary parser for phrap ace files. This parser ignores everything except the aligned reads. You will not
	/// get quality values, the consensus or any of the other annotations associated with each read. The parser is
	/// primarily meant to parse .ace files generated through the Mosaik pipeline.
	/// </summary>
	public class PhrapAceFile
	{
		public List<AceRecord> Records { get; private set; }

		public PhrapAceFile()
		{
			Records = new List<AceRecord>();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static IEnumerable<AceRecord> Stream(string path)
		{
			var reader = new LineReader(path);
			Dictionary<string, AceRecord> temp_records = new Dictionary<string,AceRecord>();

			while(true)
			{
				string line = reader.ReadLine();

				if(line == null)
				{
					reader.Close();
					yield break;
				}

				// Collect all the records (sans sequence) from the AF lines
				if(line.StartsWith("AF"))
				{
					string[] items = line.Split(' ');
					AceRecord record = new AceRecord { Header = items[1], IsForwardStrand = items[2] == "U", Offset = int.Parse(items[3]) - 1 };
					temp_records[record.Header] = record;
				}

				// Now collect the aligned sequence
				else if(line.StartsWith("RD"))
				{
					string header = line.Split(' ')[1];
					line = reader.ReadLine();
					StringBuilder sb = new StringBuilder();
					while(line != "")
					{
						sb.Append(line);
						line = reader.ReadLine();
					}
					if(!temp_records.ContainsKey(header))
					{
						Console.WriteLine("Found RD entry without preceding AF entry: " + header);
						continue;
					}
					AceRecord record = temp_records[header];
					temp_records[header] = null;
					record.Sequence = sb.ToString();
					yield return record;
				}
			}
		}

		public static PhrapAceFile Load(string path)
		{
			PhrapAceFile result = new PhrapAceFile();
			foreach(AceRecord record in Stream(path))
			{
				result.Records.Add(record);
			}
			return result;
		}
	}

	public class AceRecord
	{
		public string Header { get; set; }
		/// <summary>
		/// The (zero-based) offset
		/// </summary>
		public int Offset { get; set; }
		public bool IsForwardStrand { get; set; }
		public string Sequence { get; set; }

	}
}
