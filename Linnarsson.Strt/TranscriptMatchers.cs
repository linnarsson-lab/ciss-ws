using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public delegate List<FtInterval> TranscriptMatcher(string chr, char strand, int hitMidPos, out bool hasVariants);
    public class TranscriptMatchers
    {
        private Dictionary<string, QuickAnnotationMap> ExonAnnotations;
        private static FtInterval nullIvl = new FtInterval();
        public TranscriptMatchers(Dictionary<string, QuickAnnotationMap> exonAnnotations)
        {
            ExonAnnotations = exonAnnotations;
        }
        public TranscriptMatcher GetMatcher()
        {
            if (Props.props.DirectionalReads && Props.props.UseMost5PrimeExonMapping)
                return GetMost5PrimeTranscriptMatch;
            else
                return GetAllTranscriptMatches;
        }

        /// <summary>
        /// Finds a matching annotated interval that corresponds to a forward strand transcript.
        /// Will only return the one (if any) on the given strand where the 5' transcript end is closest.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="strand">Strand of hit</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public List<FtInterval> GetMost5PrimeTranscriptMatch(string chr, char strand, int hitMidPos, out bool hasVariants)
        {
            List<FtInterval> matches = new List<FtInterval>();
            int bestDist = int.MaxValue;
            FtInterval bestMatch = nullIvl;
            int nMatches = 0;
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand)
                {
                    nMatches++;
                    int dist = ivl.GetTranscriptPos(hitMidPos);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestMatch = ivl;
                    }

                }
            }
            if (bestDist < int.MaxValue)
                matches.Add(bestMatch);
            hasVariants = (nMatches > 1);
            return matches;
        }

        /// <summary>
        /// Finds all matching exonic intervals that correspond to (forward strand for directional reads) transcripts.
        /// </summary>
        /// <param name="chr">Chromosome of hit</param>
        /// <param name="strand">Strand of hit (for directional reads)</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public List<FtInterval> GetAllTranscriptMatches(string chr, char strand, int hitMidPos, out bool hasVariants)
        {
            List<FtInterval> matches = new List<FtInterval>();
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand || !AnnotType.DirectionalReads) matches.Add(ivl);
            }
            hasVariants = (matches.Count > 1);
            return matches;
        }
    }

    public delegate IEnumerable<FtInterval> IterTranscriptMatcher(string chr, char strand, int hitMidPos);
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
        /// <param name="strand">Strand of hit</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> IterMost5PrimeTranscriptMatch(string chr, char strand, int hitMidPos)
        {
            int bestDist = int.MaxValue;
            firstMatch = nullIvl;
            int nMatches = 0;
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand)
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
        /// <param name="strand">Strand of hit (for directional reads)</param>
        /// <param name="hitMidPos">Middle position of hit on chromosome</param>
        /// <returns></returns>
        public IEnumerable<FtInterval> IterAllTranscriptMatches(string chr, char strand, int hitMidPos)
        {
            int nMatches = 0;
            HasVariants = false;
            foreach (FtInterval ivl in ExonAnnotations[chr].IterItems(hitMidPos))
            {
                if (ivl.Strand == strand || !AnnotType.DirectionalReads)
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
