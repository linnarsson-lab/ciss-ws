 #region Using directives

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace Linnarsson.InstrumentControl
{

	public class Position3D<T>
	{	
		public T X { get; set; }
		public T Y { get; set; }
		public T Z { get; set; }

		public Position3D()
		{

		}
		public Position3D(T x, T y, T z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public override string ToString()
		{
			return "(" + X.ToString() + ", " + Y.ToString() + ", " + Z.ToString() + ")";
		}
	}
}
