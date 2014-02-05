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
        public static LabelingEfficiencyEstimator labelingEfficiencyEstimator;

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
            cachedSNPCounts = m_TagItem.GetTotalSNPCounts(m_HitStartPos);
            cachedReadCount = m_TagItem.GetNumReads();

            int observedMolCount = m_TagItem.GetFinalNumMolecules(); // Possibly filter away mutated UMIs
            if (TagItem.nRndTags == 1)
            {
                cachedEstTrueMolCount = cachedMolCount = observedMolCount;
            }
            else if (observedMolCount < TagItem.nRndTags)
            {
                cachedMolCount = labelingEfficiencyEstimator.UMICollisionCompensate(observedMolCount); // Compensate for UMI collision effect
                cachedEstTrueMolCount = labelingEfficiencyEstimator.EstimateTrueCount(observedMolCount);
            }
            else
            {
                cachedMolCount = labelingEfficiencyEstimator.UMICollisionCompensate(observedMolCount - 1);
                cachedEstTrueMolCount = labelingEfficiencyEstimator.EstimateTrueCount(observedMolCount - 1);
                throw new Exception("All UMIs are used up!");
            }
        }

        public void SetTypeOfAnnotation(int annotType)
        {
            this.m_TagItem.typeOfAnnotation = (short)annotType;
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
        /// Threshold parameter for removing molecules that are a result of mutated rnd tags.
        /// Actual meaning depends on the MutationThresholder selected.
        /// </summary>
        private static int RndTagMutationFilterParameter;

        public static void SetRndTagMutationFilter(Props props)
        {
            if (props.RndTagMutationFilter == RndTagMutationFilterMethod.FractionOfMax)
                mutationThresholder = FractionOfMaxThresholder;
            else if (props.RndTagMutationFilter == RndTagMutationFilterMethod.FractionOfMean)
                mutationThresholder = FractionOfMeanThresholder;
            else if (props.RndTagMutationFilter == RndTagMutationFilterMethod.Singleton)
                mutationThresholder = SingletonThresholder;
            else
                mutationThresholder = LowPassThresholder;
            RndTagMutationFilterParameter = props.RndTagMutationFilterParam;
        }

        /// <summary>
        /// Returns a hit count threshold for filtering away rnd labels that likely stem from mutations in other rnd labels
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        private delegate int MutationThresholder(TagItem tagItem);
        private static MutationThresholder mutationThresholder;

        /// <summary>
        /// If RndTagMutationFilterParameter==0, all singletons will be removed.
        /// Otherwise, singletons will only be removed when some UMI has #reads > RndTagMutationFilterParameter.
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        private static int SingletonThresholder(TagItem tagItem)
        {
            foreach (int c in tagItem.GetReadCountsByRndTag())
                if (c > RndTagMutationFilterParameter)
                    return 1;
            return 0;
        }
        /// <summary>
        /// Will only count UMIs with > RndTagMutationFilterParameter reads.
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        private static int LowPassThresholder(TagItem tagItem)
        {
            return RndTagMutationFilterParameter;
        }
        private static int FractionOfMaxThresholder(TagItem tagItem)
        {
            int maxNumReads = tagItem.GetReadCountsByRndTag().Max();
            return maxNumReads / RndTagMutationFilterParameter;
        }
        private static int FractionOfMeanThresholder(TagItem tagItem)
        {
            double sum = 0.0;
            int n = 0;
            foreach (int i in tagItem.GetReadCountsByRndTag())
            {
                if (i > 0) 
                {
                    sum += i;
                    n++;
                }
                       
            }
            return (int)Math.Round(sum / n / RndTagMutationFilterParameter);
        }

        /// <summary>
        /// Mirrors the number of rndTags from Barcodes
        /// </summary>
        public static int nRndTags;

        /// <summary>
        /// Counts number of reads in each rndTag
        /// </summary>
        private ushort[] readCountsByRndTag;
        /// <summary>
        /// Counts number of reads irrespective of rndTag
        /// </summary>
        private int totalReadCount;
        /// <summary>
        /// Number of non-empty UMIs, after mutation filtering, or read count if no UMIs are used. -1 indicates not calculated yet.
        /// </summary>
        private int finalFilteredCount = -1;

        /// <summary>
        /// List of the genes that share this TagItem's counts. (If some reads are SNPed, the sharing genes may be not belong to all reads.)
        /// </summary>
        public Dictionary<IFeature, int> sharingGenes;

        /// <summary>
        /// At each offset relative to the 5' pos on chr of the reads' alignment where some SNPs appear,
        /// keep an array by rndTag of counts for each SNP nt. 
        /// </summary>
        public Dictionary<byte, SNPCountsByRndTag> SNPCountsByOffset { get; private set; }

        /// <summary>
        /// true when the read sequence at the (chr, pos, strand) of this TagItem is not unique in genome.
        /// </summary>
        public bool hasAltMappings;

        /// <summary>
        /// Special use for output of wiggle plots for only specific annotations
        /// </summary>
        public short typeOfAnnotation { get; set; }

        /// <summary>
        /// TagItem summarizes all reads that map to a specific (possibly redundant when hasAltMappings==true) (chr, pos, strand) combination[s].
        /// </summary>
        /// <param name="hasAltMappings">True indicates that the location is not unique in the genome</param>
        public TagItem(bool hasAltMappings)
        {
            this.hasAltMappings = hasAltMappings;
            this.typeOfAnnotation = (short)AnnotType.NOHIT;
        }
        public TagItem(bool hasAltMappings, bool isTranscript)
        {
            this.hasAltMappings = hasAltMappings;
            this.typeOfAnnotation = isTranscript? (short)AnnotType.EXON : (short)AnnotType.NOHIT;
        }

        /// <summary>
        /// Prepare for analyzing potential SNPs at specified offset within the reads
        /// </summary>
        /// <param name="snpOffset"></param>
        public void RegisterSNP(byte snpOffset)
        {
            if (SNPCountsByOffset == null)
                SNPCountsByOffset = new Dictionary<byte, SNPCountsByRndTag>();
            SNPCountsByOffset[snpOffset] = null;
        }

        /// <summary>
        /// Add the Nt at a SNP position from a read.
        /// If the position has not been defined as a SNP by a previous call to RegisterSNPAtOffset(), it will be skipped
        /// </summary>
        /// <param name="rndTagIdx">The rndTag of the read</param>
        /// <param name="snpOffset">Offset within the read of the SNP</param>
        /// <param name="snpNt">The reads' Nt at the SNP positions</param>
        public void AddSNP(int rndTagIdx, Mismatch mm)
        {
            SNPCountsByRndTag SNPCounts;
            if (!SNPCountsByOffset.TryGetValue(mm.relPosInChrDir, out SNPCounts))
                return;
            if (SNPCounts == null)
            {
                SNPCounts = new SNPCountsByRndTag(mm.refNtInChrDir);
                SNPCountsByOffset[mm.relPosInChrDir] = SNPCounts;
            }
            SNPCounts.Add(rndTagIdx, mm.ntInChrDir);
        }

        /// <summary>
        /// Clear data before handling the next barcode
        /// </summary>
        public void Clear()
        {
            typeOfAnnotation = (short)AnnotType.NOHIT;
            readCountsByRndTag = null;
            totalReadCount = 0;
            finalFilteredCount = -1;
            if (sharingGenes != null)
                sharingGenes.Clear();
            if (SNPCountsByOffset != null)
                foreach (SNPCountsByRndTag counts in SNPCountsByOffset.Values)
                    if (counts != null)
                        counts.Clear();
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
            if (readCountsByRndTag == null)
                readCountsByRndTag = new ushort[nRndTags];
            currentCount = readCountsByRndTag[rndTagIdx];
            readCountsByRndTag[rndTagIdx] = (ushort)Math.Min(ushort.MaxValue, currentCount + 1);
            return (currentCount == 0);
        }

        /// <summary>
        /// Record other transcripts that share the count from a multiread
        /// </summary>
        /// <param name="sharingRealFeatures"></param>
        public void AddSharedGenes(Dictionary<IFeature, object> sharingRealFeatures)
        {
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

        public bool HasReads { get { return totalReadCount > 0; } }
        public bool HasSNPs { get { return SNPCountsByOffset != null; } }

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
        /// <returns>SNPCounters that summarize the (winning, if some mutated read) Nts found at each offset in valid rndTags,
        /// or null if no SNPs are present.</returns>
        public List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
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
        /// Get indices of the rndTags that represent real molecules and not only mutations from other rndTags.
        /// If no random tags are used, get all indices that contain any reads.
        /// </summary>
        /// <returns>Indices of random tags containing real data (not stemming from mutations in other random tags)</returns>
        private List<int> GetIndicesOfUsedUMIsAfterFiltering()
        {
            if (nRndTags == 1)
                return woUMIsUMIIndices;
            List<int> filteredUsedUMIIndices = new List<int>();
            int threshold = mutationThresholder(this);
            for (int i = 0; i < readCountsByRndTag.Length; i++)
                if (readCountsByRndTag[i] > threshold) filteredUsedUMIIndices.Add(i);
            return filteredUsedUMIIndices;
        }
        /// <summary>
        /// Predefined for used by above method when UMI counting is not applicable
        /// </summary>
        private static List<int> woUMIsUMIIndices = new List<int> { 0 };

        /// <summary>
        /// Count number of molecules (reads if UMIs not used) at this position-strand. Filters away mutated UMIs according to thresholding filter.
        /// </summary>
        /// <returns>Number of molecules (mutated UMIs excluded), or number of reads if UMIs are not used.</returns>
        public int CalcCurrentNumMolecules()
        {
            if (nRndTags == 1) 
                return totalReadCount;
            if (readCountsByRndTag == null || totalReadCount == 0)
                return 0;
            int threshold = mutationThresholder(this);
            return readCountsByRndTag.Count(v => v > threshold);
        }
        /// <summary>
        /// Final number of molecules (or reads if UMIS are not used) after mutation filtering. Call ONLY after all reads in barcode have been added!
        /// Used for speed up of repeated calls.
        /// </summary>
        /// <returns>Number of molecules (mutated UMIs excluded), or number of reads if UMIs are not used.</returns>
        public int GetFinalNumMolecules()
        {
            if (finalFilteredCount >= 0)
                return finalFilteredCount;
            if (nRndTags == 1)
                finalFilteredCount = totalReadCount;
            else if (readCountsByRndTag == null || totalReadCount == 0)
                finalFilteredCount = 0;
            else
            {
                int threshold = mutationThresholder(this);
                finalFilteredCount = readCountsByRndTag.Count(v => v > threshold);
            }
            return finalFilteredCount;
        }

        /// <summary>
        /// Get number of reads in each rndTag. (Mutated molecule reads are not filtered away even when rndTags are used.)
        /// </summary>
        /// <returns>null if no reads have been found</returns>
        public ushort[] GetReadCountsByRndTag()
        {
            if (nRndTags == 1)
                return new ushort[1] { (ushort)totalReadCount };
            return readCountsByRndTag;
        }

        public int GetMutationThreshold()
        {
            return mutationThresholder(this);
        }

    }
}
