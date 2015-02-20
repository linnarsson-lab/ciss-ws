using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    class ZeroOneMoreTagItem : TagItem
    {
        private BitArray detectedUMIs;
        private BitArray multitonUMIs;

        /// <summary>
        /// Create a dense TagItem for singleton (or no) filtering, which only counts the zero, one, or "many" reads.
        /// </summary>
        /// <param name="hasAltMappings">True indicates that the location is not unique in the genome</param>
        /// <param name="isTranscript">true to indicate that this TagItem represents exonic reads</param>
        public ZeroOneMoreTagItem(bool hasAltMappings, bool isTranscript)
        {
            this.hasAltMappings = hasAltMappings;
            this.typeOfAnnotation = isTranscript ? (short)AnnotType.EXON : (short)AnnotType.NOHIT;
        }

        private static bool filterSingletons = true;

        public static void Init()
        {
            TagItem.CountsReadsPerUMI = false;
            RndTagMutationFilterMethod method = Props.props.RndTagMutationFilter;
            int param = Props.props.RndTagMutationFilterParam;
            if (method == RndTagMutationFilterMethod.LowPassFilter && param == 0)
                filterSingletons = false;
            else if (method == RndTagMutationFilterMethod.LowPassFilter && param == 1 ||
                     method == RndTagMutationFilterMethod.Singleton && param == 0)
                filterSingletons = true;
            else
                throw new Exception("You can not use the specified RndTagMutationFilter with DenseUMICounter!");
        }

        private static List<SNPCounter> noSNPCounts = new List<SNPCounter>(0);
        public override List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
        {
            return noSNPCounts;
        }

        public override Dictionary<IFeature, int> SharingGenes { get { return null; } }

        public override void RegisterSNP(byte snpOffset)
        { }
        public override void AddSNP(int UMIIdx, Mismatch mm)
        { }
        public override void AddSharedGenes(Dictionary<IFeature, object> sharingRealFeatures)
        { }

        public override void Clear()
        {
            ClearBase();
            if (detectedUMIs != null)
            {
                detectedUMIs = null;
                multitonUMIs = null;
            }
        }

        public override bool Add(int UMIIdx)
        {
            bcNumReads++;
            totNumReads++;
            if (detectedUMIs == null)
            {
                detectedUMIs = new BitArray(nUMIs);
                multitonUMIs = new BitArray(nUMIs);
                detectedUMIs[UMIIdx] = true;
                return true;
            }
            if (detectedUMIs[UMIIdx])
            {
                multitonUMIs[UMIIdx] = true;
                return false;
            }
            detectedUMIs[UMIIdx] = true;
            return true;
        }

        public override bool HasSNPs { get { return false; } }

        public override int CalcCurrentBcNumMols()
        {
            if (nUMIs == 1)
                return bcNumReads;
            if (multitonUMIs == null || bcNumReads == 0)
                return 0;
            int c = 0;
            for (int UMIIdx = 0; UMIIdx < multitonUMIs.Length; UMIIdx++)
                if (multitonUMIs[UMIIdx] ||
                    (!filterSingletons && detectedUMIs[UMIIdx])) c++;
            return c;
        }

        protected override void CalcFinalBcNumMols()
        {
            if (filteredBcNumMols >= 0)
                return;
            if (nUMIs == 1)
                filteredBcNumMols = bcNumReads;
            else if (multitonUMIs == null || bcNumReads == 0)
                filteredBcNumMols = 0;
            else
            {
                filteredBcNumMols = 0;
                for (int UMIIdx = 0; UMIIdx < multitonUMIs.Length; UMIIdx++)
                    if (multitonUMIs[UMIIdx] ||
                        (!filterSingletons && detectedUMIs[UMIIdx])) filteredBcNumMols++;
            }
            filteredTotNumMols += filteredBcNumMols;
        }

        public override int GetNumUsedUMIs()
        {
            int c = 0;
            for (int UMIIdx = 0; UMIIdx < multitonUMIs.Length; UMIIdx++)
                if (multitonUMIs[UMIIdx] || detectedUMIs[UMIIdx]) c++;
            return c;
        }

        public override ushort[] GetReadCountsByUMI()
        {
            return null;
        }

        public override int GetMutationThreshold()
        {
            return filterSingletons? 1 : 0;
        }
    }
}
