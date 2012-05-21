using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorrCell
{
    /// <summary>
    /// Container for estimated pair-wise correlation data for two genes
    /// </summary>
    public class CorrPair
    {
        /// <summary>
        /// (Mean of) correlation coefficient estimate
        /// </summary>
        public double corrMean;
        /// <summary>
        /// Variance of correlation coefficient estimates
        /// </summary>
        public double corrVariance;
        /// <summary>
        /// Actual raw counts for first gene
        /// </summary>
        public int[] geneACounts;
        /// <summary>
        /// Actual raw counts for second gene
        /// </summary>
        public int[] geneBCounts;
        public int geneAIdx;
        public int geneBIdx;
        /// <summary>
        /// Name of first gene
        /// </summary>
        public string geneAName;
        /// <summary>
        /// Name of second gene
        /// </summary>
        public string geneBName;

        public CorrPair(double mean, double var, int[] countsA, int[] countsB, int idxA, int idxB, string nameA, string nameB)
        {
            corrMean = mean;
            corrVariance = var;
            geneACounts = countsA;
            geneBCounts = countsB;
            geneAIdx = idxA;
            geneBIdx = idxB;
            geneAName = nameA;
            geneBName = nameB;
        }

        public override string ToString()
        {
            return "Correlation between " + geneAName + " (" + geneAIdx + ") and " + geneBName + " (" + geneBIdx + ") is "
                   + corrMean.ToString() + " +/- " + corrVariance.ToString();
        }
    }

}
