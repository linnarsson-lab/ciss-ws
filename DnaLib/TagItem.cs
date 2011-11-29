using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
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

    public class TagItem
    {
        public static int nRndTags;

        // TagItem summarize all reads that map to a specific (chr, pos, strand) combination.
        private ushort[] molCounts; // Count of reads in each rndTag like before
        public RndTagSNPData SNPData; // SNP data for known SNP positions by offsets from read startPos. Only valid when hasAltMappings == false
        public bool hasAltMappings; // true when the (chr, pos, strand) read seq is not unique in genome.

        // molCount & snpCount can probably be reduced to byte once we know how to detect
        // saturation of reads and mutation rates for mutation removal.
        //
        // A central Dictionary saves pointers from (pre-calculated) alternative mappings to common
        // TagItems for redundant exons positions, to avoid the multiread issue:
        // Dictionary<int, TagItem> mappings;
        // where keys are (chr, pos, strand) items. All these pre-ínitialized TagItems will have hasAltMappings == true
        // For unique mappings, new KeyValuePairs (hasAltMappings == false) are added as new reads are detected.
        // Save memory by keeping the pre-initialized TagItems count data empty until some actual reads are assigned to them.
        // Note that SNPs can only be correctly analyzed for mappings that are unique by the
        // bowtie options used, i.e. hasAltMappings == false, which also controls Max/Min counts for genes during annotation.
        //
        // Annotation is performed after each barcode has been exhausted of reads, and goes through all KeyValue pairs in mappings
        // above, asking every TagItem for (#reads, #molecules, IsSNP?,HasAltMappings?) and annotates all matching features.
        //
        // The process of first counting all reads before annotating, loses the readLen of each
        // read. Maybe a stronger limit on min readLen should be used, and small A-tails not be removed,
        // so that a constant readLen can be assumed.
        //
        // This setup allows only one SNP within the readLen window of this TagItem.
        // ...handle that e.g. by changing offset and Nt when detecting a new and count is only <= 1.
        // snpCount should be 0 or equal to molCount, otherwise the molecule is probably really
        // two mixed molecules, one from each allele (ca.50/50), or due to a mutation during PCR (any ratio).
        // Whether the SNP is real can be inferred by scanning all rndTags of TagItems spanning the SNP position.
        // Then, every molecule that spans a SNP can be assigned to either allele.
        
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
        /// Clear data for handling the next barcode
        /// </summary>
        public void Clear()
        {
            molCounts = null;
            if (SNPData != null)
                SNPData.Clear();
        }

        /// <summary>
        /// Add a read to the data, skipping any SNP annotations
        /// </summary>
        /// <param name="rndTagIdx"></param>
        /// <returns>True if the rndTag is new</returns>
        public bool Add(int rndTagIdx)
        {
            if (molCounts == null)
                molCounts = new ushort[nRndTags];
            int currentCount = molCounts[rndTagIdx];
            molCounts[rndTagIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
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

        public bool HasReads { get { return molCounts != null; } }
        public bool HasSNPs { get { return SNPData != null; } }

        /// <summary>
        /// Return the total number of reads at this position-strand
        /// </summary>
        /// <returns></returns>
        public int GetNumReads()
        {
            int n = 0;
            if (molCounts != null)
                foreach (int c in molCounts) n += c;
            return n;
        }

        /// <summary>
        /// Get indices of the rndTags that represent real molecules and not only mutations from other rndTags
        /// </summary>
        /// <returns></returns>
        public List<int> GetValidMolRndTags()
        {
            List<int> validTagIndices = new List<int>();
            if (molCounts != null)
            {
                int maxNumReads = molCounts.Max();
                int cutOff = maxNumReads / 10;
                for (int rndTagIdx = 0; rndTagIdx < molCounts.Length; rndTagIdx++)
                    if (molCounts[rndTagIdx] > cutOff) validTagIndices.Add(rndTagIdx);
            }
            return validTagIndices;
        }

        /// <summary>
        /// Get number of molecules at this position-strand.
        /// Simple % cutoff used to get rid of mutated rndTags.
        /// </summary>
        /// <returns></returns>
        public int GetNumMolecules()
        {
            int n = 0;
            if (molCounts != null)
            {
                int maxNumReads = molCounts.Max();
                int cutOff = maxNumReads / 10;
                foreach (int c in molCounts)
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
            return molCounts;
        }

    }
}
