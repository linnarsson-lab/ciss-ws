using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;

namespace CorrCell
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                    throw new ArgumentException();
                int argIdx = 0;
                int nSample = 500;
                int minMeanSamplesInBin = 200;
                double fractionThreshold = 100.0;
                bool plot = false;
                double minShowCorr = 0.0;
                string pairFile = null;
                while (argIdx < args.Length && args[argIdx][0] == '-')
                {
                    if (args[argIdx] == "-s")
                        nSample = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-b")
                        minMeanSamplesInBin = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-f")
                        fractionThreshold = double.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-d")
                        minShowCorr = double.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-p")
                        pairFile = args[++argIdx];
                    else if (args[argIdx] == "--plot")
                        plot = true;
                    argIdx++;
                }
                Console.WriteLine("Reading " + args[argIdx]);
                Expression expr = new Expression(args[argIdx]);
                expr.FilterEmptyCells(fractionThreshold);
                Console.WriteLine("Data size after empty cell filtering is {0} genes and {1} cells.", expr.GeneCount, expr.CellCount);
                Console.WriteLine("minCountBinSize=" + minMeanSamplesInBin + " NSamplings=" + nSample);
                DataSampler dataSampler = new DataSampler(expr, minMeanSamplesInBin, plot);
                GeneCorrelator gc = new GeneCorrelator(nSample, minMeanSamplesInBin, CorrelationCalculators.Spearman, dataSampler);
                if (pairFile != null)
                    AnalyzePairedGenes(pairFile, expr, gc);
                if (minShowCorr > 0.0)
                    ShowCorrelations(minShowCorr, expr, gc);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("The input file does not exist!");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("\nUsage:\nmono CorrCell.exe [-s CORRSAMPLESIZE] [-b MINCOUNTBINSIZE] [-p GENEPAIRFILE]\n" +
                                  "                            [-f FILTERTHRESHOLD] [-d SHOWCORRTHRESHOLD] [--plot] EXPRESSION_FILE\n" +
                                  "CORRSAMPLESIZE        number of samples to take when calculating correlation\n" +
                                  "MINCOUNTBINSIZE       min number of means in each bin (interval) of count values\n" +
                                  "GENEPAIRFILE          file of pairs of names of potentially correlated genes to compare against background\n" +
                                  "FILTERTHRESHOLD       for filtering of empty cells. Min fraction of counts in cells compared with max cell\n" +
                                  "SHOWCORRTHRESHOLD     (> 0.0) Display list of correlations. Only higher correlations will be reported\n" +
                                  "--plot                used to output distributions to files.");
            }
        }

        private static void AnalyzePairedGenes(string pairFile, Expression expr, GeneCorrelator gc)
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            int nHistoBins = 40;
            List<Pair<int, int>> geneIdxPairs = ReadGenePairs(pairFile, expr);
            int[] pairHisto = new int[nHistoBins];
            foreach (Pair<int, int> p in geneIdxPairs)
            {
                double corr = gc.GetCorrelation(expr, p.First, p.Second).corrMean;
                pairHisto[(int)Math.Floor(Math.Abs(corr) * nHistoBins)]++;
            }
            int nNonPairSamples = geneIdxPairs.Count;
            int[] nonPairHisto = new int[nHistoBins];
            for (int i = 0; i < nNonPairSamples; i++)
            {
                int geneIdxA, geneIdxB;
                do
                {
                    geneIdxA = rnd.Next(expr.GeneCount - 1);
                    geneIdxB = rnd.Next(geneIdxA, expr.GeneCount);
                } while (geneIdxPairs.Any(p => (p.First == geneIdxA && p.Second == geneIdxB)));
                double corr = gc.GetCorrelation(expr, geneIdxA, geneIdxB).corrMean;
                nonPairHisto[(int)Math.Floor(Math.Abs(corr) * nHistoBins)]++;
            }
            StreamWriter writer = new StreamWriter(pairFile + ".correlation");
            writer.WriteLine("BinStart\tPairCount\tNonPairCount");
            for (int i = 0; i < nHistoBins; i++)
                writer.WriteLine("{0}\t{1}\t{2}", (i / (double)nHistoBins), pairHisto[i], nonPairHisto[i]);
            writer.Close();
        }

        private static List<Pair<int, int>> ReadGenePairs(string pairFile, Expression expr)
        {
            List<Pair<int, int>> geneIdxPairs = new List<Pair<int, int>>();
            using (StreamReader reader = new StreamReader(pairFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Trim().Split('\t');
                    int geneIdxA = expr.GetGeneIdx(fields[0]);
                    int geneIdxB = expr.GetGeneIdx(fields[1]);
                    if (geneIdxA >= 0 && geneIdxB >= 0)
                    {
                        if (geneIdxA > geneIdxB)
                            geneIdxPairs.Add(new Pair<int, int>(geneIdxB, geneIdxA));
                        else
                            geneIdxPairs.Add(new Pair<int, int>(geneIdxA, geneIdxB));
                    }
                }
            }
            return geneIdxPairs;
        }

        private static void ShowCorrelations(double minShowCorr, Expression expr, GeneCorrelator gc)
        {
            Console.WriteLine("Showing correlations > +/-" + minShowCorr);
            foreach (CorrPair cp in gc.IterCorrelations(expr))
                if (Math.Abs(cp.corrMean) > minShowCorr)
                    Console.WriteLine(cp.ToString());
        }
    }
}
