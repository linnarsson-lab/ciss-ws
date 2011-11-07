using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class UCSCAnnotationReader : AnnotationReader
    {
        public UCSCAnnotationReader(StrtGenome genome)
            : base(genome)
        {}

        public override Dictionary<string, List<GeneFeature>> BuildGeneModelsByChr()
        {
            ClearGenes();
            string refFlatPath = GetAnnotationPath("refFlat.txt");
            int n = 0;
            foreach (GeneFeature gf in IterAnnotationFile(refFlatPath))
            {
                if (!gf.Chr.Contains("random")) // Avoid non-mapped genes.
                {
                    AddGeneModel(gf);
                    n++;
                }
            }
            string varTxt = (genome.GeneVariants) ? " genes and" : " main gene";
            Console.WriteLine("Read {0}{1} variants from {2}", n, varTxt, refFlatPath);
            return genesByChr;
        }

        delegate IFeature GeneFileLineParser(string line);

        public IEnumerable<IFeature> IterAnnotationFile(string refFlatPath)
        {
            StreamReader refReader = refFlatPath.OpenRead();
            string line = refReader.ReadLine();
            GeneFileLineParser lineParser = FromRefFlatLine;
            while (line.StartsWith("@"))
            {
                lineParser = FromGenesFileLine;
                if (line.StartsWith("@ReadLen="))
                    SpliceFlankLen = int.Parse(line.Substring(9));
                line = refReader.ReadLine();
            }
            string[] f = line.Split('\t');
            if (f.Length < 11)
                throw new AnnotationFileException("Wrong format of file " + refFlatPath + " Should be >= 11 TAB-delimited columns.");
            while (line != null)
            {
                if (line != "")
                {
                    IFeature ft = lineParser(line);
                    yield return ft;
                }
                line = refReader.ReadLine();
            }
            refReader.Close();
        }

        public IFeature FromRefFlatLine(string refFlatLine)
        {
            string[] record = refFlatLine.Split('\t');
            string name = record[0].Trim();
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], nExons, 0);
            int[] exonEnds = SplitField(record[10], nExons, -1);
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds);
            int[] offsets = SplitField(record[11], nExons, 0);
            string[] spliceIds = record[12].Split(',');
            Array.Resize(ref spliceIds, nExons);
            int partLength = exonEnds[0] - exonStarts[0] + 1;
            if (partLength != SpliceFlankLen)
                Console.WriteLine("WARNING: Length " + partLength + " of splice chromosome exon (gene " + name +
                                  ") does not equal SpliceFlankLength (" + SpliceFlankLen + ") as specified in " + Props.configFilename);
            int[] realExonIds = new int[spliceIds.Length];
            for (int i = 0; i < spliceIds.Length; i++)
            {
                string id = spliceIds[i];
                realExonIds[i] = (id.Contains('>')) ? int.Parse(id.Split('>')[0]) : int.Parse(id.Split('<')[1]);
            }
            return new SplicedGeneFeature(name, chr, strand, exonStarts, exonEnds, offsets, realExonIds, spliceIds);
        }

        public IFeature FromGenesFileLine(string annotFileLine)
        {
            string[] record = annotFileLine.Split('\t');
            string name = record[0].Trim();
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], nExons, 0);
            int[] exonEnds = SplitField(record[10], nExons, -1);
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds);
            int[] offsets = SplitField(record[11], nExons, 0);
            int[] realExonIds = SplitField(record[12], nExons, 0);
            string[] exonsStrings = record[13].Split(',');
            return new SplicedGeneFeature(name, chr, strand, exonStarts, exonEnds, offsets, realExonIds, exonsStrings);
        }

        private static int[] SplitField(string field, int nParts, int offset)
        {
            int[] parts = new int[nParts];
            string[] items = field.Split(',');
            for (int i = 0; i < nParts; i++)
                parts[i] = int.Parse(items[i]) + offset;
            return parts;
        }

    }
}
