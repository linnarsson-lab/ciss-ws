using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using C1;

namespace C1
{
    public delegate void AnnotationChain(ref Transcript t);

    /// <summary>
    /// Add additonal annotations to new transcript models before inserting them into database
    /// </summary>
    public class TranscriptAnnotator
    {
        public AnnotationChain Annotate;

        public TranscriptAnnotator(StrtGenome genome)
        {
            Annotate += new kgXrefAnnotator(genome).AnnotateFtomUCSCkgXref;
        }
    }

    class kgXrefAnnotator
    {
        private string annotationFilename = "kgXref.txt";
        private Dictionary<string, string> trNameToDescription = new Dictionary<string, string>();

        public kgXrefAnnotator(StrtGenome genome)
        {
            string annotationPath = Path.Combine(genome.GetOriginalGenomeFolder(), annotationFilename);
            annotationPath = PathHandler.ExistsOrGz(annotationPath);
            if (annotationPath == null)
                return;
            using (StreamReader r = annotationPath.OpenRead())
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    trNameToDescription[fields[1].Trim()] = fields[7].Trim();
                }
            }
            Console.WriteLine("Initiated transcript annotator with {0} items from {1}", trNameToDescription.Count, annotationPath);
        }

        public void AnnotateFtomUCSCkgXref(ref Transcript t)
        {
            string d;
            if (t.Description == "" && trNameToDescription.TryGetValue(t.Name, out d))
                t.Description = d;
        }
    }

    class GeneAssociationAnnotator
    {
        private string goaFilepattern = "gene_association*";
        private Dictionary<string, string> geneNameToGOTerms = new Dictionary<string, string>();

        public GeneAssociationAnnotator(StrtGenome genome)
        {
            string[] annotationPaths = Directory.GetFiles(genome.GetOriginalGenomeFolder(), goaFilepattern);
            foreach (string path in annotationPaths)
            {
                using (StreamReader r = path.OpenRead())
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.StartsWith("!"))
                            continue;
                        string[] fields = line.Split('\t');
                        string geneName = fields[2].Trim();
                        string term = fields[4].Trim();
                        if (geneNameToGOTerms.ContainsKey(geneName))
                            geneNameToGOTerms[geneName] += ";" + term;
                        else
                            geneNameToGOTerms[geneName] = term;
                    }
                }
                Console.WriteLine("Initiated transcript annotator with GO terms for {0} genes from {1}",
                                  geneNameToGOTerms.Count, path);
            }
        }

        public void AnnotateFromGeneAssociation(ref Transcript t)
        {
            string terms;
            if (geneNameToGOTerms.TryGetValue(t.GeneName, out terms))
            {
                foreach (string term in terms.Split(';'))
                {
                    t.TranscriptAnnotations.Add(new TranscriptAnnotation(null, t.TranscriptID.Value, term, "", ""));
                }
            }
        }
    }
}
