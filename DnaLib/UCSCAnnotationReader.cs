using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class UCSCAnnotationReader : AnnotationReader
    {
        public UCSCAnnotationReader(StrtGenome genome)
            : base(genome)
        {}

        public override int BuildGeneModelsByChr()
        {
            ClearGenes();
            string refFlatPath = MakeFullAnnotationPath("refFlat.txt", true);
            VisitedAnnotationPaths = refFlatPath;
            int n = 0, nCreated = 0;
            foreach (ExtendedGeneFeature gf in AnnotationReader.IterAnnotationFile(refFlatPath))
            {
                if (AddGeneModel(gf)) nCreated++;
                n++;
            }
            string varTxt = (genome.GeneVariants) ? " genes and" : " main gene";
            Console.WriteLine("Read {0}{1} variants from {2}", n, varTxt, refFlatPath);
            return nCreated;
        }

    }
}
