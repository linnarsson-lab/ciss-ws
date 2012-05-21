using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorrCell
{
    /// <summary>
    /// Used to sample mean expression values from an expression data derived distribution of counts.
    /// </summary>
    public class DataSampler
    {
        private Random rnd;
        private int[] countIvlStarts;
        private List<List<double>> meansByIvl = new List<List<double>>();

        /// <summary>
        /// Setup for sampling
        /// </summary>
        /// <param name="expression">Input expression data</param>
        /// <param name="minValuesPerIvl">Minimum number of values in each bin to sample from</param>
        public DataSampler(Expression expression, int minValuesPerIvl)
        {
            rnd = new Random(DateTime.Now.Millisecond);
            Dictionary<int, List<double>> meansByCount = SortMeansByCount(expression);
            DefineIntervalsOfMeans(minValuesPerIvl, meansByCount);
        }

        /// <summary>
        /// Sample a mean expression value for a given count
        /// </summary>
        /// <param name="count"></param>
        /// <returns>An expression value</returns>
        public double Sample(int count)
        {
            if (count < countIvlStarts[0])
                throw new ArgumentOutOfRangeException("Can not sample data at count < " + countIvlStarts[0].ToString() + " Input: " + count.ToString());
            int ivlIdx = Array.BinarySearch(countIvlStarts, count);
            if (ivlIdx < 0)
                ivlIdx = ~ivlIdx - 1;
            List<double> ivlData = meansByIvl[ivlIdx];
            return ivlData[rnd.Next(ivlData.Count)];
        }

        private void DefineIntervalsOfMeans(int minValuesPerIvl, Dictionary<int, List<double>> meansByCount)
        {
            List<double> ivlMeans = new List<double>();
            int[] counts = meansByCount.Keys.ToArray();
            Array.Sort(counts);
            List<int> TempIvlStarts = new List<int>();
            int ivlNValues = 0;
            int ivlStartIdx = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                ivlMeans.AddRange(meansByCount[count]);
                ivlNValues += meansByCount[count].Count;
                if (ivlNValues >= minValuesPerIvl)
                {
                    meansByIvl.Add(ivlMeans);
                    TempIvlStarts.Add(counts[ivlStartIdx]);
                    ivlMeans = new List<double>();
                    ivlNValues = 0;
                    ivlStartIdx = i + 1;
                }
            }
            if (ivlMeans.Count > 0)
            {
                meansByIvl.Add(ivlMeans);
                TempIvlStarts.Add(counts[ivlStartIdx]);
            }
            countIvlStarts = TempIvlStarts.ToArray();
        }

        private static Dictionary<int, List<double>> SortMeansByCount(Expression expression)
        {
            Dictionary<int, List<double>> meansByCount = new Dictionary<int, List<double>>();
            for (int geneIdx = 0; geneIdx < expression.GeneCount; geneIdx++)
            {
                double mean = expression.GeneMean(geneIdx);
                foreach (int count in expression.IterGeneValues(geneIdx, false))
                {
                    List<double> meansAtCount;
                    if (!meansByCount.TryGetValue(count, out meansAtCount))
                        meansAtCount = new List<double>();
                    meansAtCount.Add(mean);
                }
            }
            return meansByCount;
        }

    }
}
