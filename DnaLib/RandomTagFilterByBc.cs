using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RandomTagFilterByBc
    {
        private class ChrTagData
        {
            private Dictionary<int, byte[]> molCounts; // posOnChr -> countsByRndTag

            public void ChangeBcIdx()
            {
                molCounts.Clear();
            }

            public bool IsNew(int pos, char strand, int rndTagIdx)
            {
                int strandIdx = (strand == '+') ? 0 : 1;
                int posStrand = (pos << 1) | strandIdx;
                if (!molCounts.ContainsKey(posStrand))
                    molCounts[posStrand] = new byte[nRndTags];
                molCounts[posStrand][rndTagIdx]++;
                return molCounts[posStrand][rndTagIdx] == 1;
            }

            public int[] GetCasesByRndTagCount()
            {
                int[] nCasesByRndTagCount = new int[nRndTags];
                foreach (byte[] posCounts in molCounts.Values)
                {
                    int idx = 0;
                    while (idx < posCounts.Length)
                    {
                        int c = 0;
                        for (int rndBcIdx = 0; rndBcIdx < nRndTags; rndBcIdx++)
                            if (posCounts[idx++] > 0) c++;
                        if (c > 0)
                            nCasesByRndTagCount[c]++;
                    }
                }
                return nCasesByRndTagCount;
            }

        }

        private bool hasRndTags;
        private Dictionary<string, ChrTagData> chrTagDatas;
        private int currentBcIdx;
        protected static int nRndTags;
        public int[] nReadsByRandomTag;
        public int[] nCasesPerRandomTagCount;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds)
        {
            hasRndTags = barcodes.HasRandomBarcodes;
            nRndTags = barcodes.RandomBarcodeCount;
            nReadsByRandomTag = new int[nRndTags];
            nCasesPerRandomTagCount = new int[nRndTags + 1];
            if (!hasRndTags) return;
            chrTagDatas = new Dictionary<string, ChrTagData>();
            foreach (string chrId in chrIds)
                chrTagDatas[chrId] = new ChrTagData();
        }

        private void ChangeBcIdx(int newBcIdx)
        {
            currentBcIdx = newBcIdx;
            foreach (ChrTagData tagData in chrTagDatas.Values)
            {
                int[] chrCounts = tagData.GetCasesByRndTagCount();
                for (int i = 0; i < nCasesPerRandomTagCount.Length; i++)
                    nCasesPerRandomTagCount[i] += chrCounts[i];
                tagData.ChangeBcIdx();
            }
        }

        /// <summary>
        /// Check if the molecule is new and add to statistics.
        /// Reads have to be submitted in sets containing all reads for each barcode.
        /// </summary>
        /// <returns>True if the chr-strand-pos-randomTag combination is new</returns>
        public bool IsNew(string chr, int pos, char strand, int bcIdx, int rndTagIdx)
        {
            if (!hasRndTags) return true;
            if (bcIdx != currentBcIdx)
                ChangeBcIdx(bcIdx);
            nReadsByRandomTag[rndTagIdx]++;
            return chrTagDatas[chr].IsNew(pos, strand, rndTagIdx);
        }

    }
}
