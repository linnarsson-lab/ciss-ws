using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using System.Security.AccessControl;

namespace Linnarsson.Strt
{
    public class GenomeAnnotationsOnFile : GenomeAnnotations
    {

        public GenomeAnnotationsOnFile(Props props, StrtGenome genome) : base(props, genome)
        { }

        protected override void RegisterGenesAndIntervals()
        {
            string annotationsPath = genome.VerifyAnAnnotationPath();
            LoadAnnotationsFile(annotationsPath);
            AdjustEndsAndMarkUpOverlaps();
            foreach (GeneFeature gf in geneFeatures.Values)
                AddGeneIntervals((GeneFeature)gf);
        }

        private void LoadAnnotationsFile(string annotationsPath)
        {
            int nLines = 0;
            int nGeneFeatures = 0;
            int nTooLongFeatures = 0;
            foreach (LocusFeature gf in AnnotationReader.IterAnnotationFile(annotationsPath))
            {
                nLines++;
                if (noGeneVariants && gf.IsVariant())
                    continue;
                if (gf.Length > props.MaxFeatureLength)
                    nTooLongFeatures++;
                else if (RegisterGeneFeature(gf))
                {
                    nGeneFeatures++;
                }
            }
            string exlTxt = (nTooLongFeatures == 0) ? "" : string.Format(" (Excluding {0} spanning > {1} bp.)",
                                                                         nTooLongFeatures, props.MaxFeatureLength);
            string exclV = noGeneVariants ? "main" : "complete";
            Console.WriteLine("{0} {1} gene variants will be mapped.{2}", nGeneFeatures, exclV, exlTxt);
        }

        private void AdjustEndsAndMarkUpOverlaps()
        {
            GeneFeature5PrimeAndOverlapMarkUpModifier m = new GeneFeature5PrimeAndOverlapMarkUpModifier();
            foreach (string chrId in GetChromosomeIds())
            {
                if (!StrtGenome.IsSyntheticChr(chrId))
                    m.Process(geneFeatures.Values.Where(gf => gf.Chr == chrId));
            }
            Console.WriteLine("{0} genes had their 5' end extended, {1} with the maximal {2} bps.",
                               m.nExtended, m.nFullyExtended5Primes, Props.props.GeneFeature5PrimeExtension);
            if (m.nMarkedExons > 0)
                Console.WriteLine("{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.",
                              m.nMarkedExons, m.nMarkedGenes, m.totalMarkedLen);
            Console.WriteLine("{0} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.", m.nMaskedIntronicFeatures);
        }
    }

}
