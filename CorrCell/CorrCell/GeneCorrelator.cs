﻿using System;
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

        private DataSampler dataSampler;
        private CorrelationCalculator correlationCalculator;

        public GeneCorrelator(int nSamples, int minSampleBinSize, CorrelationCalculator corrCalculator, DataSampler dataSampler)
        {
            this.nSamples = nSamples;
            correlationCalculator = corrCalculator;
            this.dataSampler = dataSampler;
        }

        /// <summary>
        /// Estimate pair-wise correlations between all genes in the input data set,
        /// sampling from the global distribution of means as function of counts
        /// </summary>
        /// <param name="expr"></param>
        public IEnumerable<CorrPair> IterCorrelations(Expression expr)
        {
            int nGenes = expr.GeneCount;
            for (int geneIdxA = 0; geneIdxA < nGenes - 1; geneIdxA++)
            {
                if (expr.GeneMean(geneIdxA) == 0.0)
                    continue;
                for (int geneIdxB = geneIdxA + 1; geneIdxB < nGenes; geneIdxB++)
                {
                    if (expr.GeneMean(geneIdxB) == 0.0)
                        continue;
                    yield return GetCorrelation(expr, geneIdxA, geneIdxB);
                }
            }
        }

        public CorrPair GetCorrelation(Expression expr, int geneIdxA, int geneIdxB)
        {
            int[] countsA = expr.GetGeneValues(geneIdxA);
            int[] countsB = expr.GetGeneValues(geneIdxB);
            DescriptiveStatistics ds = EstimateCorrelation(dataSampler, countsA, countsB);
            CorrPair cp = new CorrPair(ds.Mean(), ds.Variance(), countsA, countsB,
                                       geneIdxA, geneIdxB, expr.GetGeneName(geneIdxA), expr.GetGeneName(geneIdxB));
            return cp;
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
