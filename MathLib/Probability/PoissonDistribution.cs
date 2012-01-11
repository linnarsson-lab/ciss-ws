using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class PoissonDistribution : IDiscreteDistribution
	{
		private double m_Lambda;
		public double Lambda
		{
			get { return m_Lambda; }
			set { m_Lambda = value; }
		}
	
		public PoissonDistribution(double lambda)
		{
			m_Lambda = lambda;
		}

		#region IDiscreteDistribution Members

		public double PDF(int x)
		{
			if (x < 0) return 0;

			if (Lambda <= 40)
			{
				return Math.Exp(-Lambda) * Math.Pow(Lambda, x) / SpecialFunctions.Factorial(x);
			}

			// Work in log units to avoid overflow
			// Exp[-lambda + x Log[lambda] - LogGamma[x+1]]
			return Math.Exp(-Lambda + x * Math.Log(Lambda) - SpecialFunctions.LogGamma(x + 1));
		}

		public double LogPDF(int x)
		{
			return -Lambda + x * Math.Log(Lambda) - SpecialFunctions.LogGamma(x + 1);

		}

		/// <summary>
		/// CDF for values up to x *inclusive*.
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public double CDF(int x)
		{
			if (x < 0) return 0;
			return SpecialFunctions.RegularizedIncompleteGammaQ(x+1, Lambda);
		}

		public int Sample()
		{
			return poissonSampler(MersenneTwister.Instance);
		}

		public int Sample(IRandomNumberGenerator rnd)
		{
			return poissonSampler(rnd);
		}

		private double sq, temp0, temp1, oldLambda = -1.0;
		private int poissonSampler(IRandomNumberGenerator rnd)
		{
			double result,t,y;

			if (Lambda < 12.0) {
				if (Lambda != oldLambda)
				{
					oldLambda = Lambda;
					temp1 = Math.Exp(-Lambda);
				}
				result = -1;
				t=1.0;
				do {
					++result;
					t *= rnd.NextDouble();
				} while (t > temp1);
			} else {
				if (Lambda != oldLambda) {
					oldLambda = Lambda;
					sq = Math.Sqrt(2.0 * Lambda);
					temp0 = Math.Log(Lambda);
					temp1 = Lambda * temp0 - SpecialFunctions.LogGamma(Lambda + 1.0);
				}
				do {
					do {
						y=Math.Tan(Math.PI*rnd.NextDouble());
						result = sq * y + Lambda;
					} while (result < 0.0);
						result=Math.Floor(result);
						t=0.9*(1.0+y*y)*Math.Exp(result*temp0-SpecialFunctions.LogGamma(result+1.0)-temp1);
				} while (rnd.NextDouble() > t);
			}
			return (int)result;
		}
#endregion

		#region IProbabilityDistribution Members

		public double Mean
		{
			get { return Lambda; }
		}

		public double Variance
		{
			get { return Lambda; }
		}

		#endregion
	}
}
