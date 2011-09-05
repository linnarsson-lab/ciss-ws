using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RandomTagFilter
    {
        private int randomTagPos = 0;
        private int randomTagLen = 12;
        private BitArray tagUsed;
        private long m_NumDuplicates = 0;
        public long NumDuplicates { get { return m_NumDuplicates; } }
        private long m_NumUnique = 0;
        public long NumUnique { get { return m_NumUnique; } }

        public RandomTagFilter(Barcodes barcodes)
        {
            randomTagLen = barcodes.RandomTagLen;
            randomTagPos = barcodes.RandomTagPos;
            int tagDim = 96 * (1 << (2 * randomTagLen));
            tagUsed = new BitArray(tagDim);
        }

        /// <summary>
        /// Extract the random tag from the record and check if has been seen before.
        /// </summary>
        /// <param name="rec">The tag will be added to the header line</param>
        /// <param name="barcodeIdx">Already extracted barcodeIdx of the record</param>
        /// <returns>True if the random tag is new</returns>
        public bool IsUnique(FastQRecord rec, int barcodeIdx)
        {
            string randomTag = rec.Sequence.Substring(randomTagPos, randomTagLen);
            int combinedTag = MakeCombinedTag(barcodeIdx, randomTag);
            if (tagUsed[combinedTag])
            {
                m_NumDuplicates++;
                return false;
            }
            rec.Header += "_" + randomTag; // Add random tag in header line
            tagUsed[combinedTag] = true;
            m_NumUnique++;
            return true;
        }

        private static int MakeCombinedTag(int bcodeIdx, string randomTag)
        {
            int tag = bcodeIdx;
            foreach (char c in randomTag)
            {
                int nt = "ACGT".IndexOf(c);
                if (nt == -1) return -1;
                tag = tag << 2 | nt;
            }
            return tag;
        }

    }
}
