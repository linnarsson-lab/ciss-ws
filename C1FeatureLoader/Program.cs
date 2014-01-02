using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Strt;

namespace C1
{
    class C1FeatureLoader
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME [-i]\nwhere genome is e.g. 'mm10_aUCSC' or 'hg19_sENSE'");
                Console.WriteLine("Without -i, 5'-extensions are made and an update refFlat file is written, but no DB inserts made.");
                return;
            }
            StrtGenome genome = StrtGenome.GetGenome(args[0]);
            bool doInsert = (args.Length >= 2 && args[1] == "-i");
            Props.props.DirectionalReads = true;
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(genome);
            Console.WriteLine("Building transcript models...");
            int nModels = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...{0} models constructed.", nModels);
            if (Props.props.GeneFeature5PrimeExtension > 0)
            {
                Extend5Primes(annotationReader);
                Write5PrimeExtendedRefFlatFile(genome, annotationReader);
            }
            annotationReader.AddCtrlGeneModels();
            if (doInsert)
                InsertIntoC1Db(genome, annotationReader);
        }

        private static void InsertIntoC1Db(StrtGenome genome, AnnotationReader annotationReader)
        {
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            C1DB db = new C1DB();
            Console.WriteLine("Inserting transcriptome metadata into database...");
            Transcriptome tt = new Transcriptome(null, genome.BuildVarAnnot, genome.Abbrev, genome.Annotation,
                                                 annotationReader.VisitedAnnotationPaths,
                                                 "", DateTime.Now, "1", DateTime.MinValue, null);
            db.InsertTranscriptome(tt);
            Console.WriteLine("Inserting transcripts into database...");
            int n = 0;
            foreach (ExtendedGeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.TranscriptType == "" ? "gene" : gf.TranscriptType;
                Transcript t = AnnotationReader.CreateNewTranscriptFromExtendedGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = tt.TranscriptomeID.Value;
                t.ExprBlobIdx = n;
                db.InsertTranscript(t);
                n++;
            }
            Console.WriteLine("...totally {0} transcript models inserted.", n);
        }

        private static void Extend5Primes(AnnotationReader annotationReader)
        {
            Console.WriteLine("Extending 5' ends with max {0} bases...", Props.props.GeneFeature5PrimeExtension);
            GeneFeature5PrimeModifier m = new GeneFeature5PrimeModifier();
            annotationReader.AdjustGeneFeatures(m);
            Console.WriteLine("...{0} models had their 5' end extended, {1} with the maximal {2} bps.",
                               m.nExtended, m.nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
        }

        private static void Write5PrimeExtendedRefFlatFile(StrtGenome genome, AnnotationReader annotationReader)
        {
            string refFilename = Path.Combine(genome.GetOriginalGenomeFolder(), genome.BuildVarAnnot + "_C1DB5PrimeExtended_refFlat.txt");
            StreamWriter writer = new StreamWriter(refFilename);
            foreach (ExtendedGeneFeature gf in annotationReader.IterChrSortedGeneModels())
                writer.WriteLine(gf.ToRefFlatString());
            writer.Close();
            Console.WriteLine("...wrote updated gene models without CTRLs to {0}.", refFilename);
        }

    }
}
