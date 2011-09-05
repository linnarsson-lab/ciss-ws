using System;
using System.Collections.Generic;
using System.Text;


namespace Linnarsson.Mathematics
{
	public struct ChiSquareDistribution : IContinuousDistribution
	{
		private double m_DF;
		public double DF
		{
			get { return m_DF; }
			set { m_DF = value; }
		}

		public ChiSquareDistribution(double df)
		{
			m_DF = df;
		}

		public double PDF(double chiSquare)
		{
			if(chiSquare < 0d) return 0d;

			// x^(n/2-1) / (Exp[x/2] Sqrt[2]^n Gamma[n/2]) 
			return Math.Pow(chiSquare, DF / 2d - 1) / (Math.Exp(chiSquare / 2d) * Math.Pow(Math.Sqrt(2d), DF) * SpecialFunctions.Gamma(DF / 2));
		}

		public double CDF(double chiSquare)
		{
			if(chiSquare == 0d) return 0d;
			if(double.IsPositiveInfinity(chiSquare)) return 1.0d;

			return SpecialFunctions.RegularizedIncompleteGammaP(DF / 2d, chiSquare / 2);
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			return new GammaDistribution(DF/2d, 2d).Sample(rnd);
		}

		public double Mean
		{
			get { return DF; }
		}

		public double Variance
		{
			get { return 2 * DF; }
		}
	}
}
