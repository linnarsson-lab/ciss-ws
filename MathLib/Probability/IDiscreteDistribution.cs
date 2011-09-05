using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IDiscreteDistribution : IProbabilityDistribution
	{
		double PDF(int x);
		double CDF(int x);
		int Sample();
		int Sample(IRandomNumberGenerator rnd);
	}
}
