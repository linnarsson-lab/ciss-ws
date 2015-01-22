using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Parser for UCSC refFlat.txt files
    /// </summary>
    public class RefFlatAnnotationReader : AnnotationReader
    {
        public RefFlatAnnotationReader(StrtGenome genome, string annotationFile)
            : base(genome, annotationFile)
        {
        }

        protected override int ReadGenes()
        {
            AddVisitedAnnotationPaths(annotationPath);
            int n = 0, nCreated = 0;
            foreach (GeneFeature gf in AnnotationReader.IterRefFlatFile(annotationPath))
            {
                SetTranscriptType(gf);
                bool newModel = AddGeneModel(gf);
                if (newModel) nCreated++;
                n++;
            }
            Console.WriteLine("Read {0} genes and variants from {1}", n, annotationPath);
            Console.WriteLine("...constructed {0} {1} gene models.", nCreated, (genome.GeneVariants ? "variant" : "main"));
            return nCreated;
        }

    }
}
