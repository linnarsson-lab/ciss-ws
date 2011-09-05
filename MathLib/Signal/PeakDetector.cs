using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics.Signal
{
    public class PeakDetector
    {
        private int minWidth;

        public PeakDetector(int minWidth)
        {
            this.minWidth = minWidth;
        }

        public int[] FindPeakMaxima(double[] data)
        {
            double[] firstDerivate = GetDerivate(data);
            return FindIntersections(firstDerivate);
        }

        public int[] FindInflections(double[] data)
        {
            double[] firstDerivate = GetDerivate(data);
            double[] secondDerivate = GetDerivate(firstDerivate);
            return FindIntersections(secondDerivate);
        }

        public double[] GetDerivate(double[] data)
        {
            double[] derivate = new double[data.Length - 1];
            for (int i = 0; i < data.Length - 1; i++)
                derivate[i] = data[i + 1] - data[i];
            return derivate;
        }

        public int[] FindIntersections(double[] firstDerivate)
        {
            List<int> maxima = new List<int>();
            int i = 0;
            int max = firstDerivate.Length - 1;
            while (i < max)
            {
                int positiveSlopeLength = 0;
                while (i < max && (firstDerivate[i] >= 0.0 || firstDerivate[i + 1] >= 0.0))
                {
                    positiveSlopeLength++;
                    i++;
                }
                int intersection;
                if (firstDerivate[i - 1] < 0.0) intersection = i - 1;
                else intersection = i;
                int negativeSlopeLength = 0;
                while (i < max && (firstDerivate[i] < 0.0 || firstDerivate[i + 1] < 0.0))
                {
                    negativeSlopeLength++;
                    i++;
                }
                if (positiveSlopeLength >= minWidth && negativeSlopeLength >= minWidth)
                    maxima.Add(intersection);
            }
            return maxima.ToArray();
        }

        public int[] FindPeakMaxima(int[] data)
        {
            int[] firstDerivate = GetDerivate(data);
            return FindIntersections(firstDerivate);
        }

        public int[] GetDerivate(int[] data)
        {
            int[] derivate = new int[data.Length - 1];
            for (int i = 0; i < data.Length - 1; i++)
                derivate[i] = data[i + 1] - data[i];
            return derivate;
        }

        public int[] FindIntersections(int[] firstDerivate)
        {
            List<int> maxima = new List<int>();
            int i = 0;
            int max = firstDerivate.Length;
            while (i < max)
            {
                int positiveSlopeLength = 0;
                while (i < max && firstDerivate[i++] >= 0)
                    positiveSlopeLength++;
                int intersection = i - 1;
                int negativeSlopeLength = 0;
                while (i < max && firstDerivate[i++] < 0)
                    negativeSlopeLength++;
                if (positiveSlopeLength >= minWidth && negativeSlopeLength >= minWidth)
                    maxima.Add(intersection);
            }
            return maxima.ToArray();
        }

    }
}
