using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public class SnpAnalyzer
    {
        public static readonly int minAltHitsToTestSnpPos = 10;
        public static readonly double thresholdFractionAltHitsForMixPos = 0.25;
        public static readonly int MinTotalHitsToShowBarcodedSnps = 10;

        /// <summary>
        /// Outputs SNP positions within all genes with the respective Nts and counts.
        /// </summary>
        /// <param name="snpFile">output file</param>
        /// <param name="barcodes">just needed for header</param>
        /// <param name="geneFeatures">dictionary of geneNames to GeneFeatures</param>
        public static void WriteSnpsByBarcode(StreamWriter snpFile, Barcodes barcodes, Dictionary<string, GeneFeature> geneFeatures)
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

        public static readonly int REFERENCE = 0;
        public static readonly int ALTERNATIVE = 1;
        public static readonly int HETEROZYGOUS = 2;
        public static int TestSNP(SNPCounter sumCounter)
        {
            if (sumCounter.nAlt >= minAltHitsToTestSnpPos)
            {
                double ratio = sumCounter.nAlt / (double)sumCounter.nTotal;
                if (ratio > (1 - thresholdFractionAltHitsForMixPos))
                    return ALTERNATIVE;
                else if (ratio > thresholdFractionAltHitsForMixPos)
                    return HETEROZYGOUS;
            }
            return REFERENCE;
        }

        /// <summary>
        /// Summarize SNP data across all barcodes for gven gene
        /// </summary>
        /// <param name="gf">Gene of interest</param>
        /// <returns>SNPCounters that summarize Nt:s at each considered position. Each counter's posOnChr is set</returns>
        public static List<SNPCounter> GetSnpChrPositions(GeneFeature gf)
        {
            List<SNPCounter> sumCounters = new List<SNPCounter>();
            if (gf.SNPCountersByBcIdx.Count == 0) return sumCounters;
            foreach (KeyValuePair <int, SNPCounter[]> posCounts in gf.SNPCountersByBcIdx)
            {
                int chrPos = posCounts.Key;
                SNPCounter sumCounter = new SNPCounter(chrPos);
                foreach (SNPCounter counter in posCounts.Value)
                    sumCounter.Add(counter);
                sumCounters.Add(sumCounter);
            }
            sumCounters.Sort((x, y) => x.posOnChr.CompareTo(y));
            return sumCounters;
        }
    }
}
