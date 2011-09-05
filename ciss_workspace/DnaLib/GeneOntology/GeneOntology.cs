using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;

namespace Linnarsson.Dna.GeneOntology
{
	public class GeneOntology
	{
		private Dictionary<string, GoTerm> index;

		public GoTerm BiologicalProcess { get; set; }
		public GoTerm MolecularFunction { get; set; }
		public GoTerm CelularComponent { get; set; }

		public GeneOntology()
		{
			index = new Dictionary<string, GoTerm>();
		}

		/// <summary>
		/// Add a GoTerm to the ontology
		/// </summary>
		/// <param name="term"></param>
		public void AddGoTerm(GoTerm term)
		{
			index[term.Id] = term;
		}

		/// <summary>
		/// Return the GoTerm with the given id, or null if not found
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public GoTerm GetTerm(string id)
		{
			if (index.ContainsKey(id)) return index[id];
			return null;
		}

		// resolve all internal is-a relationships, and populate the three namespaces
		private void rebuild()
		{
			foreach (var term in index.Values)
			{
				foreach (string isa in term.danglingIsAs)
				{
					GoTerm parent = GetTerm(isa);
					if (parent == null) throw new InvalidDataException("Dangling is-a relationship");
					term.IsA.Add(parent);
					parent.ParentOf.Add(term);
				}
				term.danglingIsAs = null;

				if (term.Name == "biological_process") BiologicalProcess = term;
				if (term.Name == "cellular_component") CelularComponent = term;
				if (term.Name == "molecular_function") MolecularFunction = term;
			}
		}

		/// <summary>
		/// Load an ontology from an OBO 1.2 file
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static GeneOntology FromFile(string filename)
		{
			GeneOntology go = new GeneOntology();
			var file = filename.OpenRead();

			while (true)
			{
				string line = file.ReadLine();
				if (line == null) break;

				// Skip to the next term (skipping Typedefs and headers)
				if (!line.StartsWith("[")) continue;
				if (line.Trim() == "[Term]")
				{
					GoTerm term = parseTerm(file);
					if (term != null) go.AddGoTerm(term);
					continue;
				}
			}
			file.Close();
			go.rebuild();
			return go;
		}

		// Parse one term from just after [Term], consuming the next [Term] or EOF
		private static GoTerm parseTerm(StreamReader file)
		{
			GoTerm term = new GoTerm();

			while (true)
			{
				string line = file.ReadLine();
				if (line == null) break;
				if (line.Trim() == "") break; // End of record

				int colonPosition = line.IndexOf(':');
				string tag = line.Substring(0, colonPosition).Trim();
				string value = line.Substring(colonPosition + 1).Trim();

				switch (tag)
				{
					case "id":
						term.Id = value;
						break;
					case "name":
						term.Name = value;
						break;
					case "namespace":
						if (value == "biological_process") term.Namespace = GoNamespace.BiologicalProcess;
						if (value == "cellular_component") term.Namespace = GoNamespace.CellularComponent;
						if (value == "molecular_function") term.Namespace = GoNamespace.MolecularFunction;
						break;
					case "def":
						term.Definition = value;
						break;
					case "synonym":
						term.Synonyms.Add(value);
						break;
					case "is_a":
						term.danglingIsAs.Add(value.Split('!')[0].Trim());
						break;
					case "xref":
						term.CrossReferences.Add(value);
						break;
				}
			}

			return term;
		}
	}
}
