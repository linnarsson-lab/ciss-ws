﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
    public class SnpAnalyzer
    {
        public static readonly double thresholdFractionAltHitsForMixPos = 0.25;
        public static readonly int MinTotalHitsToShowBarcodedSnps = 10;

        /// <summary>
        /// Outputs SNP positions within all genes with the respective Nts and counts.
        /// </summary>
        /// <param name="snpFile">output file</param>
        /// <param name="barcodes">just needed for header</param>
        /// <param name="geneFeatures">dictionary of geneNames to GeneFeatures</param>
        public static void WriteSnpsByBarcode(string snpPath, Barcodes barcodes, Dictionary<string, GeneFeature> geneFeatures)
        {
            using (StreamWriter snpFile = new StreamWriter(snpPath))
            {
                snpFile.Write("#Gene\tTrLen\tChr\tStrand\tChrPos\tTrPos\tNt\tTotal");
                for (int idx = 0; idx < barcodes.Count; idx++)
                    snpFile.Write("\t{0}", barcodes.GetWellId(idx));
                snpFile.WriteLine();
                Dictionary<char, StringBuilder> bcBcIdxStr = new Dictionary<char, StringBuilder>(5);
                Dictionary<char, int> totals = new Dictionary<char, int>(5);
                Dictionary<char, string> totalOverflow = new Dictionary<char, string>(5);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int trLen = gf.GetTranscriptLength();
                    foreach (KeyValuePair<int, SNPCounter[]> posCounts in gf.bcSNPCountersByRealChrPos)
                    {
                        int chrPos = posCounts.Key;
                        int trPos = gf.GetTranscriptPos(chrPos);
                        snpFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\tACGT\t{6}",
                                    gf.Name, trLen, gf.Chr, gf.Strand, chrPos, trPos, gf.GetTranscriptHits());
                        int total = 0;
                        bcBcIdxStr.Clear();
                        totals.Clear();
                        totalOverflow.Clear();
                        foreach (char nt in new char[] { '0', 'A', 'C', 'G', 'T' })
                        {
                            bcBcIdxStr[nt] = new StringBuilder();
                            totals[nt] = 0;
                            totalOverflow[nt] = "";
                            foreach (SNPCounter snpc in posCounts.Value)
                            {
                                int count = snpc.GetCount(nt);
                                totals[nt] += count;
                                total += count;
                                if (count > SNPCounter.MaxCount) totalOverflow[nt] = ">=";
                                bcBcIdxStr[nt].AppendFormat("\t{0}", SNPCounter.MaxTestedString(count));
                            }
                        }
                        if (trPos >= 0 && total >= SnpAnalyzer.MinTotalHitsToShowBarcodedSnps)
                        {
                            snpFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\tACGT\t{6}{7}",
                                       gf.Name, trLen, gf.Chr, gf.Strand, chrPos, trPos, totals['0'], bcBcIdxStr['0']);
                            foreach (char nt in new char[] { 'A', 'C', 'G', 'T' })
                                snpFile.WriteLine("\t\t\t\t\t\t{0}\t{1}{2}{3}", nt, totalOverflow[nt], totals[nt], bcBcIdxStr[nt]);
                        }
                    }
                }
            }
        }

        public static readonly int REFERENCE = 0;
        public static readonly int ALTERNATIVE = 1;
        public static readonly int HETEROZYGOUS = 2;
        public static int TestSNP(SNPCounter sumCounter)
        {
            if (sumCounter.nTotal > 0)
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
        /// Summarize SNP data across all barcodes for a gene
        /// </summary>
        /// <param name="gf">Gene of interest</param>
        /// <returns>SNPCounters that summarize Nt:s at each considered position. Each counter's posOnChr is set</returns>
        public static List<SNPCounter> GetSnpChrPositions(GeneFeature gf)
        {
            List<SNPCounter> sumCounters = new List<SNPCounter>();
            if (gf.bcSNPCountersByRealChrPos.Count == 0) return sumCounters;
            foreach (KeyValuePair <int, SNPCounter[]> posCounts in gf.bcSNPCountersByRealChrPos)
            {
                int chrPos = posCounts.Key;
                SNPCounter sumCounter = new SNPCounter(chrPos);
                foreach (SNPCounter counter in posCounts.Value)
                    sumCounter.Add(counter);
                sumCounters.Add(sumCounter);
            }
            sumCounters.Sort((x, y) => x.posOnChr.CompareTo(y.posOnChr));
            return sumCounters;
        }
    }
}
