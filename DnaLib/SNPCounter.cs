using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Keeps track of which and how many SNP nts occur at a specific position
    /// </summary>
    public class SNPCounter
    {
        public int posOnChr; // Position on chr of SNP. Can refer to Splice chr - GeneFeature fixes this by adding splcOffset when marking SNP
        public char refNt = '0'; // Nucleotide on reference chromosome

        public SNPCounter(int posOnChr)
        {
            this.posOnChr = posOnChr;
        }
        public SNPCounter(int posOnChr, char refNt)
        {
            this.posOnChr = posOnChr;
            this.refNt = refNt;
        }


        /// <summary>
        /// Total sampling (read/molecule) count. Can be set after SNP analysis.
        /// </summary>
        public ushort nTotal = 0;
        public ushort nA = 0;
        public ushort nC = 0;
        public ushort nG = 0;
        public ushort nT = 0;

        public override string ToString()
        {
            return "SNPCounter(refNt=" + refNt + " nTotal=" + nTotal + " nA=" + nA + "/C=" + nC + "/G=" + nG + "/nT=" + nT +
                               " posOnChr=" + posOnChr + ")";
        }

        public void Add(char snpNt)
        {
            Add(snpNt, refNt);
        }
        /// <summary>
        /// Register a SNP at this position
        /// </summary>
        /// <param name="snpNt"></param>
        /// <param name="refNt">reference Nt, if known</param>
        public void Add(char snpNt, char refNt)
        {
            if (refNt != '0') this.refNt = refNt;
            switch (snpNt)
            {
                case 'A': nA++;
                    break;
                case 'C': nC++;
                    break;
                case 'G': nG++;
                    break;
                case 'T': nT++;
                    break;
            }
        }

        /// <summary>
        /// Get total number of SNP nucleotides registered
        /// </summary>
        /// <returns></returns>
        public int nSnps { get { return nA + nC + nG + nT; } }
    }

    /// <summary>
    /// Counter that includes info on location in genome of a SNP
    /// </summary>
    public class LocatedSNPCounter
    {
        public string chr;
        public int chrPos;
        public SNPCounter counter;
        
        public static string Header { get { return "Chr\tPosition\tnTotReads\tnA\tnC\tnG\tnT"; } }
        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", chr, chrPos, counter.nTotal, counter.nA, counter.nC, counter.nG, counter.nT);
        }
    }

    /// <summary>
    /// Summarizes SNP counts over barcodes for each SNP position of each gene.
    /// </summary>
    public class SNPCountsByBarcode
    {
        /// <summary>
        ///  Nucleotide on reference chromosome
        /// </summary>
        public char refNt;
        /// <summary>
        /// All arrays by bcIdx. Reference nt counts will only be in nTotal, not the nt array.
        /// </summary>
        private ushort[] nTotal;
        private ushort[] nA;
        private ushort[] nC;
        private ushort[] nG;
        private ushort[] nT;

        public SNPCountsByBarcode(int nBarcodes, char refNt)
        {
            this.refNt = refNt;
            this.nTotal = new ushort[nBarcodes];
            this.nA = new ushort[nBarcodes];
            this.nC = new ushort[nBarcodes];
            this.nG = new ushort[nBarcodes];
            this.nT = new ushort[nBarcodes];
        }

        public void Add(int bcIdx, SNPCounter dataFromRead)
        {
            nTotal[bcIdx] += dataFromRead.nTotal;
            nA[bcIdx] += dataFromRead.nA;
            nC[bcIdx] += dataFromRead.nC;
            nG[bcIdx] += dataFromRead.nG;
            nT[bcIdx] += dataFromRead.nT;
        }

        public void SummarizeNt(char nt, int[] selectedBarcodes, out int sum, out bool overflow)
        {
            overflow = false;
            sum = 0;
            ushort[] ntData = (nt == 'A') ? nA : (nt == 'C') ? nC : (nt == 'G') ? nG : (nt == 'T') ? nT : nTotal;
            foreach (int bcIdx in selectedBarcodes)
            {
                if (ntData[bcIdx] >= SNPCountsByBarcode.MaxCount) overflow = true;
                sum += ntData[bcIdx];
            }
        }

        public void GetTotals(int[] selectedBarcodes, out int sumTotal, out int sumAlt)
        {
            sumTotal = 0; sumAlt = 0;
            foreach (int bcIdx in selectedBarcodes)
            {
                sumTotal += nTotal[bcIdx];
                sumAlt += nA[bcIdx] + nC[bcIdx] + nG[bcIdx] + nT[bcIdx];
            }
        }

        public static string MaxTestedString(int n)
        {
            return (n == MaxCount) ? string.Format(">={0}", MaxCount) : n.ToString();
        }
        public static readonly int MaxCount = ushort.MaxValue;

        public int GetCount(int bcIdx, char snpNt)
        {
            switch (snpNt)
            {
                case 'A': return nA[bcIdx];
                case 'C': return nC[bcIdx];
                case 'G': return nG[bcIdx];
                case 'T': return nT[bcIdx];
                default: return nTotal[bcIdx];
            }
        }
    }

    /// <summary>
    /// Handles SNP counts per rndTag, to eliminate read SNPs due to PCR artifacts during amplification from individual molecules
    /// </summary>
    public class SNPCountsByRndTag
    {
        public char refNt = '0'; // Nucleotide on reference chromosome
        /// <summary>
        /// All arrays by rndTag
        /// </summary>
        public ushort[] nA;
        public ushort[] nC;
        public ushort[] nG;
        public ushort[] nT;

        public SNPCountsByRndTag(char refNt)
        {
            this.refNt = refNt;
            this.nA = new ushort[TagItem.nUMIs];
            this.nC = new ushort[TagItem.nUMIs];
            this.nG = new ushort[TagItem.nUMIs];
            this.nT = new ushort[TagItem.nUMIs];
        }
        public void Clear()
        {
            Array.Clear(nA, 0, nA.Length);
            Array.Clear(nC, 0, nC.Length);
            Array.Clear(nG, 0, nG.Length);
            Array.Clear(nT, 0, nT.Length);
        }

        public void Add(int rndTagIdx, char snpNt)
        {
            switch (snpNt)
            {
                case 'A': nA[rndTagIdx]++;
                    break;
                case 'C': nC[rndTagIdx]++;
                    break;
                case 'G': nG[rndTagIdx]++;
                    break;
                case 'T': nT[rndTagIdx]++;
                    break;
            }
        }

        /// <summary>
        /// Feed all SNP data from the valid random tags into the SNPCounter.
        /// </summary>
        /// <param name="countsAtOffset"></param>
        /// <param name="validRndTags"></param>
        public void Summarize(SNPCounter countsAtOffset, IEnumerable<int> validRndTags)
        {
            foreach (int rndTagIdx in validRndTags)
                countsAtOffset.Add(GetNt(rndTagIdx), refNt);
        }
        /// <summary>
        /// Return the winning nt in the given random tag
        /// </summary>
        /// <param name="rndTagIdx"></param>
        /// <returns></returns>
        private char GetNt(int rndTagIdx)
        {
            int maxN = nA[rndTagIdx];
            char maxC = 'A';
            if (nC[rndTagIdx] > maxN) { maxC = 'C'; maxN = nC[rndTagIdx]; }
            if (nG[rndTagIdx] > maxN) { maxC = 'G'; maxN = nG[rndTagIdx]; }
            if (nT[rndTagIdx] > maxN) return 'T';
            return (maxN > 0) ? maxC : '-';
        }

    }

}
