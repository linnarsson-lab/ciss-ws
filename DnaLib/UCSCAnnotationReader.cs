﻿using System;
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

        public override void BuildGeneModelsByChr()
        {
            ClearGenes();
            string refFlatPath = MakeFullAnnotationPath("refFlat.txt", true);
            VisitedAnnotationPaths = refFlatPath;
            int n = 0;
            foreach (GeneFeature gf in AnnotationReader.IterAnnotationFile(refFlatPath))
            {
                    AddGeneModel(gf);
                    n++;
            }
            string varTxt = (genome.GeneVariants) ? " genes and" : " main gene";
            Console.WriteLine("Read {0}{1} variants from {2}", n, varTxt, refFlatPath);
        }

    }
}
