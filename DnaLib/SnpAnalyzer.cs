using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public struct SNPInfo
    {
        private uint bcIdxRndTagNt;
        public int LocusPos;
        public char Nt { get { return "ACGT"[(int)(bcIdxRndTagNt & 3)]; } }
        public int bcIdx { get { return (int)(bcIdxRndTagNt >> 16) & 255; } }
        public int rndTag { get { return (int)(bcIdxRndTagNt >> 2) & 255; } }

        public SNPInfo(char Nt, int bcIdx, int locusPos, int rndTag)
        {
            this.bcIdxRndTagNt = ((uint)bcIdx << 16) | ((uint)rndTag << 2) | (uint)"ACGT".IndexOf(Nt);
            this.LocusPos = locusPos;
        }
        public static int Compare(SNPInfo info1, SNPInfo info2)
        {
            if (info1.LocusPos > info2.LocusPos) return 1;
            if (info1.LocusPos < info2.LocusPos) return -1;
            return info1.bcIdxRndTagNt.CompareTo(info2.bcIdxRndTagNt);
        }
    }

    public class SnpAnalyzer
    {
        public static readonly int minAltHitsToTestSnpPos = 10;
        public static readonly double thresholdFractionAltHitsForMixPos = 0.25;
        public static readonly int MinTotalHitsToShowBarcodedSnps = 10;

        private static int halfMeanHitLen;

        public void WriteSnpsByBarcode(StreamWriter snpFile, Barcodes barcodes, 
                                       Dictionary<string, GeneFeature> geneFeatures, int averageHitLength)
        {
            SnpAnalyzer.halfMeanHitLen = averageHitLength / 2;
            snpFile.Write("#Gene\tTrLen\tChr\tStrand\tChrPos\tTrPos\tNt\tTotal");
            for (int idx = 0; idx < barcodes.Count; idx++)
                snpFile.Write("\t" + barcodes.GetWellId(idx));
            snpFile.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int currentLocusPos = -1;
                Dictionary<char, int[]> bcCountsByNt = new Dictionary<char, int[]>();
                foreach (SNPInfo info in gf.LocusSnps)
                {
                    if (currentLocusPos != -1 && info.LocusPos != currentLocusPos)
                        FlushCurrentSnpPosToFile(snpFile, gf, currentLocusPos, bcCountsByNt);
                    currentLocusPos = info.LocusPos;
                    char nt = info.Nt;
                    if (!bcCountsByNt.ContainsKey(nt))
                        bcCountsByNt[nt] = new int[barcodes.Count];
                    bcCountsByNt[nt][info.bcIdx]++;
                }
                if (currentLocusPos != -1)
                    FlushCurrentSnpPosToFile(snpFile, gf, currentLocusPos, bcCountsByNt);
            }
            snpFile.Close();
        }

        private void FlushCurrentSnpPosToFile(StreamWriter snpFile, GeneFeature gf, int locusSnpPos, Dictionary<char, int[]> bcCountsByNt)
        {
            int trLen = gf.GetTranscriptLength();
            int chrPos = locusSnpPos + gf.LocusStart;
            int spanStartInLocus = locusSnpPos - SnpAnalyzer.halfMeanHitLen;
            int spanEndInLocus = locusSnpPos + SnpAnalyzer.halfMeanHitLen;
            int[] barcodedTotalHits = CompactGenePainter.GetBarcodedIvlHitCount(spanStartInLocus, spanEndInLocus, gf.Strand, gf.LocusHits);
            int totalHits = 0;
            StringBuilder bcTotalsSB = new StringBuilder();
            foreach (int c in barcodedTotalHits)
            {
                totalHits += c;
                bcTotalsSB.Append("\t" + c);
            }
            string bcTotalHits = bcTotalsSB.ToString();
            int trPos = gf.GetTranscriptPos(chrPos);
            if (trPos >= 0 && totalHits >= SnpAnalyzer.MinTotalHitsToShowBarcodedSnps)
            {
                snpFile.WriteLine(gf.Name + "\t" + trLen + "\t" + gf.Chr + "\t" + gf.Strand + "\t" + chrPos
                                  + "\t" + trPos + "\tACGT\t" + totalHits + bcTotalHits);
                foreach (char ntc in bcCountsByNt.Keys)
                {
                    StringBuilder bcCountByNtSB = new StringBuilder();
                    int totalByNt = 0;
                    foreach (int c in bcCountsByNt[ntc])
                    {
                        totalByNt += c;
                        bcCountByNtSB.Append("\t" + c);
                    }
                    snpFile.WriteLine("\t\t\t\t\t\t" + ntc + "\t" + totalByNt + bcCountByNtSB.ToString());
                }
            }
            bcCountsByNt.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="safeSpanWithinRead">Normally readLen - 2 * MaxAlignmentMismatches</param>
        /// <param name="heterozygousLocusPos"></param>
        /// <param name="altLocusPos"></param>
        public static void GetSnpLocusPositions(GeneFeature gf, int safeSpanWithinRead, 
                                                out List<int> heterozygousLocusPos, out List<int> altLocusPos)
        {
            int halfMeanHitLen = safeSpanWithinRead / 2;
            heterozygousLocusPos = new List<int>();
            altLocusPos = new List<int>();
            if (gf.LocusSnps.Length == 0) return;
            Dictionary<int, int> snpCounts = GetSnpCountsByLocusPos(gf);
            foreach (int locusSnpPos in snpCounts.Keys)
            {
                int altCount = snpCounts[locusSnpPos];
                if (altCount >= minAltHitsToTestSnpPos)
                {
                    int spanStartInLocus = locusSnpPos - halfMeanHitLen;
                    int spanEndInLocus = locusSnpPos + halfMeanHitLen;
                    int totalHits = CompactGenePainter.GetIvlHitCount(spanStartInLocus, spanEndInLocus, gf.Strand, gf.LocusHits);
                    double ratio = altCount / (double)totalHits;
                    if (ratio > (1 - thresholdFractionAltHitsForMixPos))
                        altLocusPos.Add(locusSnpPos);
                    else if (ratio > thresholdFractionAltHitsForMixPos)
                        heterozygousLocusPos.Add(locusSnpPos);
                }
            }
        }

        private static Dictionary<int, int> GetSnpCountsByLocusPos(GeneFeature gf)
        {
            Dictionary<int, int> snpCountsByLocusPos = new Dictionary<int, int>();
            foreach (SNPInfo info in gf.LocusSnps)
            {
                int locusPos = info.LocusPos;
                if (snpCountsByLocusPos.ContainsKey(locusPos))
                    snpCountsByLocusPos[locusPos]++;
                else
                    snpCountsByLocusPos[locusPos] = 1;
            }
            return snpCountsByLocusPos;
        }
    }
}
