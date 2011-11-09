using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class ReadFrag
    {
        /// <summary>
        /// 0-based position within transcript, always counting in chr direction
        /// </summary>
        public int TrPosInChrDir { get; set; }
        /// <summary>
        /// Sequence of the fragment
        /// </summary>
        public DnaSequence Seq { get; set; }
        public int Length { get { return (int)Seq.Count; } }
        /// <summary>
        /// Numbers of exons that are at least partially included in the fragment. The first is numbered '1' and always the most 5' in chr.
        /// </summary>
        public List<int> ExonIds { get; set; }
        /// <summary>
        /// Start positions within the fragment of the exons. Count is always == ExonIds.Count + 1
        /// First == 0 unless some fill-out has been added at the start.
        /// Last == Seq.Count, unless some fill-out has been added at the end.
        /// </summary>
        public List<int> ExonBoundaryPositions { get; set; }
        public int FirstJunctionPos { get { return (ExonBoundaryPositions.Count > 2) ? ExonBoundaryPositions[1] : 0; } }

        public ReadFrag()
        { }
        public ReadFrag(int trPosInChrDir, DnaSequence seq, List<int> exonIds, List<int> exonBoundaryPositions)
        {
            TrPosInChrDir = trPosInChrDir;
            Seq = seq;
            ExonIds = exonIds;
            ExonBoundaryPositions = exonBoundaryPositions;
        }
    }

    public class ReadFragGenerator
    {
        private static void MakeReadFragContinuations(int nLeft, DnaSequence accseq, List<int> exonIds, List<int> exonJunctionPositions,
                       int exonIdx, List<ReadFrag> results, int currentPos, bool splices, List<DnaSequence> exons, int maxSkip, int minOverhang)
        {
            int imax = splices ? Math.Min(exonIdx + maxSkip, exons.Count) : exonIdx + 1;
            for (int i = exonIdx; i < imax; i++)
            {
                if (i > exonIdx && (nLeft < minOverhang || accseq.Count < minOverhang)) continue; // Avoid splices where the remaining bases are very few
                int take = Math.Min(nLeft, (int)exons[i].Count);
                ShortDnaSequence seq = new ShortDnaSequence(accseq);
                seq.Append(exons[i].SubSequence(0, take));
                List<int> nextExons = new List<int>(exonIds);
                nextExons.Add(i + 1);
                List<int> nextJunctionPositions = new List<int>(exonJunctionPositions);
                nextJunctionPositions.Add((int)accseq.Count);
                if (nLeft == take)
                    results.Add(new ReadFrag(currentPos, seq, nextExons, nextJunctionPositions));
                else
                    MakeReadFragContinuations(nLeft - take, seq, nextExons, nextJunctionPositions,
                                              i + 1, results, currentPos, splices, exons, maxSkip, minOverhang);
            }
        }

        public static List<ReadFrag> MakeAllReadFrags(int readLen, int step, bool makeSplices, int maxSkip, int minOverhang, List<DnaSequence> exons)
        {
            List<ReadFrag> results = new List<ReadFrag>();
            int totLen = 0;
            foreach (ShortDnaSequence s in exons)
                totLen += (int)s.Count;
            int sIdx = 0;
            int posInExon = 0;
            int exonLeft = (int)exons[sIdx].Count;
            for (int trPosInChrDir = 0; trPosInChrDir < totLen - readLen; trPosInChrDir += step)
            {
                int take = Math.Min(readLen, exonLeft);
                DnaSequence seq = exons[sIdx].SubSequence(posInExon, take);
                int nLeft = readLen - take;
                List<int> exonIds = new List<int>();
                exonIds.Add(sIdx + 1);
                List<int> exonBoundaryPositions = new List<int>();
                exonBoundaryPositions.Add(0);
                if (nLeft == 0)
                {
                    exonBoundaryPositions.Add((int)seq.Count);
                    results.Add(new ReadFrag(trPosInChrDir, seq, exonIds, exonBoundaryPositions));
                }
                else
                    MakeReadFragContinuations(nLeft, seq, exonIds, exonBoundaryPositions,
                                              sIdx + 1, results, trPosInChrDir, makeSplices, exons, maxSkip, minOverhang);
                exonLeft-= step;
                posInExon += step;
                while (exonLeft <= 0)
                {
                    posInExon -= (int)exons[sIdx].Count;
                    sIdx++;
                    exonLeft += (int)exons[sIdx].Count;
                }
            }
            return results;
        }
    }

}
