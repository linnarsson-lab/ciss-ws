﻿using System;
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
        /// <summary>
        /// Total sampling (read/molecule) count. Can be set after SNP analysis.
        /// </summary>
        public int nTotal = 0;
        public int nA = 0;
        public int nC = 0;
        public int nG = 0;
        public int nT = 0;

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
        /// <summary>
        /// Register a SNP at this position
        /// </summary>
        /// <param name="snpNt"></param>
        public void Add(char snpNt)
        {
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
        /// Find the most common SNP nt
        /// </summary>
        /// <returns></returns>
        public char GetNt()
        {
            int maxN = nA;
            char maxC = 'A';
            if (nC > maxN) { maxC = 'C'; maxN = nC; }
            if (nG > maxN) { maxC = 'G'; maxN = nG; }
            if (nT > maxN) return 'T';
            return maxC;
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

    public class RndTagSNPData
    {
        private Dictionary<byte, SNPCounter[]> SNPData;

        public RndTagSNPData()
        {
            SNPData = new Dictionary<byte, SNPCounter[]>();
        }
        public void RegisterSNP(byte snpOffset)
        {
            SNPData[snpOffset] = null;
        }

        public byte[] GetSNPOffsets()
        {
            return SNPData.Keys.ToArray();
        }

        public void Clear()
        {
            if (SNPData != null)
                foreach (byte snpOffset in SNPData.Keys)
                    SNPData[snpOffset] = null;
        }

        public void AddSNP(int rndTagIdx, byte snpOffset, char snpNt)
        {
            SNPCounter[] snpCounterByRndTag = SNPData[snpOffset];
            if (snpCounterByRndTag == null)
            {
                snpCounterByRndTag = new SNPCounter[TagItem.nRndTags];
                for (int n = 0; n < TagItem.nRndTags; n++)
                    snpCounterByRndTag[n] = new SNPCounter();
            }
            snpCounterByRndTag[rndTagIdx].Add(snpNt);
        }

        public SNPCounter GetMolSNPCounts(byte snpOffset, List<int> validRndTagIndices)
        {
            SNPCounter counts = new SNPCounter();
            foreach (int rndTagIdx in validRndTagIndices)
            {
                char snpNt = SNPData[snpOffset][rndTagIdx].GetNt();
                counts.Add(snpNt);
            }
            return counts;
        }
    }
}
