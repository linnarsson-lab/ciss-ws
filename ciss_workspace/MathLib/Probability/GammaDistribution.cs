using System;
using System.Collections.Generic;
using System.Text;


namespace Linnarsson.Mathematics
{
	public struct GammaDistribution : IContinuousDistribution
	{
		private double m_Alpha;
		public double Alpha
		{
			get { return m_Alpha; }
			set { m_Alpha = value; }
		}

		private double m_Beta;
		public double Beta
		{
			get { return m_Beta; }
			set { m_Beta = value; }
		}


		public GammaDistribution(double alpha, double beta)
		{
			m_Alpha = alpha;
			m_Beta = beta;
		}

		public double PDF(double x)
		{
			if(x < 0) return 0d;
			// x^(alpha-1) Exp[-x/beta] / (beta^alpha Gamma[alpha]
			return Math.Pow(x, Alpha - 1) * Math.Exp(-x / Beta) / (Math.Pow(Beta, Alpha) * SpecialFunctions.Gamma(Alpha));
		}

		public double CDF(double x)
		{
			if(x < 0) return 0d;
			// GammaRegularized[alpha, 0, x/beta]
			return (SpecialFunctions.RegularizedIncompleteGammaP(Alpha, 0) - SpecialFunctions.RegularizedIncompleteGammaP(Alpha, x/Beta))/SpecialFunctions.Gamma(Alpha);
		}

		public double Sample()
		{
			return Sample(MersenneTwister.Instance);
		}

		public double Sample(IRandomNumberGenerator rnd)
		{
			if(Alpha < 1.0) return SampleGS(rnd);
			else return SampleKnuth(rnd);
		}

		// Mathematica algorithm for A < 1.0 (original algorithm by Ahrens)
		//
		//    gsGamma = Compile[{{alpha, _Real}, {r, _Real}},
		//Module[{x = 1.0, t = 1.0, q = (1 + r) Random[]},
		//      If[q < 1,
		//         x = q^(1/alpha);  t = Exp[-x],
		//         x = 1 - Log[1 + (1-q)/r];  t = x^(alpha-1)  ];
		//  While[Random[] > t,
		//    q = (1 + r) Random[];
		//            If[q < 1,
		//               x = q^(1/alpha);  t = Exp[-x],
		//               x = 1 - Log[1 + (1-q)/r];  t = x^(alpha-1)  ]	];
		//  x
		//]	]
		private double SampleGS(IRandomNumberGenerator rnd)
		{
			double x = 1d, t = 1d, r = Alpha/Math.E;

			// Initialize
			double q = (1d + r) * rnd.NextDouble();
			if(q < 1)
			{
				x = Math.Pow(q, 1/Alpha);
				t = Math.Exp(-x);
				x = 1 - Math.Log(1 + (1-q)/r);
				t = Math.Pow(x, Alpha-1);
			}

			// Repeat until not rejected
			while(rnd.NextDouble() > t)
			{
				q = (1d + r) * rnd.NextDouble();
				if(q < 1)
				{
					x = Math.Pow(q, 1/Alpha);
					t = Math.Exp(-x);
					x = 1 - Math.Log(1 + (1-q)/r);
					t = Math.Pow(x, Alpha-1);
				}
			}
			return x*Beta;
		}


		// original algorithm due to Ahrens
		// this is Knuth's version as described in Numerical recipes
		public double SampleKnuth(IRandomNumberGenerator rnd)
		{
			int j;
			double am,e,s,v1,v2,x,y;

			if (Alpha < 6) 
			{
				x=1.0;
				for (j=1;j<=Alpha;j++) x *= rnd.NextDouble();
				x = -Math.Log(x);
			} 
			else 
			{
				do 
				{
					do 
					{
						do 
						{
							v1 = 2.0 * rnd.NextDouble() - 1.0;
							v2 = 2.0 * rnd.NextDouble() - 1.0;
						} while (v1*v1+v2*v2 > 1.0);
						y=v2/v1;
						am=Alpha-1;
						s=Math.Sqrt(2.0*am+1.0);
						x=s*y+am;
					} while (x <= 0.0);
					e=(1.0+y*y)*Math.Exp(am*Math.Log(x/am)-s*y);
				} while(rnd.NextDouble() > e);
			}
			return x * Beta;
		}
		


		public double Mean
		{
			get { return Alpha * Beta; }
		}

		public double Variance
		{
			get { return Alpha * Beta * Beta; }
		}

	}
}
