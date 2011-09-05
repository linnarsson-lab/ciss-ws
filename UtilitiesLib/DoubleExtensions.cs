using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Utilities
{
	public static class DoubleExtensions
	{
		public static string ToSiUnitString(this double number)
		{
			if(number >= 1000)
			{
				int power = 0;
				double remainder = number;
				while(remainder >= 1000)
				{
					power++;
					remainder /= 1000;
				}
				if(power > 6) return number.ToString();
				return Math.Round(remainder).ToString() + ".kMGTPE"[power];
			}

			if(number <= 0.001)
			{
				int power = 0;
				double remainder = number;
				while(remainder <= 0.001)
				{
					power++;
					remainder *= 1000;
				}
				if(power > 6) return number.ToString();
				return Math.Round(remainder*1000).ToString() + "mµnpfa"[power];
			}

			return number.ToString();
		}
	}
}
