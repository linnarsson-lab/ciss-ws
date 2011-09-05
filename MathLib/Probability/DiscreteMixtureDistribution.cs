using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics.Probability
{
	public struct DiscreteMixtureDistribution : IDiscreteDistribution
	{
		private IDiscreteDistribution[] m_Components;
		public IDiscreteDistribution[] Components
		{
			get { return m_Components; }
		}

		private double[] m_Weights;
		public double[] Weights
		{
			get { return m_Weights; }
		}

		public DiscreteMixtureDistribution(IDiscreteDistribution[] components, double[] weights)
		{
			if(weights.Length != components.Length) throw new ArgumentException();
			if(Math.Abs(new MatrixDouble(1, weights.Length).RowSum(0) - 1d) > 1e-6) throw new ArgumentException();

			m_Components = components;
			m_Weights = weights;

		}

		public double PDF(int x)
		{
			double density = 0;
			for(int ix = 0; ix < Components.Length; ix++)
			{
				density += Components[ix].PDF(x);
			}
			return density;
		}

		public double CDF(int x)
		{
			double density = 0;
			for(int ix = 0; ix < Components.Length; ix++)
			{
				density += Components[ix].CDF(x);
			}
			return density;
		}

		public int Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		/// <summary>
		/// Returns a sample from the mixture. Assumes that sum(weights) == 1.
		/// </summary>
		/// <param name="rnd"></param>
		/// <returns></returns>
		public int Sample(IRandomNumberGenerator rnd)
		{
			double weight = rnd.NextDouble();
			int ix;
			for(ix = 0; ix < Components.Length; ix++)
			{
				weight -= Weights[ix];
				if(weight < 0) break;
			}
			return Components[ix].Sample(rnd);
		}

		public double Mean
		{
			get { throw new Exception("The method or operation is not implemented."); }
		}

		public double Variance
		{
			get { throw new Exception("The method or operation is not implemented."); }
		}

	}
}
