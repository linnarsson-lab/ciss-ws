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

        public override Dictionary<string, List<GeneFeature>> BuildGeneModelsByChr()
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
                if (!gf.Chr.Contains("random"))
                {
                    AddGeneModel(gf);
                    n++;
                }
            }
            Console.WriteLine("Read {0} genes and variants from {1}", n, martPath);
        }

        private void ReadRefFlat()
        {
            int refN = 0;
            string refFlatPath = GetAnnotationPath("refFlat.txt");
            if (File.Exists(refFlatPath))
            {
                foreach (GeneFeature gf in new UCSCAnnotationReader(genome).IterAnnotationFile(refFlatPath))
                {
                    if (ShouldAdd(gf))
                    {
                        AddGeneModel(gf);
                        refN++;
                    }
                }
                Console.WriteLine("Added {0} genes and their variants from {1}", refN, refFlatPath);
            }
        }

        private bool ShouldAdd(GeneFeature gf)
        {
            if (gf.Chr.Contains("random")) return false;
            try
            {
                if (genesByChr[gf.Chr].FindIndex(
                        (g) => g.NonVariantName == gf.Name) >= 0)
                    return false;
                foreach (GeneFeature oldGf in genesByChr[gf.Chr])
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

        protected IEnumerable<IFeature> IterAnnotationFile(string martPath)
        {
            Dictionary<string, bool> uniqNames = new Dictionary<string, bool>();
            StreamReader martReader = martPath.OpenRead();
            string header = martReader.ReadLine();
            int exStartCol = -1, exEndCol = -1, chrCol = -1, 
                strandCol = -1, nameCol = -1, typeCol = 0, trIdCol = -1;
            string[] fields = header.Split('\t');
            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i].Trim();
                if (f.ToLower().Contains("transcript id")) trIdCol = i;
                if (f.StartsWith("Exon Chr Start")) exStartCol = i;
                else if (f.StartsWith("Exon Chr End")) exEndCol = i;
                else if (f == "Chromosome Name") chrCol = i;
                else if (f == "Strand") strandCol = i;
                else if (f == "External Gene ID" || f == "Associated Gene Name") nameCol = i;
                else if (f == "Gene Biotype") typeCol = i;
            }
            if (exStartCol == -1 || exEndCol == -1 || chrCol == -1 || strandCol == -1 || nameCol == -1)
                throw new FormatException("BioMart input file misses some columns.\n" +
                    "Required: Transcript ID, Exon Chr Start, Exon Chr End, Chromosome Name, Strand, External Gene ID");
            List<Interval> exons = new List<Interval>();
            string name = "";
            string currentTrId = "";
            string chr = "";
            char strand = '+';
            string line = martReader.ReadLine();
            while (line != null)
            {
                fields = line.Split('\t');
                string trId = fields[trIdCol].Trim();
                if (exons.Count > 0 && trId != currentTrId) // Handle every splice variant as a gene variant
                {
                    yield return CreateGeneFeature(name, chr, strand, exons);
                    exons.Clear();
                }
                if (exons.Count == 0)
                {
                    currentTrId = trId;
                    name = fields[nameCol].Trim();
                    if (name == "") name = trId;
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
