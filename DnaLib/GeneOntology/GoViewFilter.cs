using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics.Data;

namespace Linnarsson.Dna.GeneOntology
{
	/// <summary>
	/// A filter that can be used on a Linnarsson.Mathematics.Data.DataView to filter 
	/// on Gene Ontology terms. The terms must be given as a space-separated list of GO ids
	/// in an annotation atteched to the rows or columns of the view. 
	/// </summary>
	public class GoViewFilter : ViewFilter
	{
		/// <summary>
		/// A list of terms to include in the filter. An entry that matches one of these terms,
		/// or is a descendant of one of these terms, is accepted by the filter.
		/// </summary>
		public List<GoTerm> TermsToInclude { get; set; }

		/// <summary>
		/// The name of the annotation field, typically "go" or "GeneOntology" etc.
		/// </summary>
		public string AnnotationName { get; set; }

		/// <summary>
		/// The ontology to use. All terms must be members of the onotlogy.
		/// </summary>
		public GeneOntology Ontology { get; set; }

		public override bool Examine(DataView view, int index)
		{
			string goAnnotation = view.GetAnnotationForRow(AnnotationName, index);
			if (goAnnotation == null) return false;

			string[] goTerms = goAnnotation.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string id in goTerms)
			{
				GoTerm go = Ontology.GetTerm(id);
				foreach (GoTerm included in TermsToInclude) if (go.RecursiveIsA(included)) return true;
			}
			return false;
		}

		/// <summary>
		/// Create a filter that will accept any GO term that is equal to or descends from one of the given terms
		/// </summary>
		/// <param name="ontology"></param>
		/// <param name="annotationName"></param>
		/// <param name="termsToInclude"></param>
		public GoViewFilter(GeneOntology ontology, string annotationName, List<GoTerm> termsToInclude)
		{
			Ontology = ontology;
			AnnotationName = annotationName;
			TermsToInclude = new List<GoTerm>(termsToInclude);
		}
	}
}
