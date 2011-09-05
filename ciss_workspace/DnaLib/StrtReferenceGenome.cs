using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
	public class StrtReferenceGenome
	{
		public string Name { get; set; }
		public string BowtieIndex { get; set; }
		public string GenbankGenome { get; set; }
		public string Assembly { get; set; }

		public static StrtReferenceGenome[] Genomes = new StrtReferenceGenome[] { 
			new StrtReferenceGenome { Name = "Mouse", Assembly = "C57BL6/J", BowtieIndex = "m_musculus", GenbankGenome = "Mus Musculus" },
			new StrtReferenceGenome { Name = "Human", Assembly = "reference", BowtieIndex = "h_sapiens", GenbankGenome = "Homo Sapiens" }
		};

	}
}
