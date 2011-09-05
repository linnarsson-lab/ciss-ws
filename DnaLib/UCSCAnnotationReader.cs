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
        public UCSCAnnotationReader(PathHandler ph, StrtGenome genome)
            : base(ph, genome)
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

        public static IEnumerable<IFeature> IterAnnotationFile(string refFlatPath)
        {
            StreamReader refReader = refFlatPath.OpenRead();
            string line = refReader.ReadLine();
            string[] f = line.Split('\t');
            if (f.Length < 11)
                throw new AnnotationFileException("Wrong format of file " + refFlatPath + " Should be >= 11 TAB-delimited columns.");
            while (line != null)
            {
                if (line != "")
                {
                    IFeature ft = GeneFeature.FromRefFlatLine(line);
                    yield return ft;
                }
                line = refReader.ReadLine();
            }
            refReader.Close();
        }
    }
}
