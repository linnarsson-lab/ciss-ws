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
        private int cachedEstTrueMolCount;
        private List<SNPCounter> cachedMolSNPCounts;

        public int MolCount { get { return cachedMolCount; } }
        public int ReadCount { get { return cachedReadCount; } }
        public int EstTrueMolCount { get { return cachedEstTrueMolCount; } }
        public List<SNPCounter> MolSNPCounts { get { return cachedMolSNPCounts; } }

        private TagItem m_TagItem;
        public TagItem tagItem
        {
            get { return m_TagItem; }
            set
            {
                m_TagItem = value;
                cachedMolCount = m_TagItem.GetNumMolecules();
                cachedReadCount = m_TagItem.GetNumReads();
                cachedEstTrueMolCount = EstimateFromSaturatedLabels(cachedMolCount);
                cachedMolSNPCounts = m_TagItem.GetTotalSNPCounts(hitStartPos);
            }
        }
        public static int EstimateFromSaturatedLabels(int numMolecules)
        {
            return (int)Math.Round(Math.Log(1 - numMolecules / TagItem.nRndTags) / Math.Log(1 - TagItem.LabelingEfficiency / TagItem.nRndTags));
        }

        public int bcIdx;
        public string chr;
        public int hitStartPos;
        public char strand;
        public int splcToRealChrOffset = 0;
        public bool hasAltMappings { get { return m_TagItem.hasAltMappings; } }
        public int HitLen { get { return AverageReadLen; } }
        public int HitMidPos
        {
            get { return hitStartPos + HitLen / 2 + splcToRealChrOffset; }
            set { hitStartPos = value - HitLen / 2; }
        }

        public override string ToString()
        {
            return string.Format("MappedLoc=chr{0}{1}.{2} Bc={3} HitMidPos={4} #Mols={5} #Reads={6} HasAltMappings={7}",
                                 chr, strand, hitStartPos, bcIdx, HitMidPos, MolCount, ReadCount, m_TagItem.hasAltMappings);
        }

    }

    /// <summary>
    /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
    /// </summary>
    public class TagItem
    {
        public static int ratioForMutationFilter = 50; // 20 seems a good number, but need to investigate various seq depths before final decision
        public static int nRndTags;
        public static double LabelingEfficiency;

        /// <summary>
        /// Counts number of reads in each rndTag
        /// </summary>
        private ushort[] readCountsByRndTag;
        /// <summary>
        /// SNP data for SNP positions by offsets from read startPos. Should only be affected when hasAltMappings == false
        /// The final SNP status of a chromosomal position has to be calculated from the tagSNPData:s of all spanning TagItems
        /// </summary>
        public TagSNPCounters tagSNPData;
        /// <summary>
        /// true when the read sequence at the (chr, pos, strand) of this TagItem is not unique in genome.
        /// </summary>
        public bool hasAltMappings;

        /// <summary>
        /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
        /// </summary>
        /// <param name="hasAltMappings">True indicates that the location is not unique in the genome</param>
        public TagItem(bool hasAltMappings)
        {
            this.hasAltMappings = hasAltMappings;
        }

        public override string ToString()
        {
            string res = string.Format("TagItem: #Mols={0} #Reads={1} HasAltMappings={2}\n  Locations:", GetNumMolecules(), GetNumReads(), hasAltMappings);
            return res;
        }

        /// <summary>
        /// Prepare for analyzing potential SNPs at specified offset within the reads
        /// </summary>
        /// <param name="snpOffset"></param>
        public void RegisterSNP(byte snpOffset)
        {
            if (tagSNPData == null)
                tagSNPData = new TagSNPCounters();
            tagSNPData.RegisterSNPAtOffset(snpOffset);
        }

        /// <summary>
        /// Clear data before handling the next barcode
        /// </summary>
        public void Clear()
        {
            readCountsByRndTag = null;
            if (tagSNPData != null)
                tagSNPData.Clear();
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
        /// Mark a SNP within a read
        /// </summary>
        /// <param name="rndTagIdx">rndTag of the read</param>
        /// <param name="snpOffset">0-based offset within the read, counting in chromosome direction</param>
        /// <param name="snpNt"></param>
        public void AddSNP(int rndTagIdx, byte snpOffset, char snpNt)
        {
            tagSNPData.AddSNP(rndTagIdx, snpOffset, snpNt);
        }

        /// <summary>
        /// Get Nt counts at all positions where there is SNP data available
        /// </summary>
        /// <param name="readPosOnChr">Only to set the actual SNP pos correctly</param>
        /// <returns>SNPCounters that summarize the (winning, if some mutated read) Nts found at each offset in the given rndTags</returns>
        public List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
        {
            List<SNPCounter> totalCounters = new List<SNPCounter>();
            if (tagSNPData != null)
            {
                List<int> validRndTags = GetValidMolRndTags();
                int nTotal = GetNumMolecules();
                foreach (KeyValuePair<byte, SNPCounter[]> p in tagSNPData.SNPCountersByOffset)
                {
                    if (p.Value != null)
                    {
                        SNPCounter countsAtOffset = new SNPCounter(readPosOnChr + p.Key);
                        foreach (int rndTagIdx in validRndTags)
                        {
                            char winningNtInRndTag = p.Value[rndTagIdx].GetNt();
                            countsAtOffset.Add(winningNtInRndTag);
                        }
                        countsAtOffset.nTotal = nTotal;
                        totalCounters.Add(countsAtOffset);
                    }
                }
            }
            return totalCounters;
        }

        public bool HasReads { get { return readCountsByRndTag != null; } }
        public bool HasSNPs { get { return tagSNPData != null; } }

        /// <summary>
        /// Return the total number of reads at this position-strand. (Molecule mutation filter not applied for rndTag data.)
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
        /// Get number of reads in each rndTag. (Mutated molecule reads are not filtered when rndTags are used.)
        /// </summary>
        /// <returns>null if no reads have been found</returns>
        public ushort[] GetReadCountsByRndTag()
        {
            return readCountsByRndTag;
        }

    }
}
