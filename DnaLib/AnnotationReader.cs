using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using C1;

namespace Linnarsson.Dna
{
    public class AnnotationFileException : ApplicationException
    {
        public AnnotationFileException(string msg) : base(msg)
        { }
    }

    public abstract class AnnotationReader
    {
        public static AnnotationReader GetAnnotationReader(StrtGenome genome)
        {
            if (genome.Annotation == "VEGA")
                return new BioMartAnnotationReader(genome, "VEGA");
            if (genome.Annotation.StartsWith("ENSE"))
                return new BioMartAnnotationReader(genome, genome.Annotation);
            return new UCSCAnnotationReader(genome);
        }

        public AnnotationReader(StrtGenome genome)
        {
            this.genome = genome;
        }

        public abstract int BuildGeneModelsByChr();

        protected StrtGenome genome;
        protected Dictionary<string, ExtendedGeneFeature> nameToGene;
        protected Dictionary<string, List<ExtendedGeneFeature>> genesByChr;
        protected int pseudogeneCount = 0;

        public string VisitedAnnotationPaths = "";
        public int PseudogeneCount { get { return pseudogeneCount; } }
        public int ChrCount { get { return genesByChr.Count; } }
        public List<string> ChrNames { get { return genesByChr.Keys.ToList(); } }

        public int GeneCount(string chrId)
        {
            return genesByChr.ContainsKey(chrId)? genesByChr[chrId].Count : 0;
        }

        public IEnumerable<ExtendedGeneFeature> IterChrSortedGeneModels()
        {
            foreach (string chrId in genesByChr.Keys)
            {
                List<ExtendedGeneFeature> chrGfs = genesByChr[chrId];
                chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
                foreach (ExtendedGeneFeature gf in chrGfs)
                    yield return gf;
            }
        }

        public IEnumerable<ExtendedGeneFeature> IterChrSortedGeneModels(string chrId)
        {
            if (!genesByChr.ContainsKey(chrId)) yield break;
            List<ExtendedGeneFeature> chrGfs = genesByChr[chrId];
            chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
            foreach (ExtendedGeneFeature gf in chrGfs)
                yield return gf;
        }

        protected void ClearGenes()
        {
            nameToGene = new Dictionary<string, ExtendedGeneFeature>();
            genesByChr = new Dictionary<string, List<ExtendedGeneFeature>>();
            pseudogeneCount = 0;
        }
 
        protected string MakeFullAnnotationPath(string annotationFilename, bool checkExists)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
            string annotationPath = Path.Combine(genomeFolder, annotationFilename);
            annotationPath = PathHandler.ExistsOrGz(annotationPath);
            if (checkExists && annotationPath == null)
                throw new FileNotFoundException(string.Format("Could not find {0} in {1}", annotationFilename, genomeFolder));
            return annotationPath;
        }

        protected bool AddGeneModel(ExtendedGeneFeature gf)
        {
            if (!genesByChr.ContainsKey(gf.Chr))
                genesByChr[gf.Chr] = new List<ExtendedGeneFeature>();
            if (genome.GeneVariants)
                return AddToVariantGeneModels(gf);
            else
                return AddToSingleGeneModels(gf);
        }

        private bool AddToSingleGeneModels(ExtendedGeneFeature gf)
        {
            List<ExtendedGeneFeature> chrGfs = genesByChr[gf.Chr];
            try
            {
                ExtendedGeneFeature prevGf = nameToGene[gf.Name]; // Pick up the first created locus of this name
                int v = 1;
                string locName = gf.Name; // First try the plain gene name, then with alternative locus indicators.
                while (nameToGene.ContainsKey(locName))
                {
                    ExtendedGeneFeature oldGf = nameToGene[locName];
                    if (oldGf.Chr == gf.Chr && oldGf.Strand == gf.Strand && oldGf.Overlaps(gf.Start, gf.End, 1))
                    {
                        ExtendedGeneFeature combinedGf = CreateExonUnion(oldGf, gf);
                        combinedGf.Name = oldGf.Name;
                        int idx = chrGfs.IndexOf(oldGf);
                        chrGfs[idx] = combinedGf;
                        nameToGene[oldGf.Name] = combinedGf;
                        nameToGene[locName] = combinedGf;
                        return false;
                    }
                    v++;
                    locName = string.Format("{0}{1}{2}", gf.Name, ExtendedGeneFeature.altLocusIndicator, +v);
                } // Now we need to add a new second locus for this gene name
                gf.Name = locName;
                if (!prevGf.Name.Contains(ExtendedGeneFeature.altLocusIndicator))
                { // We are adding the second locus with same name: Add locus indicator to the first gene name.
                    string firstNameWithLocus = string.Format("{0}{1}1", prevGf.Name, ExtendedGeneFeature.altLocusIndicator);
                    prevGf.Name = firstNameWithLocus;
                    nameToGene[firstNameWithLocus] = prevGf;
                }
            }
            catch (KeyNotFoundException) { }
            // Add a new (could be first or secondary with same name) locus
            chrGfs.Add(gf);
            nameToGene[gf.Name] = gf;
            return true;
        }

        private ExtendedGeneFeature CreateExonUnion(ExtendedGeneFeature gf1, ExtendedGeneFeature gf2)
        {
            List<int> newStarts = gf1.ExonStarts.ToList();
            List<int> newEnds = gf1.ExonEnds.ToList();
            for (int i2 = 0; i2 < gf2.ExonCount; i2++)
            {
                int start2 = gf2.ExonStarts[i2];
                int end2 = gf2.ExonEnds[i2];
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
            string combTrName = (gf1.TranscriptName == gf2.TranscriptName)? gf1.TranscriptName : (gf1.TranscriptName + ";" + gf2.TranscriptName);
            string combTrType = (gf1.TranscriptType == gf2.TranscriptType) ? gf1.TranscriptType : (gf1.TranscriptType + ";" + gf2.TranscriptType);
            ExtendedGeneFeature newFeature = new ExtendedGeneFeature(gf1.Name, gf1.Chr, gf1.Strand, 
                                                    newStarts.ToArray(), newEnds.ToArray(), 0, combTrType, combTrName);
            return newFeature;
        }

        private bool AddToVariantGeneModels(ExtendedGeneFeature gf)
        {
            try
            {
                ExtendedGeneFeature prevGf = nameToGene[gf.Name];
                if (!prevGf.IsVariant())
                {
                    string firstNameWithVariant = string.Format("{0}{1}1", prevGf.Name, LocusFeature.variantIndicator);
                    prevGf.Name = firstNameWithVariant;
                    nameToGene[firstNameWithVariant] = prevGf;
                }
                int ver = 2;
                string versionName = string.Format("{0}{1}{2}", gf.Name, LocusFeature.variantIndicator, ver);
                while (nameToGene.ContainsKey(versionName))
                {
                    ver++;
                    versionName = string.Format("{0}{1}{2}", gf.Name, LocusFeature.variantIndicator, ver);
                }
                gf.Name = versionName;
            }
            catch (KeyNotFoundException) { }
            genesByChr[gf.Chr].Add(gf);
            nameToGene[gf.Name] = gf;
            return true;
        }

        public void AdjustGeneFeatures(GeneFeatureModifiers m)
        {
            foreach (List<ExtendedGeneFeature> chrGenes in genesByChr.Values)
            {
                List<GeneFeature> gfs = new List<GeneFeature>(chrGenes.Count);
                foreach (ExtendedGeneFeature e in chrGenes)
                    gfs.Add((GeneFeature)e);
                m.Process(gfs);
            }
        }

        public void AddCtrlGeneModels()
        {
            string CTRLGenesPath = PathHandler.GetCTRLGenesPath();
            if (File.Exists(CTRLGenesPath))
            {
                int nCTRLs = 0;
                VisitedAnnotationPaths += ";" + CTRLGenesPath;
                foreach (ExtendedGeneFeature gf in AnnotationReader.IterRefFlatFile(CTRLGenesPath))
                    if (AddGeneModel(gf)) nCTRLs++;
                Console.WriteLine("Added {0} CTRL genes from {1}.", nCTRLs, CTRLGenesPath);
            }
        }

        // Iterates the features of a refFlat-formatted file, making no changes/variant detections of the data.
        public static IEnumerable<IFeature> IterRefFlatFile(string refFlatPath)
        {
            using (StreamReader refReader = new StreamReader(refFlatPath))
            {
                string line = refReader.ReadLine();
                string[] f = line.Split('\t');
                if (f.Length < 11)
                    throw new AnnotationFileException("Wrong format of file " + refFlatPath + " Should be 11 TAB-delimited columns.");
                while (line != null)
                {
                    if (line != "" && !line.StartsWith("#"))
                    {
                        ExtendedGeneFeature ft = ExtendedGeneFeatureFromRefFlatLine(line);
                        yield return ft;
                    }
                    line = refReader.ReadLine();
                }
            }
        }

        private static ExtendedGeneFeature ExtendedGeneFeatureFromRefFlatLine(string line)
        {
            string[] record = line.Split('\t');
            string name = record[0].Trim();
            string trName = record[1].Trim();
            if (trName == "") trName = name;
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], 0);
            int[] exonEnds = SplitField(record[10], -1); // Convert to inclusive ends
            return new ExtendedGeneFeature(name, chr, strand, exonStarts, exonEnds, 0, "gene", trName);
        }

        // Iterates the features of a STRT (refFlat-formatted) transcript file, making no changes/variant detections of the data.
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

        private static IFeature FromSTRTAnnotationsLine(string line)
        {
            string[] record = line.Split('\t');
            string name = record[0].Trim();
            string trName = record[1].Trim();
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], 0);
            int[] exonEnds = SplitField(record[10], -1); // Convert to inclusive ends
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds, 0);
            int[] offsets = SplitField(record[11], 0);
            int[] realExonIds = SplitField(record[12], 0);
            string[] exonsStrings = record[13].Split(',');
            return new SplicedGeneFeature(name, chr, strand, exonStarts, exonEnds, offsets, realExonIds, exonsStrings);
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
    }

}
