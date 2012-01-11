using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public struct BinomialDistribution : IDiscreteDistribution
	{
		private int m_N;
		/// <summary>
		/// Gets or sets the total number of trials
		/// </summary>
		public int N
		{
			get { return m_N; }
			set { m_N = value; }
		}

		private double m_P;
		/// <summary>
		/// Gets or sets the probability of success in each trial
		/// </summary>
		public double P
		{
			get { return m_P; }
			set { m_P = value; }
		}

		public int Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public int Sample(IRandomNumberGenerator rnd)
		{
			return (int)rejectionSampler(P, N, rnd);
		}

		int nold;
		double pold, pc, plog, pclog, en, oldg;
		private double rejectionSampler(double pp, int n, IRandomNumberGenerator rnd)
		{
			int j;
			double am, em, g, angle, p, bnl, sq, t, y;
			p = (pp <= 0.5 ? pp : 1.0 - pp);
			am = n * p;
			if(n < 25)
			{
				bnl = 0.0;
				for(j = 1; j <= n; j++)
					if(rnd.NextDouble() < p) ++bnl;
			}
			else if(am < 1.0)
			{
				g = Math.Exp(-am);
				t = 1.0;
				for(j = 0; j <= n; j++)
				{
					t *= rnd.NextDouble();
					if(t < g) break;
				}
				bnl = (j <= n ? j : n);
			}
			else
			{
				if(n != nold)
				{
					en = n;
					oldg = SpecialFunctions.LogGamma(en + 1.0);
					nold = n;
				} if(p != pold)
				{
					pc = 1.0 - p;
					plog = Math.Log(p);
					pclog = Math.Log(pc);
					pold = p;
				}
				sq = Math.Sqrt(2.0 * am * pc);
				do
				{
					do
					{
						angle = Math.PI * rnd.NextDouble();
						y = Math.Tan(angle);
						em = sq * y + am;
					} while(em < 0.0 || em >= (en + 1.0));
					em = Math.Floor(em);
					t = 1.2 * sq * (1.0 + y * y) * Math.Exp(oldg - SpecialFunctions.LogGamma(em + 1.0)
						- SpecialFunctions.LogGamma(en - em + 1.0) + em * plog + (en - em) * pclog);
				} while(rnd.NextDouble() > t);
				bnl = em;
			}
			if(p != pp) bnl = n - bnl;
			return bnl;
		}   

		/// <summary>
		/// Returns the probability of getting x successes in N trials each with probability P.
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public double PDF(int x)
		{
			if(x < 0 || x > N) throw new ArgumentOutOfRangeException("BinomialDistribution.PDF: x must be >= 0 and <= N");
			if (N == 0 && x == 0) return 1;
			if(P == 0 && x == 0) return 1;
			if (P == 0) return 0;

			return SpecialFunctions.BinomialCoefficientContinuous(N, x) * Math.Pow(P, x) * Math.Pow(1d - P, N-x);
		}

		public double LogPDF(int x)
		{
			if (x < 0 || x > N) throw new ArgumentOutOfRangeException("BinomialDistribution.PDF: x must be >= 0 and <= N");
			if(N == 0 && x == 0) return 0;
			if(P == 0 && x == 0) return 0;
			if(P == 0) return double.NegativeInfinity;

			return SpecialFunctions.LogGamma(N + 1)
					- SpecialFunctions.LogGamma(x + 1)
					- SpecialFunctions.LogGamma(N - x + 1)
					+ x * Math.Log(P)
					+ (N - x) * Math.Log(1d - P);
		}

		public double CDF(int x)
		{
			if(x < 0 || x > N) throw new ArgumentOutOfRangeException("BinomialDistribution.CDF: x must be >= 0 and <= N"); 
			if(x == N) return 1.0;
			if(x == 0) return Math.Pow(1-P, N);
			if(x == 0 && P == 1) return 0;

			return SpecialFunctions.IncompleteBeta(N - x, x + 1, 1 - P);
		}

		public double Mean
		{
			get { return N*P; }
		}

		public double Variance
		{
			get { return N*P*(1-P); }
		}
	
		/// <summary>
		/// Creates a binomial distribution for n trials with probability p.
		/// </summary>
		/// <param name="n"></param>
		/// <param name="p"></param>
		public BinomialDistribution(int n, double p)
		{
			m_N = n;
			m_P = p;

			// Initialize the rejection sampler
			nold = -1;
			pold = -1;
			pc = 0;
			plog = 0;
			pclog = 0;
			en = 0;
			oldg = 0;
		}
	}
}
