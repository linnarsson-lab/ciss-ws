using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using C1;

namespace Linnarsson.Strt
{
    public class CleavageSiteAnnotator
    {
        private static int CAPCloseSiteSearchStart = -100;
        private static int CAPCloseSiteSearchEnd = 200;

        private Dictionary<string, DnaSequence> ChrSeqs = new Dictionary<string, DnaSequence>();
        public string[] enzymes;

        public CleavageSiteAnnotator(StrtGenome genome)
        {
            enzymes = Props.props.CAPCloseSiteSearchCutters;
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            chrIdToFileMap[StrtGenome.chrCTRLId] = PathHandler.GetChrCTRLPath();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsASpliceAnnotation(chrId)) continue;
                DnaSequence chrSeq = DnaSequence.FromFile(chrIdToFileMap[chrId]);
                ChrSeqs.Add(chrId, chrSeq);
            }
        }

        public void AnnotateCleaveSites(GeneFeature gf)
        {
            List<int> sites = GetCleaveSites(gf);
            string siteList = (sites.Count == 0)? "" : GeneFeature.capCutSitesPrefix + string.Join(",", sites.ConvertAll(v => v.ToString()).ToArray());
            gf.GeneMetadata += ";" + siteList;
        }

        public List<int> GetCleaveSites(GeneFeature gf)
        {
            DnaSequence trSeq;
            int requestedExtension = (gf.Chr != StrtGenome.chrCTRLId) ? -CAPCloseSiteSearchStart : 0;
            int actualExtension = GetTrSeq(gf.Chr, gf.Strand, gf.ExonStartsString, gf.ExonEndsString, requestedExtension, out trSeq);
            List<int> sitePositions = GetSiteList(trSeq, actualExtension);
            return sitePositions;
        }

        public void Annotate(ref Transcript t)
        {
            DnaSequence trSeq;
            int requestedExtension = (t.Chromosome != StrtGenome.chrCTRLId) ? -CAPCloseSiteSearchStart : 0;
            int actualExtension = GetTrSeq(t.Chromosome, t.Strand, t.ExonStarts, t.ExonEnds, requestedExtension, out trSeq);
            List<int> sitePositions = GetSiteList(trSeq, actualExtension);
            string siteList = string.Join(",", sitePositions.ConvertAll(v => v.ToString()).ToArray());
            t.StartToCloseCutSite = siteList;
        }

        private List<int> GetSiteList(DnaSequence trSeq, int actualExtension)
        {
            List<int> sitePositions = new List<int>();
            foreach (string enzymeName in enzymes)
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
            return sitePositions;
        }

        private int GetTrSeq(string chr, char strand, string exonStarts, string exonEnds, int upstreamExtension, out DnaSequence trSeq)
        {
            int[] starts = Array.ConvertAll(exonStarts.Remove(exonStarts.Length - 1).Split(','), v => int.Parse(v));
            int[] ends = Array.ConvertAll(exonEnds.Remove(exonEnds.Length - 1).Split(','), v => int.Parse(v));
            int actualExtension = 0;
            if (strand == '+')
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
            if (strand == '-')
                trSeq.RevComp();
            return actualExtension;
        }

    }
}
