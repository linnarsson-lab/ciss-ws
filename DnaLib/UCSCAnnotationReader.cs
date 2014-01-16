using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class RefFlatAnnotationReader : AnnotationReader
    {
        public RefFlatAnnotationReader(StrtGenome genome, string annotationFile)
            : base(genome, annotationFile)
        {
        }

        protected override int ReadGenes()
        {
            string refFlatPath = MakeFullAnnotationPath(annotationFile, true);
            VisitedAnnotationPaths = refFlatPath;
            int n = 0, nCreated = 0;
            foreach (GeneFeature gf in AnnotationReader.IterRefFlatFile(refFlatPath))
            {
                SetTranscriptType(gf);
                bool newModel = AddGeneModel(gf);
                if (newModel) nCreated++;
                n++;
            }
            Console.WriteLine("Read {0} genes and variants from {1}", n, refFlatPath);
            Console.WriteLine("...constructed {0} {1} gene models.", nCreated, (genome.GeneVariants ? "variant" : "main"));
            return nCreated;
        }

    }
}
