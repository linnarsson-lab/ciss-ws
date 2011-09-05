using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace Linnarsson.Utilities
{
	public static class ObjectExtensions
	{
		/// <summary>
		/// Return a nicely formatted table with all properties and their values, as a single string
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public static string ToPropertyTableString(this object obj)
		{
			// Find all the properties
			var props = from p in obj.GetType().GetProperties()
						select new { Property = p, ValueAsString = p.GetValue(obj, null).ToString() };

			StringBuilder sb = new StringBuilder();
			foreach (var p in props)
			{
				sb.Append(new string(' ', 20 - p.Property.Name.Length));
				sb.Append(p.Property.Name);
				sb.Append(": ");
				sb.AppendLine(p.ValueAsString);
			}
			return sb.ToString();
		}
	}
}
