using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    public class ChrTagData
    {
        public ChrTagData(string chr)
        {
            this.chr = chr;
        }
        public static int marginInReadForSNP = 2;
        public static int averageReadLen; // Needed for SNP handling
        public string chr;
        /// <summary>
        /// PosAndStrand_on_Chr -> countsByRndTagIdx
        /// Position stored as "(pos * 2) | strand" where strand in bit0: +/- => 0/1
        /// </summary>
        private Dictionary<int, TagItem> tagItems = new Dictionary<int, TagItem>();

        /// <summary>
        /// Wiggle data in forward, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        public Wiggle wiggleFw = new Wiggle();
        /// <summary>
        /// Wiggle data in reverse, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        public Wiggle wiggleRev = new Wiggle();

        /// <summary>
        /// Pre-define a tagItem at specified position (used for shared multiread TagItems)
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <param name="tagItem"></param>
        public void Setup(int pos, char strand, TagItem tagItem)
        {
            int posStrand = MakePosStrandIdx(pos, strand);
            tagItems[posStrand] = tagItem; 
        }

        private static int MakePosStrandIdx(int pos, char strand)
        {
            int strandIdx = (strand == '+') ? 0 : 1;
            int posStrand = (pos << 1) | strandIdx;
            return posStrand;
        }

        /// <summary>
        /// Prepare for analysis of a SNP at specified position.
        /// We expect that the position is uniquely identifiable in the genome
        /// </summary>
        /// <param name="snpChrPos"></param>
        public void RegisterSNP(int snpChrPos)
        {
            for (byte snpOffset = (byte)(averageReadLen - marginInReadForSNP); snpOffset >= (byte)marginInReadForSNP; snpOffset--)
            {
                int readStartPos = snpChrPos - snpOffset;
                RegisterSNPOnTagItem(readStartPos, snpOffset, 0);
                RegisterSNPOnTagItem(readStartPos, snpOffset, 1);
            }
        }
        private void RegisterSNPOnTagItem(int chrPos, byte snpOffset, int strandIdx)
        {
            int posStrand = chrPos << 1 | strandIdx;
            TagItem item;
            if (!tagItems.TryGetValue(posStrand, out item))
            {
                item = new TagItem(false);
                tagItems[posStrand] = item;
            }
            tagItems[posStrand].RegisterSNP(snpOffset);
        }

        public void ChangeBcIdx()
        {
            AddToWiggle();
            foreach (TagItem tagItem in tagItems.Values)
                tagItem.Clear();
        }

        private void AddToWiggle()
        {
            int[] positions, molCounts, readCounts;
            GetDistinctPositionsAndCounts('+', out positions, out molCounts, out readCounts);
            wiggleFw.AddCounts(positions, molCounts, readCounts);
            GetDistinctPositionsAndCounts('-', out positions, out molCounts, out readCounts);
            wiggleRev.AddCounts(positions, molCounts, readCounts);
        }

        public Wiggle GetWiggle(char strand)
        {
            return (strand == '+') ? wiggleFw : wiggleRev;
        }

        /// <summary>
        /// Add a read and checks wether the specified rndTag has been seen before on the pos and strand.
        /// </summary>
        /// <param name="m">A multireadmapping to analyze</param>
        /// <returns>true if the rndTag is new at this position-strand</returns>
        public bool Add(MultiReadMapping m)
        {
            int posStrand = MakePosStrandIdx(m.Position, m.Strand);
            TagItem item;
            if (!tagItems.TryGetValue(posStrand, out item))
            {
                item = new TagItem(m.HasAltMappings);
                tagItems[posStrand] = item;
            }
            if (!m.HasAltMappings && item.HasSNPs) // Should maybe move this code into new TagItem.Add(MultiReadMapping m)
            {                                      // and do IterMismatches(minPhredScore).
                foreach (Mismatch mm in m.IterMismatches(0))
                {
                    if (mm.relPosInChrDir < marginInReadForSNP || mm.relPosInChrDir > m.SeqLen - marginInReadForSNP) continue;
                    //Console.WriteLine("ChrTagData.Add: MultiReadMapping.Position=" + m.Position + " Strand=" + m.Strand 
                    //                  + " RndTagIdx=" + m.RndTagIdx + " " + mm.ToString());
                    item.tagSNPData.AddSNP(m.RndTagIdx, mm);
                }
            }
            return item.Add(m.RndTagIdx);
        }

        /// <summary>
        /// Iterate through the TagItem count data for every (position, strand) hit in this chromosome
        /// </summary>
        /// <returns></returns>
        public IEnumerable<MappedTagItem> IterItems(int bcIdx, string chrId)
        {
            MappedTagItem item = new MappedTagItem();
            item.bcIdx = bcIdx;
            item.chr = chrId;
            foreach (KeyValuePair<int, TagItem> cPair in tagItems)
            {
                if (cPair.Value.HasReads)
                {
                    int hitStartPos = cPair.Key >> 1;
                    char strand = ((cPair.Key & 1) == 0) ? '+' : '-';
                    item.Update(hitStartPos, strand, cPair.Value);
                    item.splcToRealChrOffset = 0; // Need always reset this
                    //Console.WriteLine("ChrTagData.IterItems(bcIdx=" + bcIdx + " chrId=" + chrId + ") yield: " + item.ToString());
                    yield return item;
                }
            }
            yield break;
        }

        /// <summary>
        /// Use to get the estimated molCount and read count profile for a specific genomic position and strand
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <returns>Number of reads as function of rndTag index at given genomic location, or null if no reads are found</returns>
        public void GetReadCounts(int pos, char strand, out int molCount, out ushort[] readProfile)
        {
            int posStrand = MakePosStrandIdx(pos, strand);
            readProfile = tagItems[posStrand].GetReadCountsByRndTag();
            molCount = tagItems[posStrand].GetNumMolecules();
        }

        /// <summary>
        /// Generates (in arbitrary order) the number of times each registered molecule has been observed.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> IterMoleculeReadCounts()
        {
            foreach (TagItem tagItem in tagItems.Values)
            {
                ushort[] readsByRndTag = tagItem.GetReadCountsByRndTag();
                if (readsByRndTag != null)
                    foreach (ushort nOfRndTag in readsByRndTag)
                        yield return (int)nOfRndTag;
            }
        }

        /// <summary>
        /// Analyse how many position-strand combinations have been observed in each rndTag
        /// </summary>
        /// <returns>Counts of distinct mappings by rndTagIdx</returns>
        public int[] GetCasesByRndTagCount()
        {
            int[] nCasesByRndTagCount = new int[Math.Max(2, RandomTagFilterByBc.nRndTags + 1)];
            foreach (TagItem molCountsAtPos in tagItems.Values)
            {
                int nUsedRndTags = Math.Min(RandomTagFilterByBc.nRndTags, molCountsAtPos.GetNumMolecules());
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
            int n = 0;
            foreach (TagItem tagItem in tagItems.Values)
                if (tagItem.HasReads) n++;
            return n;
        }

        /// <summary>
        /// </summary>
        /// <param name="strand"></param>
        /// <returns>All positions with some mapped read on given strand</returns>
        public int[] GetDistinctPositions(char strand)
        {
            int[] positions = new int[tagItems.Count];
            int strandIdx = (strand == '+') ? 0 : 1;
            int p = 0;
            foreach (int codedPos in tagItems.Keys)
                if ((codedPos & 1) == strandIdx)
                    positions[p++] = codedPos >> 1;
            Array.Resize(ref positions, p);
            return positions;
        }

        /// <summary>
        /// Summarizes all hit positions and the the respective read and molecule count
        /// </summary>
        /// <param name="strand">Strand to analyze</param>
        /// <param name="positions">All positions with some mapped read on given strand</param>
        /// <param name="molCountAtEachPosition">Number of distinct rndTags (=molecules) mapped at each of these positions</param>
        /// <param name="readCountAtEachPosition">Number of reads mapped at each of these positions</param>
        public void GetDistinctPositionsAndCounts(char strand, out int[] positions,
                                                  out int[] molCountAtEachPosition, out int[] readCountAtEachPosition)
        {
            positions = new int[tagItems.Count];
            molCountAtEachPosition = new int[tagItems.Count];
            readCountAtEachPosition = new int[tagItems.Count];
            int strandIdx = (strand == '+') ? 0 : 1;
            int p = 0;
            foreach (KeyValuePair<int, TagItem> codedPair in tagItems)
                if ((codedPair.Key & 1) == strandIdx)
                {
                    molCountAtEachPosition[p] = codedPair.Value.GetNumMolecules();
                    readCountAtEachPosition[p] = codedPair.Value.GetNumReads();
                    positions[p++] = codedPair.Key >> 1;
                }
            Array.Resize(ref positions, p);
            Array.Resize(ref molCountAtEachPosition, p);
            Array.Resize(ref readCountAtEachPosition, p);
        }

    }

    public class RandomTagFilterByBc
    {
        private bool hasRndTags;
        public Dictionary<string, ChrTagData> chrTagDatas;
        private int currentBcIdx;
        private HashSet<int> usedBcIdxs;

        public static int nRndTags;
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
        public static readonly int MaxValueInReadCountHistogram = 1000;

        /// <summary>
        /// Number of reads that are copies of a first distinct read in each barcode.
        /// (i.e., the position, strand, and rndTag are exactly the same.)
        /// </summary>
        public int[] nDuplicatesByBarcode;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds)
        {
            hasRndTags = barcodes.HasRandomBarcodes;
            nRndTags = barcodes.RandomBarcodeCount;
            TagItem.nRndTags = nRndTags;
            nReadsByRandomTag = new int[nRndTags];
            nCasesPerRandomTagCount = new int[nRndTags + 1];
            nDuplicatesByBarcode = new int[barcodes.AllCount];
            nUniqueByBarcode = new int[barcodes.AllCount];
            chrTagDatas = new Dictionary<string, ChrTagData>();
            foreach (string chrId in chrIds)
                chrTagDatas[chrId] = new ChrTagData(chrId);
            currentBcIdx = 0;
            usedBcIdxs = new HashSet<int>();
            moleculeReadCountsHistogram = new int[MaxValueInReadCountHistogram + 1];
        }

        /// <summary>
        /// Prepare for analysis of SNPs during annotation step
        /// </summary>
        /// <param name="averageReadLen">Needed to check that SNPs are surely within reads</param>
        /// <param name="snpDatas">Iterator to get all positions where SNPs should be analyzed</param>
        /// <returns></returns>
        public int SetupSNPCounters(int averageReadLen, IEnumerable<LocatedSNPCounter> snpDatas)
        {
            int nSNPs = 0;
            ChrTagData.averageReadLen = averageReadLen;
            foreach (LocatedSNPCounter snpData in snpDatas)
            {
                nSNPs++;
                chrTagDatas[snpData.chr].RegisterSNP(snpData.chrPos);
            }
            return nSNPs;
        }

        /// <summary>
        /// Need to call this inbetween every series of reads from the same barcode
        /// </summary>
        /// <param name="newBcIdx"></param>
        public void ChangeBcIdx(int newBcIdx)
        {
            if (usedBcIdxs.Contains(newBcIdx))
                throw new Exception("Program or map file naming error: Revisiting an already analyzed barcode (" + newBcIdx + ") is not allowed.");
            usedBcIdxs.Add(newBcIdx);
            currentBcIdx = newBcIdx;
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
            {
                int[] chrCounts = chrTagData.GetCasesByRndTagCount();
                for (int i = 0; i < nCasesPerRandomTagCount.Length; i++)
                    nCasesPerRandomTagCount[i] += chrCounts[i];
                foreach (int count in chrTagData.IterMoleculeReadCounts())
                    moleculeReadCountsHistogram[Math.Min(MaxValueInReadCountHistogram, count)]++;
                chrTagData.ChangeBcIdx();
            }
        }

        /// <summary>
        /// Add a mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// </summary>
        /// <returns>True if the chr-strand-pos-randomTag combination is new</returns>
        public bool Add(MultiReadMapping m)
        {
            nReadsByRandomTag[m.RndTagIdx]++;
            bool isNew = chrTagDatas[m.Chr].Add(m);
            if (isNew) nUniqueByBarcode[m.BcIdx]++;
            else nDuplicatesByBarcode[m.BcIdx]++;
            return isNew | !hasRndTags;
        }

        /// <summary>
        /// Iterate through the TagItem count data for every (chr, position, strand) hit by some read
        /// </summary>
        /// <param name="filterChrIds">Ids to select either for exclusion or inclusion</param>
        /// <param name="includeFilter">true to only iterate specified chrs, false to iterate all but specified chrs</param>
        /// <returns></returns>
        public IEnumerable<MappedTagItem> IterItems(List<string> filterChrIds, bool includeFilter)
        {
            foreach (string chrId in chrTagDatas.Keys)
            {
                if (filterChrIds.Contains(chrId) == includeFilter)
                    foreach (MappedTagItem item in chrTagDatas[chrId].IterItems(currentBcIdx, chrId))
                        yield return item;
            }
            yield break;
        }

        /// <summary>
        /// Use to get the read count profile for a specific genomic location
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <returns>Number of reads as function of rndTag index at given genomic location, or null if no reads has hit that location</returns>
        public void GetReadCountProfile(string chr, int pos, char strand, out int molCount, out ushort[] readProfile)
        {
            molCount = 0;
            readProfile = null;
            try
            {
                 chrTagDatas[chr].GetReadCounts(pos, strand, out molCount, out readProfile);
            }
            catch (KeyNotFoundException)
            { }
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
    }
}
