using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace CorrCell
{
    /// <summary>
    /// Estimates the correlation between all gene pairs in an expression data set
    /// by repeated sampling from the experimental expression value distribution
    /// </summary>
    public class GeneCorrelator
    {
        /// <summary>
        /// Number of samples when estimating correlation coefficients
        /// </summary>
        private int nSamples;

        /// <summary>
        /// Used to group less frequent (high) raw data counts in intervals to get reasonable samples sizes
        /// </summary>
        private int minValuesPerIvl;

        private CorrelationCalculator correlationCalculator;

        public GeneCorrelator(int nSamples, int minSampleBinSize, CorrelationCalculator corrCalculator)
        {
            this.nSamples = nSamples;
            minValuesPerIvl = minSampleBinSize;
            correlationCalculator = corrCalculator;
        }

        /// <summary>
        /// Estimate pair-wise correlations between all genes in the input data set,
        /// sampling from the global distribution of means as function of counts
        /// </summary>
        /// <param name="expr"></param>
        public IEnumerable<CorrPair> IterCorrelations(Expression expr)
        {
            DataSampler dataSampler = new DataSampler(expr, minValuesPerIvl);
            int nGenes = expr.GeneCount;
            for (int geneIdxA = 0; geneIdxA < nGenes - 1; geneIdxA++)
            {
                for (int geneIdxB = geneIdxA + 1; geneIdxB < nGenes; geneIdxB++)
                {
                    int[] countsA = expr.GetGeneValues(geneIdxA);
                    int[] countsB = expr.GetGeneValues(geneIdxB);
                    DescriptiveStatistics ds = EstimateCorrelation(dataSampler, countsA, countsB);
                    CorrPair cp = new CorrPair(ds.Mean(), ds.Variance(), countsA, countsB,
                                               geneIdxA, geneIdxB, expr.GetGeneName(geneIdxA), expr.GetGeneName(geneIdxB));
                    yield return cp;
                }
            }
        }

        private DescriptiveStatistics EstimateCorrelation(DataSampler dataSampler, int[] countsA, int[] countsB)
        {
            DescriptiveStatistics ds = new DescriptiveStatistics();
            for (int n = 0; n < nSamples; n++)
            {
                double[] sampleA = new double[countsA.Length];
                double[] sampleB = new double[countsA.Length];
                for (int i = 0; i < countsA.Length; i++)
                {
                    sampleA[i] = dataSampler.Sample(countsA[i]);
                    sampleB[i] = dataSampler.Sample(countsB[i]);
                }
                double corr = correlationCalculator(sampleA, sampleB);
                ds.Add(corr);
            }
            return ds;
        }
    }
}
