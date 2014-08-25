using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public delegate IEnumerable<FtInterval> IterTranscriptMatcher(string chr, char sequencedStrand, int hitMidPos);

    /// <summary>
    /// Encapsulates methods to iterate the FtInterval(:s) that should be annotated with a read/molecule.
    /// </summary>
    public class IterTranscriptMatchers
    {
        private Dictionary<string, QuickAnnotationMap> ExonAnnotations;
        private static FtInterval nullIvl = new FtInterval();
        private static FtInterval firstMatch;
        /// <summary>
        /// set immediately by each Iter method.
        /// true if there are multiple transcripts (or variants) annotated at the hit position
        /// </summary>
        public static bool HasVariants;

        public IterTranscriptMatchers(Dictionary<string, QuickAnnotationMap> exonAnnotations)
        {
            ExonAnnotations = exonAnnotations;
        }
        public IterTranscriptMatcher GetMatcher()
        {
            if (Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping)
                return IterMost5PrimeTranscriptMatch;
            else
                return IterAllTranscriptMatches;
        }

        /// <summary>
        /// Yields a matching annotated interval that corresponds to a transcript.
        /// Will only yield the one (if any) on the given strand where the 5' transcript end is closest.
        /// Will set HasVariants to indicate if there are several alternative matches
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="sequencedStrand">Strand of hit</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> IterMost5PrimeTranscriptMatch(string chr, char sequencedStrand, int hitMidPos)
        {
            int bestDist = int.MaxValue;
            firstMatch = nullIvl;
            int nMatches = 0;
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.IsTrDetectingStrand(sequencedStrand)) //(ivl.Strand == strand)
                {
                    nMatches++;
                    int dist = ivl.GetTranscriptPos(hitMidPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        firstMatch = ivl;
                    }
                }
            }
            HasVariants = (nMatches > 1);
            if (bestDist < int.MaxValue)
                yield return firstMatch;
        }

        /// <summary>
        /// Yields all matching exonic intervals that correspond to (forward strand for directional reads) transcripts.
        /// Will set HasVariants to indicate if there are several alternative matches.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="sequencedStrand">Strand of hit (for directional reads)</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> IterAllTranscriptMatches(string chr, char sequencedStrand, int hitMidPos)
        {
            int nMatches = 0;
            HasVariants = false;
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.IsTrDetectingStrand(sequencedStrand)) //(ivl.Strand == strand || !Props.props.DirectionalReads)
                {
                    nMatches++;
                    if (nMatches == 1)
                        firstMatch = ivl; // Save first to be able to set HasVariants correctly before yielding
                    else
                    {
                        HasVariants = true;
                        yield return ivl;
                    }
                }
            }
            if (nMatches >= 1)
                yield return firstMatch;
        }
    }

}
