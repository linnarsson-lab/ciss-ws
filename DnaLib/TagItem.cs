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
        private List<SNPCounter> cachedSNPCounts;

        /// <summary>
        /// Number of molecules (after filtering mutated rndTags). Equals ReadCount if rndTags are not used.
        /// </summary>
        public int MolCount { get { return cachedMolCount; } }
        public int ReadCount { get { return cachedReadCount; } }
        public int EstTrueMolCount { get { return cachedEstTrueMolCount; } }
        public List<SNPCounter> SNPCounts { get { return cachedSNPCounts; } }

        private TagItem m_TagItem;
        public TagItem tagItem { get { return m_TagItem; } }

        public void Update(int hitStartPos, char strand, TagItem tagItem)
        {
            this.m_HitStartPos = hitStartPos;
            this.m_Strand = strand;
            this.m_TagItem = tagItem;
            cachedMolCount = m_TagItem.GetNumMolecules();
            cachedReadCount = m_TagItem.GetNumReads();
            cachedEstTrueMolCount = EstimateFromSaturatedLabels(cachedMolCount);
            cachedSNPCounts = m_TagItem.GetTotalSNPCounts(m_HitStartPos);
        }
        public static int EstimateFromSaturatedLabels(int numMolecules)
        {
            return (int)Math.Round(Math.Log(1 - numMolecules / (double)TagItem.nRndTags) / Math.Log(1 - TagItem.LabelingEfficiency / (double)TagItem.nRndTags));
        }

        public int bcIdx;
        public string chr;
        private int m_HitStartPos;
        public int hitStartPos { get { return m_HitStartPos; } }
        private char m_Strand;
        public char strand { get { return m_Strand; } }
        public int splcToRealChrOffset = 0;
        public bool hasAltMappings { get { return m_TagItem.hasAltMappings; } }
        public int HitLen { get { return AverageReadLen; } }
        public int HitMidPos { get { return hitStartPos + HitLen / 2 + splcToRealChrOffset; } }

        public override string ToString()
        {
            return string.Format("MappedTagItem(chr={0} strand={1} hitStartPos={2} [Average]HitLen={3} bcIdx={4} HitMidPos={5} MolCount={6} ReadCount={7} HasAltMappings={8})",
                                 chr, strand, hitStartPos, HitLen, bcIdx, HitMidPos, MolCount, ReadCount, m_TagItem.hasAltMappings);
        }

    }

    /// <summary>
    /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
    /// </summary>
    public class TagItem
    {
        /// <summary>
        /// Threshold for removing molecules that are a result of mutated rnd tags.
        /// RndTags having less than (1 / value) of the reads of the rndTag with maximum reads are not counted.
        /// Need to investigate various seq depths before final decision. 20 may be more appropriate.
        /// </summary>
        public static int ratioForMutationFilter = 50;
        /// <summary>
        /// Mirrors the number of rndTags from Barcodes
        /// </summary>
        public static int nRndTags;
        public static double LabelingEfficiency;

        /// <summary>
        /// Counts number of reads in each rndTag
        /// </summary>
        private ushort[] readCountsByRndTag;
        /// <summary>
        /// Counts number of reads irrespective of rndTag
        /// </summary>
        private int totalReadCount;

        /// <summary>
        /// List of the genes that share this TagItem's counts. (If some reads are SNPed, the sharing genes may be not belong to all reads.)
        /// </summary>
        public Dictionary<IFeature, int> sharingGenes = new Dictionary<IFeature, int>();

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
            totalReadCount = 0;
            sharingGenes.Clear();
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
            int currentCount = totalReadCount;
            totalReadCount++;
//            if (nRndTags > 1)
//            {
                if (readCountsByRndTag == null)
                    readCountsByRndTag = new ushort[nRndTags];
                currentCount = readCountsByRndTag[rndTagIdx];
                readCountsByRndTag[rndTagIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
//            }
            return currentCount == 0;
        }

        /// <summary>
        /// Record other transcripts that share the count from a multiread
        /// </summary>
        /// <param name="sharingRealFeatures"></param>
        public void AddSharedGenes(Dictionary<IFeature, object> sharingRealFeatures)
        {
            foreach (IFeature sGf in sharingRealFeatures.Keys)
            {
                if (this.sharingGenes.ContainsKey(sGf))
                    this.sharingGenes[sGf] += 1;
                else
                    this.sharingGenes[sGf] = 1;
            }
        }

        public bool HasReads { get { return totalReadCount > 0; } }
        public bool HasSNPs { get { return tagSNPData != null; } }

        /// <summary>
        /// Return the total number of reads at this position-strand. (Molecule mutation filter not applied for rndTag data.)
        /// </summary>
        /// <returns></returns>
        public int GetNumReads()
        {
            return totalReadCount;
        }

        /// <summary>
        /// Get Nt counts at all positions where there is SNP data available
        /// </summary>
        /// <param name="readPosOnChr">Needed to convert the SNP offset to position within chromosome</param>
        /// <returns>SNPCounters that summarize the (winning, if some mutated read) Nts found at each offset in the given rndTags</returns>
        public List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
        {
            List<SNPCounter> totalCounters = new List<SNPCounter>();
            if (tagSNPData != null)
            {
                List<int> validRndTags = GetValidRndTags();
                int nTotal = GetNumMolecules();
                foreach (KeyValuePair<byte, SNPCountsByRndTag> p in tagSNPData.SNPCountsByOffset)
                {
                    if (p.Value != null)
                    {
                        int snpPosOnChr = readPosOnChr + p.Key;
                        SNPCounter countsAtOffset = new SNPCounter(snpPosOnChr);
                        p.Value.Summarize(countsAtOffset, validRndTags);
                        countsAtOffset.nTotal = (ushort)nTotal;
                        totalCounters.Add(countsAtOffset);
                    }
                }
            }
            return totalCounters;
        }

        /// <summary>
        /// Get indices of the rndTags that represent real molecules and not only mutations from other rndTags.
        /// If no random tags are used, get all indices that contain any reads.
        /// </summary>
        /// <returns>Indices of random tags containing real data (not stemming from mutations in other random tags)</returns>
        public List<int> GetValidRndTags()
        {
            List<int> validTagIndices = new List<int>();
// 3 new lines:
            if (nRndTags == 1)
                validTagIndices.Add(0);
            else
//            if (readCountsByRndTag != null)
            {
                int cutOff = readCountsByRndTag.Max() / ratioForMutationFilter;
                //int cutOff = (nRndTags > 1) ? readCountsByRndTag.Max() / ratioForMutationFilter : 0;
                for (int rndTagIdx = 0; rndTagIdx < readCountsByRndTag.Length; rndTagIdx++)
                    if (readCountsByRndTag[rndTagIdx] > cutOff)
                        validTagIndices.Add(rndTagIdx);
            }
            return validTagIndices;
        }

        /// <summary>
        /// Get number of molecules (reads if rndTag not used) at this position-strand.
        /// Simple % cutoff used to get rid of mutated rndTags.
        /// </summary>
        /// <returns>Number of molecules (mutated rndTags excluded), or number of reads if no rndTags were used.</returns>
        public int GetNumMolecules()
        {
            if (nRndTags == 1) return totalReadCount;
            int n = 0;
            if (readCountsByRndTag != null)
            {
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
            if (nRndTags == 1)
                return new ushort[1] { (ushort)totalReadCount };
            return readCountsByRndTag;
        }

    }
}
