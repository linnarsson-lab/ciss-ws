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
        public BioMartAnnotationReader(StrtGenome genome, string annotationFile)
            : base(genome, annotationFile)
        { }

        protected override int ReadGenes()
        {
            string martPath = MakeFullAnnotationPath(annotationFile, true);
            VisitedAnnotationPaths = martPath;
            int nRead = 0, nCreated = 0;
            foreach (GeneFeature gf in IterMartFile(martPath))
            {
                {
                    if (AddGeneModel(gf)) nCreated++;
                    nRead++;
                }
            }
            Console.WriteLine("Read {0} genes and variants from {1}", nRead, martPath);
            Console.WriteLine("...constructed {0} {1} gene models.", nCreated, (genome.GeneVariants ? "variant" : "main"));
            return nCreated;
        }

        private IEnumerable<IFeature> IterMartFile(string martPath)
        {
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
            GeneFeature egf = new GeneFeature(name, chr, strand, exonStarts, exonEnds, trType, trName);
            return egf;
        }

    }
}
