using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace Linnarsson.Mathematics
{
	public interface IInterpolation2D
	{
		double this[double x, double y] { get; }
	}
}
