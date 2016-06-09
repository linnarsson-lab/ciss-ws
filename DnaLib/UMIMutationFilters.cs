﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Not used by ZeroOneMoreTagItem, which has its' own simplified singleton filter
    /// </summary>
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
            if (Props.props.RndTagMutationFilter == UMIMutationFilter.LowPassFilter && Props.props.RndTagMutationFilterParam == 1)
            {
                Props.props.RndTagMutationFilter = UMIMutationFilter.Singleton;
                Props.props.RndTagMutationFilterParam = 0;
            }
            if (Props.props.RndTagMutationFilter == UMIMutationFilter.None)
                Props.props.RndTagMutationFilterParam = 0;
            UMIMutationFilterParameter = Props.props.RndTagMutationFilterParam;
            switch (Props.props.RndTagMutationFilter)
            {
                case UMIMutationFilter.FractionOfMax:
                    filter = FractionOfMaxThresholder;
                    break;
                case UMIMutationFilter.FractionOfMean:
                    filter = FractionOfMeanThresholder;
                    break;
                case UMIMutationFilter.Singleton:
                    filter = SingletonThresholder;
                    if (UMIMutationFilterParameter == 0)
                        Console.WriteLine("Singletons are filtered away.");
                    else
                        Console.WriteLine("Singletons at each genomic position are filtered away if some UMI at that position has > " + UMIMutationFilterParameter + " reads.");
                    break;
                case UMIMutationFilter.Hamming1Singleton:
                    throw new Exception("You can only use RndTagMutationFilter=" + Props.props.RndTagMutationFilter.ToString()
                                    + " with DenseUMICounter!");
                default: // LowPassFilter or None
                    filter = LowPassThresholder;
                    if (UMIMutationFilterParameter <= 0)
                        Console.WriteLine("No UMI mutation filter is applied.");
                    else if (UMIMutationFilterParameter == 1)
                        Console.WriteLine("Singletons are filtered away.");
                    else
                        Console.WriteLine("Molecules are only counted when detected by > " + UMIMutationFilterParameter + " reads.");
                    break;
            }
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
