using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Holds read counts and SNP data (as TagItems) for one chromosome
    /// </summary>
    public class ChrTagData
    {
        public ChrTagData(string chr)
        {
            this.chr = chr;
            if (Props.props.GenerateWiggle || Props.props.AnalyzeSNPs)
            {
                wiggleFw = new Wiggle();
                wiggleRev = new Wiggle();
            }
        }

        /// <summary>
        /// Mismatches closer than this to either read end will not be used for SNP analysis
        /// </summary>
        public static int marginInReadForSNP = 2;
        public static int averageReadLen; // Needed for SNP handling
        public string chr;

        /// <summary>
        /// PosAndStrand_on_Chr -> countsByRndTagIdx
        /// Read start position stored as "(pos * 2) | strand" where strand in bit0: +/- => 0/1
        /// </summary>
        private Dictionary<int, TagItem> tagItems = new Dictionary<int, TagItem>();

        /// <summary>
        /// Wiggle data in forward, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        private Wiggle wiggleFw;
        /// <summary>
        /// Wiggle data in reverse, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        private Wiggle wiggleRev;

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

        /// <summary>
        /// Should be called when all reads from the same barcode have been registered
        /// </summary>
        public void FinishBarcode()
        {
            AddToWiggle();
            foreach (TagItem tagItem in tagItems.Values)
                tagItem.Clear();
        }

/*        private void AddToWiggleOld()
        {
            if (wiggleFw == null) return;
            int[] positions, molCounts, readCounts;
            GetDistinctPositionsAndCounts('+', null, out positions, out molCounts, out readCounts);
            wiggleFw.AddCounts(positions, molCounts, readCounts);
            GetDistinctPositionsAndCounts('-', null, out positions, out molCounts, out readCounts);
            wiggleRev.AddCounts(positions, molCounts, readCounts);
        }*/

        public void AddToWiggle()
        {
            if (wiggleFw == null) return;
            foreach (KeyValuePair<int, TagItem> codedPair in tagItems)
            {
                int numReads = codedPair.Value.GetNumReads();
                if (numReads > 0)
                {
                    int hitStartPos = codedPair.Key >> 1;
                    int nMols = codedPair.Value.GetNumMolecules();
                    if ((codedPair.Key & 1) == 0)
                        wiggleFw.AddCount(hitStartPos, numReads, nMols);
                    else
                        wiggleRev.AddCount(hitStartPos, numReads, nMols);
                }
            }
        }

        public Wiggle GetWiggle(char strand)
        {
            return (strand == '+') ? wiggleFw : wiggleRev;
        }

        /// <summary>
        /// Add a read and checks whether the specified rndTag has been seen before on the pos and strand.
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
            else if (item.hasAltMappings && !m.HasAltMappings && !m.HasMismatches && item.GetNumReads() == 1)
            { // When the first mapped read contained mismatches and was a multiread, but the second is a perfect match singleread,
              // rethink the TagItem to consist of singlereads. Increases the chance of detecting true exon signals.
                item.hasAltMappings = false;
            }
            if (!m.HasAltMappings && item.HasSNPs) // Should maybe move this code into new TagItem.Add(MultiReadMapping m)
            {                                      // and do IterMismatches(minPhredScore).
                foreach (Mismatch mm in m.IterMismatches(0))
                {
                    if (mm.relPosInChrDir < marginInReadForSNP || mm.relPosInChrDir > m.SeqLen - marginInReadForSNP) continue;
                    item.AddSNP(m.UMIIdx, mm);
                }
            }
            return item.Add(m.UMIIdx);
        }

        /// <summary>
        /// Add a read and check whether the specified rndTag has been seen before on the pos and strand.
        /// </summary>
        /// <param name="m">A multireadmapping to analyze</param>
        /// <param name="sharingRealFeatures">List of other features that compete due to the read being a multiread</param>
        /// <returns>true if the rndTag is new at this position-strand</returns>
        public bool Add(MultiReadMapping m, Dictionary<IFeature, object> sharingRealFeatures)
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
                    item.AddSNP(m.UMIIdx, mm);
                }
            }
            item.AddSharedGenes(sharingRealFeatures);
            return item.Add(m.UMIIdx);
        }

        /// <summary>
        /// Iterate through the TagItem count data for every (position, strand) hit in this chromosome
        /// </summary>
        /// <param name="bcIdx">barcode to analyze - only used to set the return values properly</param>
        /// <param name="chrId">chromosome to analyze</param>
        /// <returns>A reused(!) MappedTagItem for every mapped position on the chromosome</returns>
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
                    try
                    {
                        item.Update(hitStartPos, strand, cPair.Value);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: {0} at chr={1}, strand={2}, hitStart={3}, bcIdx={4}!", e.Message, chrId, strand, hitStartPos, bcIdx);
                    }
                    item.splcToRealChrOffset = 0; // Need always reset this
                    yield return item;
                }
            }
            yield break;
        }

        /// <summary>
        /// Use to get the filtered molecule count and read count profile for a specific genomic position and strand.
        /// If no data exists, readProfile == null and moCount == 0.
        /// </summary>
        /// <param name="pos">Requested hit start pos</param>
        /// <param name="strand"></param>
        /// <param name="molCount">Estimated number of molecules detected after filtering</param>
        /// <param name="readProfile">Array with number of reads in each rndTag</param>
        public void GetReadCounts(int pos, char strand, out int molCount, out ushort[] readProfile)
        {
            int posStrand = MakePosStrandIdx(pos, strand);
            TagItem t;
            if (tagItems.TryGetValue(posStrand, out  t))
            {
                readProfile = t.GetReadCountsByRndTag();
                molCount = t.GetNumMolecules();
            }
            else
            {
                readProfile = null;
                molCount = 0;
            }
        }

        /// <summary>
        /// Iterate all TagItems that contain any data
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TagItem> IterNonEmptyTagItems()
        {
            foreach (TagItem tagItem in tagItems.Values)
                if (tagItem.HasReads)
                    yield return tagItem;
        }

        /// <summary>
        /// Number of distinct mappings (position-strand) that have been observed, irrespective of rndTags.
        /// Note that this may be more than number of distinct molecules, if all multiread mappings to exons are analyzed
        /// </summary>
        /// <returns>Number of distinct mappings (position-strand) that have been observed, irrespective of rndTags</returns>
        public int GetNumDistinctMappings()
        {
            return tagItems.Values.Count(v => v.HasReads);
        }

        /// <summary>
        /// Summarizes all hit start positions and the the respective read and molecule count
        /// </summary>
        /// <param name="strand">Strand to analyze</param>
        /// <param name="positions">All positions with some mapped read start on given strand</param>
        /// <param name="molCountAtEachPosition">Number of distinct rndTags (=molecules) mapped at each of these positions</param>
        /// <param name="readCountAtEachPosition">Number of reads mapped at each of these positions</param>
        public void GetDistinctPositionsAndCounts(char strand, int[] selectedAnnotTypes, out int[] positions,
                                                  out int[] molCountAtEachPosition, out int[] readCountAtEachPosition)
        {
            positions = new int[tagItems.Count];
            molCountAtEachPosition = new int[tagItems.Count];
            readCountAtEachPosition = new int[tagItems.Count];
            int strandIdx = (strand == '+') ? 0 : 1;
            int p = 0;
            foreach (KeyValuePair<int, TagItem> codedPair in tagItems)
            {
                int numReads = codedPair.Value.GetNumReads();
                if (numReads > 0 && (codedPair.Key & 1) == strandIdx &&
                    (selectedAnnotTypes == null || selectedAnnotTypes.Contains(codedPair.Value.typeOfAnnotation)))
                {
                    readCountAtEachPosition[p] = numReads;
                    molCountAtEachPosition[p] = codedPair.Value.GetNumMolecules();
                    positions[p++] = codedPair.Key >> 1;
                }
            }
            Array.Resize(ref positions, p);
            Array.Resize(ref molCountAtEachPosition, p);
            Array.Resize(ref readCountAtEachPosition, p);
        }

        internal int TagItemCount()
        {
            return tagItems.Count;
        }
    }

    public class RandomTagFilterByBc
    {
        private bool hasRndTags;
        public Dictionary<string, ChrTagData> chrTagDatas;

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
        /// Histogram of number of times (reads) every molecule has been seen
        /// </summary>
        public int[] moleculeReadCountsHistogram;
        /// <summary>
        /// Histogram showing distribution of #reads for each number of rndTags detected across all mapped positions in the experiment
        /// </summary>
        public int[,] readDistributionByMolCount;

        public static readonly int MaxValueInReadCountHistogram = 4100;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds)
        {
            hasRndTags = barcodes.HasUMIs;
            nRndTags = barcodes.UMICount;
            TagItem.nRndTags = nRndTags;
            nReadsByRandomTag = new int[nRndTags];
            nCasesPerRandomTagCount = new int[nRndTags + 1];
            chrTagDatas = new Dictionary<string, ChrTagData>();
            foreach (string chrId in chrIds)
                chrTagDatas[chrId] = new ChrTagData(chrId);
            moleculeReadCountsHistogram = new int[MaxValueInReadCountHistogram + 1];
            readDistributionByMolCount = new int[nRndTags + 1, MaxValueInReadCountHistogram + 1];
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
        /// Need to call this after finishing every series of reads from the same barcode
        /// </summary>
        public void FinishBarcode()
        {
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
            {
                if (nRndTags > 1)
                {
                    FinishBarcodeWRndTags(chrTagData);
                }
                chrTagData.FinishBarcode();
            }
        }

        private void FinishBarcodeWRndTags(ChrTagData chrTagData)
        {
            foreach (TagItem tagItem in chrTagData.IterNonEmptyTagItems())
            {
                ushort[] readsByRndTag = tagItem.GetReadCountsByRndTag();
                int nUsedRndTags = readsByRndTag.Count(c => c > 0);
                nCasesPerRandomTagCount[nUsedRndTags]++;
                foreach (ushort countInRndTag in readsByRndTag.Where(c => c > 0))
                {
                    int limitedCount = Math.Min(MaxValueInReadCountHistogram, countInRndTag);
                    moleculeReadCountsHistogram[limitedCount]++;
                    readDistributionByMolCount[nUsedRndTags, limitedCount]++;
                }
            }
        }

        /// <summary>
        /// Add a mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// </summary>
        /// <returns>True if the chr-strand-pos-bc-rndTag combination is new</returns>
        public bool Add(MultiReadMapping m)
        {
            nReadsByRandomTag[m.UMIIdx]++;
            bool isNew = chrTagDatas[m.Chr].Add(m);
            return isNew | !hasRndTags;
        }

        /// <summary>
        /// Add a mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// Will record the transcripts competing for the read if it is multiread
        /// </summary>
        /// <param name="m">The mapping to add</param>
        /// <param name="sharingRealFeatures">List of other features compete due to the read being a multiread</param>
        /// <returns>True if the chr-strand-pos-bc-rndTag combination is new</returns>
        public bool Add(MultiReadMapping m, Dictionary<IFeature, object> sharingRealFeatures)
        {
            nReadsByRandomTag[m.UMIIdx]++;
            bool isNew = chrTagDatas[m.Chr].Add(m, sharingRealFeatures);
            return isNew | !hasRndTags;
        }

        /// <summary>
        /// Iterate through the TagItem count data for every (chr, position, strand) hit by some read
        /// </summary>
        /// <param name="bcIdx">Only needed to set the bcIdx properly in the MappedTagItems</param>
        /// <param name="filterChrIds">Ids to select either for exclusion or inclusion</param>
        /// <param name="includeFilter">true to only iterate specified chrs, false to iterate all but specified chrs</param>
        /// <returns></returns>
        public IEnumerable<MappedTagItem> IterItems(int bcIdx, List<string> filterChrIds, bool includeFilter)
        {
            foreach (string chrId in chrTagDatas.Keys)
            {
                if (filterChrIds.Contains(chrId) == includeFilter)
                    foreach (MappedTagItem item in chrTagDatas[chrId].IterItems(bcIdx, chrId))
                        yield return item;
            }
            yield break;
        }

        /// <summary>
        /// Use to get the read count profile for a specific genomic location.
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="pos"></param>
        /// <param name="strand"></param>
        /// <param name="molCount">Filtered molecule count at given genomic location</param>
        /// <param name="readProfile">Number of reads as function of rndTag index at given genomic location, or null if no reads has hit that location</param>
        public void GetReadCountProfile(string chr, int pos, char strand, out int molCount, out ushort[] readProfile)
        {
            chrTagDatas[chr].GetReadCounts(pos, strand, out molCount, out readProfile);
        }

        /// <summary>
        /// Number of distinct mappings (position + strand) since last barcode change.
        /// Note that this may be higher than number of distinct molecules if multiread mappings to exons are analyzed
        /// </summary>
        /// <returns>Number of distinct mappings (position + strand) that have been observed in current barcode, irrespective of rndTags</returns>
        public int GetNumDistinctMappings()
        {
            int nAllChr = 0;
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
                nAllChr += chrTagData.GetNumDistinctMappings();
            return nAllChr;
        }

        public int TagItemCount()
        {
            int n = 0;
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
                n += chrTagData.TagItemCount();
            return n;
        }
    }
}
