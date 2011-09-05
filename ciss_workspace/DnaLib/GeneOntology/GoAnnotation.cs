using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace Linnarsson.Dna.GeneOntology
{
	public class GoAnnotation
	{
		public GeneOntology Ontology { get; set; }
		private Dictionary<string, List<GoTerm>> Annotations = new Dictionary<string, List<GoTerm>>();

		private GoAnnotation(GeneOntology ontology)
		{
			Ontology = ontology;
		}

		/// <summary>
		/// Read a GO annotation file in GAF 2.0 format
		/// </summary>
		/// <param name="filename">The full path to the file (can be text or .gz)</param>
		/// <param name="ontology">The ontology that is used by the annotation</param>
		/// <returns></returns>
		public static GoAnnotation FromFile(string filename, GeneOntology ontology)
		{
			GoAnnotation result = new GoAnnotation(ontology);

			// Read the file
			var file = filename.OpenRead();

			while (true)
			{
				string line = file.ReadLine();
				if (line == null) break;

				if (line.StartsWith(";")) continue;

				string[] items = line.Split('\t');
				if (items.Length < 5) continue;

				if (items[3].Trim().ToUpper() == "NOT") continue;	// We don't accept negative annotations, because they are hard to interpret

				string gene = items[2];
				string go = items[4];
				GoTerm term = ontology.GetTerm(go);
				if (term == null) continue;

				if (!result.Annotations.ContainsKey(gene)) result.Annotations[gene] = new List<GoTerm>();
				result.Annotations[gene].Add(term);
			}
			file.Close();

			return result;
		}


		/// <summary>
		/// Returns a list of GO terms
		/// </summary>
		/// <param name="gene"></param>
		/// <returns></returns>
		public List<GoTerm> TermsForGene(string gene)
		{
			if (Annotations.ContainsKey(gene)) return new List<GoTerm>(Annotations[gene]);
			return new List<GoTerm>();
		}

		public List<string> GenesForTerm(GoTerm term)
		{
			List<string> result = new List<string>();
			foreach (var kvp in Annotations) if (kvp.Value.Contains(term)) result.Add(kvp.Key);
			return result;
		}

		/// <summary>
		/// Returns a semicolon-separated string of GO term identifiers
		/// </summary>
		/// <param name="gene"></param>
		/// <returns></returns>
		public string TermsForGeneAsString(string gene)
		{
			if (!Annotations.ContainsKey(gene)) return "";

			StringBuilder sb = new StringBuilder();
			foreach (var term in Annotations[gene])
			{
				sb.Append(term.Id);
				sb.Append(";");
			}
			if (sb.Length > 0) sb.Length--;	// Remove trailing semicolon
			return sb.ToString();
		}

		/// <summary>
		/// Returns a dictionary mapping gene names to semicolon-separated strings of GO term IDs
		/// </summary>
		/// <returns></returns>
		public Dictionary<string, string> AllAnnotationsAsStrings()
		{
			Dictionary<string, string> result = new Dictionary<string, string>();
			foreach (var key in Annotations.Keys)
			{
				result[key] = TermsForGeneAsString(key);
			}
			return result;
		}
	}
}
