using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Counts reads/molecules that hit features, sorted by chr, barcode, and annotatation type
    /// </summary>
    public class TotalHitCounter
    {
        private int nBcs;

        /// <summary>
        /// Per chromosome hits of each type and barcode. Separates sense and antisense
        /// </summary>
        Dictionary<string, int[,]> TotalHitsByChrTypeBc;

        public int TotalHits { get; private set; }

        public TotalHitCounter(GenomeAnnotations Annotations)
        {
            nBcs = Props.props.Barcodes.Count;
            TotalHitsByChrTypeBc = new Dictionary<string, int[,]>(Annotations.GetChromosomeIds().Length);
            foreach (string chr in Annotations.GetChromosomeIds())
                TotalHitsByChrTypeBc[chr] = new int[AnnotType.Count, nBcs];
        }

        public void Add(int annotType, MappedTagItem item)
        {
            TotalHitsByChrTypeBc[item.chr][annotType, item.bcIdx] += item.MolCount;
            TotalHits += item.MolCount;
        }

        /// <summary>
        /// Includes antisense if analysis is non-directional
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <returns></returns>
        public int GetEXONHits(int bcIdx)
        {
            int c = TotalHitsByChrTypeBc.Values.Sum(v => v[AnnotType.EXON, bcIdx]);
            if (!Props.props.DirectionalReads)
                c += TotalHitsByChrTypeBc.Values.Sum(v => v[AnnotType.AEXON, bcIdx]);
            return c;
        }

        /// <summary>
        /// Includes antisense if analysis is non-directional and annotType is a sense type
        /// </summary>
        /// <param name="annotType"></param>
        /// <returns></returns>
        public int GetTotalAnnotHits(int annotType)
        {
            int c = GetAnnotHits(annotType);
            if (!Props.props.DirectionalReads && annotType != AnnotType.REPT)
                c += GetAnnotHits(AnnotType.MakeAntisense(annotType));
            return c;
        }

        public int GetAnnotHits(int annotType)
        {
            int c = 0;
            for (int bcIdx = 0; bcIdx < nBcs; bcIdx++)
                c += TotalHitsByChrTypeBc.Values.Sum(v => v[annotType, bcIdx]);
            return c;
        }
        
        /// <summary>
        /// Includes antisense if analysis is non-directional and annotType is a sense type
        /// </summary>
        /// <param name="selectedBcIndexes"></param>
        /// <param name="annotType"></param>
        /// <returns></returns>
        public int GetTotalBcsAnnotHits(int[] selectedBcIndexes, int annotType)
        {
            int ASannotType = (!Props.props.DirectionalReads && annotType != AnnotType.REPT) ? AnnotType.MakeAntisense(annotType) : -1;
            int c = 0;
            foreach (int bcIdx in selectedBcIndexes)
            {
                c += TotalHitsByChrTypeBc.Values.Sum(v => v[annotType, bcIdx]);
                if (ASannotType >= 0)
                    c += TotalHitsByChrTypeBc.Values.Sum(v => v[ASannotType, bcIdx]);
            }
            return c;
        }

        /// <summary>
        /// Includes antisense if analysis is non-directional and annotType is a sense type
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <param name="annotType"></param>
        /// <returns></returns>
        public int GetTotalBcAnnotHits(int bcIdx, int annotType)
        {
            int c = TotalHitsByChrTypeBc.Values.Sum(v => v[annotType, bcIdx]);
            if (!Props.props.DirectionalReads && annotType != AnnotType.REPT)
                c += GetAnnotHits(AnnotType.MakeAntisense(annotType));
            return c;
        }

        public int GetBcAnnotHits(int bcIdx, int annotType)
        {
            return TotalHitsByChrTypeBc.Values.Sum(v => v[annotType, bcIdx]);
        }

        public IEnumerable<string> ChrIds()
        {
            return TotalHitsByChrTypeBc.Keys;
        }

        public int GetChrAnnotHits(string chr, int annotType)
        {
            int c = 0;
            for (int bcIdx = 0; bcIdx < nBcs; bcIdx++)
                c += TotalHitsByChrTypeBc[chr][annotType, bcIdx];
            return c;
        }

        public int GetBcHits(int bcIdx)
        {
            int c = 0;
            for (int annotType = 0; annotType < AnnotType.Count; annotType++)
                c += TotalHitsByChrTypeBc.Values.Sum(v => v[annotType, bcIdx]);
            return c;
        }

        public int[] GetTotalHitsByBarcode()
        {
            int[] cs = new int[nBcs];
            for (int bcIdx = 0; bcIdx < nBcs; bcIdx++)
                cs[bcIdx] = GetBcHits(bcIdx);
            return cs;
        }
    }
}
