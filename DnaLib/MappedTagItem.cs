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
        private int observedMolCount;
        private List<SNPCounter> cachedSNPCounts;

        /// <summary>
        /// Number of molecules after filtering mutated UMIs but NOT collision compensation. Equals ReadCount if UMIs are not used.
        /// </summary>
        public int ObservedMolCount { get { return observedMolCount; } }
        /// <summary>
        /// Number of molecules after filtering mutated UMIs and collision compensation. Equals ReadCount if UMIs are not used.
        /// </summary>
        public int MolCount { get { return cachedMolCount; } }
        public int ReadCount { get { return cachedReadCount; } }
        public int EstTrueMolCount { get { return cachedEstTrueMolCount; } }
        public List<SNPCounter> SNPCounts { get { return cachedSNPCounts; } }

        private TagItem m_TagItem;
        public TagItem tagItem { get { return m_TagItem; } }

        /// <summary>
        /// MappedTagItem is used as a singleton during iterations in order to save HEAP, and this methods loads a new set of data
        /// </summary>
        /// <param name="hitStartPos"></param>
        /// <param name="strand"></param>
        /// <param name="tagItem"></param>
        public void Update(int hitStartPos, char strand, TagItem tagItem)
        {
            this.m_HitStartPos = hitStartPos;
            this.m_Strand = strand;
            this.m_TagItem = tagItem;
            cachedSNPCounts = m_TagItem.GetTotalSNPCounts(m_HitStartPos);
            cachedReadCount = m_TagItem.GetBcNumReads();

            observedMolCount = m_TagItem.GetFinalBcNumMols(); // Apply filter for mutated UMIs
            if (TagItem.nUMIs == 1)
            {
                cachedEstTrueMolCount = cachedMolCount = observedMolCount;
            }
            else if (observedMolCount < TagItem.nUMIs)
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

        /// <summary>
        /// Needed when calculating per-barcode statistics of reads/mol and filtered reads
        /// </summary>
        /// <param name="annotType"></param>
        public void SetTypeOfAnnotation(int annotType)
        {
            this.m_TagItem.typeOfAnnotation = (short)annotType;
        }

        public int bcIdx;
        public string chr;
        private int m_HitStartPos;
        public int hitStartPos { get { return m_HitStartPos; } }
        private char m_Strand;
        /// <summary>
        /// The strand the read sequence equals
        /// </summary>
        public char SequencedStrand { get { return m_Strand; } }
        /// <summary>
        /// The strand that is actually detected
        /// </summary>
        public char DetectedStrand { get { return Props.props.SenseStrandIsSequenced ? m_Strand : (m_Strand == '+') ? '-' : '+'; } }

        public int splcToRealChrOffset = 0;
        public bool hasAltMappings { get { return m_TagItem.hasAltMappings; } }
        public int HitLen { get { return AverageReadLen; } }
        public int HitMidPos { get { return hitStartPos + HitLen / 2 + splcToRealChrOffset; } }

        public override string ToString()
        {
            return string.Format("MappedTagItem(chr={0} strand={1} hitStartPos={2} [Average]HitLen={3}" +
                                 " bcIdx={4} HitMidPos={5} MolCount={6} ReadCount={7} HasAltMappings={8} DetectedStrand={9})",
                                 chr, SequencedStrand, hitStartPos, HitLen, bcIdx, HitMidPos, MolCount, ReadCount, m_TagItem.hasAltMappings,
                                 DetectedStrand);
        }

    }
}
