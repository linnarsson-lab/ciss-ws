using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.C1Model;

namespace C1FeatureLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Length > 2)
            {
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME\nwhere genome is e.g. 'mm10', 'mm10_aUCSC', 'hg19_sENSE'");
                return;
            }
            StrtGenome g = StrtGenome.GetGenome(args[1]);
            string organism = g.Abbrev;
            string trName = g.BuildVarAnnot;
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(g);
            annotationReader.BuildGeneModelsByChr();
            Transcriptome tt = new Transcriptome(null, g.BuildVarAnnot, g.Abbrev, g.Annotation, annotationReader.VisitedAnnotationPaths,
                                                 "", DateTime.Now, "1", DateTime.MinValue, null);
            new C1DB().InsertTranscriptome(tt);
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                Transcript t = new Transcript(null, tt.TranscriptomeID.Value, gf.TranscriptID, gf.TranscriptType, gf.NonVariantName, "",
                                      gf.Chr, gf.Start, gf.End, gf.GetTranscriptLength(), gf.Strand,
                                      gf.Extension5Prime, gf.ExonStartsString, gf.ExonEndsString);

            }
        }
    }
}
