using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Keeps track of which and how many SNP Nts occur at a specific position
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

        public void Clear()
        {
            nTotal = nA = nC = nG = nT = 0;
        }

        public static string Header { get { return "RefNt\tTotal\tMut-A\tMut-C\tMut-G\tMut-T"; } }
        public string ToLine()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}\t{1}\t", refNt, MaxTestedString(nTotal));
            if (nA > 0) sb.Append(MaxTestedString(nA));
            sb.Append('\t');
            if (nC > 0) sb.Append(MaxTestedString(nC));
            sb.Append('\t');
            if (nG > 0) sb.Append(MaxTestedString(nG));
            sb.Append('\t');
            if (nT > 0) sb.Append(MaxTestedString(nT));
            return sb.ToString();
        }
        public static string MaxTestedString(int n)
        {
            return (n == MaxCount) ? string.Format(">={0}", MaxCount) : n.ToString();
        }
        public static int MaxCount { get { return ushort.MaxValue; } }

        public override string ToString()
        {
            return "SNPCounter(refNt=" + refNt + " nTotal=" + nTotal + " nA=" + nA + "/C=" + nC + "/G=" + nG + "/nT=" + nT +
                               " posOnChr=" + posOnChr + ")";
        }
        public int nAlt { get { return nA + nC + nG + nT; } }

        public int GetCount(char snpNt)
        {
            switch (snpNt)
            {
                case 'A': return nA;
                case 'C': return nC;
                case 'G': return nG;
                case 'T': return nT;
                default: return nTotal;
            }
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

        public void Add(SNPCounter other)
        {
            nTotal += other.nTotal;
            if (refNt == '0') refNt = other.refNt;
            nA += other.nA;
            nC += other.nC;
            nG += other.nG;
            nT += other.nT;
        }

        /// <summary>
        /// Get total number of SNP nucleotides registered
        /// </summary>
        /// <returns></returns>
        public int nSnps { get { return nA + nC + nG + nT; } }

        /// <summary>
        /// Find the most common SNP Nt
        /// </summary>
        /// <returns>The most common alternative Nt, or '-' if no non-ref Nt has been observed</returns>
        public char GetNt()
        {
            int maxN = nA;
            char maxC = 'A';
            if (nC > maxN) { maxC = 'C'; maxN = nC; }
            if (nG > maxN) { maxC = 'G'; maxN = nG; }
            if (nT > maxN) return 'T';
            return (maxN > 0)? maxC : '-'; // return maxC;
        }

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
            this.nA = new ushort[TagItem.nRndTags];
            this.nC = new ushort[TagItem.nRndTags];
            this.nG = new ushort[TagItem.nRndTags];
            this.nT = new ushort[TagItem.nRndTags];
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

        public void Summarize(SNPCounter countsAtOffset, List<int> validRndTags)
        {
            foreach (int rndTagIdx in validRndTags)
                countsAtOffset.Add(GetNt(rndTagIdx), refNt);
        }
        private char GetNt(int rndTagIdx)
        {
            int maxN = nA[rndTagIdx];
            char maxC = 'A';
            if (nC[rndTagIdx] > maxN) { maxC = 'C'; maxN = nC[rndTagIdx]; }
            if (nG[rndTagIdx] > maxN) { maxC = 'G'; maxN = nG[rndTagIdx]; }
            if (nT[rndTagIdx] > maxN) return 'T';
            return (maxN > 0) ? maxC : '-'; // return maxC;
        }

    }

    /// <summary>
    /// Keeps track of SNPs by molecule and relative offset within read for reads mapping at the same genomic position
    /// </summary>
    public class TagSNPCounters
    {
        /// <summary>
        /// At each offset relative to the 5' pos on chr of the reads' alignment where some SNPs appear,
        /// keep an array by rndTag of counts for each SNP nt. 
        /// </summary>
        public Dictionary<byte, SNPCountsByRndTag> SNPCountsByOffset { get; private set; }

        public TagSNPCounters()
        {
            SNPCountsByOffset = new Dictionary<byte, SNPCountsByRndTag>();
        }
        /// <summary>
        /// </summary>
        /// <param name="snpOffset">Offset from 5' pos on chr of reads' alignment</param>
        public void RegisterSNPAtOffset(byte snpOffset)
        {
            SNPCountsByOffset[snpOffset] = null;
        }

        public void Clear()
        {
            foreach (SNPCountsByRndTag counts in SNPCountsByOffset.Values)
                if (counts != null)
                    counts.Clear();
        }

        /// <summary>
        /// Add the Nt at a SNP position from a read.
        /// If the position has not been defined as a SNP by a previous call to RegisterSNPAtOffset(), it will be skipped
        /// </summary>
        /// <param name="rndTagIdx">The rndTag of the read</param>
        /// <param name="snpOffset">Offset within the read of the SNP</param>
        /// <param name="snpNt">The reads' Nt at the SNP positions</param>
        public void AddSNP(int rndTagIdx, Mismatch mm)
        {
            SNPCountsByRndTag SNPCounts;
            if (!SNPCountsByOffset.TryGetValue(mm.relPosInChrDir, out SNPCounts))
                return;
            if (SNPCounts == null)
            {
                SNPCounts = new SNPCountsByRndTag(mm.refNtInChrDir);
                SNPCountsByOffset[mm.relPosInChrDir] = SNPCounts;
            }
            SNPCounts.Add(rndTagIdx, mm.ntInChrDir);
        }

    }
}
