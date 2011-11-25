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
        public static int marginInReadForSNP = 2;
        public static int averageReadLen; // Needed for SNP handling

        /// <summary>
        /// PosAndStrand_on_Chr -> countsByRndTagIdx
        /// Position stored as "(pos * 2) | strand" where strand in bit0: +/- => 0/1
        /// </summary>
        //private Dictionary<int, ushort[]> molCounts = new Dictionary<int, ushort[]>();
        private Dictionary<int, TagItem> tagItems = new Dictionary<int, TagItem>();
        /// <summary>
        /// Max value of data items in the tagItems arrays.
        /// </summary>
        public static int MaxMoleculeReadCount { get { return ushort.MaxValue; } }

        /// <summary>
        /// Wiggle data in forward, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        public Wiggle wiggleFw = new Wiggle();
        /// <summary>
        /// Wiggle data in reverse, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        public Wiggle wiggleRev = new Wiggle();

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

        public void RegisterSNP(int chrPos)
        {
            for (byte snpOffset = (byte)(averageReadLen - marginInReadForSNP); snpOffset <= (byte)marginInReadForSNP; snpOffset--)
            {
                RegisterSNPOnTagItem(chrPos, snpOffset, 0);
                RegisterSNPOnTagItem(chrPos, snpOffset, 1);
            }
        }

        private void RegisterSNPOnTagItem(int chrPos, byte snpOffset, int strandIdx)
        {
            int posStrand = chrPos << 1 | strandIdx;
            if (!tagItems.ContainsKey(posStrand))
                tagItems[posStrand] = new TagItem(false);
            tagItems[posStrand].RegisterSNP(snpOffset);
        }

        /// <summary>
        /// Count, for each SNP nt at given position, the number of mapped molecules
        /// </summary>
        /// <param name="chrPos"></param>
        /// <param name="strand"></param>
        /// <returns>SNPCounter with totals of each nt</returns>
        public SNPCounter GetMolSNPData(int snpChrPos, char strand)
        {
            SNPCounter totalCounts = new SNPCounter();
            TagItem tagItem;
            for (byte snpOffset = (byte)(averageReadLen - marginInReadForSNP); snpOffset <= (byte)marginInReadForSNP; snpOffset--)
            {
                int hitStartPos = snpChrPos - snpOffset;
                int posStrand = MakePosStrandIdx(hitStartPos, strand);
                if (tagItems.TryGetValue(posStrand, out tagItem))
                {
                    SNPCounter mapPosCounts = tagItem.GetMolSNPCounts(snpOffset);
                    if (mapPosCounts != null)
                        totalCounts.Add(mapPosCounts);
                }
            }
            return totalCounts;
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
        /// Add a read and checks weather the specified rndTag has been seen before on the pos and strand.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <param name="rndTagIdx"></param>
        /// <param name="hasAltMappings">As indicated by the map file</param>
        /// <returns>true if the rndTag is new at this position-strand</returns>
        public bool Add(int pos, char strand, int rndTagIdx, bool hasAltMappings, string mismatches, int readLen)
        {
            int strandIdx = (strand == '+') ? 0 : 1;
            int posStrand = (pos << 1) | strandIdx;
            if (!tagItems.ContainsKey(posStrand))
                tagItems[posStrand] = new TagItem(false);
            TagItem item = tagItems[posStrand];
            if (!hasAltMappings && mismatches != "")
            {
                foreach (string snp in mismatches.Split(','))
                {
                    int p = snp.IndexOf(':');
                    if (p == -1)
                        continue;
                    int posInRead = int.Parse(snp.Substring(0, p));
                    if (posInRead < marginInReadForSNP || posInRead > readLen - marginInReadForSNP) continue;
                    byte relPos = (byte)((strand == '+') ? posInRead : readLen - 1 - posInRead);
                    char snpNt = snp[p + 3];
                    item.AddSNP(rndTagIdx, relPos, snpNt);
                }
            }
            return item.Add(rndTagIdx);
        }

        public IEnumerable<MappedTagItem> IterItems()
        {
            MappedTagItem item = new MappedTagItem();
            foreach (KeyValuePair<int, TagItem> cPair in tagItems)
            {
                item.tagItem = cPair.Value;
                item.strand = ((cPair.Key & 1) == 0)? '+' : '-';
                item.hitStartPos = cPair.Key >> 1;
                yield return item;
            }
        }

        /// <summary>
        /// Use to get the read count profile for a specific genomic position and strand
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <returns>Number of reads as function of rndTag index at given genomic location</returns>
        public ushort[] GetReadCounts(int pos, char strand)
        {
            int strandIdx = (strand == '+') ? 0 : 1;
            int posStrand = (pos << 1) | strandIdx;
            return tagItems[posStrand].GetReadCountsByRndTag();
        }

        /// <summary>
        /// Generates (in arbitrary order) the number of times each registered molecule has been observed.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> IterMoleculeReadCounts()
        {
            foreach (TagItem tagItem in tagItems.Values)
                foreach (ushort nOfRndTag in tagItem.GetReadCountsByRndTag())
                    yield return (int)nOfRndTag;
        }

        /// <summary>
        /// Analyse how many position-strand combinations have been observed in each rndTag
        /// </summary>
        /// <returns>Counts of distinct mappings by rndTagIdx</returns>
        public int[] GetCasesByRndTagCount()
        {
            int[] nCasesByRndTagCount = new int[RandomTagFilterByBc.nRndTags + 1];
            foreach (TagItem molCountsAtPos in tagItems.Values)
            {
                int nUsedRndTags = molCountsAtPos.GetNumMolecules();
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
        /// </summary>
        /// <param name="strand"></param>
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
        public static readonly int MaxValueInReadCountHistogram = 255;

        /// <summary>
        /// Number of reads that are copies of a first distinct read in each barcode.
        /// (i.e., the position, strand, and rndTag are exactly the same.)
        /// </summary>
        public int[] nDuplicatesByBarcode;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds, string tagMappingFile)
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
                chrTagDatas[chrId] = new ChrTagData();
            currentBcIdx = 0;
            usedBcIdxs = new HashSet<int>();
            moleculeReadCountsHistogram = new int[MaxValueInReadCountHistogram + 1];
            if (File.Exists(tagMappingFile))
                Setup(tagMappingFile);
        }

        /// <summary>
        /// Read and initiate multiread mappings from the input file
        /// </summary>
        /// <param name="tagMappingFile"></param>
        public void Setup(string tagMappingFile)
        {
            Console.WriteLine("Reading redundant mappings from " + tagMappingFile);
            int n = 0;
            using (StreamReader reader = new StreamReader(tagMappingFile))
            {
                string line = reader.ReadLine();
                while (line.StartsWith("#")) line = reader.ReadLine();
                while (line != null)
                {
                    if (++n % 10000000 == 0) Console.WriteLine(n + "...");
                    TagItem tagItem = new TagItem(true);
                    string[] groups = line.Split('\t');
                    foreach (string group in groups)
                    {
                        string[] parts = group.Split(',');
                        chrTagDatas[parts[0]].Setup(int.Parse(parts[1]), parts[2][0], tagItem);
                    }
                    line = reader.ReadLine();
                }
            }
        }

        public void SetupSNPCounters(int averageReadLen, IEnumerable<LocatedSNPCounter> snpDatas)
        {
            ChrTagData.averageReadLen = averageReadLen;
            foreach (LocatedSNPCounter snpData in snpDatas)
                chrTagDatas[snpData.chr].RegisterSNP(snpData.chrPos);
        }

        private void ChangeBcIdx(int newBcIdx)
        {
            if (usedBcIdxs.Contains(newBcIdx))
                throw new Exception("Program or map file labelling error: Revisiting an already analyzed barcode ("
                                    + newBcIdx + ") is not allowed when using random tags.");
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
        /// Add a single-mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// </summary>
        /// <returns>True if the chr-strand-pos-randomTag combination is new</returns>
        public bool Add(MultiReadMappings mrm)
        {
            int bcIdx = mrm.BarcodeIdx;
            if (bcIdx != currentBcIdx)
                ChangeBcIdx(bcIdx);
            nReadsByRandomTag[mrm.RandomBcIdx]++;
            bool isNew = chrTagDatas[mrm[0].Chr].Add(mrm[0].Position, mrm[0].Strand, mrm.RandomBcIdx, mrm.HasAltMappings, mrm[0].Mismatches, mrm.SeqLen);
            if (isNew) nUniqueByBarcode[bcIdx]++;
            else nDuplicatesByBarcode[bcIdx]++;
            return isNew | !hasRndTags;
        }

        public IEnumerable<MappedTagItem> IterItems()
        {
            foreach (KeyValuePair<string, ChrTagData> chrData in chrTagDatas)
                foreach (MappedTagItem item in chrData.Value.IterItems())
                {
                    item.bcIdx = currentBcIdx;
                    item.chr = chrData.Key;
                    yield return item;
                }
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
                return chrTagDatas[chr].GetReadCounts(pos, strand);
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
