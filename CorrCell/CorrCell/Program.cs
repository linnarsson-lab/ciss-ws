using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                int minMeanSamplesInBin = 50;
                while (argIdx < args.Length && args[argIdx][0] == '-')
                {
                    if (args[argIdx] == "-s")
                        nSample = int.Parse(args[++argIdx]);
                    else if (args[argIdx] == "-b")
                        nSample = int.Parse(args[++argIdx]);
                    argIdx++;
                }
                Expression expr = new Expression(args[argIdx]);
                Console.WriteLine("Data size is {0} genes and {1} cells.", expr.GeneCount, expr.CellCount);
                Console.WriteLine("minCountBinSize=" + minMeanSamplesInBin + " NSamplings=" + nSample);
                GeneCorrelator gc = new GeneCorrelator(nSample, minMeanSamplesInBin, CorrelationCalculators.Spearman);
                double minShowCorr = 0.1;
                Console.WriteLine("Showing correlations > +/-" + minShowCorr);
                foreach (CorrPair cp in gc.IterCorrelations(expr))
                    if (Math.Abs(cp.corrMean) > minShowCorr)
                        Console.WriteLine(cp.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Console.WriteLine("\nUsage:\nmono CorrCell.exe [-s CORRSAMPLESIZE] [-b MINCOUNTBINSIZE] EXPRESSION_FILE");
            }
        }
    }
}
