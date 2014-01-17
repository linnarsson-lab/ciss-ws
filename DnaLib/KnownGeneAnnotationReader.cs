using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    class KnownGeneAnnotationReader : AnnotationReader
    {
        public KnownGeneAnnotationReader(StrtGenome genome, string annotationFile)
            : base(genome, annotationFile)
        { }

        private Dictionary<string, string[]> xrefData = new Dictionary<string, string[]>();

        protected override int ReadGenes()
        {
            string kgPath = MakeFullAnnotationPath(annotationFile, true);
            SetupXrefData();
            VisitedAnnotationPaths = kgPath;
            int nRead = 0, nCreated = 0;
            foreach (GeneFeature gf in IterKnownGeneFile(kgPath))
            {
                {
                    if (AddGeneModel(gf)) nCreated++;
                    nRead++;
                }
            }
            Console.WriteLine("Read {0} genes and variants from {1}", nRead, kgPath);
            Console.WriteLine("...constructed {0} {1} gene models.", nCreated, (genome.GeneVariants ? "variant" : "main"));
            return nCreated;
        }

        private void SetupXrefData()
        {
            string XrefPath = MakeFullAnnotationPath("kgXref.txt", true);
            if (XrefPath == null)
                return;
            using (StreamReader reader = XrefPath.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    string ucId = fields[0].Trim();
                    string trId = fields[1].Trim();
                    string geneName = fields[4].Trim();
                    string geneType = ValidateKgXrefType(fields[7]);
                    xrefData[ucId] = new string[] { geneName, trId, geneType };

                }
            }
        }

        private IEnumerable<IFeature> IterKnownGeneFile(string knownGenePath)
        {
            using (StreamReader refReader = new StreamReader(knownGenePath))
            {
                string line;
                while ((line = refReader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    if (fields.Length < 11)
                        throw new AnnotationFileException("Wrong format of file " + knownGenePath + " Should be 11 TAB-delimited columns.");
                    string ucId = fields[0].Trim();
                    string chr = fields[1].Trim();
                    char strand = fields[2][0];
                    int[] exonStarts = SplitField(fields[8], 0);
                    int[] exonEnds = SplitExonEndsField(fields[9]); // Convert to inclusive ends
                    string proteinId = fields[10].Trim();
                    string geneName = ucId, geneType = "", trName = "";
                    string[] xrefItem;
                    if (xrefData.TryGetValue(ucId, out xrefItem))
                    {
                        geneName = xrefItem[0];
                        trName = xrefItem[1];
                        geneType = xrefItem[2];
                    }
                    if (!geneName.StartsWith("abParts"))
                        yield return new GeneFeature(geneName, chr, strand, exonStarts, exonEnds, geneType, trName);
                }
            }
        }

    }
}
