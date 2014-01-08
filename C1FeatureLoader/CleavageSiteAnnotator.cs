using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace C1
{
    public class CleavageSiteAnnotator
    {
        private static int CAPCloseSiteSearchStart = -100;
        private static int CAPCloseSiteSearchEnd = 200;

        private Dictionary<string, DnaSequence> ChrSeqs = new Dictionary<string, DnaSequence>();

        public CleavageSiteAnnotator(StrtGenome genome)
        {
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            chrIdToFileMap[StrtGenome.chrCTRLId] = PathHandler.GetChrCTRLPath();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chrId)) continue;
                DnaSequence chrSeq = DnaSequence.FromFile(chrIdToFileMap[chrId]);
                ChrSeqs.Add(chrId, chrSeq);
            }
        }

        public void Annotate(ref Transcript t)
        {
            DnaSequence trSeq;
            int requestedExtension = (t.Chromosome != StrtGenome.chrCTRLId) ? -CAPCloseSiteSearchStart : 0;
            int actualExtension = GetTrSeq(t, requestedExtension, out trSeq);
            List<int> sitePositions = new List<int>();
            foreach (string enzymeName in C1Props.props.CAPCloseSiteSearchCutters)
            {
                RestrictionEnzyme enzyme = RestrictionEnzymes.Get(enzymeName);
                DnaSequence[] fragments = enzyme.Cut(trSeq);
                int cutPos = -actualExtension;
                foreach (DnaSequence fragment in fragments)
                {
                    cutPos += (int)fragment.Count;
                    if (cutPos < CAPCloseSiteSearchEnd)
                        sitePositions.Add(cutPos);
                }
            }
            sitePositions.Sort();
            string siteList = string.Join(",", sitePositions.ConvertAll(v => v.ToString()).ToArray());
            t.StartToCloseCutSite = siteList;
        }

        private int GetTrSeq(Transcript t, int upstreamExtension, out DnaSequence trSeq)
        {
            int[] starts = Array.ConvertAll(t.ExonStarts.Remove(t.ExonStarts.Length - 1).Split(','), v => int.Parse(v));
            int[] ends = Array.ConvertAll(t.ExonEnds.Remove(t.ExonEnds.Length - 1).Split(','), v => int.Parse(v));
            string chr = t.Chromosome;
            int actualExtension = 0;
            if (t.Strand == '+')
            {
                actualExtension = Math.Min(upstreamExtension, starts[0] - upstreamExtension);
                starts[0] -= actualExtension;
            }
            else
            {
                actualExtension = (int)Math.Min(upstreamExtension, ChrSeqs[chr].Count - ends[ends.Length - 1]);
                ends[ends.Length - 1] += actualExtension;
            }
            trSeq = new ShortDnaSequence();
            for (int i = 0; i < starts.Length; i++)
            {
                trSeq.Append(ChrSeqs[chr].SubSequence(starts[i], ends[i] - starts[i]));
            }
            if (t.Strand == '-')
                trSeq.RevComp();
            return actualExtension;
        }
    }
}
