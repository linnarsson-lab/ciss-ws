using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace C1
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME\nwhere genome is e.g. 'mm10', 'mm10_aUCSC', 'hg19_sENSE'");
                return;
            }
            StrtGenome g = StrtGenome.GetGenome(args[0]);
            string organism = g.Abbrev;
            string trName = g.BuildVarAnnot;
            Props.props.DirectionalReads = true;
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(g);
            Console.WriteLine("Building gene models...");
            annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("Extending 5' ends...");
            annotationReader.Extend5PrimeEnds();
            Transcriptome tt = new Transcriptome(null, g.BuildVarAnnot, g.Abbrev, g.Annotation, annotationReader.VisitedAnnotationPaths,
                                                 "", DateTime.Now, "1", DateTime.MinValue, null);
            Console.WriteLine("Inserting transcriptome metadata into database...");
            C1DB db = new C1DB();
            db.InsertTranscriptome(tt);
            Console.WriteLine("Inserting transcripts into database...");
            int n = 0;
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.TranscriptType == "" ? "gene" : gf.TranscriptType;
                Transcript t = new Transcript(null, tt.TranscriptomeID.Value, gf.TranscriptID, type, gf.NonVariantName, "",
                                      gf.Chr, gf.Start, gf.End, gf.GetTranscriptLength(), gf.Strand,
                                      gf.Extension5Prime, gf.ExonStartsString, gf.ExonEndsString);
                db.InsertTranscript(t);
                n++;
            }
            Console.WriteLine("...totally {0} transcripts.", n);
        }
    }
}
