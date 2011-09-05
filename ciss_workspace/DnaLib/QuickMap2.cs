using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Only reason for using this class instead of QuickAnnotationMap is to avoid
    /// mono's limitation on number of objects (FtIntervals in this case) on the heap.
    /// Only gives a slight improvment.
    /// </summary>
    public class QuickMap2
    {
        public int BinSize { get; set; }

        private List<List<int>> binnedStarts;
        private List<List<int>> binnedEnds;
        private List<List<DoMarkHit>> binnedMethods;
        private List<List<ushort>> binnedExtras;

        public QuickMap2(int binSize)
        {
            BinSize = binSize;
            binnedStarts = new List<List<int>>();
            binnedEnds = new List<List<int>>();
            binnedMethods = new List<List<DoMarkHit>>();
            binnedExtras = new List<List<ushort>>();
        }

        public void Add(int start, int end, DoMarkHit method, int extra)
        {
            int startBin = start / BinSize;
            int endBin = end / BinSize;
            while (binnedStarts.Count <= endBin)
            {
                binnedStarts.Add(new List<int>());
                binnedEnds.Add(new List<int>());
                binnedMethods.Add(new List<DoMarkHit>());
                binnedExtras.Add(new List<ushort>());
            }
            for (int i = startBin; i < endBin + 1; i++)
            {
                if (binnedStarts[i] == null)
                {
                    binnedStarts[i] = new List<int>();
                    binnedEnds[i] = new List<int>();
                    binnedMethods[i] = new List<DoMarkHit>();
                    binnedExtras[i] = new List<ushort>();
                }
                binnedStarts[i].Add(start);
                binnedEnds[i].Add(end);
                binnedMethods[i].Add(method);
                binnedExtras[i].Add((ushort)extra);
            }
        }

        public IEnumerable<FtInterval> GetItems(int pos)
        {
            int bin = pos / BinSize;
            if (bin >= binnedStarts.Count) yield break;
            var s = binnedStarts[bin];
            var e = binnedEnds[bin];
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i] <= pos && e[i] >= pos) 
                    yield return new FtInterval(s[i], e[i], binnedMethods[bin][i], (int)binnedExtras[bin][i]);
            }
        }

    }
}
