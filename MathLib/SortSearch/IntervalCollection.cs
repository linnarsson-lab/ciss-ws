using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
    public interface IIntervalCollection<T> where T : IComparable<T>
    {
        IEnumerable<T> GetIntervalStarts();
        int Count { get; }
    }

    /// <summary>
    /// A collection of elements associated with distinct intervals on a range,
    /// for example intervals on the real number line. Intervals are required to be non-overlapping.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U"></typeparam>
    [Serializable]
    public class IntervalCollection<T, U> : IIntervalCollection<T> where T : IComparable<T>
    {
        protected List<T> intervalStarts = new List<T>();
        protected List<T> intervalEnds = new List<T>();
        protected List<U> elements = new List<U>();

        public int Count { get { return intervalStarts.Count; } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="start">Start of interval</param>
        /// <param name="end">Inclusive end of interval</param>
        /// <param name="element"></param>
        public void Add(T start, T end, U element)
        {
            int idx = intervalStarts.BinarySearch(start);
            if (idx < 0) idx = ~idx;
            if (idx > 0 && intervalEnds[idx - 1].CompareTo(end) >= 0)
                throw new IndexOutOfRangeException("Trying to add overlapping interval to IntervalCollection: " + start + "-" + end);
            intervalStarts.Insert(idx, start);
            intervalEnds.Insert(idx, end);
            elements.Insert(idx, element);
        }

        public void AddWithAdjustment(T start, T end, U element, bool adjustStart)
        {
            int idx = intervalStarts.BinarySearch(start);
            if (idx < 0) idx = ~idx;
            if (idx > 0 && intervalEnds[idx - 1].CompareTo(end) >= 0)
            {
                if (adjustStart)
                    start = intervalEnds[idx - 1];
                else
                    intervalEnds[idx - 1] = start;
            }
            intervalStarts.Insert(idx, start);
            intervalEnds.Insert(idx, end);
            elements.Insert(idx, element);
        }

        public U Find(T position)
        {
            int idx = intervalStarts.BinarySearch(position);
            if (idx < 0) idx = (~idx) - 1;
            if (intervalEnds[idx].CompareTo(position) < 0) return default(U);
            return elements[idx];
        }

        public IEnumerable<T> GetIntervalStarts()
        {
            foreach (T elm in intervalStarts) yield return elm;
        }

        public IEnumerable<U> Values
        {
            get
            {
                foreach (U val in elements) yield return val;
            }
        }
    }
}
