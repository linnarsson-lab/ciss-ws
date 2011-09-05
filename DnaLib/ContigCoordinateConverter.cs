using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
	public class ContigCoordinateConverter
	{
		Dictionary<string, ContigMapRecord> Contigs = new Dictionary<string,ContigMapRecord>();

		/// <summary>
		/// 
		/// </summary>
		/// <param name="contigMapFile">The path to the contig map file</param>
		/// <param name="groupLabel">A label for a group of contigs, such as "C57BL/6J"</param>
		public ContigCoordinateConverter(string contigMapFile, string groupLabel)
		{
			if(!File.Exists(contigMapFile))
			{
				Console.Error.WriteLine("Couldn't find " + contigMapFile);
				return;
			}

			var temp = TabDelimitedFileReader<ContigMapRecord>.ReadAll(contigMapFile, true, true);
			foreach(var ctg in temp)
			{
				if(ctg.GroupLabel == groupLabel && ctg.Orientation == '+')
				{
					if(ctg.Chromosome == "MT") ctg.Chromosome = "M";
					Contigs[ctg.ContigAccession] = ctg;
				}
			}
			Console.Error.WriteLine("Found {0} contigs", Contigs.Count);
		}

		public string GetChromosome(string contig)
		{
			if(Contigs.ContainsKey(contig)) return Contigs[contig].Chromosome;
			else return null;
		}

		public long GetChromosomeLength(string chr)
		{
			ScoreTracker<long> tracker = new ScoreTracker<long>();
			foreach(var ctg in Contigs.Values)
			{
				tracker.Examine(ctg.End);
			}
			return tracker.MaxScore;
		}

		/// <summary>
		/// Convert the 1-based contig position to a 1-based chromosome position
		/// </summary>
		/// <param name="contig"></param>
		/// <param name="pos"></param>
		/// <returns></returns>
		public int Convert(string contig, int pos)
		{
			if(Contigs.ContainsKey(contig))
			{
				return Contigs[contig].Start + pos - 1;
			}
			throw new IndexOutOfRangeException("Contig " + contig + " not found");
		
		}

		public bool Exists(string contig)
		{
			return Contigs.ContainsKey(contig);
		}

		public int Convert(string contig, long pos)
		{
			return Convert(contig, (int)pos);
		}
		public List<string> GetChromosomeNames()
		{
			List<string> result = new List<string>();
			foreach(var ctg in Contigs.Values)
			{
				if(!result.Contains(ctg.Chromosome)) result.Add(ctg.Chromosome);
			}
			return result;
		}
	}

	class ContigMapRecord
	{
		public int TaxonomyId { get; set; }
		public string Chromosome { get; set; }
		public int Start { get; set; }
		public int End { get; set; }
		public char Orientation { get; set; }
		public string ContigAccession { get; set; }
		public string FeatureId { get; set; }
		public string FeatureType { get; set; }
		public string GroupLabel { get; set; }
		public int Weight { get; set; }

		public long Length { get { return End - Start + 1; } }
	}
}
