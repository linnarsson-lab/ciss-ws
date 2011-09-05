using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class PolynomialInterpolation : IInterpolation
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

		private double[] m_XValues;
		private double[] m_YValues;
		public double this[double x]
		{
			get
			{
				int N = m_YValues.Length;
				double y = 0.0;
				for (int yIndex = 0; yIndex < N; yIndex++)
				{
					double numerator = 1.0;
					double denominator = 1.0;
					for (int xIndex = 0; xIndex < N; xIndex++)
					{
						if (xIndex == yIndex) continue;
						numerator *= (x - m_XValues[xIndex]);
						denominator *= (m_XValues[yIndex] - m_XValues[xIndex]);
					}
					y += numerator / denominator * m_YValues[yIndex];
				}
				return y;
			}
		}

		/// <summary>
		/// Gives the interpolated value at x, of a polynomial of degree N-1, given N data points. 
		/// The method uses Lagrange's formula directly.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="xValues"></param>
		/// <param name="yValues"></param>
		/// <returns></returns>
		public PolynomialInterpolation(double[] xValues, double[] yValues)
		{
			Construct(xValues, yValues);
		}

		public void Construct(double[] xValues, double[] yValues)
		{
			this.m_XValues = xValues;
			this.m_YValues = yValues;
			for (int ix = 0; ix < xValues.Length; ix++)
			{
				m_Max = Math.Max(m_Max, xValues[ix]);
				m_Min = Math.Min(m_Min, xValues[ix]);
			}
			if (m_XValues.Length != m_YValues.Length) throw new ArgumentException("There must be the same number if x and y values for interpolation!");
		}

		/// <summary>
		/// Creates an empty interpolation, which must then be constructed using Construct().
		/// </summary>
		public PolynomialInterpolation() { }
	}
}
