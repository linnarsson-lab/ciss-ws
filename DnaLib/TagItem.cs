using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
    /// </summary>
    public abstract class TagItem
    {
        public static TagItem CreateTagItem(bool hasAltMappings, bool isTranscript)
        {
            if (Props.props.DenseUMICounter)
                return new ZeroOneMoreTagItem(hasAltMappings, isTranscript);
            return new UShortTagItem(hasAltMappings, isTranscript);
        }
        public static TagItem CreateTagItem()
        {
            return CreateTagItem(false, false);
        }

        /// <summary>
        /// true indicates that # reads/UMI is counted and available (by the xxxTagItem subclass)
        /// </summary>
        public static bool CountsReadsPerUMI { get; protected set; }

        public static void InitTagItemType()
        {
            if (Props.props.DenseUMICounter)
                UShortTagItem.Init();
            else
                ZeroOneMoreTagItem.Init();
        }

        /// <summary>
        /// Mirrors the number of UMIs from Barcodes
        /// </summary>
        public static int nUMIs;

        /// <summary>
        /// Counts number of reads in barcode irrespective of UMI
        /// </summary>
        protected int bcNumReads;
        protected int totNumReads;

        /// <summary>
        /// Number of non-empty UMIs, after mutation filtering, or read count if no UMIs are used. -1 indicates not calculated yet.
        /// </summary>
        protected int filteredBcNumMols = -1;
        protected int filteredTotNumMols = 0;

        /// <summary>
        /// Return the total number of reads at this position-strand. (Molecule mutation filter not applied for UMI data.)
        /// </summary>
        /// <returns></returns>
        public virtual int GetBcNumReads()
        {
            return bcNumReads;
        }
        public int GetNumReads(bool allBarcodes)
        {
            return allBarcodes ? totNumReads : bcNumReads;
        }

        public virtual bool HasReads { get { return bcNumReads > 0; } }

        /// <summary>
        /// true when the read sequence at the (chr, pos, strand) of this TagItem is not unique in genome.
        /// </summary>
        public bool hasAltMappings;

        /// <summary>
        /// Special use for output of wiggle plots for only specific annotations
        /// </summary>
        public short typeOfAnnotation { get; set; }

        /// <summary>
        /// Predefined for used by GetIndicesOfUsedUMIsAfterFiltering() when UMI counting is not applicable
        /// </summary>
        protected static List<int> woUMIsUMIIndices = new List<int> { 0 };

        public int GetFinalNumMols(bool allBarcodes)
        {
            if (filteredBcNumMols == -1)
                CalcFinalBcNumMols();
            return allBarcodes ? filteredTotNumMols : filteredBcNumMols;
        }

        public int GetFinalBcNumMols()
        {
            if (filteredBcNumMols == -1)
                CalcFinalBcNumMols();
            return filteredBcNumMols;
        }

        /// <summary>
        /// Final number of molecules (or reads if UMIs are not used) after mutation filtering. 
        /// Call ONLY after all reads in barcode have been added! Used for speed up of repeated calls.
        /// </summary>
        /// <returns>Number of molecules (mutated UMIs excluded), or number of reads if UMIs are not used.</returns>
        protected abstract void CalcFinalBcNumMols();

        /// <summary>
        /// Should always be called by Clear() implementations
        /// </summary>
        public void ClearBase()
        {
            typeOfAnnotation = (short)AnnotType.NOHIT;
            bcNumReads = 0;
            filteredBcNumMols = -1;
        }

        /// <summary>
        /// Clear data before handling the next barcode. Always call ClearBase() in  implementations
        /// </summary>
        public virtual void Clear()
        {
            ClearBase();
        }

        public abstract Dictionary<IFeature, int> SharingGenes { get; }

        /// <summary>
        /// Prepare for analyzing potential SNPs at specified offset within the reads
        /// </summary>
        /// <param name="snpOffset"></param>
        public abstract void RegisterSNP(byte snpOffset);

        /// <summary>
        /// Add the Nt at a SNP position from a read.
        /// If the position has not been defined as a SNP by a previous call to RegisterSNPAtOffset(), it will be skipped
        /// </summary>
        /// <param name="UMIIdx">The UMI of the read</param>
        /// <param name="snpOffset">Offset within the read of the SNP</param>
        /// <param name="snpNt">The reads' Nt at the SNP positions</param>
        public abstract void AddSNP(int UMIIdx, Mismatch mm);

        public abstract bool HasSNPs { get; }

        /// <summary>
        /// Add a read to the data, ignoring any SNP annotations
        /// </summary>
        /// <param name="UMIIdx"></param>
        /// <returns>True if the UMI is new</returns>
        public abstract bool Add(int UMIIdx);

        /// <summary>
        /// Record other transcripts that share the count from a multiread
        /// </summary>
        /// <param name="sharingRealFeatures"></param>
        public abstract void AddSharedGenes(Dictionary<IFeature, object> sharingRealFeatures);

        /// <summary>
        /// Get Nt counts at all positions where there is SNP data available
        /// </summary>
        /// <param name="readPosOnChr">Needed to convert the SNP offset to position within chromosome</param>
        /// <returns>SNPCounters that summarize the (winning, if some mutated read) Nts found at each offset in valid UMIs,
        /// or null if no SNPs are present.</returns>
        public abstract List<SNPCounter> GetTotalSNPCounts(int readPosOnChr);

        /// <summary>
        /// Count number of molecules (reads if UMIs not used) at this position-strand.
        /// Filters away mutated UMIs according to thresholding filter.
        /// </summary>
        /// <returns>Number of molecules (mutated UMIs excluded), or number of reads if UMIs are not used.</returns>
        public abstract int CalcCurrentBcNumMols();

        public abstract int GetMutationThreshold();

        /// <summary>
        /// Count # UMIs with at least one read (no filtering applied)
        /// </summary>
        /// <returns></returns>
        public abstract int GetNumUsedUMIs();

        /// <summary>
        /// Get number of reads in each UMI. (Mutated molecule reads are not filtered away even when UMIs are used.)
        /// </summary>
        /// <returns>null if no reads have been found</returns>
        public abstract ushort[] GetReadCountsByUMI();
    }


    /// <summary>
    /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
    /// </summary>
    public class UShortTagItem : TagItem
    {
        public static void Init()
        {
            TagItem.CountsReadsPerUMI = false;
            UMIMutationFilters.SetUMIMutationFilter();
        }

        /// <summary>
        /// Counts number of reads in each UMI
        /// </summary>
        private ushort[] readCountsByUMI;

        /// <summary>
        /// List of the genes that share this TagItem's counts. (If some reads are SNPed, the sharing genes may be not belong to all reads.)
        /// </summary>
        private Dictionary<IFeature, int> sharingGenes;

        public override Dictionary<IFeature, int> SharingGenes { get { return sharingGenes; } }

        /// <summary>
        /// At each offset relative to the 5' pos on chr of the reads' alignment where some SNPs appear,
        /// keep an array by UMI of counts for each SNP nt. 
        /// </summary>
        private Dictionary<byte, SNPCountsByRndTag> SNPCountsByOffset { get; set; }

        /// <summary>
        /// Create a new full TagItem, which counts every read
        /// </summary>
        /// <param name="hasAltMappings">True indicates that the location is not unique in the genome</param>
        /// <param name="isTranscript">true to indicate that this TagItem represents exonic reads</param>
        public UShortTagItem(bool hasAltMappings, bool isTranscript)
        {
            this.hasAltMappings = hasAltMappings;
            this.typeOfAnnotation = isTranscript? (short)AnnotType.EXON : (short)AnnotType.NOHIT;
        }

        public override void RegisterSNP(byte snpOffset)
        {
            if (SNPCountsByOffset == null)
                SNPCountsByOffset = new Dictionary<byte, SNPCountsByRndTag>();
            SNPCountsByOffset[snpOffset] = null;
        }

        public override void AddSNP(int UMIIdx, Mismatch mm)
        {
            SNPCountsByRndTag SNPCounts;
            if (!SNPCountsByOffset.TryGetValue(mm.relPosInChrDir, out SNPCounts))
                return;
            if (SNPCounts == null)
            {
                SNPCounts = new SNPCountsByRndTag(mm.refNtInChrDir);
                SNPCountsByOffset[mm.relPosInChrDir] = SNPCounts;
            }
            SNPCounts.Add(UMIIdx, mm.ntInChrDir);
        }

        public override void Clear()
        {
            ClearBase();
            readCountsByUMI = null;
            if (sharingGenes != null)
                sharingGenes.Clear();
            if (SNPCountsByOffset != null)
                foreach (SNPCountsByRndTag counts in SNPCountsByOffset.Values)
                    if (counts != null)
                        counts.Clear();
        }

        public override bool Add(int UMIIdx)
        {
            int currentCount = bcNumReads;
            bcNumReads++;
            totNumReads++;
            if (readCountsByUMI == null)
                readCountsByUMI = new ushort[nUMIs];
            currentCount = readCountsByUMI[UMIIdx];
            readCountsByUMI[UMIIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
            return (currentCount == 0);
        }

        public override void AddSharedGenes(Dictionary<IFeature, object> sharingRealFeatures)
        {
            if (sharingRealFeatures.Count == 0) return;
            if (sharingGenes == null)
                sharingGenes = new Dictionary<IFeature, int>();
            foreach (IFeature sGf in sharingRealFeatures.Keys)
            {
                if (sharingGenes.ContainsKey(sGf))
                    sharingGenes[sGf] += 1;
                else
                    sharingGenes[sGf] = 1;
            }
        }

        public override bool HasSNPs { get { return SNPCountsByOffset != null; } }

        public override List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
        {
            if (SNPCountsByOffset == null)
                return null;
            List<SNPCounter> totalCounters = new List<SNPCounter>();
            var validRndTags = GetIndicesOfUsedUMIsAfterFiltering();
            ushort nTotal = (ushort)validRndTags.Count;
            foreach (KeyValuePair<byte, SNPCountsByRndTag> p in SNPCountsByOffset)
            {
                if (p.Value != null)
                {
                    int snpPosOnChr = readPosOnChr + p.Key;
                    SNPCounter countsAtOffset = new SNPCounter(snpPosOnChr);
                    p.Value.Summarize(countsAtOffset, validRndTags);
                    countsAtOffset.nTotal = nTotal;
                    totalCounters.Add(countsAtOffset);
                }
            }
            return totalCounters;
        }
        /// <summary>
        /// Get indices of the UMIs that represent real molecules and not only mutations from other UMIs.
        /// If no UMIs are used, get all indices that contain any reads.
        /// </summary>
        /// <returns>Indices of UMIs containing real data (not stemming from mutations in other UMIs)</returns>
        private List<int> GetIndicesOfUsedUMIsAfterFiltering()
        {
            if (nUMIs == 1)
                return woUMIsUMIIndices;
            List<int> filteredUsedUMIIndices = new List<int>();
            int threshold = UMIMutationFilters.filter(this);
            for (int i = 0; i < readCountsByUMI.Length; i++)
                if (readCountsByUMI[i] > threshold) filteredUsedUMIIndices.Add(i);
            return filteredUsedUMIIndices;
        }

        public override int CalcCurrentBcNumMols()
        {
            if (nUMIs == 1) 
                return bcNumReads;
            if (readCountsByUMI == null || bcNumReads == 0)
                return 0;
            int threshold = UMIMutationFilters.filter(this);
            return readCountsByUMI.Count(v => v > threshold);
        }

        protected override void CalcFinalBcNumMols()
        {
            if (filteredBcNumMols >= 0)
                return;
            if (nUMIs == 1)
                filteredBcNumMols = bcNumReads;
            else if (readCountsByUMI == null || bcNumReads == 0)
                filteredBcNumMols = 0;
            else
            {
                int threshold = UMIMutationFilters.filter(this);
                filteredBcNumMols = readCountsByUMI.Count(v => v > threshold);
            }
            filteredTotNumMols += filteredBcNumMols;
        }

        public override int GetNumUsedUMIs()
        {
            return readCountsByUMI.Count(c => c > 0);
        }

        public override ushort[] GetReadCountsByUMI()
        {
            if (nUMIs == 1)
                return new ushort[1] { (ushort)bcNumReads };
            return readCountsByUMI;
        }

        public override int GetMutationThreshold()
        {
            return UMIMutationFilters.filter(this);
        }

    }
}
