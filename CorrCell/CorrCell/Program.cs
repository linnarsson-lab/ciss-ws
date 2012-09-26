using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;

namespace CorrCell
{
    class Program
    {
        static void Main(string[] args)
        {
            int nSample = 1000;
            int minMeanSamplesInBin = 200;
            double fractionThreshold = 100.0;
            double minExprLevel = 5.0;
            bool plot = false;
            bool shuffleGenes = false;
            bool shuffleCells = false;
            bool totalShuffle = false;
            int all = -1;
            string outFileBase = "CorrCell_output";
            string pairFile = null;
            string classFile = null;
            CorrelationCalculator cc = CorrelationCalculators.Spearman;
            try
            {
                if (args.Length == 0)
                    throw new ArgumentException();
                int argIdx = 0;
                while (argIdx < args.Length && args[argIdx][0] == '-')
                {
                    if (args[argIdx] == "-s")
                        nSample = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-b")
                        minMeanSamplesInBin = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-f")
                        fractionThreshold = double.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-e")
                        minExprLevel = double.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-o")
                        outFileBase = args[++argIdx];
                    else if (args[argIdx] == "-p")
                        pairFile = args[++argIdx];
                    else if (args[argIdx] == "-c")
                        classFile = args[++argIdx];
                    else if (args[argIdx] == "--plot")
                        plot = true;
                    else if (args[argIdx] == "--shuffle-genes")
                        shuffleGenes = true;
                    else if (args[argIdx] == "--shuffle-cells")
                        shuffleCells = true;
                    else if (args[argIdx] == "--shuffle-all")
                        totalShuffle = true;
                    else if (args[argIdx] == "-a")
                        all = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-P")
                        cc = CorrelationCalculators.Pearson;
                    else if (args[argIdx] == "-D")
                        cc = CorrelationCalculators.Distance;
                    else throw new ArgumentException();
                    argIdx++;
                }
                if (args.Length == argIdx)
                    throw new ArgumentException();
                Console.WriteLine("Reading " + args[argIdx]);
                Expression expr = new Expression(args[argIdx]);
                expr.FilterEmptyCells(fractionThreshold);
                expr.FilterLowGenes(minExprLevel);
                Console.WriteLine("Data size after empty cell/low level filtering is {0} genes and {1} cells.", expr.GeneCount, expr.CellCount);
                if (shuffleGenes)
                {
                    Console.WriteLine("Random shuffling of gene values within cells to estimate background.");
                    expr.ShuffleBetweenSimilarLevelGenes();
                    outFileBase += "_shuffledGenes";
                }
                if (shuffleCells)
                {
                    Console.WriteLine("Random shuffling of each gene's values between cells to estimate background.");
                    expr.ShuffleBetweenCells();
                    outFileBase += "_shuffledCells";
                }
                if (totalShuffle)
                {
                    Console.WriteLine("Random shuffling of all values.");
                    expr.TotalShuffle();
                    outFileBase += "_totalshuffle";
                }
                Console.WriteLine("minCountBinSize=" + minMeanSamplesInBin + " NSamplings=" + nSample);
                Console.WriteLine("Correlations calculated using method: " + cc.Method.Name);
                string plotFileBase = plot? outFileBase : "";
                DataSampler dataSampler = new DataSampler(expr, minMeanSamplesInBin, plotFileBase);
                GeneCorrelator gc = new GeneCorrelator(nSample, minMeanSamplesInBin, cc, dataSampler);
                if (pairFile != null)
                    AnalyzePairedGenes(pairFile, expr, gc, outFileBase);
                else if (classFile != null)
                    AnalyzeGeneClasses(classFile, expr, gc, outFileBase);
                if (all >= 0)
                    ShowCorrelations(expr, gc, outFileBase, all);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("The gene list input file does not exist!");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("\nUsage:\n" + 
                                  "mono CorrCell.exe [-s CORRSAMPLESIZE] [-b MINCOUNTBINSIZE] [-p GENEPAIRFILE] [-c GENECLASSFILE] [-a LIMIT]\n" +
                                  "                  [-f FILTERTHRESHOLD] [-e EXPTHRESHOLD] -o OUTFILENAMEBASE [--plot] [--shuffle-genes] [-P|-D] EXPRFILE\n" +
                                  "CORRSAMPLESIZE        [" + nSample.ToString() + "] number of samples to take when calculating correlation\n" +
                                  "MINCOUNTBINSIZE       [" + minMeanSamplesInBin.ToString() + "] min number of means in each bin (interval) of count values\n" +
                                  "GENEPAIRFILE          file of pairs of names of potentially correlated genes to compare against background\n" +
                                  "GENECLASSFILE         file of gene names (1st col) and their respective class names (2nd col)\n" + 
                                  "FILTERTHRESHOLD       [" + fractionThreshold.ToString() + "] filtering of empty cells. Min fraction of counts in cells compared with max cell\n" +
                                  "EXPRTHRESHOLD         [" + minExprLevel.ToString() + "] minimum average expression level of a gene to be used\n" +
                                  "-a LIMIT              output top LIMIT (0 == all) correlations for genes above EXPRTHRESHOLD.\n" +
                                  "OUTFILENAMEBASE       the output file(s) will get this basename (with different suffixes)\n" +
                                  "--plot                used to output expression value distributions to files\n" +
                                  "--shuffle-genes       shuffle values within cell between similar-level genes to estimate 'random' data background\n" +
                                  "--shuffle-cells       shuffle each gene's values between cell to estimate 'random' data background\n" +
                                  "--shuffle-all         shuffle all values randomly in expression table\n" + 
                                  "EXPRFILE              the Lxxx_expression.tab output file from the STRT pipeline\n" +
                                  "-P -D                 change distance measure from Spearman to either Pearson or Distance");
            }
        }

        private static void AnalyzePairedGenes(string pairFile, Expression expr, GeneCorrelator gc, string outFileBase)
        {
            GeneClassAnalyzer gca = new GeneClassAnalyzer(expr, gc);
            gca.AnalyzePairedGenes(pairFile, outFileBase + "_pairs_distrib.tab");
        }

        private static void AnalyzeGeneClasses(string classFile, Expression expr, GeneCorrelator gc, string outFileBase)
        {
            GeneClassAnalyzer gca = new GeneClassAnalyzer(expr, gc);
            gca.AnalyzeGeneClasses(classFile, outFileBase + "_classes_distrib.tab");
        }

        /// <summary>
        /// Display correlations between all gene pairs
        /// </summary>
        /// <param name="expr">Expression data matrix</param>
        /// <param name="gc">Correlator method of choice</param>
        /// <param name="outFileBase">File for output</param>
        /// <param name="limit">Optional lower limit for the correlation value of displayed pairs</param>
        private static void ShowCorrelations(Expression expr, GeneCorrelator gc, string outFileBase, int limit)
        {
            if (limit > 0)
                ShowTopCorrelations(expr, gc, outFileBase, limit);
            else
                ShowAllCorrelations(expr, gc, outFileBase);
        }

        private static void ShowAllCorrelations(Expression expr, GeneCorrelator gc, string outFileBase)
        {
            using (StreamWriter writer = new StreamWriter(outFileBase + "_all_values.tab"))
            {
                writer.WriteLine(CorrPair.Header);
                foreach (CorrPair cp in gc.IterCorrelations(expr))
                    writer.WriteLine(cp.ToString());
            }
        }

        private class DoubleReverseComparer : IComparer<double>
        {
            public int Compare(double x, double y)
            {
                return y.CompareTo(x);
            }
        }

        private static void ShowTopCorrelations(Expression expr, GeneCorrelator gc, string outFileBase, int limit)
        {
            IComparer<double> ic = new DoubleReverseComparer();
            SortedList<double, CorrPair> topPairs = new SortedList<double, CorrPair>(limit, ic);
            foreach (CorrPair cp in gc.IterCorrelations(expr))
            {
                if (topPairs.Count == 0 || cp.corrMean > topPairs.Keys[topPairs.Count - 1])
                {
                    if (topPairs.Count == limit)
                        topPairs.RemoveAt(limit - 1);
                    topPairs.Add(cp.corrMean, cp);
                }
            }
            using (StreamWriter writer = new StreamWriter(outFileBase + "_top" + limit.ToString() + "_values.tab"))
            {
                writer.WriteLine(CorrPair.Header);
                foreach (CorrPair cp in topPairs.Values)
                    writer.WriteLine(cp.ToString());
            }
        }
    }
}
