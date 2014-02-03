using System;
using System.Collections.Generic;

namespace Linnarsson.Mathematics
{
    /// <summary>
    /// Use to put objects defined by integer ranges (genomic coordinates) for quick access and iteration
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class IntervalMap<T>
    {
        public int BinSize { get; set; }
        List<List<ISmallInterval>> bins;

        public IntervalMap(int binSize)
        {
            bins = new List<List<ISmallInterval>>(300000000 / binSize);
            BinSize = binSize;
        }

        public void Add(ISmallInterval item)
        {
            int startBin = item.Start / BinSize;
            int endBin = item.End / BinSize;
            while (bins.Count <= endBin)
            {
                bins.Add(new List<ISmallInterval>());
            }
            for (int i = startBin; i < endBin + 1; i++)
            {
                if (bins[i] == null) bins[i] = new List<ISmallInterval>();
                bins[i].Add(item);
            }
        }
        public void Add(int start, int end, T item)
        {
            SmallInterval<T> it = new SmallInterval<T>(start, end, item);
            Add(it);
        }

        public IEnumerable<ISmallInterval> IterItems(int pos)
        {
            int bin = pos / BinSize;
            if (bin >= bins.Count) yield break;
            foreach (ISmallInterval item in bins[bin])
                if (item.Contains(pos)) yield return item;
        }
    }
}
