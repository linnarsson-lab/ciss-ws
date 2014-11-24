﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using C1;

namespace Linnarsson.Dna
{
    public class AnnotationFileException : ApplicationException
    {
        public AnnotationFileException(string msg) : base(msg)
        { }
    }

    /// <summary>
    /// Methods to read gene annotation files, both original RefFlat and MART files, but also STRT-generated RefFlat-style files
    /// </summary>
    public abstract class AnnotationReader
    {
        protected static string[] validXrefGeneTypes = new string[] {"pseudogene", "antisense RNA", "RNase MRP RNA", "microRNA",
                                                            "RNase P RNA", "small cytoplasmic RNA", "small nuclear RNA",
                                                            "small nucleolar RNA", "telomerase RNA", "transfer RNA", "non-coding RNA", "mRNA"};

        public static AnnotationReader GetAnnotationReader(StrtGenome genome)
        {
            return GetAnnotationReader(genome, "");
        }
        /// <summary>
        /// If annotationFile is empty, replace with default filename for genome
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="annotationFile"></param>
        /// <returns></returns>
        public static string GetAnnotationFile(StrtGenome genome, string annotationFile)
        {
            if (annotationFile != null && annotationFile != "")
                return annotationFile;
            if (genome.Annotation == "UCSC" || genome.Annotation == "RFSQ")
                return "refFlat.txt";
            if (genome.Annotation == "UALL")
                return "knownGene.txt";
            if (genome.Annotation == "GENC")
                return Path.GetFileName(Directory.GetFiles(genome.GetOriginalGenomeFolder(), "wgEncodeGencodeCompV*")[0]);
            return genome.Annotation + "_mart_export.txt";
        }

        /// <summary>
        /// Returns a proper AnnotationReader for the annotationFile by parsing its name. If it is empty,
        /// uses the default annotationFile of the genome
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="annotationFile"></param>
        /// <returns></returns>
        public static AnnotationReader GetAnnotationReader(StrtGenome genome, string annotationFile)
        {
            annotationFile = GetAnnotationFile(genome, annotationFile);
            if (annotationFile.Contains("refFlat"))
                return new RefFlatAnnotationReader(genome, annotationFile);
            if (annotationFile.Contains("knownGene"))
                return new KnownGeneAnnotationReader(genome, annotationFile);
            if (annotationFile.Contains("wgEncodeGencodeCompV"))
                return new GencodeAnnotationReader(genome, annotationFile);
            return new BioMartAnnotationReader(genome, annotationFile);
        }

        public AnnotationReader(StrtGenome genome, string annotationFile)
        {
            this.genome = genome;
            this.annotationFile = annotationFile;
        }

        /// <summary>
        /// Read external gene definition files (UCSC refFlat.txt, VEGA mart files...)
        /// and construct gene models for single or variant annotations
        /// </summary>
        /// <returns>Number of gene models constructed</returns>
        public virtual int BuildGeneModelsByChr()
        {
            ClearGenes();
            int nCreated = ReadGenes();
            if (Props.props.AddRefFlatToNonRefSeqBuilds)
                nCreated += AddRefFlatGenes();
            //FuseNearIdenticalMainGenes();
            return nCreated;
        }
        /// <summary>
        /// Read and setup gene models from the file defined by the particular subclass. Do not add extra genes from e.g. refFlat.
        /// </summary>
        /// <returns></returns>
        protected abstract int ReadGenes();

        private Dictionary<string, string> kgXRefTrIdToType;

        protected StrtGenome genome;
        protected string annotationFile { get; private set; }

        protected Dictionary<string, List<GeneFeature>> locNameToGenes; // "name_p" / "name_loc" => genes. Used for all variant building
        protected Dictionary<string, GeneFeature> nameToGene; // "name_pN" / "name_locN" => gene. Used for main variant building
        protected Dictionary<string, List<GeneFeature>> genesByChr;
        protected int pseudogeneCount = 0;

        public string VisitedAnnotationPaths = "";
        public int PseudogeneCount { get { return pseudogeneCount; } }
        public int ChrCount { get { return genesByChr.Count; } }
        public List<string> ChrNames { get { return genesByChr.Keys.ToList(); } }

        public int GeneCount(string chrId)
        {
            return genesByChr.ContainsKey(chrId)? genesByChr[chrId].Count : 0;
        }

        /// <summary>
        /// Iterate all gene models, order by increasing start position
        /// </summary>
        /// <returns></returns>
        public IEnumerable<GeneFeature> IterChrSortedGeneModels()
        {
            foreach (string chrId in genesByChr.Keys)
            {
                List<GeneFeature> chrGfs = genesByChr[chrId];
                chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
                foreach (GeneFeature gf in chrGfs)
                    yield return gf;
            }
        }

        protected string ValidateKgXrefType(string typeField)
        {
            foreach (string type in validXrefGeneTypes)
            {
                if (typeField.Contains(type))
                    return type;
            }
            return "";
        }

        private void ReadBackupTypeAnnontations(StrtGenome genome)
        {
            kgXRefTrIdToType = new Dictionary<string, string>();
            string xrefPath = PathHandler.ExistsOrGz(Path.Combine(genome.GetOriginalGenomeFolder(), "kgXref.txt"));
            if (xrefPath == null)
                return;
            string line;
            using (StreamReader reader = xrefPath.OpenRead())
            {
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    string trId = fields[1].Trim();
                    string type = ValidateKgXrefType(fields[7]);
                    if (type != "")
                        kgXRefTrIdToType[trId] = type;
                }
            }
            Console.WriteLine("Read {0} gene type annotations from {1}.", kgXRefTrIdToType.Count, xrefPath);
        }

        /// <summary>
        /// Try to find out what type of transcript it is and assign to gf
        /// </summary>
        /// <param name="gf"></param>
        protected void SetTranscriptType(GeneFeature gf)
        {
            if (kgXRefTrIdToType == null)
                ReadBackupTypeAnnontations(genome);
            if (gf.GeneType == "")
            {
                if (!kgXRefTrIdToType.TryGetValue(gf.GeneMetadata, out gf.GeneType))
                    gf.GeneType = "gene";

            }
        }

        /// <summary>
        /// Add gene models defined in the refFlat.txt file of the original genome folder
        /// </summary>
        /// <returns></returns>
        protected int AddRefFlatGenes()
        {
            int nTotal = 0, nMerged = 0, nCreated = 0, nRandom = 0, nUpdated = 0;
            string refFlatPath = MakeFullAnnotationPath("refFlat.txt", false);
            if (File.Exists(refFlatPath))
            {
                VisitedAnnotationPaths += ";" + refFlatPath;
                foreach (GeneFeature gf in AnnotationReader.IterRefFlatFile(refFlatPath))
                {
                    SetTranscriptType(gf);
                    nTotal++;
                    if (FusedWithOverlapping(gf))
                        nMerged++;
                    else
                    {
                        if (AddGeneModel(gf)) nCreated++;
                        else nUpdated++;
                    }
                }
                Console.WriteLine("Read {0} genes and variants from {1}", nTotal, refFlatPath);
                Console.WriteLine("...skipped {0} that are not properly mapped ('random' chromosomes)", nRandom);
                Console.WriteLine("...added {0} new genes, merged {1} and silently updated exons of {2}.", nCreated, nMerged, nUpdated);
            }
            return nCreated;
        }

        /// <summary>
        /// Check if gf totally overlaps with an already existing transcript. If so, fuse gf to the old one, 
        /// and annotate it appropriately in the GeneMetadata
        /// </summary>
        /// <param name="gf"></param>
        /// <returns></returns>
        protected bool FusedWithOverlapping(GeneFeature gf)
        {
            try
            {
                foreach (GeneFeature oldGf in genesByChr[gf.Chr])
                    if (oldGf.IsSameTranscript(gf, 5, 100))
                    {
                        if (!oldGf.Name.Contains(gf.Name) && !oldGf.GeneMetadata.Contains(gf.Name))
                            oldGf.GeneMetadata = oldGf.GeneMetadata + "/" + gf.Name;
                        oldGf.Start = Math.Min(oldGf.Start, gf.Start);
                        oldGf.End = Math.Max(oldGf.End, gf.End);
                        return true;
                    }
            }
            catch (KeyNotFoundException) { }
            return false;
        }

        /// <summary>
        /// Iterate the genes features on specified chromosome, order by increasing start position
        /// </summary>
        /// <param name="chrId">Specified chromosome</param>
        /// <returns></returns>
        public IEnumerable<GeneFeature> IterChrSortedGeneModels(string chrId)
        {
            if (!genesByChr.ContainsKey(chrId)) yield break;
            List<GeneFeature> chrGfs = genesByChr[chrId];
            chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
            foreach (GeneFeature gf in chrGfs)
                yield return gf;
        }

        /// <summary>
        /// Clear data before building gene models
        /// </summary>
        protected void ClearGenes()
        {
            locNameToGenes = new Dictionary<string, List<GeneFeature>>();
            nameToGene = new Dictionary<string, GeneFeature>();
            genesByChr = new Dictionary<string, List<GeneFeature>>();
            pseudogeneCount = 0;
        }
 
        /// <summary>
        /// Make a full annotation file path inot original genome folder, and assert the file exists
        /// </summary>
        /// <param name="annotationFilename"></param>
        /// <param name="checkExists"></param>
        /// <returns></returns>
        protected string MakeFullAnnotationPath(string annotationFilename, bool checkExists)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
            string annotationPath = Path.Combine(genomeFolder, annotationFilename);
            annotationPath = PathHandler.ExistsOrGz(annotationPath);
            if (checkExists && annotationPath == null)
                throw new FileNotFoundException(string.Format("Could not find {0} in {1}", annotationFilename, genomeFolder));
            return annotationPath;
        }

        /// <summary>
        /// Add a new or fuse (may happen for 'main' variant setup) with an existing gene model
        /// </summary>
        /// <param name="gf"></param>
        /// <returns>True if a new model was added</returns>
        protected bool AddGeneModel(GeneFeature gf)
        {
            if (!genesByChr.ContainsKey(gf.Chr))
                genesByChr[gf.Chr] = new List<GeneFeature>();
            bool createdNew;
            if (genome.GeneVariants)
                createdNew = AddToVariantGeneModels(gf);
            else
                createdNew = AddToSingleGeneModels(gf);
            if (createdNew && gf.IsPseudogeneType()) pseudogeneCount++;
            return createdNew;
        }

        /// <summary>
        /// Add a new gene model. If one exists with the same name, add appropriate suffixes telling the
        /// locus or variant of the gene.
        /// </summary>
        /// <param name="gf"></param>
        /// <returns></returns>
        private bool AddToVariantGeneModels(GeneFeature gf)
        {
            //Console.Write("{0}: chr{1}{2}: {3}-{4}", gf.Name, gf.Chr, gf.Strand, gf.Start, gf.End);
            string locIndicator = gf.IsPseudogeneType() ? GeneFeature.pseudoGeneIndicator : GeneFeature.altLocusIndicator;
            string lociPrefix = gf.Name + locIndicator;
            int maxLocNo = 0, maxVarNo = 1;
            List<GeneFeature> locNameGenes = null;
            if (locNameToGenes.TryGetValue(lociPrefix, out locNameGenes))
            {
                foreach (GeneFeature oldGf in locNameGenes)
                {
                    Match m = Regex.Match(oldGf.Name, locIndicator + "([0-9]+)");
                    int locNo = m.Success ? int.Parse(m.Groups[1].Value) : 1;
                    maxLocNo = Math.Max(maxLocNo, locNo);
                    if (oldGf.Chr == gf.Chr && oldGf.Strand == gf.Strand && oldGf.Overlaps(gf.Start, gf.End, 1))
                    { // We have a new variant of an already defined locus
                        string thisLocusPat = m.Success? lociPrefix + locNo.ToString() : gf.Name;
                        foreach (GeneFeature locGf in locNameGenes)
                        {
                            if (locGf.Name == thisLocusPat || locGf.Name.StartsWith(thisLocusPat + "_"))
                            {
                                int varIdx = locGf.Name.LastIndexOf(GeneFeature.variantIndicator);
                                if (varIdx == -1)
                                { // There is only one previous variant of this locus - add the variant 1 indicator to it
                                    //Console.WriteLine("Adding '_v1' to " + locGf.Name);
                                    locGf.Name += GeneFeature.variantIndicator + "1";
                                    break;
                                }
                                maxVarNo = Math.Max(maxVarNo, int.Parse(locGf.Name.Substring(varIdx + GeneFeature.variantIndicator.Length)));
                            }
                        }
                        int newVarNo = maxVarNo + 1;
                        gf.Name = (!m.Success)? string.Format("{0}{1}{2}", gf.Name, GeneFeature.variantIndicator, newVarNo) :
                                                          string.Format("{0}{1}{2}{3}", lociPrefix, locNo, GeneFeature.variantIndicator, newVarNo);
                        genesByChr[gf.Chr].Add(gf);
                        locNameToGenes[lociPrefix].Add(gf);
                        //Console.WriteLine(" Created {0}", gf.Name);
                        return true;
                    }
                }
            } // Now we create a new gene or a new locus for the gene
            bool creatingNewMainGene = (locNameGenes == null);
            int newLocNo = maxLocNo + 1;
            if (newLocNo == 2 && !gf.IsPseudogeneType())
            { // We will add the second locus for this gene - add "_loc1" extension to all variants of the first
                foreach (GeneFeature oldGf in locNameGenes)
                {
                    int vi = oldGf.Name.IndexOf(GeneFeature.variantIndicator);
                    oldGf.Name = (vi > 0) ? oldGf.Name.Insert(vi, locIndicator + "1") : oldGf.Name + locIndicator + "1";
                }
            }
            string newLocName = string.Format("{0}{1}", lociPrefix, newLocNo);
            if (newLocNo > 1 || gf.IsPseudogeneType())
                gf.Name = newLocName;
            if (creatingNewMainGene)
                locNameToGenes[lociPrefix] = new List<GeneFeature>();
            genesByChr[gf.Chr].Add(gf);
            locNameToGenes[lociPrefix].Add(gf);
            //Console.WriteLine(" Created {0}{1}", gf.Name, (creatingNewMainGene)? " and new locNameToGenes key " + lociPrefix : "");
            return true;
        }

        /// <summary>
        /// Fuse gene model gf with an overlapping existing, else register a new model
        /// </summary>
        /// <param name="gf"></param>
        /// <returns>True if a new gene was constructed</returns>
        private bool AddToSingleGeneModels(GeneFeature gf)
        {
            //Console.Write("{0}: chr{1}{2}: ", gf.Name, gf.Chr, gf.Strand);
            string locIndicator = gf.IsPseudogeneType() ? GeneFeature.pseudoGeneIndicator : GeneFeature.altLocusIndicator;
            int altLocNo = 1;
            string locName = string.Format("{0}{1}{2}", gf.Name, locIndicator, altLocNo);
            GeneFeature oldGf, saveGf = null;
            while (nameToGene.TryGetValue(locName, out oldGf))
            { // Pick up the first gene of this name (the plain name Key in nameToGene for the first is kept)
                if (oldGf.Chr == gf.Chr && oldGf.Strand == gf.Strand && oldGf.Overlaps(gf.Start, gf.End, 1))
                {
                    GeneFeature combinedGf = CreateExonUnion(oldGf, gf);
                    combinedGf.Name = oldGf.Name;
                    int idx = genesByChr[gf.Chr].IndexOf(oldGf);
                    genesByChr[gf.Chr][idx] = combinedGf;
                    nameToGene[oldGf.Name] = combinedGf;
                    nameToGene[locName] = combinedGf;
                    //Console.WriteLine("{0}-{1} Merged into {2}", gf.Start, gf.End, oldGf.Name);
                    return false;
                }
                locName = string.Format("{0}{1}{2}", gf.Name, locIndicator, ++altLocNo);
                saveGf = oldGf;
            }
            if (altLocNo == 2 && !gf.IsPseudogeneType())
            { // We will add the second locus for this gene - add "_loc1" extension of the first
                saveGf.Name += locIndicator + "1";
            }
            if (altLocNo > 1 || gf.IsPseudogeneType())
                gf.Name = locName;
            genesByChr[gf.Chr].Add(gf);
            nameToGene[locName] = gf;
            //Console.WriteLine("{0}-{1} Created {2}", gf.Start, gf.End, gf.Name);
            return true;
        }

        /// <summary>
        /// Combine the exons of two overlapping models, and annotate the combined model.
        /// </summary>
        /// <param name="oldGf"></param>
        /// <param name="newGf"></param>
        /// <returns></returns>
        private GeneFeature CreateExonUnion(GeneFeature oldGf, GeneFeature newGf)
        {
            List<int> newStarts = oldGf.ExonStarts.ToList();
            List<int> newEnds = oldGf.ExonEnds.ToList();
            for (int i2 = 0; i2 < newGf.ExonCount; i2++)
            {
                int start2 = newGf.ExonStarts[i2];
                int end2 = newGf.ExonEnds[i2];
                int insertPoint = newStarts.FindIndex(s => s > start2);
                if (insertPoint == -1) insertPoint = newStarts.Count;
                newStarts.Insert(insertPoint, start2);
                newEnds.Insert(insertPoint, end2);
            }
            for (int i = 0; i < newStarts.Count - 1; i++)
            {
                if (newStarts[i + 1] < newEnds[i])
                {
                    newEnds[i] = Math.Max(newEnds[i], newEnds[i + 1]);
                    newStarts.RemoveAt(i + 1);
                    newEnds.RemoveAt(i + 1);
                    i--;
                }
            }
            bool newCoding = newGf.GeneType == "mRNA" || newGf.GeneType == "protein_coding";
            bool oldCoding = oldGf.GeneType == "mRNA" || oldGf.GeneType == "protein_coding";
            string combTrType = oldGf.GeneType;
            if (oldGf.GeneType == "gene" && newCoding) combTrType = newGf.GeneType;
            else if (!(oldCoding && newGf.GeneType == "gene") && !oldGf.GeneType.Contains(newGf.GeneType))
                combTrType += ";" + newGf.GeneType;
            string combTrName = oldGf.GeneMetadata.Contains(newGf.GeneMetadata) ? oldGf.GeneMetadata : (oldGf.GeneMetadata + "/" + newGf.GeneMetadata);
            GeneFeature newFeature = new GeneFeature(oldGf.Name, oldGf.Chr, oldGf.Strand, 
                                                    newStarts.ToArray(), newEnds.ToArray(), combTrType, combTrName);
            return newFeature;
        }

        /// <summary>
        /// Adjust genes according to the filters/modifiers supplied.
        /// Can be 5' end, masking of overlaps etc...
        /// </summary>
        /// <param name="m"></param>
        public void AdjustGeneFeatures(GeneFeatureModifiers m)
        {
            foreach (List<GeneFeature> chrGenes in genesByChr.Values)
            {
                List<GeneFeature> gfs = new List<GeneFeature>(chrGenes.Count);
                foreach (GeneFeature e in chrGenes)
                    gfs.Add((GeneFeature)e);
                m.Process(gfs);
            }
        }

        /// <summary>
        /// Adds the transcript models from the spike or other common control chromosome
        /// </summary>
        /// <param name="chrId">CTRL or e.g. EXTRA</param>
        public void AddCommonGeneModels(string chrId)
        {
            string genesPath = PathHandler.GetCommonGenesPath(chrId);
            if (File.Exists(genesPath))
            {
                int nAddedGenes = 0;
                VisitedAnnotationPaths += ";" + genesPath;
                foreach (GeneFeature gf in AnnotationReader.IterRefFlatFile(genesPath))
                    if (AddGeneModel(gf)) nAddedGenes++;
                Console.WriteLine("Added {0} genes from {1}.", nAddedGenes, genesPath);
            }
        }

        /// <summary>
        /// Iterates the transcripts of a UCSC refFlat-formatted file, making no changes/variant detections of the data
        /// </summary>
        /// <param name="refFlatPath"></param>
        /// <returns></returns>
        public static IEnumerable<IFeature> IterRefFlatFile(string refFlatPath)
        {
            using (StreamReader refReader = new StreamReader(refFlatPath))
            {
                string line;
                while ((line = refReader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] f = line.Split('\t');
                    if (f.Length < 11)
                        throw new AnnotationFileException("Wrong format of file " + refFlatPath + " Should be 11 TAB-delimited columns.");
                    GeneFeature ft = GeneFeatureFromRefFlatLine(line);
                    yield return ft;
                }
            }
        }

        /// <summary>
        /// Iterates the transcript models of a STRT (refFlat-like) transcript file
        /// </summary>
        /// <param name="STRTAnnotationsPath"></param>
        /// <returns></returns>
        public static IEnumerable<IFeature> IterSTRTAnnotationsFile(string STRTAnnotationsPath)
        {
            using (StreamReader annotReader = new StreamReader(STRTAnnotationsPath))
            {
                string line = annotReader.ReadLine();
                while (line.StartsWith("@") || line.StartsWith("#"))
                    line = annotReader.ReadLine();
                string[] f = line.Split('\t');
                if (f.Length < 11)
                    throw new AnnotationFileException("Wrong format of file " + STRTAnnotationsPath + " Should be >= 11 TAB-delimited columns.");
                while (line != null)
                {
                    if (line != "" && !line.StartsWith("#"))
                    {
                        IFeature ft = FromSTRTAnnotationsLine(line);
                        yield return ft;
                    }
                    line = annotReader.ReadLine();
                }
            }
        }

        private static GeneFeature GeneFeatureFromRefFlatLine(string line)
        {
            string[] record = line.Split('\t');
            string name = record[0].Trim();
            string trName = record[1].Trim();
            string geneType = "";
            int i = trName.IndexOf(';');
            if (trName == "")
                trName = name;
            else if (i > 0)
            {
                geneType = trName.Substring(0, i);
                trName = trName.Substring(i + 1);
            }
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], 0);
            int[] exonEnds = SplitExonEndsField(record[10]); // Convert to inclusive ends
            return new GeneFeature(name, chr, strand, exonStarts, exonEnds, geneType, trName);
        }

        private static IFeature FromSTRTAnnotationsLine(string line)
        {
            string[] record = line.Split('\t');
            string name = record[0].Trim();
            string geneMetadata = record[1].Trim();
            string geneType = "";
            int i = geneMetadata.IndexOf(';');
            if (geneMetadata == "")
                geneMetadata = name;
            else if (i > 0)
            {
                geneType = geneMetadata.Substring(0, i);
                geneMetadata = geneMetadata.Substring(i + 1);
            }
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], 0);
            int[] exonEnds = SplitExonEndsField(record[10]); // Convert to inclusive ends
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds, geneType, geneMetadata);
            int[] offsets = SplitField(record[11], 0);
            int[] realExonIds = SplitField(record[12], 0);
            string[] exonsStrings = record[13].Split(',');
            return new SplicedGeneFeature(name, chr, strand, exonStarts, exonEnds, offsets, realExonIds, exonsStrings);
        }

        public static int[] SplitExonEndsField(string exonEnds)
        {
            return SplitField(exonEnds, -1);
        }
        public static int[] SplitField(string field, int offset)
        {
            string[] items = field.Split(',');
            int nParts = items.Length - 1;
            int[] parts = new int[nParts];
            for (int i = 0; i < nParts; i++)
                parts[i] = int.Parse(items[i]) + offset;
            return parts;
        }

        public static GeneFeature GeneFeatureFromDBTranscript(Transcript tt)
        {
            int[] exonStarts = SplitField(tt.ExonStarts, 0); // 0-based
            int[] exonEnds = SplitExonEndsField(tt.ExonEnds); // Convert to 0-based inclusive ends
            return new GeneFeature(tt.UniqueGeneName, tt.Chromosome, tt.Strand, exonStarts, exonEnds,
                                   tt.Type, tt.Name, tt.TranscriptID.Value, tt.ExprBlobIdx);
        }

        public static Transcript CreateNewTranscriptFromGeneFeature(GeneFeature gf)
        {
            string type = gf.GeneType == "" ? "gene" : gf.GeneType;
            string trName = gf.GeneMetadata.Split(';')[0];
            return new Transcript(trName, type, gf.NonVariantName, gf.Name, "",  "",
                                  gf.Chr, gf.Start + 1, gf.End + 1, gf.GetTranscriptLength(), gf.Strand,
                                  gf.Extension5Prime, gf.ExonStartsString, gf.ExonEndsString);
        }

    }
}
