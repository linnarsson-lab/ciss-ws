using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public delegate void ProcessStep();

    /// <summary>
    /// Collects various mwthods that modify the transcript models, by extending 5' ends, or marking overlap
    /// with other transcripts on the same strand. These methods are called either just before expression analysis,
    /// or when inserting gene models into the cells10k database.
    /// </summary>
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
        public int nShrunkFlanks { get; private set; }
        public Dictionary<string, int> antisensePairExons = new Dictionary<string, int>();

        private List<GeneFeature> chrGenes;

        private int[] sortedExonStarts;
        private int[] startSortedExonEnds;
        private int[] endSortedExonEnds;
        private bool[] startSortedExonStrands;
        private bool[] endSortedExonStrands;
        private GeneFeature[] geneFeatureByExon;
        protected ProcessStep processSteps;

        public void Process(List<GeneFeature> chrGenes)
        {
            this.chrGenes = chrGenes;
            processSteps();
        }

        public void MarkUpOverlapsOnChr()
        {
            CollectExonsFromChrGenes();
            foreach (GeneFeature gf in chrGenes)
            {
                nShrunkFlanks += (gf.AdjustFlanks(sortedExonStarts, startSortedExonStrands, endSortedExonEnds, endSortedExonStrands) > 0)? 1 : 0;
                nMaskedIntronicFeatures += gf.MaskOverlappingUSTRDSTRINTR(sortedExonStarts, startSortedExonEnds);
                List<int> indicesOfMasked = gf.MaskOverlappingAntisenseExons(sortedExonStarts, startSortedExonEnds, startSortedExonStrands);
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
        public string MarkUpOverlapsResult()
        {
            return string.Format("{0} genes had the USTR or DSTR part shrunk due to overlaps.\n", nShrunkFlanks) +
              (!Props.props.DirectionalReads ? "" : string.Format("{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.\n", nMarkedExons, nMarkedGenes, totalMarkedLen)) +
              string.Format("{0} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.", nMaskedIntronicFeatures);
        }

        public void Extend5PrimeEnds()
        {
            CollectExonsFromChrGenes();
            //Console.WriteLine("Process {0} chrGenes {1} exonStarts.", chrGenes.Count, sortedExonStarts.Length);
            foreach (GeneFeature gf in chrGenes)
            {
                int extension = gf.Extend5Prime(sortedExonStarts, startSortedExonStrands, endSortedExonEnds, endSortedExonStrands);
                if (extension == Props.props.GeneFeature5PrimeExtension) nFullyExtended5Primes++;
                if (extension > 0) nExtended++;
            }
        }
        public string Extend5PrimeEndsResult()
        {
            return string.Format("{0} genes had their 5' end extended, {1} with the maximal {2} bps.",
                                 nExtended, nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
        }

        private void CollectExonsFromChrGenes()
        {
            int nExons = 0;
            foreach (GeneFeature gf in chrGenes)
                nExons += gf.ExonCount;
            sortedExonStarts = new int[nExons];
            startSortedExonEnds = new int[nExons];
            startSortedExonStrands = new bool[nExons];
            geneFeatureByExon = new GeneFeature[nExons];
            int exonIdx = 0;
            foreach (GeneFeature gf in chrGenes)
            {
                for (int i = 0; i < gf.ExonCount; i++)
                {
                    sortedExonStarts[exonIdx] = gf.ExonStarts[i];
                    startSortedExonEnds[exonIdx] = gf.ExonEnds[i];
                    startSortedExonStrands[exonIdx] = (gf.Strand == '+') ? true : false;
                    geneFeatureByExon[exonIdx] = gf;
                    exonIdx++;
                }
            }
            Sort.QuickSort(sortedExonStarts, startSortedExonEnds, startSortedExonStrands, geneFeatureByExon);
            endSortedExonEnds = (int[])startSortedExonEnds.Clone();
            endSortedExonStrands = (bool[])startSortedExonStrands.Clone();
            Sort.QuickSort(endSortedExonEnds, endSortedExonStrands);
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
            return Extend5PrimeEndsResult();
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
            return MarkUpOverlapsResult();
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
            return Extend5PrimeEndsResult() + "\n" + MarkUpOverlapsResult();
        }
    }
}
