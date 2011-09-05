using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class UniformDistribution : IContinuousDistribution
	{
		private double m_LowerLimit;
		public double LowerLimit
		{
			get { return m_LowerLimit; }
		}

		private double m_UpperLimit;
		public double UpperLimit
		{
			get { return m_UpperLimit; }
		}

		private double m_Range;
		public double Range
		{
			get { return m_Range; }
		}
	
		public UniformDistribution(double lower, double upper)
		{
			m_LowerLimit = lower;
			m_UpperLimit = upper;
			m_Range = UpperLimit - LowerLimit;
		}

		#region IContinuousDistribution Members

		public double PDF(double x)
		{
			if (x < LowerLimit) return 0;
			if (x >= UpperLimit) return 0;
			return 1/Range;
		}

		public double CDF(double x)
		{
			if (x < LowerLimit) return 0;
			if (x >= UpperLimit) return 1;
			return (x - LowerLimit) / Range;
		}

		public double Sample()
		{
			return MersenneTwister.Instance.NextDouble() * Range + LowerLimit;
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			return rnd.NextDouble() * Range + LowerLimit;
		}

		#endregion

		#region IProbabilityDistribution Members

		public double Mean
		{
			get { return Range / 2 + LowerLimit; }
		}

		public double Variance
		{
			get { return Range*Range / 12; }
		}

		#endregion
	}
}
