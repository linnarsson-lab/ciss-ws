using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class GCAnalyzer
    {
        private int[] GCCounts;
        private int[] totalCounts;

        public GCAnalyzer(int nBarcodes)
        {
            GCCounts = new int[nBarcodes];
            totalCounts = new int[nBarcodes];
        }

        public void Add(int bcIdx, DnaSequence readSeq)
        {
            int nGC = (int)readSeq.CountCases(IupacEncoding.GC);
            totalCounts[bcIdx] += (int)readSeq.Count;
            GCCounts[bcIdx] += nGC;
        }

        public int[] GetPercentGCByBarcode()
        {
            int[] percents = new int[GCCounts.Length];
            for (int bcIdx = 0; bcIdx < GCCounts.Length; bcIdx++)
                percents[bcIdx] = (int)Math.Round(100.0 *GCCounts[bcIdx] / (double)totalCounts[bcIdx]);
            return percents;
        }
    }
}
