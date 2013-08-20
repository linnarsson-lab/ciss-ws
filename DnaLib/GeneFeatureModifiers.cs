using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public delegate void ProcessStep();

    public abstract class GeneFeatureModifiers
    {
        public virtual string GetStatsOutput()
        {
            return "";
        }

        public int nMarkedExons { get; private set; }
        public int nMarkedGenes { get; private set; }
        public int nFullyExtended5Primes { get; private set; }
        public int nMaskedIntronicFeatures { get; private set; }
        public int totalMarkedLen { get; private set; }
        public int nExtended { get; private set; }
        public Dictionary<string, int> antisensePairExons = new Dictionary<string, int>();

        private List<GeneFeature> chrGenes;

        private int[] sortedExonStarts;
        private int[] exonEnds;
        private bool[] startSortedExonStrands;
        private bool[] endSortedExonStrands;
        private GeneFeature[] geneFeatureByExon;
        protected ProcessStep processSteps;

        public void Process(List<GeneFeature> chrGenes)
        {
            this.chrGenes = chrGenes;
            CollectExonsFromChrGenes();
            /*Console.WriteLine("GeneFeatureModifers.Process({0} genes)", chrGenes.Count);
            Console.WriteLine("{0} geneFeatureByExon: {1},{2},{3},{4}...",
                     geneFeatureByExon.Length, geneFeatureByExon[0].Name, geneFeatureByExon[1].Name, geneFeatureByExon[2].Name, geneFeatureByExon[3].Name);
            Console.WriteLine("{0} sortedExonStarts: {1},{2},{3},{4}...",
                     sortedExonStarts.Length, sortedExonStarts[0], sortedExonStarts[1], sortedExonStarts[2], sortedExonStarts[3]);
            Console.WriteLine("{0} exonEnds: {1},{2},{3},{4}...",
                     exonEnds.Length, exonEnds[0], exonEnds[1], exonEnds[2], exonEnds[3]);
            Console.WriteLine("{0} startSortedExonStrands: {1},{2},{3},{4}...",
                     startSortedExonStrands.Length, startSortedExonStrands[0], startSortedExonStrands[1], startSortedExonStrands[2], startSortedExonStrands[3]);
            Console.WriteLine("{0} endSortedExonStrands: {1},{2},{3},{4}...",
                     endSortedExonStrands.Length, endSortedExonStrands[0], endSortedExonStrands[1], endSortedExonStrands[2], endSortedExonStrands[3]);*/
            processSteps();
        }

        public void MarkUpOverlapsOnChr()
        {
            foreach (GeneFeature gf in chrGenes)
            {
                nMaskedIntronicFeatures += gf.MaskOverlappingUSTRDSTRINTR(sortedExonStarts, exonEnds);
                List<int> indicesOfMasked = gf.MaskOverlappingAntisenseExons(sortedExonStarts, exonEnds, startSortedExonStrands);
                if (indicesOfMasked.Count > 0)
                {
                    nMarkedExons += indicesOfMasked.Count;
                    nMarkedGenes++;
                    totalMarkedLen += gf.GetTranscriptLength() - gf.GetNonMaskedTranscriptLength();
                    foreach (int idx in indicesOfMasked)
                    {
                        string[] names = new string[] { gf.Name, geneFeatureByExon[idx].Name };
                        Array.Sort(names);
                        string gfPair = string.Join("#", names);
                        if (!antisensePairExons.ContainsKey(gfPair))
                            antisensePairExons[gfPair] = 1;
                        else
                            antisensePairExons[gfPair]++;
                    }
                }
            }
        }

        public void Extend5PrimeEnds()
        {
            foreach (GeneFeature gf in chrGenes)
            {
                int extension = gf.AdjustFlanksAnd5PrimeExtend(sortedExonStarts, startSortedExonStrands, exonEnds, endSortedExonStrands);
                if (extension == Props.props.GeneFeature5PrimeExtension) nFullyExtended5Primes++;
                if (extension > 0) nExtended++;
            }
        }

        private void CollectExonsFromChrGenes()
        {
            int nExons = 0;
            foreach (GeneFeature gf in chrGenes)
                nExons += gf.ExonCount;
            sortedExonStarts = new int[nExons];
            exonEnds = new int[nExons];
            startSortedExonStrands = new bool[nExons];
            geneFeatureByExon = new GeneFeature[nExons];
            int exonIdx = 0;
            foreach (GeneFeature gf in chrGenes)
            {
                for (int i = 0; i < gf.ExonCount; i++)
                {
                    sortedExonStarts[exonIdx] = gf.ExonStarts[i];
                    exonEnds[exonIdx] = gf.ExonEnds[i];
                    startSortedExonStrands[exonIdx] = (gf.Strand == '+') ? true : false;
                    geneFeatureByExon[exonIdx] = gf;
                    exonIdx++;
                }
            }
            Sort.QuickSort(sortedExonStarts, exonEnds, startSortedExonStrands, geneFeatureByExon);
            int[] sortHelperByExonEnds = (int[])exonEnds.Clone();
            endSortedExonStrands = (bool[])startSortedExonStrands.Clone();
            Sort.QuickSort(sortHelperByExonEnds, endSortedExonStrands);
        }
    }

    public class GeneFeature5PrimeModifier : GeneFeatureModifiers
    {
        public GeneFeature5PrimeModifier()
        {
            processSteps = Extend5PrimeEnds;
        }

        public override string GetStatsOutput()
        {
            return string.Format("{0} genes had their 5' end extended, {1} with the maximal {2} bps.",
                                 nExtended, nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
        }
    }

    public class GeneFeatureOverlapMarkUpModifier : GeneFeatureModifiers
    {
        public GeneFeatureOverlapMarkUpModifier()
        {
            processSteps = MarkUpOverlapsOnChr;
        }
        public override string GetStatsOutput()
        {
            string asPart = Props.props.DirectionalReads? "{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.\n" : "";
            return string.Format(asPart + "{3} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.",
                                 nMarkedExons, nMarkedGenes, totalMarkedLen, nMaskedIntronicFeatures);
        }
    }
    public class GeneFeature5PrimeAndOverlapMarkUpModifier : GeneFeatureModifiers
    {
        public GeneFeature5PrimeAndOverlapMarkUpModifier()
        {
            processSteps = Extend5PrimeEnds;
            processSteps += MarkUpOverlapsOnChr;
        }

        public override string GetStatsOutput()
        {
            string asPart = Props.props.DirectionalReads ? "{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.\n" : "";
            return string.Format("{0} genes had their 5' end extended, {1} with the maximal {2} bps.\n",
                                 nExtended, nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension) +
                   string.Format(asPart + "{3} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.",
                                 nMarkedExons, nMarkedGenes, totalMarkedLen, nMaskedIntronicFeatures);
        }
    }
}
