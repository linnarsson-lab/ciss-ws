using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    class GeneExpression
    {
        public int[] nReads, nUniqueReads, nMols, nUniqueMols;

        public GeneExpression(int nBcs)
        {
            nReads = new int[nBcs];
            nUniqueReads = new int[nBcs];
            nMols = new int[nBcs];
            nUniqueMols = new int[nBcs];
        }

        public void Add(MappedTagItem item, bool isUnique)
        {
            nReads[item.bcIdx] += item.ReadCount;
            nMols[item.bcIdx] += item.MolCount;
            if (isUnique)
            {
                nUniqueReads[item.bcIdx] += item.ReadCount;
                nUniqueMols[item.bcIdx] += item.MolCount;
            }
        }
    }

    /// <summary>
    /// Summarizes reads and molecules for single-transcript gene models when running an all-transcript analysis
    /// </summary>
    public class GeneExpressionSummary
    {
        private GenomeAnnotations annotations;
        private Dictionary<string, GeneExpression> data = new Dictionary<string, GeneExpression>();

        private HashSet<string> nonVariantNames = new HashSet<string>();

        public GeneExpressionSummary(GenomeAnnotations annotations)
        {
            this.annotations = annotations;
            foreach (GeneFeature gf in annotations.IterOrderedGeneFeatures(true, true))
                if (!data.ContainsKey(gf.NonVariantName))
                    data[gf.NonVariantName] = new GeneExpression(Props.props.Barcodes.Count);
        }

        public void Summarize(MappedTagItem item, List<IFeature> exonHitFeatures)
        {
            nonVariantNames.Clear();
            foreach (IFeature gf in exonHitFeatures)
                nonVariantNames.Add(gf.NonVariantName);
            bool isUnique = (nonVariantNames.Count > 1);
            foreach (string nonVariantName in nonVariantNames)
            {
                    data[nonVariantName].Add(item, isUnique);
            }
        }

        public void WriteOutput(string outputPathbase)
        {
            WriteGeneSummary(outputPathbase + "_expression_genemax.tab", (x => x.nMols));
            WriteGeneSummary(outputPathbase + "_expression_genemin.tab", (x => x.nUniqueMols));
            if (TagItem.nUMIs > 1)
            {
                WriteGeneSummary(outputPathbase + "_reads_genemax.tab", (x => x.nReads));
                WriteGeneSummary(outputPathbase + "_reads_genemin.tab", (x => x.nUniqueReads));
            }
        }

        private void WriteGeneSummary(string file, Func<GeneExpression, int[]> valGetter)
        {
            int[] speciesBcIndexes = Props.props.Barcodes.GenomeAndEmptyBarcodeIndexes(annotations.Genome);
            using (StreamWriter writer = new StreamWriter(file))
            {
                foreach (int idx in speciesBcIndexes)
                    writer.Write("\t{0}_{1}", annotations.PlateId, Props.props.Barcodes.GetWellId(idx));
                writer.WriteLine();
                foreach (KeyValuePair<string, GeneExpression> p in data)
                {
                    writer.Write(p.Key);
                    int[] exprs = valGetter(p.Value);
                    foreach (int idx in speciesBcIndexes)
                        writer.Write("\t" + exprs[idx]);
                    writer.WriteLine();
                }
            }
        }
    }
}
