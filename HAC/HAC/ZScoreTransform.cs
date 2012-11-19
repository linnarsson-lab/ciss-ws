using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    /// <summary>
    /// Center every row to mean = 0.0 and standard deviation = 1.0
    /// </summary>
    public class ZScoreTransform : Transformation
    {
        public override void Transform(Element[] elements)
        {
            int nValues = elements.Length;
            int nPoints = elements[0].DataPointCount;
            for (int vIdx = 0; vIdx < nPoints; vIdx++)
            {
                double n = 0.0;
                double mean = 0.0;
                double M2 = 0.0;
                for (int eIdx = 0; eIdx < nValues; eIdx++)
                {
                    double v = (double)elements[eIdx][vIdx];
                    n += 1.0;
                    double delta = v - mean;
                    mean += delta / n;
                    M2 += delta * (v - mean);
                }
                double variance = M2 / (n - 1);
                double stddev = Math.Sqrt(variance);
                for (int eIdx = 0; eIdx < nValues; eIdx++)
                {
                    elements[eIdx][vIdx] = ((double)elements[eIdx][vIdx] - mean) / stddev;
                }
            }
        }
    }
}
