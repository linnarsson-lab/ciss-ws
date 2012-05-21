using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorrCell
{
    /// <summary>
    /// Represents expression values of transcripts for a number of cells
    /// </summary>
    public class Expression
    {
        private double[,] data;

        /// <summary>
        /// Number of genes in data the data set
        /// </summary>
        public int GeneCount { get { return data.GetLength(0); } }
        /// <summary>
        /// Number of cells in the data set
        /// </summary>
        public int CellCount { get { return data.GetLength(1); } }

        /// <summary>
        /// Fetch the expression value for a specific gene and cell
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public double GetValue(int geneIdx, int cellIdx)
        {
            return data[geneIdx, cellIdx];
        }

        /// <summary>
        /// Get the average count for a specific gene
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <returns></returns>
        public double GeneMean(int geneIdx)
        {
            int n = 0;
            double sum = 0.0;
            foreach (double value in IterGeneValues(geneIdx, false))
            {
                n++;
                sum += value;
            }
            return sum / n;
        }

        /// <summary>
        /// Get the average count for a specific cell
        /// </summary>
        /// <param name="cellIdx"></param>
        /// <returns></returns>
        public double CellMean(int cellIdx)
        {
            int n = 0;
            double sum = 0.0;
            foreach (double value in IterCellValues(cellIdx, false))
            {
                n++;
                sum += value;
            }
            return sum / n;
        }

        /// <summary>
        /// Iterate the counts for all cells of a specific gene
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <param name="includeNaN">If false, will only return values for cells where data exist</param>
        /// <returns></returns>
        public IEnumerable<double> IterGeneValues(int geneIdx, bool includeNaN)
        {
            for (int col = 0; col < GeneCount; col++)
                if (!double.IsNaN(data[geneIdx, col]) || includeNaN)
                    yield return data[geneIdx, col];
        }

        /// <summary>
        /// Iterate the counts for all genes of a specific cell
        /// </summary>
        /// <param name="geneIdx"></param>
        /// <param name="includeNaN">If false, will only return values for genes where data exist</param>
        /// <returns></returns>
        public IEnumerable<double> IterCellValues(int cellIdx, bool includeNaN)
        {
            for (int row = 0; row < CellCount; row++)
                if (!double.IsNaN(data[row, cellIdx]) || includeNaN)
                    yield return data[row, cellIdx];
        }

        /// <summary>
        /// Iterate all counts in the whole table
        /// </summary>
        /// <param name="includeNaN">If false, will only return valid counts</param>
        /// <returns></returns>
        public IEnumerable<double> IterValues(bool includeNaN)
        {
            for (int row = 0; row < CellCount; row++)
                for (int col = 0; col < GeneCount; col++)
                    if (!double.IsNaN(data[row, col]) || includeNaN)
                        yield return data[row, col];
        }

        /// <summary>
        /// Init from a expression.tab STRT pipeline output file
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static Expression FromExpressionFile(string file)
        {
            return new Expression();
        }

    }
}
