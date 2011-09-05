using System;
using System.Collections.Generic;
using System.Text;


namespace Linnarsson.Mathematics
{
	public struct FDistribution : IContinuousDistribution
	{
		private double m_DF1;
		public double DF1
		{
			get { return m_DF1; }
			set { m_DF1 = value; }
		}

		private double m_DF2;
		public double DF2
		{
			get { return m_DF2; }
			set { m_DF2 = value; }
		}
	
		/// <summary>
		/// Creates an F distribution with df1 and df2 degrees of freedom for the numerator and
		/// denominator, respectively
		/// </summary>
		/// <param name="df"></param>
		public FDistribution(double df1, double df2)
		{
			m_DF1 = df1;
			m_DF2 = df2;
		}

		public double PDF(double f)
		{
			// n1^(n1/2) n2^(n2/2) x^(n1/2 - 1)/((n2 + n1 x)^((n1 + n2)/2) Beta[n1/2, n2/2])
			double num = Math.Pow(DF1, DF1 / 2) * Math.Pow(DF2, DF2 / 2) * Math.Pow(f, DF1 / 2 - 1);
			double denom = Math.Pow(DF2 + DF1 * f, (DF1 + DF2) / 2) * SpecialFunctions.Beta(DF1 / 2, DF2 / 2);
			return num / denom;
		}

		public double CDF(double f)
		{
			if(f == 0.0) return 0.0d;

			return SpecialFunctions.IncompleteBeta(DF2/2, DF1/2, DF2/(DF2 + DF1*f));
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			double chi1 = new ChiSquareDistribution(DF1).Sample(rnd);
			double chi2 = new ChiSquareDistribution(DF2).Sample(rnd);

			return DF2 / DF1 * chi1 / chi2;
		}


		public double Mean
		{
			get
			{
				// n2/(n2 - 2) 
				return DF2 / (DF2 - 2);
			}
		}

		public double Variance
		{
			get
			{
				// 2 n2^2 (n1+n2-2) / (n1 (n2-2)^2 (n2-4)) 
				return 2 * DF2 * DF2 * (DF1 + DF2 - 2) / (DF1 * (DF2 - 2) * (DF2 - 2) * (DF2 - 4));
			}
		}
	}
}
