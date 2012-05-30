using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;

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
        public DataSampler(Expression expression, int minValuesPerIvl, bool plotDistributions)
        {
            rnd = new Random(DateTime.Now.Millisecond);
            Dictionary<int, List<double>> meansByCount = SortMeansByCount(expression);
            DefineIntervalsOfMeans(minValuesPerIvl, meansByCount);
            if (plotDistributions)
            {
                PlotMeansByCount(meansByCount);
                PlotIntervals();
            }
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
            List<int> tempIvlStarts = new List<int>();
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
                    tempIvlStarts.Add(counts[ivlStartIdx]);
                    ivlMeans = new List<double>();
                    ivlNValues = 0;
                    ivlStartIdx = i + 1;
                }
            }
            if (ivlMeans.Count > 0)
            {
                meansByIvl.Add(ivlMeans);
                tempIvlStarts.Add(counts[ivlStartIdx]);
            }
            countIvlStarts = tempIvlStarts.ToArray();
        }

        private static Dictionary<int, List<double>> SortMeansByCount(Expression expression)
        {
            Dictionary<int, List<double>> meansByCount = new Dictionary<int, List<double>>();
            for (int geneIdx = 0; geneIdx < expression.GeneCount; geneIdx++)
            {
                double mean = expression.GeneMean(geneIdx);
                foreach (int count in expression.GetGeneValues(geneIdx))
                {
                    List<double> meansAtCount;
                    if (!meansByCount.TryGetValue(count, out meansAtCount))
                        meansByCount[count] = new List<double>();
                    meansByCount[count].Add(mean);
                }
            }
            return meansByCount;
        }

        public static void PlotMeansByCount(Dictionary<int, List<double>> meansByCount)
        {
            string outFile = "means_by_count.txt";
            Console.WriteLine("Writing data by count to " + outFile);
            int[] counts = meansByCount.Keys.ToArray();
            Array.Sort(counts);
            StreamWriter writer = new StreamWriter(outFile);
            writer.WriteLine("Count\tNValues\tMeanOfMeans\tStdDev");
            foreach (int c in counts)
            {
                DescriptiveStatistics ds = new DescriptiveStatistics(meansByCount[c].ToArray());
                writer.WriteLine("{0}\t{1}\t{2}\t{3}", c, ds.Count, ds.Mean(), ds.StandardDeviation());
            }
            writer.Close();
        }

        public void PlotIntervals()
        {
            string outFile = "interval_data.txt";
            Console.WriteLine("Writing data by intervals to " + outFile);
            StreamWriter writer = new StreamWriter(outFile);
            writer.WriteLine("IvlStart\tIvlEnd\tMidIvl\tNValues\tMeanOfMeans\tStdDev");
            for (int ivlIdx = 0; ivlIdx < meansByIvl.Count; ivlIdx++)
            {
                int ivlStart = countIvlStarts[ivlIdx];
                int ivlEnd = (ivlIdx == meansByIvl.Count - 1) ? int.MaxValue : countIvlStarts[ivlIdx + 1];
                List<double> ivlData = meansByIvl[ivlIdx];
                DescriptiveStatistics ds = new DescriptiveStatistics(ivlData.ToArray());
                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", ivlStart, ivlEnd, (ivlStart + ivlEnd) / 2.0,
                                                                 ds.Count, ds.Mean(), ds.StandardDeviation());

            }
        }
    }
}
