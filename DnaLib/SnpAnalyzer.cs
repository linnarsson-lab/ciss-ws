using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public struct SNPInfo
    {
        public char Nt;
        public uint bcIdx;
        public uint LocusPos;
        public uint CodedSNP {
            get
            {
                return (LocusPos << 9) | (bcIdx << 2) | (uint)"ACGT".IndexOf(Nt);
            }
        }

        public SNPInfo(uint codedSnp)
        {
            Nt = "ACGT"[ (int)(codedSnp & 3) ];
            bcIdx = (codedSnp >> 2) & 127;
            LocusPos = (codedSnp >> 9);
        }
        public SNPInfo(char Nt, int bcIdx, int locusPos)
        {
            this.Nt = Nt;
            this.bcIdx = (uint)bcIdx;
            this.LocusPos = (uint)locusPos;
        }
    }

    public class SnpAnalyzer
    {
        public static readonly int minAltHitsToTestSnpPos = 10;
        public static readonly double thresholdFractionAltHitsForMixPos = 0.25;
        public static readonly int MinTotalHitsToShowBarcodedSnps = 10;

        private Dictionary<char, int[]> countsByNt;
        private static int halfMeanHitLen;

        public void WriteSnpsByBarcode(StreamWriter snpFile, Barcodes barcodes, Dictionary<string, GeneFeature> geneFeatures, int averageHitLength)
        {
            SnpAnalyzer.halfMeanHitLen = averageHitLength / 2;
            snpFile.Write("#Gene\tChr\tChrPos\tStrand\tTrLen\tPosFrom5'\tNt\tTotal");
            for (int idx = 0; idx < barcodes.Count; idx++)
                snpFile.Write("\t" + barcodes.GetWellId(idx));
            snpFile.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int currentLocusPos = -1;
                countsByNt = new Dictionary<char, int[]>();
                foreach (uint codedSnp in gf.LocusSnps)
                {
                    SNPInfo info = new SNPInfo(codedSnp);
                    int nextLocusPos = (int)info.LocusPos;
                    if (currentLocusPos != -1 && nextLocusPos != currentLocusPos)
                        FlushCurrentSnpPosToFile(snpFile, gf, currentLocusPos);
                    currentLocusPos = nextLocusPos;
                    char nt = info.Nt;
                    if (!countsByNt.ContainsKey(nt))
                        countsByNt[nt] = new int[barcodes.Count];
                    countsByNt[nt][info.bcIdx]++;
                }
                if (currentLocusPos != -1)
                    FlushCurrentSnpPosToFile(snpFile, gf, currentLocusPos);
            }
            snpFile.Close();
        }

        private void FlushCurrentSnpPosToFile(StreamWriter snpFile, GeneFeature gf, int locusSnpPos)
        {
            int chrPos = locusSnpPos + gf.LocusStart;
            int spanStartInLocus = locusSnpPos - SnpAnalyzer.halfMeanHitLen;
            int spanEndInLocus = locusSnpPos + SnpAnalyzer.halfMeanHitLen;
            int[] barcodedTotalHits = CompactGenePainter.GetBarcodedIvlHitCount(spanStartInLocus, spanEndInLocus, gf.Strand, gf.LocusHits);
            int totalHits = 0;
            foreach (int c in barcodedTotalHits)
                totalHits += c;
            int trPos = gf.GetTranscriptPos(chrPos);
            if (trPos >= 0 && totalHits >= SnpAnalyzer.MinTotalHitsToShowBarcodedSnps)
            {
                foreach (char ntc in countsByNt.Keys)
                {
                    int totalByNt = 0;
                    foreach (int c in countsByNt[ntc])
                        totalByNt += c;
                    snpFile.Write(gf.Name + "\t" + gf.Chr + "\t" + chrPos + "\t" + gf.Strand
                                    + "\t" + gf.GetTranscriptLength() + "\t" + trPos + "\t" + ntc + "\t" + totalByNt);
                    foreach (int c in countsByNt[ntc])
                        snpFile.Write("\t" + c);
                    snpFile.WriteLine();
                    snpFile.Write("\t\t\t\t\t" + trPos + "\tACGT\t" + totalHits);
                    foreach (int c in barcodedTotalHits)
                        snpFile.Write("\t" + c);
                    snpFile.WriteLine();
                }
            }
            countsByNt.Clear();
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
            foreach (uint codedSnp in gf.LocusSnps)
            {
                int locusPos = (int) new SNPInfo(codedSnp).LocusPos;
                if (snpCountsByLocusPos.ContainsKey(locusPos))
                    snpCountsByLocusPos[locusPos]++;
                else
                    snpCountsByLocusPos[locusPos] = 1;
            }
            return snpCountsByLocusPos;
        }
    }
}
