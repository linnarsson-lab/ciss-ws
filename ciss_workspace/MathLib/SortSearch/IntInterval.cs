using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
    [Serializable]
    public class IntInterval
    {
        public int Start { get; set; }
        public int End { get; set; }

        public IntInterval(int start, int end)
        {
            Start = start;
            End = end;
        }

        public bool Intersects(IInterval other)
        {
            if (Start > other.End || End < other.Start) return false;
            return true;
        }

        public bool Contains(int pt)
        {
            if (Start <= pt && End >= pt) return true;
            return false;
        }

        public long Length { get { return End - Start + 1; } }
    }

    /// <summary>
    /// An interval with an associated item
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class IntInterval<T> : IntInterval
    {
        public T Item { get; set; }
        public int ExtraData;

        public IntInterval(int start, int end, T item, int extraData)
            : base(start, end)
        {
            Item = item;
            ExtraData = extraData;
        }
    }
}
