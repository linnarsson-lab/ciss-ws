using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    class GencodeAnnotationReader : AnnotationReader
    {
        public GencodeAnnotationReader(StrtGenome genome, string annotFilePath)
            : base(genome, annotFilePath)
        { }

        private Dictionary<string, string[]> attrsData = new Dictionary<string, string[]>();

        protected override int ReadGenes()
        {
            SetupAttrsData();
            AddVisitedAnnotationPaths(annotationPath);
            int nRead = 0, nCreated = 0;
            foreach (GeneFeature gf in IterGencodeFile(annotationPath))
            {
                {
                    if (AddGeneModel(gf)) nCreated++;
                    nRead++;
                }
            }
            Console.WriteLine("Read {0} genes and variants from {1}", nRead, annotationPath);
            Console.WriteLine("...constructed {0} {1} gene models.", nCreated, (genome.GeneVariants ? "variant" : "main"));
            return nCreated;
        }

        private void SetupAttrsData()
        {
            string attrsPath = annotationPath.Replace("Comp", "Attrs");
            if (!File.Exists(attrsPath))
                return;
            using (StreamReader reader = attrsPath.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    string geneId = fields[0].Trim();
                    string geneName = fields[1].Trim();
                    string geneType = fields[2].Trim();
                    string geneStatus = fields[3].Trim();
                    string trId = fields[4].Trim();
                    string trName = fields[5].Trim();
                    string trType = fields[6].Trim();
                    if (Props.props.AnalyzeAllGeneVariants)
                        attrsData[trId] = new string[] { trName, trId, trType };
                    else
                        attrsData[trId] = new string[] { geneName, trId, trType };

                }
            }
        }

        private IEnumerable<IFeature> IterGencodeFile(string gencodePath)
        {
            using (StreamReader refReader = gencodePath.OpenRead())
            {
                string line;
                while ((line = refReader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    if (fields.Length < 16)
                        throw new AnnotationFileException("Wrong format of file " + gencodePath + " Should be 16 TAB-delimited columns.");
                    string trId = fields[1].Trim();
                    string chr = fields[2].Trim();
                    char strand = fields[3][0];
                    int[] exonStarts = SplitField(fields[9], 0);
                    int[] exonEnds = SplitExonEndsField(fields[10]); // Convert to inclusive ends
                    string geneName = fields[12].Trim();
                    string geneType = "", trName = "";
                    string[] attrsItem;
                    if (attrsData.TryGetValue(trId, out attrsItem))
                    {
                        geneName = attrsItem[0];
                        trName = attrsItem[1];
                        geneType = attrsItem[2];
                    }
                    yield return new GeneFeature(geneName, chr, strand, exonStarts, exonEnds, geneType, trName);
                }
            }
        }

    }
}
