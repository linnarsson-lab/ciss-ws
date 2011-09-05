using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IProbabilityDistribution
	{
		double Mean
		{
			get;
		}

		double Variance
		{
			get;
		}
	}
}
