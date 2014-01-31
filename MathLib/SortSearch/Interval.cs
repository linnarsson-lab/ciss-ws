using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
	public interface IInterval
	{
		long Start { get;  }
		long End { get;  }
		bool Intersects(IInterval other);
		bool Contains(long pt);
	}

	[Serializable]
	public class Interval : IInterval
	{
		public long Start { get; set; }
		public long End { get; set; }

		public Interval(long start, long end)
		{
			Start = start;
			End = end;
		}

		public bool Intersects(IInterval other)
		{
			if(Start > other.End || End < other.Start) return false;
			return true;
		}

		public bool Contains(long pt)
		{
			if(Start <= pt && End >= pt) return true;
			return false;
		}

		public long Length { get { return End - Start + 1; } }
	}

	/// <summary>
	/// An interval with an associated item
	/// </summary>
	/// <typeparam name="T"></typeparam>
	[Serializable]
	public class Interval<T> : Interval
	{
		public T Item { get; set; }

		public Interval(long start, long end, T item)
			: base(start, end)
		{
			Item = item;
		}
	}

    public interface ISmallInterval
    {
        int Start { get; }
        int End { get; }
        bool Intersects(ISmallInterval other);
        bool Contains(long pt);
    }

    [Serializable]
    public class SmallInterval : ISmallInterval
    {
        public int Start { get; set; }
        public int End { get; set; }

        public SmallInterval(int start, int end)
        {
            Start = start;
            End = end;
        }

        public bool Intersects(ISmallInterval other)
        {
            if (Start > other.End || End < other.Start) return false;
            return true;
        }

        public bool Contains(long pt)
        {
            if (Start <= pt && End >= pt) return true;
            return false;
        }

        public long Length { get { return End - Start + 1; } }
    }

    [Serializable]
    public class SmallInterval<T> : SmallInterval
    {
        public T Item { get; set; }

        public SmallInterval(int start, int end, T item)
            : base(start, end)
        {
            Item = item;
        }
    }

}
