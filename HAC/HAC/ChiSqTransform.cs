using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    /// <summary>
    /// Transform data points to square_of(deviation from expected value) divided by expected value,
    /// where expected value is the row mean scaled to the relative total of the column.
    /// </summary>
    public class ChiSqTransform : Transformation
    {
        public override void Transform(Element[] elements)
        {
            double[] rowSums, colSums;
            GetSums(elements, out rowSums, out colSums);
            double totSum = colSums.Sum();
            int nPoints = elements[0].DataPointCount;
            int nValues = elements.Length;
            for (int vIdx = 0; vIdx < nPoints; vIdx++)
            {
                for (int eIdx = 0; eIdx < nValues; eIdx++)
                {
                    double E = colSums[eIdx] * rowSums[vIdx] / totSum;
                    double delta = (double)elements[eIdx][vIdx] - E;
                    double newVal = (delta * delta) / E;
                    elements[eIdx][vIdx] = newVal;
                }
            }
        }

        private void GetSums(Element[] elements, out double[] rowSums, out double[] colSums)
        {
            int nPoints = elements[0].DataPointCount;
            colSums = new double[elements.Length];
            rowSums = new double[nPoints];
            for (int eIdx = 0; eIdx < elements.Length; eIdx++)
                for (int vIdx = 0; vIdx < nPoints; vIdx++)
                {
                    colSums[eIdx] += (double)elements[eIdx][vIdx];
                    rowSums[vIdx] += (double)elements[eIdx][vIdx];
                }
        }

    }
}
