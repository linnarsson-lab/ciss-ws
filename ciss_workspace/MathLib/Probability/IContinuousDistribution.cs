using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IContinuousDistribution : IProbabilityDistribution
	{
		double PDF(double x);
		double CDF(double x);
		double Sample();
		double Sample(IRandomNumberGenerator rnd);
	}
}
