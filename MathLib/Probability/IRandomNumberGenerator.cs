using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IRandomNumberGenerator
	{
		/// <summary>
		/// Returns a random number in [0,1).
		/// </summary>
		/// <returns></returns>
		double NextDouble();

		/// <summary>
		/// Returns an unsigned 32-bit random number 
		/// </summary>
		/// <returns></returns>
		uint NextUInt32();
	}
}
