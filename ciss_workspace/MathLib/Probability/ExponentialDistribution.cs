using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
	public struct ExponentialDistribution : IContinuousDistribution
	{
		private double m_Lambda;
		public double Lambda
		{
			get { return m_Lambda; }
			set { m_Lambda = value; }
		}

		public ExponentialDistribution(double lambda)
		{
			m_Lambda = lambda;
		}

		public double PDF(double x)
		{
			if(x < 0d) return 0d;

			return Lambda * Math.Exp(-Lambda * x);
		}

		public double CDF(double x)
		{
			if(x < 0d) return 0d;

			return 1 - Math.Exp(-Lambda * x);
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			return -Math.Log(rnd.NextDouble())/Lambda;
		}

		public double Mean
		{
			get { return 1d/Lambda; }
		}

		public double Variance
		{
			get { return 1d/(Lambda*Lambda); }
		}
	}
}
