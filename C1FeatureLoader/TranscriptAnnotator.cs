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

}
