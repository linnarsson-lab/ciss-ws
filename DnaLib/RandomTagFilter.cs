using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RandomTagFilter
    {
        private Barcodes barcodes;
        private Dictionary<int, byte[]> molCounts;
        private int windowSize;
        private int windowStart;
        private int rndBcCount;
        private int posStrandDataSize;
        public int[] nReadsByRandomTag;
        public int[] nCasesPerRandomTagCount;

        public RandomTagFilter(Barcodes barcodes, int windowSize)
        {
            this.barcodes = barcodes;
            this.windowSize = windowSize;
            rndBcCount = barcodes.RandomBarcodeCount;
            posStrandDataSize = rndBcCount * barcodes.Count;
            molCounts = new Dictionary<int, byte[]>();
            nReadsByRandomTag = new int[rndBcCount];
            nCasesPerRandomTagCount = new int[rndBcCount + 1];
        }

        private void MoveWindow(int pos)
        {
            foreach (byte[] posCounts in molCounts.Values)
            {
                int idx = 0;
                while (idx < posCounts.Length)
                {
                    int c = 0;
                    for (int rndBcIdx = 0; rndBcIdx < rndBcCount; rndBcIdx++)
                        if (posCounts[idx++] > 0) c++;
                    if (c > 0)
                        nCasesPerRandomTagCount[c]++;
                }
            }
            molCounts.Clear();
            windowStart = pos / windowSize;
        }
        /// <summary>
        /// Check if the molecule is new and add to statistics.
        /// N.B.: Calls have to ordered by pos and chromosome.
        /// </summary>
        /// <param name="barcodeIdx">Already extracted barcodeIdx of the record</param>
        /// <returns>True if the pos-strand-bc-randomBc combination is new</returns>
        public bool IsNew(int pos, char strand, int bcIdx, int rndBcIdx)
        {
            int relPos = pos - windowStart;
            if (relPos > windowSize || relPos < 0) MoveWindow(pos);
            nReadsByRandomTag[rndBcIdx]++;
            int strandIdx = (strand == '+') ? 0 : 1;
            int posStrand = (pos << 1) | strandIdx;
            if (!molCounts.ContainsKey(posStrand))
                molCounts[posStrand] = new byte[posStrandDataSize];
            int combIdx = (bcIdx * rndBcCount) + rndBcIdx;
            molCounts[posStrand][combIdx]++;
            return molCounts[posStrand][combIdx] == 1;
        }
    }
}
