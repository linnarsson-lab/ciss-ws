using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Keeps TagItems together with their location during the annotation step
    /// </summary>
    public class MappedTagItem
    {
        public static int AverageReadLen;

        private int cachedMolCount;
        private int cachedReadCount;
        public int MolCount { get { return cachedMolCount; } }
        public int ReadCount { get { return cachedReadCount; } }

        private TagItem m_TagItem;
        public TagItem tagItem
        {
            get { return m_TagItem; }
            set
            {
                m_TagItem = value;
                cachedMolCount = m_TagItem.GetNumMolecules();
                cachedReadCount = m_TagItem.GetNumReads();
            }
        }
        public int bcIdx;
        public string chr;
        public int hitStartPos;
        public char strand;
        public int splcToRealChrOffset = 0;
        public int HitLen { get { return AverageReadLen; } }
        public int HitMidPos
        {
            get { return hitStartPos + HitLen / 2 + splcToRealChrOffset; }
            set { hitStartPos = value - HitLen / 2; }
        }

        public override string ToString()
        {
            return string.Format("Loc=chr{0}{1}{2} Bc={3} HitStartPos={4} #Mols={5} #Reads={6}", chr, strand, hitStartPos, bcIdx, hitStartPos, MolCount, ReadCount);
        }

        public IEnumerable<LocatedSNPCounter> IterMolSNPCounts()
        {
            if (tagItem.SNPData == null) yield break;
            List<int> validRndTags = tagItem.GetValidMolRndTags();
            LocatedSNPCounter locCounter = new LocatedSNPCounter();
            locCounter.chr = chr;
            foreach (byte snpOffset in tagItem.SNPData.GetSNPOffsets())
            {
                SNPCounter snpc = tagItem.SNPData.GetMolSNPCounts(snpOffset, validRndTags);
                snpc.nTotal = tagItem.GetNumMolecules();
                locCounter.chrPos = hitStartPos + snpOffset;
                locCounter.counter = snpc;
                yield return locCounter;
            }
        }
    
    }

    /// <summary>
    /// TagItem summarizes all reads that map to a specific [or a set of redundant] (chr, pos, strand) combination[s].
    /// </summary>
    public class TagItem
    {
        public static int ratioForMutationFilter = 1000; //10
        public static int nRndTags;
        /// <summary>
        /// Counts number of reads in each rndTag
        /// </summary>
        private ushort[] readCountsByRndTag;
        /// <summary>
        /// SNP data for known SNP positions by offsets from read startPos. Only valid when hasAltMappings == false
        /// </summary>
        public RndTagSNPData SNPData;
        /// <summary>
        /// true when the read sequence at the (chr, pos, strand) of this TagItem is not unique in genome.
        /// </summary>
        public bool hasAltMappings;

        // A central Dictionary saves pointers from (pre-calculated) alternative mappings to common
        // TagItems for redundant exons positions, to avoid the multiread issue:
        // Dictionary<int, TagItem> mappings;
        // where keys are (chr, pos, strand) items. All these pre-initialized TagItems will have hasAltMappings == true
        // For unique mappings, new KeyValuePairs with hasAltMappings == false are added as new reads are detected.
        // Note that SNPs can only be correctly analyzed for mappings that are unique, i.e. hasAltMappings == false.
        // hasAltMappings together with gene-shared exons will affect the Max/Min counts for genes during annotation.
        //
        // The process of first counting all reads before annotating, loses the readLen of each
        // read. A strong limit on min readLen should be used, and small A-tails not be removed,
        // so that a constant readLen can be assumed.
        
        /// <summary>
        /// Setup a TagItem that may be shared by all the mappings of a multiread.
        /// </summary>
        /// <param name="hasAltMappings">True indicates that this TagItem is shared by several (chr, pos, strand) locations</param>
        public TagItem(bool hasAltMappings)
        {
            this.hasAltMappings = hasAltMappings;
        }

        public void RegisterSNP(byte snpOffset)
        {
            if (SNPData == null)
                SNPData = new RndTagSNPData();
            SNPData.RegisterSNP(snpOffset);
        }

        /// <summary>
        /// Clear data before handling the next barcode
        /// </summary>
        public void Clear()
        {
            readCountsByRndTag = null;
            if (SNPData != null)
                SNPData.Clear();
        }

        /// <summary>
        /// Add a read to the data, ignoring any SNP annotations
        /// </summary>
        /// <param name="rndTagIdx"></param>
        /// <returns>True if the rndTag is new</returns>
        public bool Add(int rndTagIdx)
        {
            if (readCountsByRndTag == null)
                readCountsByRndTag = new ushort[nRndTags];
            int currentCount = readCountsByRndTag[rndTagIdx];
            readCountsByRndTag[rndTagIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
            return currentCount == 0;
        }

        /// <summary>
        /// Register a SNP within a read
        /// </summary>
        /// <param name="rndTagIdx"></param>
        /// <param name="snpOffset">0-based offset within the read, counting in chromosome direction</param>
        /// <param name="snpNt"></param>
        public void AddSNP(int rndTagIdx, byte snpOffset, char snpNt)
        {
            if (SNPData == null) return;
            SNPData.AddSNP(rndTagIdx, snpOffset, snpNt);
        }

        /// <summary>
        /// Count number of molecules with various bases at given SNP offset in read (offset in chromosome direction)
        /// </summary>
        /// <param name="snpOffset"></param>
        /// <returns></returns>
        public SNPCounter GetMolSNPCounts(byte snpOffset)
        {
            if (SNPData == null) return null;
            SNPCounter snpc = SNPData.GetMolSNPCounts(snpOffset, GetValidMolRndTags());
            snpc.nTotal = GetNumMolecules();
            return snpc;
        }

        public bool HasReads { get { return readCountsByRndTag != null; } }
        public bool HasSNPs { get { return SNPData != null; } }

        /// <summary>
        /// Return the total number of reads at this position-strand
        /// </summary>
        /// <returns></returns>
        public int GetNumReads()
        {
            int n = 0;
            if (readCountsByRndTag != null)
                foreach (int c in readCountsByRndTag) n += c;
            return n;
        }

        /// <summary>
        /// Get indices of the rndTags that represent real molecules and not only mutations from other rndTags
        /// </summary>
        /// <returns></returns>
        public List<int> GetValidMolRndTags()
        {
            List<int> validTagIndices = new List<int>();
            if (readCountsByRndTag != null)
            {
                int maxNumReads = readCountsByRndTag.Max();
                int cutOff = maxNumReads / ratioForMutationFilter;
                for (int rndTagIdx = 0; rndTagIdx < readCountsByRndTag.Length; rndTagIdx++)
                    if (readCountsByRndTag[rndTagIdx] > cutOff) validTagIndices.Add(rndTagIdx);
            }
            return validTagIndices;
        }

        /// <summary>
        /// Get number of molecules at this position-strand.
        /// Simple % cutoff used to get rid of mutated rndTags.
        /// </summary>
        /// <returns>Number of molecules (mutated rndTags excluded), or number of reads if no rndTags were used.</returns>
        public int GetNumMolecules()
        {
            int n = 0;
            if (readCountsByRndTag != null)
            {
                if (nRndTags == 1)
                    return readCountsByRndTag[0];
                int maxNumReads = readCountsByRndTag.Max();
                int cutOff = maxNumReads / ratioForMutationFilter;
                foreach (int c in readCountsByRndTag)
                    if (c > cutOff) n++;
            }
            return n;
        }

        /// <summary>
        /// Get number of reads in each rndTag
        /// </summary>
        /// <returns>null if no reads have been found</returns>
        public ushort[] GetReadCountsByRndTag()
        {
            return readCountsByRndTag;
        }

    }
}
