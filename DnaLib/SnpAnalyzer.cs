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

        public void WriteSnpsByBarcode(StreamWriter snpFile, Barcodes barcodes,
                                       Dictionary<string, GeneFeature> geneFeatures, int averageHitLength)
        {
            snpFile.Write("#Gene\tTrLen\tChr\tStrand\tChrPos\tTrPos\tNt\tTotal");
            for (int idx = 0; idx < barcodes.Count; idx++)
                snpFile.Write("\t" + barcodes.GetWellId(idx));
            snpFile.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int trLen = gf.GetTranscriptLength();
                foreach (KeyValuePair<int, SNPCounter[]> posCounts in gf.SNPCountersByBcIdx)
                {
                    int chrPos = posCounts.Key;
                    int trPos = gf.GetTranscriptPos(chrPos);
                    snpFile.WriteLine(gf.Name + "\t" + trLen + "\t" + gf.Chr + "\t" + gf.Strand + "\t" + chrPos
                                      + "\t" + trPos + "\tACGT\t" + gf.GetTranscriptHits());
                    Dictionary<char, StringBuilder> bcBcIdxStr = new Dictionary<char, StringBuilder>(5);
                    Dictionary<char, int> totals = new Dictionary<char, int>(5);
                    foreach (char nt in new char[] { '0', 'A', 'C', 'G', 'T' })
                    {
                        bcBcIdxStr[nt] = new StringBuilder();
                        totals[nt] = 0;
                        foreach (SNPCounter snpc in posCounts.Value)
                        {
                            int count = snpc.GetCount(nt);
                            totals[nt] += count;
                            bcBcIdxStr[nt].Append("\t" + count.ToString());
                        }
                    }
                    if (trPos >= 0 && totals['T'] >= SnpAnalyzer.MinTotalHitsToShowBarcodedSnps)
                    {
                        snpFile.WriteLine(gf.Name + "\t" + trLen + "\t" + gf.Chr + "\t" + gf.Strand + "\t" + chrPos
                                          + "\t" + trPos + "\tACGT\t" + totals['0'] + bcBcIdxStr['0']);
                        foreach (char nt in new char[] { 'A', 'C', 'G', 'T' })
                            snpFile.WriteLine("\t\t\t\t\t\t" + nt + "\t" + totals[nt] + bcBcIdxStr[nt]);
                    }
                    snpFile.Close();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="safeSpanWithinRead">Normally readLen - 2 * MaxAlignmentMismatches</param>
        /// <param name="heterozygousPos"></param>
        /// <param name="altPos"></param>
        public static void GetSnpChrPositions(GeneFeature gf, out List<int> heterozygousPos, out List<int> altPos)
        {
            heterozygousPos = new List<int>();
            altPos = new List<int>();
            if (gf.SNPCountersByBcIdx.Count == 0) return;
            foreach (KeyValuePair <int, SNPCounter[]> posCounts in gf.SNPCountersByBcIdx)
            {
                int chrPos = posCounts.Key;
                int altCount = 0;
                int totCount = 0;
                foreach (SNPCounter counter in posCounts.Value)
                {
                    totCount += counter.nTotal;
                    altCount += counter.nAlt;
                }
                if (altCount >= minAltHitsToTestSnpPos)
                {
                    double ratio = altCount / (double)totCount;
                    if (ratio > (1 - thresholdFractionAltHitsForMixPos))
                        altPos.Add(chrPos);
                    else if (ratio > thresholdFractionAltHitsForMixPos)
                        heterozygousPos.Add(chrPos);
                }
            }
        }
    }
}
