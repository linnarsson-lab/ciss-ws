using System;
using System.Collections.Generic;
using System.Text;


namespace Linnarsson.Mathematics
{
	public struct NormalDistribution : IContinuousDistribution
	{
	
		public double PDF(double x)
		{
			double temp = (x - Mean) / Math.Sqrt(Variance);
			return (0.3989422804014326779399460599343818684759 / Math.Sqrt(Variance)) * Math.Exp(-temp * temp / 2);
		}

		public double CDF(double x)
		{
			double temp = Math.Sqrt(2) * Math.Sqrt(Variance);
			if((Mean - x) / temp < -1.55)
			{
				return SpecialFunctions.ErrorFunctionComplement((Mean - x) / temp) / 2;
			}
			else
			{
				return (SpecialFunctions.ErrorFunction((x - Mean) / temp) + 1) / 2;
			}
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			return normedSample(rnd) * Math.Sqrt(Variance) + Mean;
		}

		private bool hasSample;
		private double sample;
		/// <summary>
		/// Returns a sample from the normed normal distribution (mean 0 variance 1) using the Herschel 
		/// derivation (Herschel 1850 Edinburgh Rev 92:14) of the Gaussian.
		/// </summary>
		/// <param name="rnd"></param>
		/// <returns></returns>
		private double normedSample(IRandomNumberGenerator rnd) 
		{
			double factor, v1v2square, v1, v2;
			if(!hasSample) // we generate samples two at a time, so one may already be available
			{
				do
				{
					// make a random vector in the unit square [-1,1],[-1,1]
					v1 = 2.0 * rnd.NextDouble() - 1.0;
					v2 = 2.0 * rnd.NextDouble() - 1.0;

					// square it, i.e.: rsq = (v1^2 + v2^2)
					v1v2square = v1 * v1 + v2 * v2;
				} while(v1v2square >= 1.0 || v1v2square == 0.0); // avoid some pathological cases

				//         ______________
				// fac = \/-2ln(rsq)/rsq)
				factor = Math.Sqrt(-2.0 * Math.Log(v1v2square) / v1v2square);
				
				// sample = v1^2 * -2ln(v1^2 + v2^2)/(v1^2 + v2^2)
				sample = v1 * factor;
				hasSample = true;
				return v2 * factor;
			}
			else
			{
				hasSample = false;
				return sample;
			}
		}  

		private double m_Mean;
		public double Mean
		{
			get { return m_Mean; }
			set { m_Mean = value; }
		}

		private double m_Variance;
		public double Variance
		{
			get { return m_Variance; }
			set { m_Variance = value; }
		}
	
		public NormalDistribution(double mean, double var)
		{
			m_Mean = mean;
			m_Variance = var;

			// Initialize the sampler
			hasSample = false;
			sample = 0;
		}
	}
}
