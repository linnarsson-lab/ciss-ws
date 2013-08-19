using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Strt;

namespace C1
{
    class C1FeatureLoader
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME\nwhere genome is e.g. 'mm10_aUCSC' or 'hg19_sENSE'");
                return;
            }
            StrtGenome genome = StrtGenome.GetGenome(args[0]);
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            string organism = genome.Abbrev;
            string trName = genome.BuildVarAnnot;
            Props.props.DirectionalReads = true;
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(genome);
            Console.WriteLine("Building transcript models...");
            int nModels = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...{0} models constructed.", nModels);
            if (Props.props.GeneFeature5PrimeExtension > 0)
                Extend5Primes(annotationReader);
            annotationReader.AddCtrlGeneModels();
            Transcriptome tt = new Transcriptome(null, genome.BuildVarAnnot, genome.Abbrev, genome.Annotation, 
                                                 annotationReader.VisitedAnnotationPaths,
                                                 "", DateTime.Now, "1", DateTime.MinValue, null);
            Console.WriteLine("Inserting transcriptome metadata into database...");
            C1DB db = new C1DB();
            db.InsertTranscriptome(tt);
            Console.WriteLine("Inserting transcripts into database...");
            int n = 0;
            foreach (ExtendedGeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.TranscriptType == "" ? "gene" : gf.TranscriptType;
                Transcript t = AnnotationReader.TranscriptFromExtendedGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = tt.TranscriptomeID.Value;
                db.InsertTranscript(t);
                n++;
            }
            Console.WriteLine("...totally {0} transcript models.", n);
        }

        private static void Extend5Primes(AnnotationReader annotationReader)
        {
            Console.WriteLine("Extending 5' ends...");
            GeneFeature5PrimeModifier m = new GeneFeature5PrimeModifier();
            annotationReader.AdjustGeneFeatures(m);
            Console.WriteLine("...{0} models had their 5' end extended, {1} with the maximal {2} bps.",
                               m.nExtended, m.nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
        }
    }
}
