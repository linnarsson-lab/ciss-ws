using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	public enum TaxonId
	{
		Human = 9606,
		Mouse = 10090,
		Ecoli = 562,
		Arabidopsis = 3702,
		Rat = 10116,
		Zebrafish = 7955
	}

	/// <summary>
	/// Convert gene names from one species to another using Homologene
	/// </summary>
	public class Transmogrifier
	{
		public Dictionary<string,string> Homologenes { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="fromTaxonId">Taxon ID to convert from</param>
		/// <param name="ToTaxonId">Taxon ID to convert to</param>
		public Transmogrifier(int fromTaxonId, int ToTaxonId)
		{
			Homologenes = new Dictionary<string, string>();

			var file = RemoteFile.Get("ftp://ftp.ncbi.nih.gov/pub/HomoloGene/current/homologene.data").OpenRead();
			int currentHomoloGene = 0;
			string currentFromGene = null;
			string currentToGene = null;
			while (true)
			{
				string line = file.ReadLine();
				if (line == null) break;
				var items = line.Split('\t');

				int homoloGene = int.Parse(items[0]);
				int taxonId = int.Parse(items[1]);
				string gene = items[3];
				if (taxonId == fromTaxonId) currentFromGene = gene;
				if (taxonId == ToTaxonId) currentToGene = gene;
				if (homoloGene != currentHomoloGene)
				{
					if(currentToGene != null && currentFromGene != null) Homologenes[currentFromGene] = currentToGene;
					currentFromGene = null;
					currentToGene = null;
					currentHomoloGene = homoloGene;
				}
			}
			if (currentToGene != null && currentFromGene != null) Homologenes[currentFromGene] = currentToGene; // Last record
			file.Close();
		}

		public string Convert(string gene)
		{
			if(Homologenes.ContainsKey(gene)) return Homologenes[gene];
			return null;
		}
	}
}
