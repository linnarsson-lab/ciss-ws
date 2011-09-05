using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IInterpolation
	{
		double Max { get; }
		double Min { get; }
		bool CanInterpolate(double x);
		double this[double x] { get; }
		void Construct(double[] xValues, double[] yValues);
	}
}
