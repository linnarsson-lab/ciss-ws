﻿using System;
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
        {}

        public override int BuildGeneModelsByChr()
        {
            ClearGenes();
            string refFlatPath = MakeFullAnnotationPath(annotationFile, true);
            VisitedAnnotationPaths = refFlatPath;
            int n = 0, nCreated = 0;
            foreach (ExtendedGeneFeature gf in AnnotationReader.IterRefFlatFile(refFlatPath))
            {
                if (gf.TranscriptType == "") gf.TranscriptType = "transcript";
                bool newModel = AddGeneModel(gf);
                if (newModel) nCreated++;
                n++;
            }
            string varTxt = (genome.GeneVariants) ? " genes and" : " main gene";
            Console.WriteLine("Read {0}{1} variants from {2}", n, varTxt, refFlatPath);
            return nCreated;
        }

    }
}
