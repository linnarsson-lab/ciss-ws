using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna.GeneOntology
{
	public enum GoNamespace { MolecularFunction, BiologicalProcess, CellularComponent, None }

	public class GoTerm
	{
		public string Id { get; set; }
		public string Name { get; set; }
		public string Definition { get; set; }
		public GoNamespace Namespace { get; set; }
		public List<string> Synonyms { get; set; }
		public List<string> GoSubsets { get; set; }
		public List<string> CrossReferences { get; set; }
		public List<GoTerm> IsA { get; set; }
		public List<GoTerm> ParentOf { get; set; }

		// Unresolved is-a references
		internal List<string> danglingIsAs = new List<string>();

		/// <summary>
		/// Returns true if this term is a descendant of 'other'
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool RecursiveIsA(GoTerm other)
		{
			if (IsA.Contains(other)) return true;
			foreach (var i in IsA) if (i.RecursiveIsA(other)) return true;
			return false;
		}

		public GoTerm()
		{
			Synonyms = new List<string>();
			GoSubsets = new List<string>();
			CrossReferences = new List<string>();
			IsA = new List<GoTerm>();
			Namespace = GoNamespace.None;
			ParentOf = new List<GoTerm>();
		}

		public bool IsValid()
		{
			return (Id != null) && Id.StartsWith("GO:") && Name != null && Definition != null;
		}
	}
}
