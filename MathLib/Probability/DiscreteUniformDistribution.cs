using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class DiscreteUniformDistribution : IDiscreteDistribution
	{
		private int m_UpperLimit;
		public int UpperLimit
		{
			get { return m_UpperLimit; }
		}

		private int m_LowerLimit;
		public int LowerLimit
		{
			get { return m_LowerLimit; }
		}

		private int m_Range;
		public int Range
		{
			get { return m_Range; }
		}
	
		public DiscreteUniformDistribution(int lower, int upper)
		{
			m_LowerLimit = lower;
			m_UpperLimit = upper;
			m_Range = upper - lower;
		}

		#region IDiscreteDistribution Members

		public double PDF(int x)
		{
			if (x < LowerLimit) return 0;
			if (x > UpperLimit) return 0;
			return 1 / (double)Range;
		}

		public double CDF(int x)
		{
			if (x < LowerLimit) return 0;
			if (x >= UpperLimit) return 1;
			return (x - LowerLimit) / (double)Range;
		}

		public int Sample()
		{
			return (int)(MersenneTwister.Instance.NextUInt32() % Range + LowerLimit);
		}

		public int Sample(IRandomNumberGenerator rnd)
		{
			return (int)(rnd.NextUInt32() % Range + LowerLimit);
		}

		#endregion

		#region IProbabilityDistribution Members

		public double Mean
		{
			get { return 1 / (double)(Range + 1); }
		}

		public double Variance
		{
			get { return (Range + 1)*(Range -1)/(double)12; }
		}

		#endregion
	}
}
