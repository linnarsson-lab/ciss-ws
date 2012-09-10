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
            int nSample = 1000;
            int minMeanSamplesInBin = 200;
            double fractionThreshold = 100.0;
            double minExprLevel = 5.0;
            bool plot = false;
            string corrFile = "";
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
                        corrFile = args[++argIdx];
                    else if (args[argIdx] == "-p")
                        pairFile = args[++argIdx];
                    else if (args[argIdx] == "-c")
                        classFile = args[++argIdx];
                    else if (args[argIdx] == "--plot")
                        plot = true;
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
                Console.WriteLine("minCountBinSize=" + minMeanSamplesInBin + " NSamplings=" + nSample);
                Console.WriteLine("Correlations calculated using method: " + cc.Method.Name);
                DataSampler dataSampler = new DataSampler(expr, minMeanSamplesInBin, plot);
                GeneCorrelator gc = new GeneCorrelator(nSample, minMeanSamplesInBin, cc, dataSampler);
                if (pairFile != null)
                    AnalyzePairedGenes(pairFile, expr, gc);
                else if (classFile != null)
                    AnalyzeGeneClasses(classFile, expr, gc);
                if (corrFile != "")
                    ShowCorrelations(corrFile, expr, gc);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("The gene list input file does not exist!");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("\nUsage:\n" + 
                                  "mono CorrCell.exe [-s CORRSAMPLESIZE] [-b MINCOUNTBINSIZE] [-p GENEPAIRFILE] [-c GENECLASSFILE]\n" +
                                  "                  [-f FILTERTHRESHOLD] [-e EXPTHRESHOLD] [-o OUTFILE] [--plot] [-P|-D] EXPRESSIONFILE\n" +
                                  "CORRSAMPLESIZE        [" + nSample.ToString() + "] number of samples to take when calculating correlation\n" +
                                  "MINCOUNTBINSIZE       [" + minMeanSamplesInBin.ToString() + "] min number of means in each bin (interval) of count values\n" +
                                  "GENEPAIRFILE          file of pairs of names of potentially correlated genes to compare against background\n" +
                                  "GENECLASSFILE         file of gene names (1st col) and their respective class names (2nd col)\n" + 
                                  "FILTERTHRESHOLD       [" + fractionThreshold.ToString() + "] filtering of empty cells. Min fraction of counts in cells compared with max cell\n" +
                                  "EXPRTHRESHOLD         [" + minExprLevel.ToString() + "] minimum average expression level of a gene to be used\n" +
                                  "OUTFILE               Output list of correlations for all gene pairs above EXPRTHRESHOLD.\n" +
                                  "--plot                used to output distributions to files.\n" + 
                                  "EXPRESSIONFILE is the Lxxx_expression.tab output file from the STRT pipeline." +
                                  "-P -D                 change distance measure from Spearman to either Pearson or Distance");
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

        /// <summary>
        /// Display correlations between all gene pairs
        /// </summary>
        /// <param name="corrFile">File for output</param>
        /// <param name="expr">Expression data matrix</param>
        /// <param name="gc">Correlator method of choice</param>
        private static void ShowCorrelations(string corrFile, Expression expr, GeneCorrelator gc)
        {
            using (StreamWriter writer = new StreamWriter(corrFile))
            {
                writer.WriteLine(CorrPair.Header);
                foreach (CorrPair cp in gc.IterCorrelations(expr))
                        writer.WriteLine(cp.ToString());
            }
        }
    }
}
