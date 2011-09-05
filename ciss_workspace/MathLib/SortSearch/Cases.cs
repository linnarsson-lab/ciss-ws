using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public delegate bool Selector<T>(T item);
	
	public class Cases
	{
		public static T[] Select<T>(T[] array, Selector<T> selector)
		{
			T[] result = new T[array.Length];
			int count = 0;
			for (int ix = 0; ix < array.Length; ix++)
			{
				if (selector(array[ix])) result[count++] = array[ix];
			}
			Array.Resize(ref result, count);
			return result;
		}
		public static List<T> Select<T>(IList<T> array, Selector<T> selector)
		{
			List<T> result = new List<T>();
			for (int ix = 0; ix < array.Count; ix++)
			{
				if (selector(array[ix])) result.Add(array[ix]);
			}
			return result;
		}
	}
}
