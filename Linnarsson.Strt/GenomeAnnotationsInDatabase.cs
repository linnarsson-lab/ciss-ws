using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class GenomeAnnotationsInDatabase : AbstractGenomeAnnotations
    {
        public GenomeAnnotationsInDatabase(Props props, StrtGenome genome)
            : base(props, genome)
        { }

        protected override void RegisterGenesAndIntervals()
        {
            MarkUpOverlaps();
            foreach (GeneFeature gf in geneFeatures.Values)
                AddGeneIntervals((GeneFeature)gf);

            
            throw new NotImplementedException();
        }

        private void MarkUpOverlaps()
        {
            GeneFeatureOverlapMarkUpModifier m = new GeneFeatureOverlapMarkUpModifier();
            foreach (string chrId in GetChromosomeIds())
            {
                if (!StrtGenome.IsSyntheticChr(chrId))
                    m.Process(geneFeatures.Values.Where(gf => gf.Chr == chrId));
            }
            Console.WriteLine("{0} genes had the 5' exon extended {1} bp as specified by GeneFeature5PrimeExtension property.",
                              m.nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
            if (m.nMarkedExons > 0)
                Console.WriteLine("{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.",
                              m.nMarkedExons, m.nMarkedGenes, m.totalMarkedLen);
            Console.WriteLine("{0} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.", m.nMaskedIntronicFeatures);
        }

    }
}
