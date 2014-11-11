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
        /// <summary>
        /// Mismatches closer than this to either read end will not be used for SNP analysis
        /// </summary>
        public static int marginInReadForSNP = 2;
        public static int averageReadLen; // Needed for SNP handling

        public ChrTagData(string chr)
        {
            this.chr = chr;
            tagItems = new Dictionary<int, TagItem>();
        }

        public string chr;

        /// <summary>
        /// PosAndStrand_on_Chr -> countsByRndTagIdx
        /// Read start position stored as "(pos * 2) | strand" where strand in bit0: +/- => 0/1
        /// </summary>
        private Dictionary<int, TagItem> tagItems;
        public int NumTagItems { get { return tagItems.Count; } }

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
                item = TagItem.CreateTagItem();
                tagItems[posStrand] = item;
            }
            tagItems[posStrand].RegisterSNP(snpOffset);
        }

        /// <summary>
        /// Should be called when all reads from the same barcode have been registered
        /// </summary>
        public void FinishBarcode()
        {
            foreach (TagItem tagItem in tagItems.Values)
                tagItem.Clear();
        }

        /// <summary>
        /// Add a read and checks whether the specified UMI has been seen before on the pos and strand.
        /// </summary>
        /// <param name="m">A multireadmapping to analyze</param>
        /// <param name="isTranscript"></param>
        /// <returns>true if the UMI is new at this position-strand</returns>
        public bool Add(MultiReadMapping m, bool isTranscript)
        {
            int posStrand = MakePosStrandIdx(m.Position, m.Strand);
            TagItem item;
            if (!tagItems.TryGetValue(posStrand, out item))
            {
                item = TagItem.CreateTagItem(m.HasAltMappings, isTranscript);
                tagItems[posStrand] = item;
            }
            else if (item.hasAltMappings && !m.HasAltMappings && !m.HasMismatches && item.GetBcNumReads() == 1)
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
        /// Add a read and check whether the specified UMI has been seen before on the pos and strand.
        /// </summary>
        /// <param name="m">A multireadmapping to analyze</param>
        /// <param name="sharingRealFeatures">List of other features that compete due to the read being a multiread</param>
        /// <param name="isTranscript"></param>
        /// <returns>true if the UMI is new at this position-strand</returns>
        public bool Add(MultiReadMapping m, Dictionary<IFeature, object> sharingRealFeatures, bool isTranscript)
        {
            int posStrand = MakePosStrandIdx(m.Position, m.Strand);
            TagItem item;
            if (!tagItems.TryGetValue(posStrand, out item))
            {
                item = TagItem.CreateTagItem(m.HasAltMappings, isTranscript);
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
            if (Props.props.ShowTranscriptSharingGenes)
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
        /// Use to get the filtered molecule count and read count profile for a specific genomic position and strand.
        /// If no data exists, readProfile == null and moCount == 0.
        /// </summary>
        /// <param name="pos">Requested hit start pos</param>
        /// <param name="strand"></param>
        /// <param name="molCount">Estimated number of molecules detected after filtering</param>
        /// <param name="readProfile">Array with number of reads in each UMI</param>
        public void GetReadCounts(int pos, char strand, out int molCount, out ushort[] readProfile)
        {
            int posStrand = MakePosStrandIdx(pos, strand);
            TagItem t;
            if (tagItems.TryGetValue(posStrand, out  t))
            {
                readProfile = t.GetReadCountsByUMI();
                molCount = t.GetFinalBcNumMols();
            }
            else
            {
                readProfile = null;
                molCount = 0;
            }
        }

        /// <summary>
        /// Number of distinct mappings (position-strand) that have been observed, irrespective of UMIs.
        /// Note that this may be more than number of distinct molecules, if all multiread mappings to exons are analyzed
        /// </summary>
        /// <returns>Number of distinct mappings (position-strand) that have been observed, irrespective of UMIs</returns>
        public int GetNumDistinctMappings()
        {
            return tagItems.Values.Count(v => v.HasReads);
        }

        /// <summary>
        /// Summarizes all hit start positions and the the respective read and molecule count
        /// </summary>
        /// <param name="strand">Strand to analyze</param>
        /// <param name="positions">All positions with some mapped read start on given strand</param>
        /// <param name="molCountAtEachPosition">Number of distinct UMIs (=molecules) mapped at each of these positions</param>
        /// <param name="readCountAtEachPosition">Number of reads mapped at each of these positions</param>
        public void GetDistinctPositionsAndCounts(char strand, int[] selectedAnnotTypes, out int[] positions,
                                                  out int[] molCountAtEachPosition, out int[] readCountAtEachPosition, bool allBarcodes)
        {
            positions = new int[tagItems.Count];
            molCountAtEachPosition = new int[tagItems.Count];
            readCountAtEachPosition = new int[tagItems.Count];
            int strandIdx = (strand == '+') ? 0 : 1;
            int p = 0;
            foreach (KeyValuePair<int, TagItem> codedPair in tagItems)
            {
                int numReads = codedPair.Value.GetNumReads(allBarcodes);
                if (numReads > 0 && (codedPair.Key & 1) == strandIdx &&
                    (selectedAnnotTypes == null || selectedAnnotTypes.Contains(codedPair.Value.typeOfAnnotation)))
                {
                    readCountAtEachPosition[p] = numReads;
                    molCountAtEachPosition[p] = codedPair.Value.GetFinalNumMols(allBarcodes);
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
        private bool hasUMIs;
        public Dictionary<string, ChrTagData> chrTagDatas;

        public static int nUMIs;
        /// <summary>
        /// Number of reads in every UMI
        /// </summary>
        public int[] nReadsByUMI;
        /// <summary>
        /// Histogram of saturation of UMIs by different position-strand combinations
        /// </summary>
        public int[] nCasesPerUMICount;

        /// <summary>
        /// Total number of detected molecules mapping to any feature type before mutation filter
        /// </summary>
        public int totalMolecules = 0;
        /// <summary>
        /// Total number of detected EXON mapping molecules before mutation filter
        /// </summary>
        public int totalTrMolecules = 0;
        /// <summary>
        /// Total number of detected molecules mapping to any feature type after mutation filter
        /// </summary>
        public int totalFilteredMolecules = 0;
        /// <summary>
        /// Total number of detected EXON mapping molecules after mutation filter
        /// </summary>
        public int totalFilteredTrMolecules = 0;
        /// <summary>
        /// Histogram of #reads/molecule (mapping to any feature type) after mutation filter
        /// </summary>
        public int[] readsPerMolHistogram;
        /// <summary>
        /// Histogram of #reads/molecule for EXON mappings after mutation filter
        /// </summary>
        public int[] readsPerTrMolHistogram;
        /// <summary>
        /// Histogram showing distribution of #reads for each number of rndTags detected across all mapped positions in the experiment
        /// </summary>
        public int[,] readDistributionByMolCount;

        public static readonly int MaxValueInReadCountHistogram = 5000;

        public RandomTagFilterByBc(Barcodes barcodes, string[] chrIds)
        {
            hasUMIs = barcodes.HasUMIs;
            nUMIs = barcodes.UMICount;
            TagItem.nUMIs = nUMIs;
            nReadsByUMI = new int[nUMIs];
            nCasesPerUMICount = new int[nUMIs + 1];
            chrTagDatas = new Dictionary<string, ChrTagData>();
            foreach (string chrId in chrIds)
                chrTagDatas[chrId] = new ChrTagData(chrId);
            readsPerMolHistogram = new int[MaxValueInReadCountHistogram + 1];
            readsPerTrMolHistogram = new int[MaxValueInReadCountHistogram + 1];
            readDistributionByMolCount = new int[nUMIs + 1, MaxValueInReadCountHistogram + 1];
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
                if (nUMIs > 1)
                {
                    FinishBarcodeWithUMIs(chrTagData);
                }
                chrTagData.FinishBarcode();
            }
            //Console.WriteLine("RndTagFilterByBc: Current total NumTagItems=" + NumTagItems);
        }

        public int NumTagItems
        {
            get
            {
                int n = 0;
                foreach (ChrTagData d in chrTagDatas.Values)
                    n += d.NumTagItems;
                return n;
            }
        }

        /// <summary>
        /// Add to summary of read/mol-related statistics for which raw data is lost for each new barcode
        /// </summary>
        /// <param name="chrTagData"></param>
        private void FinishBarcodeWithUMIs(ChrTagData chrTagData)
        {
            foreach (TagItem tagItem in chrTagData.IterNonEmptyTagItems())
            {
                int nUsedUMIs = tagItem.GetNumUsedUMIs();
                totalMolecules += nUsedUMIs;
                int nFilteredBcMols = tagItem.GetFinalBcNumMols();
                totalFilteredMolecules += nFilteredBcMols;
                if (tagItem.typeOfAnnotation == AnnotType.EXON)
                {
                    totalTrMolecules += nUsedUMIs;
                    totalFilteredTrMolecules += nFilteredBcMols;
                }
                nCasesPerUMICount[nUsedUMIs]++;
                if (TagItem.CountsReadsPerUMI)
                    AddToReadsPerMolecule(tagItem, nUsedUMIs);
            }
        }

        private void AddToReadsPerMolecule(TagItem tagItem, int nUsedUMIs)
        {
            int threshold = tagItem.GetMutationThreshold();
            foreach (ushort nReadsInUMI in tagItem.GetReadCountsByUMI().Where(c => c > threshold))
            {
                int limitedReadCount = Math.Min(MaxValueInReadCountHistogram, nReadsInUMI);
                readsPerMolHistogram[limitedReadCount]++;
                readDistributionByMolCount[nUsedUMIs, limitedReadCount]++;
                if (tagItem.typeOfAnnotation == AnnotType.EXON)
                {
                    readsPerTrMolHistogram[limitedReadCount]++;
                }
            }
        }

        /// <summary>
        /// Add a mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="isTranscript"></param>
        /// <returns>True if the chr-strand-pos-bc-rndTag combination is new</returns>
        public bool Add(MultiReadMapping m, bool isTranscript)
        {
            nReadsByUMI[m.UMIIdx]++;
            bool isNew = chrTagDatas[m.Chr].Add(m, isTranscript);
            return isNew | !hasUMIs;
        }

        /// <summary>
        /// Add a mapped read and check if the read represents a new molecule.
        /// Reads have to be submitted in series containing all reads for each barcode.
        /// Will record the transcripts competing for the read if it is multiread
        /// </summary>
        /// <param name="m">The mapping to add</param>
        /// <param name="sharingRealFeatures">List of other features compete due to the read being a multiread</param>
        /// <param name="isTranscript"></param>
        /// <returns>True if the chr-strand-pos-bc-UMI combination is new</returns>
        public bool Add(MultiReadMapping m, Dictionary<IFeature, object> sharingRealFeatures, bool isTranscript)
        {
            nReadsByUMI[m.UMIIdx]++;
            bool isNew = chrTagDatas[m.Chr].Add(m, sharingRealFeatures, isTranscript);
            return isNew | !hasUMIs;
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
        /// <param name="readProfile">Number of reads as function of UMI index at given genomic location, or null if no reads has hit that location</param>
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

        /// <summary>
        /// Snapshot of number of molecules, after mutated UMI filtering, detected so far in current barcode.
        /// </summary>
        /// <returns>Current estimate after filtering of mutations (note that some molecules may be filtered away later during processing)</returns>
        public int GetCurrentNumFilteredMolecules()
        {
            int total = 0;
            foreach (ChrTagData chrTagData in chrTagDatas.Values)
            {
                foreach (TagItem tagItem in chrTagData.IterNonEmptyTagItems())
                    if (tagItem.typeOfAnnotation == AnnotType.EXON)
                        total += tagItem.CalcCurrentBcNumMols();
            }
            return total;
        }

    }
}
