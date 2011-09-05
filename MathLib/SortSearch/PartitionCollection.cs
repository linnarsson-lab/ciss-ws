using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IPartitionCollection<T> where T : IComparable<T>
	{
		T FindStart(T position);
		IEnumerable<T> GetIntervalStarts();
		int Count { get; }
	}

	/// <summary>
	/// A collection of elements associated with distinct intervals on a range,
	/// for example intervals on the real number line. The range is fully tiled by
	/// the elements.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <typeparam name="U"></typeparam>
	[Serializable]
	public class PartitionCollection<T, U> : IPartitionCollection<T> where T : IComparable<T>
	{
		protected List<T> intervalStarts = new List<T>();
		protected List<U> elements = new List<U>();

		public int Count { get { return intervalStarts.Count; } }

		public void Add(T start, U element)
		{
			intervalStarts.Add(start);
			elements.Add(element);
		}

		public U Find(T position)
		{
			int pos = intervalStarts.BinarySearch(position);
			if(pos < 0) return elements[(~pos) - 1];
			else return elements[pos];
		}

		public T FindStart(T position)
		{
			int pos = intervalStarts.BinarySearch(position);
			if(pos < 0) return intervalStarts[(~pos) - 1];
			return intervalStarts[pos];
		}

		public IEnumerable<T> GetIntervalStarts()
		{
			foreach(T elm in intervalStarts) yield return elm;
		}

		public IEnumerable<U> Values
		{
			get
			{
				foreach(U val in elements) yield return val;
			}
		}
	}
}
