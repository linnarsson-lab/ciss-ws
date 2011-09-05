using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Utilities
{
	public static class DateTimeExtensions
	{
		public static string ToPathSafeString(this DateTime dt)
		{
			return dt.ToString("yyyyMMdd_HHmmss");
		}
	}
}
