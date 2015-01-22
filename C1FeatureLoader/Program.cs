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
                Console.WriteLine("This program reads gene definition files, builds transcript models, annotates them using various annotation files,");
                Console.WriteLine("and inserts the transcript models together with CTRL spike data and repeat type annotations into the cells10k database.");
                Console.WriteLine("The raw gene models are read from 'refFlat.txt' (for UCSC) or 'XXXX_mart_export.txt' (for ENSE/VEGA) files in the genome folders.");
                Console.WriteLine("5' ends are extended according to SilverBulletConfig property 'GeneFeature5PrimeExtension' before inserting.");
                Console.WriteLine("Note that during analysis these extensions are made on-the-fly to the transcripts read from annotation files for non-C1 samples,");
                Console.WriteLine("but for C1 samples the final transcript models including extensions are read from the database and used directly.");
                Console.WriteLine("This should be the same models, but asserts that the database gene models exactly reflect the database expression values.");
                Console.WriteLine("The input gene definition and annotation files can be downloaded with the 'SB.exe download' command.");
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME [-f ANNOTATIONFILE] [-i]\nwhere genome is e.g. 'mm10_aUCSC' or 'hg19_sENSE'");
                Console.WriteLine("Without -i, an updated refFlat flat of the 5'-extended genes is written, but no DB inserts are made.");
                Console.WriteLine("Use -u ID to only update/replace the transcript annotations of the transcriptome with database id ID.");
                return;
            }
            StrtGenome genome = StrtGenome.GetGenome(args[0]);
            string doInsert = "";
            int updateTomeID = -1;
            string annotFilePath = "";
            int i = 1;
            while (i < args.Length)
            {
                if (args[i] == "-i")
                    doInsert = "i";
                else if (args[i] == "-f")
                    annotFilePath = args[++i];
                else if (args[i] == "-u")
                {
                    doInsert = "u";
                    updateTomeID = int.Parse(args[++i]);
                }
                i++;
            }
            Props.props.DirectionalReads = true;
            string strtAnnotFolder = genome.GetStrtAnnotFolder();
            string annotFileCopyPath = Path.Combine(strtAnnotFolder, Path.GetFileName(annotFilePath));
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(genome, annotFileCopyPath);
            Console.WriteLine("Building transcript models from " + annotFilePath + "...");
            int nModels = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...{0} models constructed.", nModels);
            if (Props.props.GeneFeature5PrimeExtension > 0)
            {
                Extend5Primes(annotationReader);
                Write5PrimeExtendedRefFlatFile(genome, annotationReader);
            }
            foreach (string commonChrId in Props.props.CommonChrIds)
                annotationReader.AddCommonGeneModels(commonChrId);
            if (doInsert == "i")
            {
                int transcriptomeID = InsertTranscriptomeIntoC1Db(genome, annotationReader);
                InsertGenesIntoC1Db(transcriptomeID, genome, annotationReader);
                InsertRepeatsIntoC1Db(transcriptomeID, genome);
            }
            else if (doInsert == "u")
                UpdateTranscriptAnnotations(updateTomeID, genome, annotationReader);
        }

        private static int InsertTranscriptomeIntoC1Db(StrtGenome genome, AnnotationReader annotationReader)
        {
            C1DB db = new C1DB();
            Console.WriteLine("Inserting transcriptome metadata into database...");
            Transcriptome tt = new Transcriptome(null, genome.BuildVarAnnot, genome.Abbrev, genome.Annotation,
                                                 annotationReader.VisitedAnnotationPaths,
                                                 "", genome.AnnotationDateTime, "1", DateTime.MinValue, null);
            db.InsertTranscriptome(tt);
            return tt.TranscriptomeID.Value;
        }

        private static void InsertGenesIntoC1Db(int transcriptomeID, StrtGenome genome, AnnotationReader annotationReader)
        {
            C1DB db = new C1DB();
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            Console.WriteLine("Inserting transcripts into database...");
            int n = 0;
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.GeneType == "" ? "gene" : gf.GeneType;
                Transcript t = AnnotationReader.CreateNewTranscriptFromGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = transcriptomeID;
                t.ExprBlobIdx = n;
                db.InsertTranscript(t);
                n++;
            }
            Console.WriteLine("...totally {0} transcript models inserted.", n);
        }

        private static void UpdateTranscriptAnnotations(int transcriptomeID, StrtGenome genome, AnnotationReader annotationReader)
        {
            C1DB db = new C1DB();
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            Console.WriteLine("Updating transcript annotations for transcriptome " + transcriptomeID + " in database...");
            int n = 0;
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                string type = gf.GeneType == "" ? "gene" : gf.GeneType;
                Transcript t = AnnotationReader.CreateNewTranscriptFromGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = transcriptomeID;
                if (db.UpdateTranscriptAnnotations(t)) n++;
            }
            Console.WriteLine("...updated annotations for {0} uniquely identified transcript models.", n);
        }

        /// <summary>
        /// Repeat names and total lengths are stored in cell10k db, but not the indivudal regions.
        /// These are read directly from the same repeat mask files during analysis.
        /// </summary>
        /// <param name="genome"></param>
        private static void InsertRepeatsIntoC1Db(int transcriptomeID, StrtGenome genome)
        {
            Console.WriteLine("Inserting repeat types into database...");
            C1DB db = new C1DB();
            Dictionary<string, int> repeatTypeLengths = new Dictionary<string, int>();
            string[] rmskFiles = PathHandler.GetRepeatMaskFiles(genome);
            foreach (string rmskPath in rmskFiles)
            {
                int nRepeatFeatures = 0;
                foreach (RmskData rd in RmskData.IterRmskFile(rmskPath))
                {
                        if (!repeatTypeLengths.ContainsKey(rd.Name))
                        {
                            nRepeatFeatures++;
                            repeatTypeLengths[rd.Name] = 0;
                        }
                        repeatTypeLengths[rd.Name] +=  rd.Length;
                }
                foreach (KeyValuePair<string, int> p in repeatTypeLengths)
                {
                    Transcript rt = new Transcript(p.Key, "repeat", p.Key, p.Key, "", "", "", 0, 0, p.Value, '0', 0, "0,", "0,");
                    rt.TranscriptomeID = transcriptomeID;
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
