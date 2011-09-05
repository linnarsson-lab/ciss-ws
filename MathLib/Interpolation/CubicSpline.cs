using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class CubicSpline : IInterpolation
	{

		private double m_Max;
		public double Max
		{
			get { return m_Max; }
		}

		private double m_Min;
		public double Min
		{
			get { return m_Min; }
		}

		public bool CanInterpolate(double x)
		{
			return x >= Min && x <= Max;
		}

		private double[] m_SecondDerivatives;
		public double[] SecondDerivatives
		{
			get { return m_SecondDerivatives; }
			set { m_SecondDerivatives = value; }
		}

		private double[] m_XValues;
		public double[] XValues
		{
			get { return m_XValues; }
			set { m_XValues = value; }
		}

		private double[] m_YValues;
		public double[] YValues
		{
			get { return m_YValues; }
			set { m_YValues = value; }
		}

		public double this[double x]
		{
			get
			{
				int pos = Array.BinarySearch<double>(XValues, x);
				if (pos < 0)
				{
					pos = ~pos;
				}
				if (pos == 0) pos++;

				// pos is now the index of the first element larger than or equal to the given value
				if (pos >= XValues.Length) throw new ArgumentOutOfRangeException("X value is outside of the interpolated function");
				double h = XValues[pos] - XValues[pos - 1];
				if (h == 0) throw new ArgumentException("The X values used to construct the spline must all be distinct");
				double a = (XValues[pos] - x) / h;
				double b = (x - XValues[pos - 1]) / h;
				return a * YValues[pos - 1] + b * YValues[pos] + ((Math.Pow(a, 3) - a) * SecondDerivatives[pos - 1] + (Math.Pow(b, 3) - b) * SecondDerivatives[pos]) * (h * h) / 6d;
			}
		}

		/// <summary>
		/// Creates a (natural) cubic spline interpolating between the given data points. The spline
		/// is 'natural' in that the second derivatives are zero at the boundaries.
		/// </summary>
		/// <param name="xValues"></param>
		/// <param name="yValues"></param>
		public CubicSpline(double[] xValues, double[] yValues)
		{
			Construct(xValues, yValues);
		}
		public void Construct(double[] xValues, double[] yValues)
		{
			if (xValues.Length != yValues.Length) throw new ArgumentException("CubicSpline requires the same number of x and y values.");

			XValues = xValues;
			YValues = yValues;
			int N = xValues.Length;
			SecondDerivatives = new double[N];

			for (int ix = 0; ix < xValues.Length; ix++)
			{
				m_Max = Math.Max(m_Max, xValues[ix]);
				m_Min = Math.Min(m_Min, xValues[ix]);
			}

			// Solve the tridiagonal system to get the second derivatives
			double[] u = new double[N];
			for (int ix = 1; ix < N - 1; ix++)
			{
				double sig = (xValues[ix] - xValues[ix - 1]) / (xValues[ix + 1] - xValues[ix - 1]);
				double p = sig * SecondDerivatives[ix - 1] + 2d;
				SecondDerivatives[ix] = (sig - 1d) / p;
				u[ix] = (yValues[ix + 1] - yValues[ix]) / (xValues[ix + 1] - xValues[ix]) - (yValues[ix] - yValues[ix - 1]) / (xValues[ix] - xValues[ix - 1]);
				u[ix] = (6d * u[ix] / (xValues[ix + 1] - xValues[ix - 1]) - sig * u[ix - 1]) / p;
			}
			for (int ix = N-2; ix >= 0; ix--)
			{
				SecondDerivatives[ix] = SecondDerivatives[ix] * SecondDerivatives[ix + 1] + u[ix];
			}
		}

		/// <summary>
		/// Creates an empty interpolation, which must then be constructed using Construct().
		/// </summary>
		public CubicSpline() { }

        public double Integral()
        {
            return Integral(0, XValues.Length);
        }
        public double Integral(int startIndex, int stopIndex)
        {
            double sum = 0.0;
            for (int ix = startIndex + 1; ix < stopIndex; ix++)
            {
                double xp = XValues[ix];
                double xpm1 = XValues[ix-1];
                double yp = YValues[ix];
                double ypm1  = YValues[ix-1];
                double sp = SecondDerivatives[ix];
                double spm1 = SecondDerivatives[ix-1];
                double h = xp - xpm1;
                double A = (ypm1 * xp + yp * xpm1) / h + (sp * xpm1 - spm1 * xp) * h / 6;
                double B = (yp - ypm1) / h + (spm1 - sp) * h / 6;
                double intervalSum = A * (xp - xpm1) + B / 2 * (xp * xp - xpm1 * xpm1) + h * h * h / 24 * (sp - spm1);
                sum += intervalSum;
            }
            return sum;
        }

    }
}
