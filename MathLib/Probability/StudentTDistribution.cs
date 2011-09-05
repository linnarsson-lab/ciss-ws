using System;
using System.Collections.Generic;
using System.Text;


namespace Linnarsson.Mathematics
{
	public struct StudentTDistribution : IContinuousDistribution
	{
		private double m_DF;
		public double DF
		{
			get { return m_DF; }
			set { m_DF = value; }
		}

		/// <summary>
		/// Creates a Student t distribution with df degrees of freedom
		/// </summary>
		/// <param name="df"></param>
		public StudentTDistribution(double df)
		{
			m_DF = df;
		}

		public double PDF(double t)
		{
			return 1.0d / (Math.Sqrt(DF) * SpecialFunctions.Beta(DF / 2.0d, 0.5d)) * Math.Pow(Math.Sqrt(DF / (DF + t * t)), DF + 1);
		}

		public double CDF(double t)
		{
			if(t == 0.0) return 0.0d;
			if(double.IsPositiveInfinity(t)) return 1.0d;
			return 1.0 - SpecialFunctions.IncompleteBeta(DF / 2.0, 0.5d, DF / (DF + t * t));
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			// Check arguments
			if(DF <= 0d) return double.NaN;

			// Get a sample from the normed normal distribution
			double normalSample = new NormalDistribution(0, 1).Sample(rnd);

			// Handle the infinite limit
			if(double.IsPositiveInfinity(DF)) return normalSample;
			else
			{
				return normalSample / (Math.Sqrt(new ChiSquareDistribution(DF).Sample(rnd)) / DF);
			}
		}


		public double Mean
		{
			get 
			{
				if(DF <= 1.0d) return double.NaN;
				else return 0.0d;
			}
		}

		public double Variance
		{
			get 
			{
				if(DF <= 2.0d) return double.NaN;
				else return DF / (DF - 2.0d);
			}
		}
	}
}
