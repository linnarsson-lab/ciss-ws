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

        public override void BuildGeneModelsByChr()
        {
            ClearGenes();
            string refFlatPath = MakeFullAnnotationPath("refFlat.txt", true);
            VisitedAnnotationPaths = refFlatPath;
            int n = 0;
            foreach (GeneFeature gf in IterAnnotationFile(refFlatPath))
            {
                    AddGeneModel(gf);
                    n++;
            }
            string varTxt = (genome.GeneVariants) ? " genes and" : " main gene";
            Console.WriteLine("Read {0}{1} variants from {2}", n, varTxt, refFlatPath);
        }

        // Iterates the features of a refFlat-formatted file, making no changes/variant detections of the data.
        public IEnumerable<IFeature> IterAnnotationFile(string refFlatPath)
        {
            using (StreamReader refReader = new StreamReader(refFlatPath))
            {
                string line = refReader.ReadLine();
                while (line.StartsWith("@") || line.StartsWith("#"))
                    line = refReader.ReadLine();
                string[] f = line.Split('\t');
                if (f.Length < 11)
                    throw new AnnotationFileException("Wrong format of file " + refFlatPath + " Should be >= 11 TAB-delimited columns.");
                while (line != null)
                {
                    if (line != "" && !line.StartsWith("#"))
                    {
                        IFeature ft = FromAnnotationFileLine(line);
                        yield return ft;
                    }
                    line = refReader.ReadLine();
                }
            }
        }

        public IFeature FromAnnotationFileLine(string annotFileLine)
        {
            string[] record = annotFileLine.Split('\t');
            string name = record[0].Trim();
            string trId = record[1].Trim();
            string chr = record[2].Trim();
            char strand = record[3].Trim()[0];
            int nExons = int.Parse(record[8]);
            int[] exonStarts = SplitField(record[9], nExons, 0);
            int[] exonEnds = SplitField(record[10], nExons, -1); // Convert to inclusive ends
            if (record.Length == 11)
                return new GeneFeature(name, chr, strand, exonStarts, exonEnds, trId, null);
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
