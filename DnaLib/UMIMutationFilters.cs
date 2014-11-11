using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class UMIMutationFilters
    {
        public delegate int MutationThresholder(TagItem tagItem);

        /// <summary>
        /// Returns a hit count threshold for filtering away UMIs that likely stem from mutations in other UMIs
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        public static MutationThresholder filter;

        /// <summary>
        /// Threshold parameter for removing molecules that are a result of mutated UMIs.
        /// Actual meaning depends on the MutationThresholder selected.
        /// </summary>
        private static int UMIMutationFilterParameter;

        public static void SetUMIMutationFilter()
        {
            if (Props.props.RndTagMutationFilter == RndTagMutationFilterMethod.FractionOfMax)
                filter = FractionOfMaxThresholder;
            else if (Props.props.RndTagMutationFilter == RndTagMutationFilterMethod.FractionOfMean)
                filter = FractionOfMeanThresholder;
            else if (Props.props.RndTagMutationFilter == RndTagMutationFilterMethod.Singleton)
                filter = SingletonThresholder;
            else
                filter = LowPassThresholder;
            UMIMutationFilterParameter = Props.props.RndTagMutationFilterParam;
        }

        /// <summary>
        /// If UMIMutationFilterParameter==0, all singletons will be removed.
        /// Otherwise, singletons will only be removed when some UMI has #reads > UMIMutationFilterParameter.
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        private static int SingletonThresholder(TagItem tagItem)
        {
            foreach (int c in tagItem.GetReadCountsByUMI())
                if (c > UMIMutationFilterParameter)
                    return 1;
            return 0;
        }

        /// <summary>
        /// Will only count UMIs with > UMIMutationFilterParameter reads.
        /// E.g., with UMIMutationFilterParameter==1, all singletons will be removed.
        /// </summary>
        /// <param name="tagItem"></param>
        /// <returns></returns>
        private static int LowPassThresholder(TagItem tagItem)
        {
            return UMIMutationFilterParameter;
        }

        private static int FractionOfMaxThresholder(TagItem tagItem)
        {
            int maxNumReads = tagItem.GetReadCountsByUMI().Max();
            return maxNumReads / UMIMutationFilterParameter;
        }

        private static int FractionOfMeanThresholder(TagItem tagItem)
        {
            double sum = 0.0;
            int n = 0;
            foreach (int i in tagItem.GetReadCountsByUMI())
            {
                if (i > 0) 
                {
                    sum += i;
                    n++;
                }
                       
            }
            return (int)Math.Round(sum / n / UMIMutationFilterParameter);
        }
    }
}
