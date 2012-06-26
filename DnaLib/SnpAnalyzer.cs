using System;
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
                if (barcodes.HasRandomBarcodes)
                    snpFile.WriteLine("#Read counts at all positions that have at least one read with a non-reference nucleotide.");
                else
                    snpFile.WriteLine("#Molecule counts at all positions that have at least one molecule with a non-reference nucleotide,\n" +
                                      "#Spurious molecules in random tags that likely are results of artefactial (PCR) mutations have been removed.");
                snpFile.Write("#Gene\tTrLen\tChr\tStrand\tChrPos\tTrPos\tNt\tTotal");
                for (int idx = 0; idx < barcodes.Count; idx++)
                    snpFile.Write("\t{0}", barcodes.GetWellId(idx));
                snpFile.WriteLine();
                Dictionary<char, StringBuilder> byBcNtStrings = new Dictionary<char, StringBuilder>(5);
                Dictionary<char, int> totals = new Dictionary<char, int>(5);
                Dictionary<char, string> totalOverflow = new Dictionary<char, string>(5);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int trLen = gf.GetTranscriptLength();
                    int[] chrPositions = gf.bcSNPCountersByRealChrPos.Keys.ToArray();
                    Array.Sort(chrPositions);
                    foreach (int chrPos in chrPositions)
                    {
                        SNPCounter[] bcSnpCounters = gf.bcSNPCountersByRealChrPos[chrPos];
                        int trPos = gf.GetTranscriptPos(chrPos);
                        int total = 0;
                        char refNt = bcSnpCounters[0].refNt;
                        int[] refCounts = new int[bcSnpCounters.Length];
                        foreach (char nt in new char[] { '0', 'A', 'C', 'G', 'T' })
                        {
                            byBcNtStrings[nt] = new StringBuilder();
                            totals[nt] = 0;
                            totalOverflow[nt] = "";
                            int bcIdx = 0;
                            foreach (SNPCounter bcSnpCounter in bcSnpCounters)
                            {
                                int count = bcSnpCounter.GetCount(nt);
                                totals[nt] += count;
                                total += count;
                                refCounts[bcIdx++] += (nt == '0') ? count : -count;
                                if (count > SNPCounter.MaxCount) totalOverflow[nt] = ">=";
                                byBcNtStrings[nt].AppendFormat("\t{0}", SNPCounter.MaxTestedString(count));
                            }
                        }
                        if (trPos >= 0 && total >= SnpAnalyzer.MinTotalHitsToShowBarcodedSnps)
                        {
                            int refTotal = totals['0'] - totals['A'] - totals['C'] - totals['G'] - totals['T'];
                            string refIdxStr = string.Join("\t", Array.ConvertAll(refCounts, (w) => w.ToString()));
                            snpFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\tRef{6}\t{7}{8}\t{9}", gf.Name, trLen,
                                              gf.Chr, gf.Strand, chrPos, trPos, refNt, totalOverflow['0'], refTotal, refIdxStr);
                            foreach (char nt in new char[] { 'A', 'C', 'G', 'T' })
                            {
                                if (nt == refNt) continue;
                                int ntTotal = totals[nt];
                                string ntIdxStr = byBcNtStrings[nt].ToString();
                                snpFile.WriteLine("\t\t\t\t\t\t{0}\t{1}{2}{3}", nt, totalOverflow[nt], ntTotal, ntIdxStr);
                            }
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
            foreach (KeyValuePair <int, SNPCounter[]> bcCountsByChrPos in gf.bcSNPCountersByRealChrPos)
            {
                int chrPos = bcCountsByChrPos.Key;
                SNPCounter sumCounter = new SNPCounter(chrPos);
                foreach (SNPCounter bcCounter in bcCountsByChrPos.Value)
                    sumCounter.Add(bcCounter);
                sumCounters.Add(sumCounter);
            }
            sumCounters.Sort((x, y) => x.posOnChr.CompareTo(y.posOnChr));
            return sumCounters;
        }
    }
}
