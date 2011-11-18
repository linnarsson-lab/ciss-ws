using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RandomTagFilterByBc
    {
        public class TagItem
        {
            // TagItem summarize all reads that map to a specific (chr, pos, strand) combination.
            ushort[] molCounts; // Count of reads in each rndTag like before
            ushort[] snpCounts; // Count of a read-common SNP within each rndTag
            byte[] snpOffsets; // Offset from Read startPos of the SNP
            char[] snpNt; // The alternative Nt of the SNP (really needed?)
            bool hasAltMappings; // true when the (chr, pos, strand) read seq is not unique in genome.
            // molCount & snpCount can probably be reduced to byte once we know how to detect
            // saturation of reads and mutation rates for mutation removal.
            //
            // Could potentially save pointers to the pre-calculated alternative mappings for
            // important sequences (exons) here, to avoid the multiread issue:
            // AltMappings altMappings;
            // with AltMappings being an array of (chr, pos, strand) items, pointed to by all member TagItems.
            // or:
            // TagItem[] altTagItems;
            // being pointers to all the alternative mapped TagItems
            // All items in either array should then be visited during the annotation step,
            // or better, all the other TagItems should also be assigned immediately, so that annotation can
            // process gene-by-gene as well as TagItem-by-TagItem.
            // If analyzing gene-by-gene, a flag should indicate that the TagItem has been visited, to 
            // be able to separate Max/Min counts for redundant genes:
            // bool visited = false;
            // These ways require all TagItems with alternative mappings to be initiated even if no reads will map.
            // Save memory by keeping the TagItems data empty until some actual reads are assigned to them.
            //
            // The process of first counting all reads before annotating, loses the readLen of each
            // read. Maybe a stronger limit on min readLen should be used, and small A-tails not be removed,
            // so that a constant readLen can be assumed.
            //
            // SNPs can only be correctly analyzed if the mapping is unique, i.e. hasAltMappings = false
            // This setup allows only one SNP within the readLen window of this TagItem.
            // ...handle that e.g. by changing offset and Nt when detecting a new and count is only <= 1.
            // snpCount should be 0 or equal to molCount, otherwise the molecule is probably really
            // two mixed molecules, one from each allele (ca.50/50), or due to a mutation during PCR (any ratio).
            // Whether the SNP is real can be inferred by scanning all rndTags of TagItems spanning the SNP position.
            // Then, every molecule that spans a SNP can be assigned to either allele.
        }

        public class ChrTagData
        {
            /// <summary>
            /// PosAndStrand_on_Chr -> countsByRndTagIdx
            /// Position stored as "(pos * 2) | strand" where strand in bit0: +/- => 0/1
            /// </summary>
            private Dictionary<int, ushort[]> molCounts = new Dictionary<int, ushort[]>();
            /// <summary>
            /// Max value of data items in the molCounts arrays.
            /// </summary>
            public static int MaxMoleculeReadCount { get { return ushort.MaxValue; } }

            public void ChangeBcIdx()
            {
                molCounts.Clear();
            }

            /// <summary>
            /// Checks weather the specified rndTag has been seen before on the pos and strand.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="strand"></param>
            /// <param name="rndTagIdx"></param>
            /// <returns></returns>
            public bool IsNew(int pos, char strand, int rndTagIdx)
            {
                int strandIdx = (strand == '+') ? 0 : 1;
                int posStrand = (pos << 1) | strandIdx;
                if (!molCounts.ContainsKey(posStrand))
                    molCounts[posStrand] = new ushort[nRndTags];
                int currentCount = molCounts[posStrand][rndTagIdx];
                molCounts[posStrand][rndTagIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
                return currentCount == 0;
            }

            /// <summary>
            /// Use to get the read count profile for a specific genomic position and strand
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="strand"></param>
            /// <returns>Number of reads as function of rndTag index at given genomic location</returns>
            public ushort[] GetMoleculeCounts(int pos, char strand)
            {
                int strandIdx = (strand == '+') ? 0 : 1;
                int posStrand = (pos << 1) | strandIdx;
                return molCounts[posStrand];
            }

            /// <summary>
            /// Generates (in arbitrary order) the number of times each registered molecule has been observed.
            /// </summary>
            /// <returns></returns>
            public IEnumerable<int> IterMoleculeReadCounts()
            {
                foreach (ushort[] molCountsAtPos in molCounts.Values)
                    foreach (ushort nOfRndTag in molCountsAtPos)
                        yield return (int)nOfRndTag;
            }

            /// <summary>
            /// Analyse how many position-strand combinations have been observed in each rndTag
            /// </summary>
            /// <returns>Counts of distinct mappings by rndTagIdx</returns>
            public int[] GetCasesByRndTagCount()
            {
                int[] nCasesByRndTagCount = new int[nRndTags + 1];
                foreach (ushort[] molCountsAtPos in molCounts.Values)
                {
                    int nUsedRndTags = 0;
                    foreach (ushort nOfRndTag in molCountsAtPos)
                        if (nOfRndTag > 0) nUsedRndTags++;
                    if (nUsedRndTags > 0)
                        nCasesByRndTagCount[nUsedRndTags]++;
                }
                return nCasesByRndTagCount;
            }

            /// <summary>
            /// </summary>
            /// <returns>Number of distinct mappings (position-strand) that have been observed, irrespective of rndTags</returns>
            public int GetNumDistinctMappings()
            {
                return molCounts.Count;
            }

            /// <summary>
            /// </summary>
            /// <param name="strand"></param>
            /// <returns>All positions with some mapped read on given strand</returns>
            public int[] GetDistinctPositions(char strand)
            {
                int[] positions = new int[molCounts.Count];
                int strandIdx = (strand == '+') ? 0 : 1;
                int p = 0;
                foreach (int codedPos in molCounts.Keys)
                    if ((codedPos & 1) == strandIdx)
                        positions[p++] = codedPos >> 1;
                Array.Resize(ref positions, p);
                return positions;
            }

            /// <summary>
            /// </summary>
            /// <param name="strand"></param>
            /// <param name="positions">All positions with some mapped read on given strand</param>
            /// <param name="tagCountAtEachPosition">Number of distinct rndTags mapped at each of these positions</param>
            public void GetDistinctPositionsAndMoleculeCounts(char strand, out int[] positions, out int[] tagCountAtEachPosition)
            {
                positions = new int[molCounts.Count];
                tagCountAtEachPosition = new int[molCounts.Count];
                int strandIdx = (strand == '+') ? 0 : 1;
                int p = 0;
                foreach (KeyValuePair<int, ushort[]> codedPair in molCounts)
                    if ((codedPair.Key & 1) == strandIdx)
                    {
                        int nUsedRndTags = 0;
                        foreach (ushort nReadsInRndTag in codedPair.Value)
                            if (nReadsInRndTag > 0) nUsedRndTags++;
                        tagCountAtEachPosition[p] = nUsedRndTags;
                        positions[p++] = codedPair.Key >> 1;
                    }
                Array.Resize(ref positions, p);
                Array.Resize(ref tagCountAtEachPosition, p);
            }

        }

        private bool hasRndTags;
        public Dictionary<string, ChrTagData> chrTagDatas;
        private int currentBcIdx;
        private HashSet<int> usedBcIdxs;

        protected static int nRndTags;
        /// <summary>
        /// Number of reads in every random tag
        /// </summary>
        public int[] nReadsByRandomTag;
        /// <summary>
        /// Histogram of saturation of random tags by different position-strand combinations
        /// </summary>
        public int[] nCasesPerRandomTagCount;
        /// <summary>
        /// Number of reads that are distinct in each barcode.
        /// (i.e., the position, strand, and rndTag are distinct.)
        /// </summary>
        public int[] nUniqueByBarcode;
        /// <summary>
        /// Histogram of number of times (reads) every molecule has been seen
        /// </summary>
        public int[] moleculeReadCountsHistogram;
        public static readonly int MaxValueInReadCountHistogram = 255;

        /// <summary>
        /// Number of reads that are copies of a first distinct read in each barcode.
        /// (i.e., the position, strand, and rndTag are exactly the same.)
        /// </summary>
        public int[] nDuplicatesByBarcode;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds)
        {
            hasRndTags = barcodes.HasRandomBarcodes;
            nRndTags = barcodes.RandomBarcodeCount;
            nReadsByRandomTag = new int[nRndTags];
            nCasesPerRandomTagCount = new int[nRndTags + 1];
            nDuplicatesByBarcode = new int[barcodes.AllCount];
            nUniqueByBarcode = new int[barcodes.AllCount];
            chrTagDatas = new Dictionary<string, ChrTagData>();
            foreach (string chrId in chrIds)
                chrTagDatas[chrId] = new ChrTagData();
            currentBcIdx = 0;
            usedBcIdxs = new HashSet<int>();
            moleculeReadCountsHistogram = new int[MaxValueInReadCountHistogram + 1];
        }

        private void ChangeBcIdx(int newBcIdx)
        {
            if (usedBcIdxs.Contains(newBcIdx))
                throw new Exception("Program or map file labelling error: Revisiting an already analyzed barcode (" + newBcIdx + ") is not allowed when using random tags.");
            usedBcIdxs.Add(newBcIdx);
            currentBcIdx = newBcIdx;
            foreach (ChrTagData tagData in chrTagDatas.Values)
            {
                int[] chrCounts = tagData.GetCasesByRndTagCount();
                for (int i = 0; i < nCasesPerRandomTagCount.Length; i++)
                    nCasesPerRandomTagCount[i] += chrCounts[i];
                foreach (int count in tagData.IterMoleculeReadCounts())
                    moleculeReadCountsHistogram[Math.Min(MaxValueInReadCountHistogram, count)]++;
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
            if (bcIdx != currentBcIdx)
                ChangeBcIdx(bcIdx);
            nReadsByRandomTag[rndTagIdx]++;
            bool isNew = chrTagDatas[chr].IsNew(pos, strand, rndTagIdx);
            if (isNew) nUniqueByBarcode[bcIdx]++;
            else nDuplicatesByBarcode[bcIdx]++;
            return isNew | !hasRndTags;
        }

        /// <summary>
        /// Use to get the read count profile for a specific genomic location
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <returns>Number of reads as function of rndTag index at given genomic location, or null if no reads has hit that location</returns>
        public ushort[] GetMoleculeCounts(string chr, int pos, char strand)
        {
            try
            {
                return chrTagDatas[chr].GetMoleculeCounts(pos, strand);
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        /// <summary>
        /// Number of distinct mappings since last barcode change
        /// </summary>
        /// <returns>Number of distinct mappings (position & strand) that have been observed in current barcode, irrespective of rndTags</returns>
        public int GetNumDistinctMappings()
        {
            int nAllChr = 0;
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
                nAllChr += chrTagData.GetNumDistinctMappings();
            return nAllChr;
        }

        /// <summary>
        /// </summary>
        /// <returns>Total number of distinct molecules</returns>
        public int GetNumUniqueMolecules()
        {
            int n = 0;
            foreach (int c in nUniqueByBarcode)
                n += c;
            return n;
        }

    }
}
