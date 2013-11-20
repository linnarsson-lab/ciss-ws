using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class BioMartAnnotationReader : AnnotationReader
    {
        private string sourceName;

        public BioMartAnnotationReader(StrtGenome genome, string sourceName)
            : base(genome)
        {
            this.sourceName = sourceName;
        }

        public override int BuildGeneModelsByChr()
        {
            ClearGenes();
            int nCreated = ReadMartGenes();
            nCreated += ReadRefFlatGenes();
            return nCreated;
        }

        private int ReadMartGenes()
        {
            string martPath = MakeFullAnnotationPath(sourceName + "_mart_export.txt", true);
            VisitedAnnotationPaths = martPath;
            int nRead = 0, nCreated = 0;
            foreach (ExtendedGeneFeature gf in IterMartFile(martPath))
            {
                if (!gf.Chr.Contains("random"))
                {
                    if (AddGeneModel(gf)) nCreated++;
                    nRead++;
                }
            }
            Console.WriteLine("Read {0} genes and variants from {1}", nRead, martPath);
            return nCreated;
        }

        private int ReadRefFlatGenes()
        {
            int refN = 0, nCreated = 0;
            string refFlatPath = MakeFullAnnotationPath("refFlat.txt", false);
            if (File.Exists(refFlatPath))
            {
                VisitedAnnotationPaths += ";" + refFlatPath;
                foreach (ExtendedGeneFeature gf in AnnotationReader.IterRefFlatFile(refFlatPath))
                {
                    if (ShouldAdd(gf))
                    {
                        if (AddGeneModel(gf)) nCreated++;
                        refN++;
                    }
                }
                Console.WriteLine("Added {0} genes and their variants from {1}", refN, refFlatPath);
            }
            return nCreated;
        }

        private bool ShouldAdd(ExtendedGeneFeature gf)
        {
            if (gf.Chr.Contains("random")) return false;
            try
            {
                if (genesByChr[gf.Chr].FindIndex(
                        (g) => g.NonVariantName == gf.Name) >= 0)
                    return false;
                foreach (ExtendedGeneFeature oldGf in genesByChr[gf.Chr])
                    if (oldGf.IsSameTranscript(gf, 5))
                    {
                        oldGf.Name = gf.Name + "/" + oldGf.Name;
                        oldGf.Start = Math.Min(oldGf.Start, gf.Start);
                        oldGf.End = Math.Max(oldGf.End, gf.End);
                        return false;
                    }
            }
            catch (KeyNotFoundException) { }
            return true;
        }

        private IEnumerable<IFeature> IterMartFile(string martPath)
        {
            Dictionary<string, bool> uniqNames = new Dictionary<string, bool>();
            using (StreamReader martReader = new StreamReader(martPath))
            {
                string header = martReader.ReadLine();
                int exStartCol = -1, exEndCol = -1, chrCol = -1,
                    strandCol = -1, nameCol = -1, typeCol = 0, trNameCol = -1, descrCol = -1;
                string[] fields = header.Split('\t');
                for (int i = 0; i < fields.Length; i++)
                {
                    string f = fields[i].Trim();
                    if (f.ToLower().Contains("transcript id")) trNameCol = i;
                    if (f.StartsWith("Exon Chr Start")) exStartCol = i;
                    else if (f.StartsWith("Exon Chr End")) exEndCol = i;
                    else if (f == "Chromosome Name") chrCol = i;
                    else if (f == "Strand") strandCol = i;
                    else if (f == "Description") descrCol = i;
                    else if (f == "External Gene ID" || f == "Associated Gene Name") nameCol = i;
                    else if (f == "Transcript Biotype") typeCol = i;
                    else if (f == "Gene Biotype") typeCol = i;
                }
                if (trNameCol == -1 || exStartCol == -1 || exEndCol == -1 || chrCol == -1 || strandCol == -1 || nameCol == -1 || typeCol == -1)
                    throw new FormatException("BioMart input file misses some columns.\n" +
                        "Required: Transcript ID, Exon Chr Start, Exon Chr End, Chromosome Name, Strand, External Gene ID, Biotype");
                List<Interval> exons = new List<Interval>();
                char strand = '+';
                string name = "", currentTrName = "", chr = "", trType = null, trName = null;
                string line;
                while ((line = martReader.ReadLine()) != null)
                {
                    fields = line.Split('\t');
                    trName = fields[trNameCol].Trim();
                    if (exons.Count > 0 && trName != currentTrName) // Handle every splice variant as a gene variant
                    {
                        yield return CreateGeneFeature(name, chr, strand, exons, currentTrName, trType);
                        exons.Clear();
                    }
                    if (exons.Count == 0)
                    {
                        currentTrName = trName;
                        name = fields[nameCol].Trim();
                        if (name == "") name = trName;
                        trType = fields[typeCol].Trim();
                        if (trType.Contains("pseudogene"))
                        {
                            pseudogeneCount++;
                            int n = 1;
                            while (uniqNames.ContainsKey(name + GeneFeature.pseudoGeneIndicator + n))
                                n++;
                            name += GeneFeature.pseudoGeneIndicator + n;
                            uniqNames[name] = true;
                        }
                        chr = fields[chrCol].Trim();
                        strand = (fields[strandCol].Trim() == "1") ? '+' : '-';
                    }
                    exons.Add(new Interval(int.Parse(fields[exStartCol]) - 1, int.Parse(fields[exEndCol]) - 1));
                }
                if (name != "")
                    yield return CreateGeneFeature(name, chr, strand, exons, trName, trType);
            }
        }

        private IFeature CreateGeneFeature(string name, string chr, char strand,
                                   List<Interval> exons, string trName, string trType)
        {
            exons.Sort((i1, i2) => (i1.Start == i2.Start) ? 0 : (i1.Start > i2.Start) ? 1 : -1);
            int i = 0;
            while (i < exons.Count - 1)
            {
                if (exons[i].Intersects(exons[i + 1]))
                {
                    exons[i] = new Interval(Math.Min(exons[i].Start, exons[i + 1].Start),
                                            Math.Max(exons[i].End, exons[i + 1].End));
                    exons.RemoveAt(i + 1);
                }
                else
                    i++;
            }
            int[] exonStarts = new int[exons.Count];
            int[] exonEnds = new int[exons.Count];
            for (i = 0; i < exons.Count; i++)
            {
                exonStarts[i] = (int)exons[i].Start;
                exonEnds[i] = (int)exons[i].End;
            }
            ExtendedGeneFeature egf = new ExtendedGeneFeature(name, chr, strand, exonStarts, exonEnds, trType, trName);
            return egf;
        }

    }
}
