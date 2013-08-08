using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using C1;

namespace Linnarsson.Strt
{
    public class GenomeAnnotationsInDatabase : GenomeAnnotations
    {
        public GenomeAnnotationsInDatabase(Props props, StrtGenome genome)
            : base(props, genome)
        { }

        protected void RegisterGenesAndIntervals()
        {
            C1DB db = new C1DB();
            Transcriptome tm = db.GetTranscriptome(genome.BuildVarAnnot);
            string annotationsPath = genome.VerifyAnAnnotationPath();
            bool transcriptsFromDb = (tm != null);
            if (transcriptsFromDb)
            {
                foreach (Transcript tt in db.IterTranscripts(tm.TranscriptomeID.Value))
                {
                    LocusFeature feature = GeneFeatureFromTranscript(tt);
                    RegisterGeneFeature(feature);
                }
                ModifyGeneFeatures(new GeneFeatureOverlapMarkUpModifier());
            }
            string onlySplcChrFromFile = (transcriptsFromDb) ? genome.Annotation : "";
            foreach (LocusFeature gf in AnnotationReader.IterAnnotationFile(annotationsPath))
            {
                if (gf.Chr != onlySplcChrFromFile)
                {
                    RegisterGeneFeature(gf);
                }
            }
            if (!transcriptsFromDb)
                ModifyGeneFeatures(new GeneFeature5PrimeAndOverlapMarkUpModifier());
            foreach (GeneFeature gf in geneFeatures.Values)
                AddGeneIntervals((GeneFeature)gf);
        }

        private static GeneFeature GeneFeatureFromTranscript(Transcript tt)
        {
            int[] exonStarts = AnnotationReader.SplitField(tt.ExonStarts, 0);
            int[] exonEnds = AnnotationReader.SplitField(tt.ExonEnds, -1); // Convert to inclusive ends
            return new GeneFeature(tt.Name, tt.Chromosome, tt.Strand, null, null, tt.TranscriptID.Value);
        }

        private void ModifyGeneFeatures(GeneFeatureModifiers m)
        {
            foreach (string chrId in GetChromosomeIds())
            {
                if (!StrtGenome.IsSyntheticChr(chrId))
                    m.Process(geneFeatures.Values.Where(gf => gf.Chr == chrId));
            }
            Console.WriteLine(m.GetStatsOutput());
        }
    }
}
