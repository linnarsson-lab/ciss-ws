using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class LocusHistogram
    {
        public static bool AnalyzeByBarcode = false;
        public static int basesPerBin = 50;
        public int[] binCounts;
        public ushort[,] binCountsByBarcode;
        private int nBins;

        public LocusHistogram(int maxGeneLength)
        {
            nBins = (int)(maxGeneLength / basesPerBin);
            binCounts = new int[nBins];
            if (AnalyzeByBarcode)
                binCountsByBarcode = new ushort[nBins, Barcodes.MaxCount];
        }

        public void MarkHit(int pos, int bcodeIdx)
        {
            int bin = (int)(pos / basesPerBin);
            if (bin < 0) bin = 0;
            if (bin >= nBins) bin = nBins - 1;
            binCounts[bin]++;
            if (AnalyzeByBarcode) binCountsByBarcode[bin, bcodeIdx]++;
        }

    }
}
