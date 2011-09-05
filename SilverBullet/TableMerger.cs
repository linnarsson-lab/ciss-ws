using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;

namespace SilverBullet
{
	/// <summary>
	/// Add one or more data tables, then save the merged table to a file
	/// </summary>
	public class TableMerger
	{
		private List<string> columns = new List<string>();
		private Dictionary<string, List<string>> rows = new Dictionary<string, List<string>>();
		int colsSoFar = 0;
        public MergeTool Settings { get; set; }

        public TableMerger(MergeTool settings)
        {
            Settings = settings;
        }

		public void AddTable(string filename, bool union)
		{
			Console.WriteLine("Loading " + filename + "...");
			var file = filename.OpenRead();
			string sample = Path.GetFileNameWithoutExtension(filename);
			if(sample.Contains('_')) sample = sample.Split('_')[0];

			string[] headers = file.ReadLine().Split('\t');
			for(int i = 1; i < headers.Length; i++)
			{
				if(headers[i] != "") columns.Add(sample + "_" + headers[i]);
			}

			HashSet<string> uniqueSymbols = new HashSet<string>();

			while(true)
			{
				string line = file.ReadLine();
				if(line == null) break;
				string[] items = line.Split('\t');
				string key = items[0];
				
                // Maybe skip repeats
                if (key.StartsWith("repeat_") && !Settings.IncludeRepeats) continue;

				// Keep only the first instance of a symbol
				if(uniqueSymbols.Contains(key)) continue;
				else uniqueSymbols.Add(key);

				if(!rows.ContainsKey(key))
				{
					rows[key] = new List<string>();
					for(int i = 0; i < colsSoFar; i++)
					{
						rows[key].Add("0");
					}
				}
				for(int i = 1; i < headers.Length; i++)
				{
					rows[key].Add(items[i]);
				}
			}
			colsSoFar += headers.Length - 1;

			// Add enough columns to existing symbols that weren't in this file
			foreach(var kvp in rows)
			{
				while(kvp.Value.Count < colsSoFar)
				{
					kvp.Value.Add("0");
				}
			}

			file.Close();
			Console.WriteLine(rows.Count.ToString() + " distinct symbols loaded so far");
		}

        public void Save(string outputFile)
        {
            Console.WriteLine("Analyzing...");
            int[] columnTotals = new int[columns.Count];

            // First count total reads
            foreach (var kvp in rows)
            {
                for (int ix = 0; ix < columns.Count; ix++)
                {
                    // This column contained non-numerical data
                    if (columnTotals[ix] == -1) continue;

                    int temp = 0;
                    if (kvp.Value[ix] == "" || int.TryParse(kvp.Value[ix], out temp))
                    {
                        columnTotals[ix] += temp;
                    }
                    else
                    {
                        // -1 means non-numerical
                        columnTotals[ix] = -1;
                    }
                }
            }

            // Then remove columns with low read number
            int removed = 0;
            if (Settings.MinimumReadsPerCell > 0)
            {
                for (int ix = 0; ix < columnTotals.Length; ix++)
                {
                    if(columnTotals[ix] < 0) continue;  // -1 means retain because non-numeric
                    if (columnTotals[ix] < Settings.MinimumReadsPerCell)
                    {
                        foreach (var kvp in rows) kvp.Value.RemoveAt(ix - removed);
                        columns.RemoveAt(ix - removed);
                        removed++;
                        columnTotals[ix] = 0; // 0 means removed because less than minimum
                    }
                }
            }
            Console.WriteLine("{0} columns removed", removed);

            // Then normalize to tpm
            if (Settings.Normalize)
            {
                foreach (var kvp in rows)
                {
                    removed = 0;
                    for (int ix = 0; ix < columnTotals.Length; ix++)
                    {
                        if (columnTotals[ix] == 0)  // 0 means removed, -1 means retained because non-numeric
                        {
                            removed++;
                            continue;
                        }
                        long temp;
                        long.TryParse(kvp.Value[ix - removed], out temp);
                        kvp.Value[ix - removed] = (temp * 1000000 / columnTotals[ix]).ToString();
                    }
                }
            }
            Console.WriteLine("Saving...");
			var file = outputFile.OpenWrite();
			file.Write("Symbol\t");
			file.WriteLine(GetTabDelimited(columns));

            // Write totals
            file.Write("TOTAL\t");
            for (int ix = 0; ix < columnTotals.Length; ix++)
            {
                if (columnTotals[ix] == 0) continue; // 0 means removed, -1 means retained because non-numeric
                file.Write(columnTotals[ix].ToString());
                file.Write("\t");
            }
            file.WriteLine();

            // Write genes
			foreach(var kvp in rows)
			{
				file.Write(kvp.Key);
				file.Write('\t');
				file.WriteLine(GetTabDelimited(kvp.Value));
			}
			file.Close();
			Console.WriteLine("Done.");

		}

		private string GetTabDelimited(IList<string> items)
		{
			StringBuilder sb = new StringBuilder();
			foreach(string s in items) 
			{
				sb.Append(s);
				sb.Append('\t');
			}
			if(items.Count > 0) sb.Length--;
			return sb.ToString();
		}
	}
}
