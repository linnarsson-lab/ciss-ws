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
                double minExprLevel = 0.0;
                bool plot = false;
                double minShowCorr = 0.0;
                string pairFile = null;
                string classFile = null;
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
                    else if (args[argIdx] == "-d")
                        minShowCorr = double.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-p")
                        pairFile = args[++argIdx];
                    else if (args[argIdx] == "-c")
                        classFile = args[++argIdx];
                    else if (args[argIdx] == "--plot")
                        plot = true;
                    else throw new ArgumentException();
                    argIdx++;
                }
                if (args.Length == argIdx)
                    throw new ArgumentException();
                Console.WriteLine("Reading " + args[argIdx]);
                Expression expr = new Expression(args[argIdx]);
                expr.FilterEmptyCells(fractionThreshold);
                expr.FilterLowGenes(minExprLevel);
                Console.WriteLine("Data size after empty cell filtering is {0} genes and {1} cells.", expr.GeneCount, expr.CellCount);
                Console.WriteLine("minCountBinSize=" + minMeanSamplesInBin + " NSamplings=" + nSample);
                DataSampler dataSampler = new DataSampler(expr, minMeanSamplesInBin, plot);
                GeneCorrelator gc = new GeneCorrelator(nSample, minMeanSamplesInBin, CorrelationCalculators.Spearman, dataSampler);
                if (pairFile != null)
                    AnalyzePairedGenes(pairFile, expr, gc);
                else if (classFile != null)
                    AnalyzeGeneClasses(classFile, expr, gc);
                if (minShowCorr > 0.0)
                    ShowCorrelations(minShowCorr, expr, gc);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("The gene list input file does not exist!");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("\nUsage:\n" + 
                                  "mono CorrCell.exe [-s CORRSAMPLESIZE] [-b MINCOUNTBINSIZE] [-p GENEPAIRFILE] [-c GENECLASSFILE]\n" +
                                  "                  [-f FILTERTHRESHOLD] [-e EXPTHRESHOLD] [-d SHOWCORRTHRESHOLD] [--plot] EXPRESSIONFILE\n" +
                                  "CORRSAMPLESIZE        number of samples to take when calculating correlation\n" +
                                  "MINCOUNTBINSIZE       min number of means in each bin (interval) of count values\n" +
                                  "GENEPAIRFILE          file of pairs of names of potentially correlated genes to compare against background\n" +
                                  "GENECLASSFILE         file of gene names (1st col) and their respective class names (2nd col)\n" + 
                                  "FILTERTHRESHOLD       for filtering of empty cells. Min fraction of counts in cells compared with max cell\n" +
                                  "EXPRTHRESHOLD         minimum average expression level of a gene to be used\n" +
                                  "SHOWCORRTHRESHOLD     (> 0.0) Display list of correlations. Only higher correlations will be reported\n" +
                                  "--plot                used to output distributions to files.\n" + 
                                  "EXPRESSIONFILE is the Lxxx_expression.tab output file from the STRT pipeline.");
            }
        }

        private static void AnalyzePairedGenes(string pairFile, Expression expr, GeneCorrelator gc)
        {
            GeneClassAnalyzer gca = new GeneClassAnalyzer(expr, gc);
            gca.AnalyzePairedGenes(pairFile);
        }

        private static void AnalyzeGeneClasses(string classFile, Expression expr, GeneCorrelator gc)
        {
            GeneClassAnalyzer gca = new GeneClassAnalyzer(expr, gc);
            gca.AnalyzeGeneClasses(classFile);
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
