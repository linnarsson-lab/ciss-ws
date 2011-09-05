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

        public BioMartAnnotationReader(PathHandler ph, StrtGenome genome, string sourceName)
            : base(ph, genome)
        {
            this.sourceName = sourceName;
        }

        public override Dictionary<string, List<GeneFeature>> ReadGenesByChr()
        {
            ClearGenes();
            ReadVEGAMart();
            ReadRefFlat();
            return genesByChr;
        }

        private void ReadVEGAMart()
        {
            string martPath = GetAnnotationPath(sourceName + "_mart_export.txt");
            int n = 0;
            foreach (GeneFeature gf in IterAnnotationFile(martPath))
            {
                AddGene(gf);
                n++;
            }
            Console.WriteLine("Read {0} genes and variants from {1}", n, martPath);
        }

        private void ReadRefFlat()
        {
            int refN = 0;
            string refFlatPath = GetAnnotationPath("refFlat.txt");
            if (File.Exists(refFlatPath))
            {
                foreach (GeneFeature gf in UCSCAnnotationReader.IterAnnotationFile(refFlatPath))
                {
                    if (!gf.Chr.Contains("random"))
                    {
                        try
                        {
                            if (genesByChr[gf.Chr].FindIndex(
                                    (g) => GeneFeature.StripVersionPart(g.Name) == gf.Name) >= 0)
                                continue;
                        }
                        catch (KeyNotFoundException) { }
                        AddGene(gf);
                        refN++;
                    }
                }
                Console.WriteLine("Added {0} genes and their variants from {1}", refN, refFlatPath);
            }
        }

        protected IEnumerable<IFeature> IterAnnotationFile(string martPath)
        {
            Dictionary<string, bool> uniqNames = new Dictionary<string, bool>();
            StreamReader martReader = martPath.OpenRead();
            string header = martReader.ReadLine();
            int exStartCol = -1, exEndCol = -1, exRankCol = -1, chrCol = -1, 
                strandCol = -1, nameCol = -1, typeCol = 0;
            string[] fields = header.Split('\t');
            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i].Trim();
                if (f.StartsWith("Exon Chr Start")) exStartCol = i;
                else if (f.StartsWith("Exon Chr End")) exEndCol = i;
                else if (f.StartsWith("Exon Rank")) exRankCol = i;
                else if (f == "Chromosome Name") chrCol = i;
                else if (f == "Strand") strandCol = i;
                else if (f == "External Gene ID") nameCol = i;
                else if (f == "Gene Biotype") typeCol = i;
            }
            if (exStartCol == -1 || exEndCol == -1 || exRankCol == -1 || chrCol == -1 || strandCol == -1 || nameCol == -1)
                throw new FormatException("BioMart input file misses some columns.\n" +
                    "Required: Exon Chr Start, Exon Chr End, Exon Rank in Transcript, Chromosome Name, Strand, External Gene ID");
            List<Interval> exons = new List<Interval>();
            string name = "";
            string chr = "";
            char strand = '+';
            string line = martReader.ReadLine();
            while (line != null)
            {
                fields = line.Split('\t');
                if (exons.Count > 0 && fields[exRankCol].Trim() == "1") // Handle every splice variant as a gene variant
                {
                    yield return CreateGeneFeature(name, chr, strand, exons);
                    exons.Clear();
                }
                if (exons.Count == 0)
                {
                    name = fields[nameCol].Trim();
                    if (fields[typeCol].Contains("pseudogene"))
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
                line = martReader.ReadLine();
            }
            if (name != "")
                yield return CreateGeneFeature(name, chr, strand, exons);
            martReader.Close();
        }

        private IFeature CreateGeneFeature(string name, string chr, char strand,
                                   List<Interval> exons)
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
            return new GeneFeature(name, chr, strand, exonStarts, exonEnds);
        }

    }
}
