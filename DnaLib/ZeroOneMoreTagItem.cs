using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    class ZeroOneMoreTagItem : TagItem
    {
        /// <summary>
        /// Set to true the first time the UMI is detected
        /// </summary>
        private BitArray detectedUMIs;
        /// <summary>
        /// Set to true the second time the UMI is detected, i.e. the molecule is represented by > 1 read
        /// </summary>
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

        public static void Init()
        {
            TagItem.CountsReadsPerUMI = false;
            int param = Props.props.RndTagMutationFilterParam;
            if (Props.props.RndTagMutationFilter == UMIMutationFilter.LowPassFilter && param == 1)                 
                Props.props.RndTagMutationFilter = UMIMutationFilter.Singleton;
            else if (Props.props.RndTagMutationFilter != UMIMutationFilter.Hamming1Singleton
                     && !(Props.props.RndTagMutationFilter == UMIMutationFilter.LowPassFilter && param == 0)
                     && !(Props.props.RndTagMutationFilter == UMIMutationFilter.Singleton && param == 0))
                throw new Exception("You can not use RndTagMutationFilter=" + Props.props.RndTagMutationFilter.ToString()
                                    + " and Param=" + param + " with DenseUMICounter!");
            Console.WriteLine("Using compact TagItems with UMIMutationFilter=" + Props.props.RndTagMutationFilter.ToString());
        }

        private static List<SNPCounter> noSNPCounts = new List<SNPCounter>(0);
        public override List<SNPCounter> GetTotalSNPCounts(int readPosOnChr)
        {
            return noSNPCounts;
        }

        public override void RegisterSNP(byte snpOffset)
        { }
        public override void AddSNP(int UMIIdx, Mismatch mm)
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
            if (Props.props.RndTagMutationFilter == UMIMutationFilter.Singleton)
            { // Singleton filter
                for (int UMIIdx = 0; UMIIdx < multitonUMIs.Length; UMIIdx++)
                    if (multitonUMIs[UMIIdx]) c++;
            }
            else if (Props.props.RndTagMutationFilter == UMIMutationFilter.LowPassFilter)
            { // No filter at all
                for (int UMIIdx = 0; UMIIdx < multitonUMIs.Length; UMIIdx++)
                    if (detectedUMIs[UMIIdx]) c++;
            }
            else if (Props.props.RndTagMutationFilter == UMIMutationFilter.Hamming1Singleton)
            { // Singleton with Hamming distance == 1 filter
                c = CalcHamming1NumMols();
            }
            return c;
        }

        private int CalcHamming1NumMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
            {
                if (multitonUMIs[i]) n++;
                else if (detectedUMIs[i])
                {
                    bool tooClose = false;
                    for (int j = 0; j < detectedUMIs.Length; j++)
                    {
                        if (j == i) continue;
                        bool close = (hDistCalc.Dist(i, j) < 2);
                        if (close && (multitonUMIs[j] || (detectedUMIs[j] && i > j)))
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) n++;
                }
            }
            return n;
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

        public override IEnumerable<ushort> IterFilteredReadCounts()
        {
            yield break;
        }
    }
}
