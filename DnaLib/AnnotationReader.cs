using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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

        protected StrtGenome genome;
        protected Dictionary<string, GeneFeature> nameToGene;
        protected Dictionary<string, List<GeneFeature>> genesByChr;
        protected int pseudogeneCount = 0;

        public AnnotationReader(StrtGenome genome)
        {
            this.genome = genome;
        }

        public string VisitedAnnotationPaths = "";
        public abstract void BuildGeneModelsByChr();

        public int PseudogeneCount { get { return pseudogeneCount; } }
        public int ChrCount { get { return genesByChr.Count; } }
        public List<string> ChrNames { get { return genesByChr.Keys.ToList(); } }

        public int GeneCount(string chrId)
        {
            return genesByChr.ContainsKey(chrId)? genesByChr[chrId].Count : 0;
        }

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

        public IEnumerable<GeneFeature> IterChrSortedGeneModels(string chrId)
        {
            if (!genesByChr.ContainsKey(chrId)) yield break;
            List<GeneFeature> chrGfs = genesByChr[chrId];
            chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
            foreach (GeneFeature gf in chrGfs)
                yield return gf;
        }

        protected void ClearGenes()
        {
            nameToGene = new Dictionary<string, GeneFeature>();
            genesByChr = new Dictionary<string, List<GeneFeature>>();
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

        protected void AddGeneModel(GeneFeature gf)
        {
            if (!genesByChr.ContainsKey(gf.Chr))
                genesByChr[gf.Chr] = new List<GeneFeature>();
            if (genome.GeneVariants)
                AddToVariantGeneModels(gf);
            else
                AddToSingleGeneModels(gf);
        }

        private void AddToSingleGeneModels(GeneFeature gf)
        {
            List<GeneFeature> chrGfs = genesByChr[gf.Chr];
            try
            {
                GeneFeature prevGf = nameToGene[gf.Name]; // Pick up the first created locus of this name
                int v = 1;
                string locName = gf.Name; // First try the plain gene name, then with alternative locus indicators.
                while (nameToGene.ContainsKey(locName))
                {
                    GeneFeature oldGf = nameToGene[locName];
                    if (oldGf.Chr == gf.Chr && oldGf.Strand == gf.Strand && oldGf.Overlaps(gf.Start, gf.End, 1))
                    {
                        GeneFeature combinedGf = CreateExonUnion(oldGf, gf);
                        combinedGf.Name = oldGf.Name;
                        int idx = chrGfs.IndexOf(oldGf);
                        chrGfs[idx] = combinedGf;
                        nameToGene[oldGf.Name] = combinedGf;
                        nameToGene[locName] = combinedGf;
                        return;
                    }
                    v++;
                    locName = string.Format("{0}{1}{2}", gf.Name, GeneFeature.altLocusIndicator, + v);
                } // Now we need to add a new second locus for this gene name
                gf.Name = locName;
                if (!prevGf.Name.Contains(GeneFeature.altLocusIndicator))
                { // We are adding the second locus with same name: Add locus indicator to the first gene name.
                    string firstNameWithLocus = string.Format("{0}{1}1", prevGf.Name, GeneFeature.altLocusIndicator);
                    prevGf.Name = firstNameWithLocus;
                    nameToGene[firstNameWithLocus] = prevGf;
                }
            }
            catch (KeyNotFoundException) { }
            // Add a new (could be first or secondary with same name) locus
            chrGfs.Add(gf);
            nameToGene[gf.Name] = gf;
        }

        private GeneFeature CreateExonUnion(GeneFeature gf1, GeneFeature gf2)
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
            string combTrId = (gf1.TranscriptID == gf2.TranscriptID)? gf1.TranscriptID : (gf1.TranscriptID + ";" + gf2.TranscriptID);
            string combTrType = (gf1.TranscriptType == gf2.TranscriptType) ? gf1.TranscriptType : (gf1.TranscriptType + ";" + gf2.TranscriptType);
            GeneFeature newFeature = new GeneFeature(gf1.Name, gf1.Chr, gf1.Strand, newStarts.ToArray(), newEnds.ToArray(), combTrId, combTrType
                );
            return newFeature;
        }

        private void AddToVariantGeneModels(GeneFeature gf)
        {
            try
            {
                GeneFeature prevGf = nameToGene[gf.Name];
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
        }

    }
}
