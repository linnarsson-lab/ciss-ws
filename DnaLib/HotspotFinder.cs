using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class HotspotFinder
    {
        private int maxCount;
        private int[] topCounts;
        private int[] topLocations;

        public HotspotFinder(int maxCount)
        {
            this.maxCount = maxCount;
            topCounts = new int[maxCount];
             topLocations = new int[maxCount];

        }

        public void Add(int count, int value)
        {
            int insP = Array.FindIndex(topCounts, (v) => (v < count));
            if (insP >= 0)
            {
                for (int i = maxCount - 1; i > insP; i--)
                {
                    topCounts[i] = topCounts[i - 1];
                    topLocations[i] = topLocations[i - 1];
                }
                topCounts[insP] = count;
                topLocations[insP] = value;
            }
        }

        public void GetTop(out int[] counts, out int[] values)
        {
            int n = Array.FindIndex(topCounts, (v) => (v == 0));
            if (n == -1) n = topCounts.Length;
            counts = new int[n];
            values = new int[n];
            Array.ConstrainedCopy(topCounts, 0, counts, 0, n);
            Array.ConstrainedCopy(topLocations, 0, values, 0, n);
        }
    }
}
