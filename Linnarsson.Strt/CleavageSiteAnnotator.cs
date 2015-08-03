using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.C1;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Annotator for cleavage sites of restriction enzymes close to 5' ends of features
    /// </summary>
    public class CleavageSiteAnnotator
    {
        private static int CAPCloseSiteSearchStart = -100;
        private static int CAPCloseSiteSearchEnd = 200;

        private Dictionary<string, DnaSequence> ChrSeqs = new Dictionary<string, DnaSequence>();
        public string[] enzymes;

        public CleavageSiteAnnotator(StrtGenome genome)
        {
            enzymes = Props.props.CAPCloseSiteSearchCutters;
            Console.WriteLine("CleavageSiteAnnotator loading chromosome sequences...");
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            foreach (string commonChrId in Props.props.CommonChrIds)
            {
                string commonChrPath = PathHandler.GetCommonChrPath(commonChrId);
                if (File.Exists(commonChrPath))
                    chrIdToFileMap[commonChrId] = commonChrPath;
            }
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsASpliceAnnotation(chrId)) continue;
                DnaSequence chrSeq = DnaSequence.FromFile(chrIdToFileMap[chrId]);
                ChrSeqs.Add(chrId, chrSeq);
            }
        }

        /// <summary>
        /// Annotate cleavage sites close to TSS for method-critical restriction enzymes and annotate in the GeneMetadata
        /// </summary>
        /// <param name="gf"></param>
        public void AnnotateCleaveSites(GeneFeature gf)
        {
            List<int> sites = GetCleaveSites(gf);
            string siteList = "";
            if (sites.Count > 0)
            {
                string[] sitesStr = sites.ConvertAll(v => v.ToString()).ToArray();
                siteList = string.Join(GeneFeature.metadataSubDelim.ToString(), sitesStr);
            }
            gf.GeneMetadata += GeneFeature.metadataDelim + siteList;
        }

        private List<int> GetCleaveSites(GeneFeature gf)
        {
            DnaSequence trSeq;
            int requestedExtension = StrtGenome.IsACommonChrId(gf.Chr)? 0 : -CAPCloseSiteSearchStart;
            int actualExtension = GetTrSeq(gf.Chr, gf.Strand, gf.ExonStartsString, gf.ExonEndsString, requestedExtension, out trSeq);
            List<int> sitePositions = GetSiteList(trSeq, actualExtension);
            return sitePositions;
        }

        public void Annotate(ref Transcript t)
        {
            if (!ChrSeqs.ContainsKey(t.Chromosome))
            {
                Console.WriteLine("Can not find chr" + t.Chromosome + " for " + t.GeneName + ". Skipping CleavageSiteAnnotation.");
                return;
            }
            DnaSequence trSeq;
            int requestedExtension = StrtGenome.IsACommonChrId(t.Chromosome) ? 0 : -CAPCloseSiteSearchStart;
            int actualExtension = GetTrSeq(t.Chromosome, t.Strand, t.ExonStarts, t.ExonEnds, requestedExtension, out trSeq);
            List<int> sitePositions = GetSiteList(trSeq, actualExtension);
            string siteList = string.Join(",", sitePositions.ConvertAll(v => v.ToString()).ToArray());
            t.StartToCloseCutSites = siteList;
        }

        /// <summary>
        /// Finds the location of cut sites just before or at most CAPCloseSiteSearchEnd after TSS.
        /// </summary>
        /// <param name="trSeq">Transcript sequence, including any 5' extension</param>
        /// <param name="actualExtension">Distance from 5' end of seq to TSS</param>
        /// <returns>List of cut positions relative to TSS</returns>
        private List<int> GetSiteList(DnaSequence trSeq, int actualExtension)
        {
            List<int> sitePositions = new List<int>();
            foreach (string enzymeName in enzymes)
            {
                RestrictionEnzyme enzyme = RestrictionEnzymes.Get(enzymeName);
                DnaSequence[] fragments = enzyme.Cut(trSeq);
                int cutPos = -actualExtension;
                for (int i = 0; i < fragments.Length - 1; i++)
                {
                    cutPos += (int)fragments[i].Count;
                    if (cutPos < CAPCloseSiteSearchEnd)
                        sitePositions.Add(cutPos);
                }
            }
            sitePositions.Sort();
            return sitePositions;
        }

        /// <summary>
        /// Extracts the trancript sequence given by the exon intervals and the requested upstream 5' extension
        /// </summary>
        /// <param name="chr"></param>
        /// <param name="strand"></param>
        /// <param name="exonStarts">comma separated list of exon starts on chr</param>
        /// <param name="exonEnds">comma separated list of exon ends on chr</param>
        /// <param name="upstreamExtension">requested upstream of TSS extension</param>
        /// <param name="trSeq">output transcript (mRNA orientation) sequence</param>
        /// <returns>actual extension 5' of TSS. May be less than upstreamExtension if end of chr is reached</returns>
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
