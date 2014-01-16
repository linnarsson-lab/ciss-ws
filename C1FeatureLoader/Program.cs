using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace C1
{
    class C1FeatureLoader
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME [-f ANNOTATIONFILE] [-i]\nwhere genome is e.g. 'mm10_aUCSC' or 'hg19_sENSE'");
                Console.WriteLine("Without -i, 5'-extensions are made and an update refFlat file is written, but no DB inserts made.");
                Console.WriteLine("Use -f to specify a non-standard (mart or refFlat style) annotation file.");
                return;
            }
            StrtGenome genome = StrtGenome.GetGenome(args[0]);
            bool doInsert = false;
            string annotationFile = "";
            int i = 1;
            while (i < args.Length)
            {
                if (args[i] == "-i")
                    doInsert = true;
                if (args[i] == "-f")
                    annotationFile = args[++i];
                i++;
            }
            Props.props.DirectionalReads = true;
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(genome, annotationFile);
            Console.WriteLine("Building transcript models from...", annotationFile);
            int nModels = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...{0} models constructed.", nModels);
            if (Props.props.GeneFeature5PrimeExtension > 0)
            {
                Extend5Primes(annotationReader);
                Write5PrimeExtendedRefFlatFile(genome, annotationReader);
            }
            annotationReader.AddCtrlGeneModels();
            if (doInsert)
            {
                InsertIntoC1Db(genome, annotationReader);
                InsertRepeatsIntoC1Db(genome);
            }
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
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.GeneType == "" ? "gene" : gf.GeneType;
                Transcript t = AnnotationReader.CreateNewTranscriptFromGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = tt.TranscriptomeID.Value;
                t.ExprBlobIdx = n;
                db.InsertTranscript(t);
                n++;
            }
            Console.WriteLine("...totally {0} transcript models inserted.", n);
        }

        private static void InsertRepeatsIntoC1Db(StrtGenome genome)
        {
            Console.WriteLine("Inserting repeat types into database...");
            C1DB db = new C1DB();
            Dictionary<string, int> repeatTypeLengths = new Dictionary<string, int>();
            string[] rmskFiles = PathHandler.GetRepeatMaskFiles(genome);
            foreach (string rmskPath in rmskFiles)
            {
                int nRepeatFeatures = 0;
                string[] record;
                int fileTypeOffset = 0;
                if (rmskPath.EndsWith("out"))
                    fileTypeOffset = -1;
                using (StreamReader reader = rmskPath.OpenRead())
                {
                    string line = reader.ReadLine();
                    while (line == "" || !char.IsDigit(line.Trim()[0]))
                        line = reader.ReadLine();
                    while (line != null)
                    {
                        record = line.Split('\t');
                        int start = int.Parse(record[6 + fileTypeOffset]);
                        int end = int.Parse(record[7 + fileTypeOffset]);
                        int len = 1 + end - start;
                        string name = record[10 + fileTypeOffset];
                        if (!repeatTypeLengths.ContainsKey(name))
                        {
                            nRepeatFeatures++;
                            repeatTypeLengths[name] = 0;
                        }
                        repeatTypeLengths[name] += len;
                        line = reader.ReadLine();
                    }
                }
                foreach (KeyValuePair<string, int> p in repeatTypeLengths)
                {
                    Transcript rt = new Transcript(p.Key, "repeat", p.Key, p.Key, "", "", "", 0, 0, p.Value, '0', 0, "0,", "0,");
                    db.InsertTranscript(rt);
                }
                Console.WriteLine("... totally {0} repeat types inserted.", nRepeatFeatures);
            }
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
            foreach (GeneFeature egf in annotationReader.IterChrSortedGeneModels())
                writer.WriteLine(egf.ToRefFlatString());
            writer.Close();
            Console.WriteLine("...wrote updated gene models without CTRLs to {0}.", refFilename);
        }

    }
}
