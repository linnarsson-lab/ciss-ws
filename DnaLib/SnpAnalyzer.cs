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

        /// <summary>
        /// Outputs SNP positions within all genes with the respective Nts and counts.
        /// </summary>
        /// <param name="snpFile">output file</param>
        /// <param name="barcodes">just needed for header</param>
        /// <param name="geneFeatures">dictionary of geneNames to GeneFeatures</param>
        public static void WriteSnpsByBarcode(string snpPath, Barcodes barcodes, int[] selectedBarcodes, 
                                              Dictionary<string, GeneFeature> geneFeatures)
        {
            int minHitsToTestSNP = (barcodes.HasUMIs) ? Props.props.MinMoleculesToTestSnp : Props.props.MinReadsToTestSnp;
            using (StreamWriter snpFile = new StreamWriter(snpPath))
            {
                if (barcodes.HasUMIs)
                    snpFile.WriteLine("#Read counts at positions with >= {0} hits and >= 1 non-reference nt hits.", minHitsToTestSNP);
                else
                {
                    snpFile.WriteLine("#Molecule counts at positions with >= {0} hits and >= 1 non-reference nt hits,", minHitsToTestSNP);
                    snpFile.WriteLine("#Spurious molecules in random tags that likely are results of artefactial (PCR) mutations have been removed.");
                }
                snpFile.Write("#Gene\tTrLen\tChr\tStrand\tChrPos\tTrPos\tNt\tTotal");
                foreach (int bcIdx in selectedBarcodes)
                    snpFile.Write("\t{0}", barcodes.GetWellId(bcIdx));
                snpFile.WriteLine();
                Dictionary<char, StringBuilder> byBcNtStrings = new Dictionary<char, StringBuilder>(5);
                Dictionary<char, int> totals = new Dictionary<char, int>(5);
                Dictionary<char, string> totalOverflow = new Dictionary<char, string>(5);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    if (gf.bcSNPCountsByRealChrPos == null)
                        continue;
                    int trLen = gf.GetTranscriptLength();
                    foreach (KeyValuePair<int, SNPCountsByBarcode> chrPosAndBcCounts in gf.bcSNPCountsByRealChrPos)
                    {
                        int chrPos = chrPosAndBcCounts.Key;
                        SNPCountsByBarcode bcSnpCounters = chrPosAndBcCounts.Value;
                        int trPos = gf.GetTranscriptPos(chrPos);
                        int total = 0;
                        char refNt = bcSnpCounters.refNt;
                        int[] refCounts = new int[barcodes.Count];
                        foreach (char nt in new char[] { '0', 'A', 'C', 'G', 'T' })
                        {
                            byBcNtStrings[nt] = new StringBuilder();
                            totals[nt] = 0;
                            totalOverflow[nt] = "";
                            int i = 0;
                            foreach (int bcIdx in selectedBarcodes)
                            { 
                                int count = bcSnpCounters.GetCount(bcIdx, nt);
                                totals[nt] += count;
                                total += count;
                                refCounts[i++] += (nt == '0') ? count : -count;
                                if (count > SNPCountsByBarcode.MaxCount) totalOverflow[nt] = ">=";
                                byBcNtStrings[nt].AppendFormat("\t{0}", SNPCountsByBarcode.MaxTestedString(count));
                            }
                        }
                        if (trPos >= 0 && total >= minHitsToTestSNP)
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
        public static int TestSNP(int nTotal, int nAlt)
        {
            if (nTotal > 0)
            {
                double ratio = nAlt / (double)nTotal;
                if (ratio > (1 - thresholdFractionAltHitsForMixPos))
                    return ALTERNATIVE;
                else if (ratio > thresholdFractionAltHitsForMixPos)
                    return HETEROZYGOUS;
            }
            return REFERENCE;
        }

    }
}
