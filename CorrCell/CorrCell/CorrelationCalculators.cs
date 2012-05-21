using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace CorrCell
{
    public delegate double CorrelationCalculator(double[] seriesA, double[] seriesB);

    public class CorrelationCalculators
    {
        public static double Spearman(double[] seriesA, double[] seriesB)
        {
            int n = seriesA.Length;
            return Linnarsson.Mathematics.Correlation.spearmanrankcorrelation(seriesA, seriesB, n);
        }

        public static double Pearson(double[] seriesA, double[] seriesB)
        {
            int n = seriesA.Length;
            return Linnarsson.Mathematics.Correlation.pearsoncorrelation(ref seriesA, ref seriesB, n);
        }

    }

}
