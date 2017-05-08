using System;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace Linnarsson.C1
{
    class C1FeatureLoader
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            if (args.Length == 0 || args[0] == "--help")
            {
                Console.WriteLine("This program reads gene definition files, builds transcript models, annotates them using various annotation files,");
                Console.WriteLine("and inserts the transcript models together with CTRL spike data and repeat type annotations into the database.");
                Console.WriteLine("The raw gene models are read from 'refFlat.txt' (for UCSC) or 'XXXX_mart_export.txt' (for ENSE/VEGA) files in the genome folders.");
                Console.WriteLine("5' ends are extended according to SilverBulletConfig property 'GeneFeature5PrimeExtension' before inserting.");
                Console.WriteLine("Note that during analysis these extensions are made on-the-fly to the transcripts read from annotation files for non-C1 samples,");
                Console.WriteLine("but for C1 samples the final transcript models including extensions are read from the database and used directly.");
                Console.WriteLine("This should be the same models, but asserts that the database gene models exactly reflect the database expression values.");
                Console.WriteLine("The input gene definition and annotation files can be downloaded with the 'SB.exe download' command.");
                Console.WriteLine("Usage:\nmono C1FeatureLoader.exe GENOME [-f ANNOTATIONFILE] [-l | -i | -u ID]\nwhere genome is e.g. 'mm10_aUCSC' or 'hg19_sENSE'");
                Console.WriteLine("Without -i, an updated refFlat flat of the 5'-extended genes is written, but no DB inserts are made.");
                Console.WriteLine("Use -u to only update/replace the transcript annotations of the transcriptome with database id ID.");
                Console.WriteLine("Use -l to only insert the chromosome length for the specified genome");
                return;
            }
            string doInsert = "", genomeName = "", annotFilePath = "";
            int updateTomeID = -1, i = 1;
            while (i < args.Length)
            {
                if (args[i] == "-i" || args[i] == "-l")
                    doInsert = args[i];
                else if (args[i] == "-f")
                    annotFilePath = args[++i];
                else if (args[i] == "-u")
                {
                    doInsert = args[i];
                    updateTomeID = int.Parse(args[++i]);
                }
                else
                    genomeName = args[i];
                i++;
            }
            StrtGenome genome = StrtGenome.GetGenome(genomeName);
            if (doInsert == "-l")
            {
                InsertWigChroms(genome, genomeName);
                return;
            }
            Props.props.DirectionalReads = true;
            AnnotationReader annotationReader = BuildAndExtendTrModels(genome, ref annotFilePath);
            if (doInsert == "i")
                InsertAll(genome, annotationReader);
            else if (doInsert == "u")
                UpdateTranscriptAnnotations(updateTomeID, genome, annotationReader);
        }

        private static AnnotationReader BuildAndExtendTrModels(StrtGenome genome, ref string annotFilePath)
        {
            if (annotFilePath == "") // Use annot file (e.g. refFlat.txt) copied into the strt (e.g. UCSCxxxxxx) subfolder during build
                annotFilePath = Path.Combine(genome.GetStrtAnnotFolder(), AnnotationReader.GetDefaultAnnotFilename(genome));
            Console.WriteLine("Building transcript models from " + annotFilePath + "...");
            AnnotationReader annotationReader = AnnotationReader.GetAnnotationReader(genome, annotFilePath);
            int nModels = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...{0} models constructed.", nModels);
            if (Props.props.GeneFeature5PrimeExtension > 0)
            {
                Extend5Primes(annotationReader);
                Write5PrimeExtendedRefFlatFile(genome, annotationReader);
            }
            foreach (string commonChrId in Props.props.CommonChrIds)
                annotationReader.AddCommonGeneModels(commonChrId);
            return annotationReader;
        }

        private static void InsertWigChroms(StrtGenome genome, string buildName)
        {
            IExpressionDB db = DBFactory.GetExpressionDB();
            Transcriptome t = db.GetTranscriptome(buildName);
            if (t != null)
                InsertWigChromsIntoDb(t.TranscriptomeID.Value, genome);
            else
                Console.WriteLine("Error: Can not find a transcriptome with Build=" + buildName);
        }

        private static void InsertAll(StrtGenome genome, AnnotationReader annotationReader)
        {
            int transcriptomeID = InsertTranscriptomeIntoDb(genome, annotationReader);
            InsertWigChromsIntoDb(transcriptomeID, genome);
            InsertGenesIntoDb(transcriptomeID, genome, annotationReader);
            InsertRepeatsIntoDb(transcriptomeID, genome);
        }

        private static int InsertTranscriptomeIntoDb(StrtGenome genome, AnnotationReader annotationReader)
        {
            IExpressionDB db = DBFactory.GetExpressionDB();
            Console.WriteLine("Inserting transcriptome metadata into database...");
            Transcriptome tt = new Transcriptome(null, genome.BuildVarAnnot, genome.Abbrev, genome.Annotation,
                                                 annotationReader.VisitedAnnotationPaths,
                                                 "", genome.AnnotationDateTime, "1", DateTime.MinValue, null);
            db.InsertTranscriptome(tt);
            return tt.TranscriptomeID.Value;
        }

        private static void InsertGenesIntoDb(int transcriptomeID, StrtGenome genome, AnnotationReader annotationReader)
        {
            IExpressionDB db = DBFactory.GetExpressionDB();
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            Console.WriteLine("Inserting transcripts into database...");
            int n = 0;
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
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
            IExpressionDB db = DBFactory.GetExpressionDB();
            TranscriptAnnotator ta = new TranscriptAnnotator(genome);
            Console.WriteLine("Updating transcript annotations for transcriptome " + transcriptomeID + " in database...");
            int n = 0;
            foreach (GeneFeature gf in annotationReader.IterChrSortedGeneModels())
            {
                Transcript t = AnnotationReader.CreateNewTranscriptFromGeneFeature(gf);
                ta.Annotate(ref t);
                t.TranscriptomeID = transcriptomeID;
                if (db.UpdateTranscriptAnnotations(t)) n++;
            }
            Console.WriteLine("...updated annotations for {0} uniquely identified transcript models.", n);
        }

        private static void InsertWigChromsIntoDb(int transcriptomeID, StrtGenome genome)
        {
            Console.WriteLine("Inserting chromosome lengths into database...");
            IExpressionDB db = DBFactory.GetExpressionDB();
            int genomePos = 0;
            foreach (KeyValuePair<string, int> p in genome.GetChromosomeLengths())
            {
                db.InsertChromosomePos(transcriptomeID, p.Key, genomePos, genomePos + p.Value);
                genomePos += p.Value;
            }
        }

        /// <summary>
        /// Repeat names and total lengths are stored in cell10k db, but not the indivudal regions.
        /// These are read directly from the same repeat mask files during analysis.
        /// </summary>
        /// <param name="genome"></param>
        private static void InsertRepeatsIntoDb(int transcriptomeID, StrtGenome genome)
        {
            Console.WriteLine("Inserting repeat types into database...");
            IExpressionDB db = DBFactory.GetExpressionDB();
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
