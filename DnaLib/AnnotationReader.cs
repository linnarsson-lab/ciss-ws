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
        protected StrtGenome genome;
        protected Dictionary<string, GeneFeature> nameToGene;
        protected Dictionary<string, List<GeneFeature>> genesByChr;
        protected int pseudogeneCount = 0;

        public AnnotationReader(StrtGenome genome)
        {
            this.genome = genome;
        }

        public abstract Dictionary<string, List<GeneFeature>> BuildGeneModelsByChr();

        protected void ClearGenes()
        {
            nameToGene = new Dictionary<string, GeneFeature>();
            genesByChr = new Dictionary<string, List<GeneFeature>>();
            pseudogeneCount = 0;
        }
 
        public virtual int GetPseudogeneCount()
        {
            return pseudogeneCount;
        }

        protected string GetAnnotationPath(string annotationFilename)
        {
            string genomeFolder = genome.GetOriginalGenomeFolder();
            string annotationPath = Path.Combine(genomeFolder, annotationFilename);
            annotationPath = PathHandler.ExistsOrGz(annotationPath);
            if (annotationPath == null)
                throw new FileNotFoundException("Could not find " + annotationFilename + " in " + genomeFolder);
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
                    locName = gf.Name + GeneFeature.altLocusIndicator + v.ToString();
                } // Now we need to add a new second locus for this gene name
                gf.Name = locName;
                if (!prevGf.Name.Contains(GeneFeature.altLocusIndicator))
                { // We are adding the second locus with same name: Add locus indicator to the first gene name.
                    string firstNameWithLocus = prevGf.Name + GeneFeature.altLocusIndicator + "1";
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
            GeneFeature newFeature = new GeneFeature(gf1.Name, gf1.Chr, gf1.Strand, newStarts.ToArray(), newEnds.ToArray());
            return newFeature;
        }

        private void AddToVariantGeneModels(GeneFeature gf)
        {
            try
            {
                GeneFeature prevGf = nameToGene[gf.Name];
                if (!prevGf.IsVariant())
                {
                    string firstNameWithVariant = prevGf.Name + LocusFeature.variantIndicator + "1";
                    prevGf.Name = firstNameWithVariant;
                    nameToGene[firstNameWithVariant] = prevGf;
                }
                int ver = 2;
                string versionName = gf.Name + LocusFeature.variantIndicator + ver.ToString();
                while (nameToGene.ContainsKey(versionName))
                {
                    ver++;
                    versionName = gf.Name + LocusFeature.variantIndicator + ver.ToString();
                }
                gf.Name = versionName;
            }
            catch (KeyNotFoundException) { }
            genesByChr[gf.Chr].Add(gf);
            nameToGene[gf.Name] = gf;
        }

    }
}
