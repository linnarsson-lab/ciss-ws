using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class SnpRndTagVerifierData
    {
        public static readonly int MinCountForValidAltNt = 2;
        public static readonly int MaxRatioToTopCountForValidAltNt = 4;

        /// <summary>
        /// SNPNt in first index (-,A,C,G,T) and counts in each rndTag in second index 
        /// </summary>
        public short[,] countsByNtAndRndTagIdx;
        /// <summary>
        /// Points out the nt which is on the reference chromosome
        /// </summary>
        public int refNtIdx = 0;

        public SnpRndTagVerifierData(int nRndTags)
        {
            countsByNtAndRndTagIdx = new short[5, nRndTags];
        }

        public static int GetNtIdx(char nt)
        {
            if (nt == '-') return 0;
            int idx = "NACGT".IndexOf(nt);
            if (idx >= 0) return idx;
            idx = "nacgt".IndexOf(nt);
            return idx;
        }

        public void Add(int rndTagIdx, Mismatch mm)
        {
            int ntIdx = GetNtIdx(mm.ntInChrDir);
            try
            {
                countsByNtAndRndTagIdx[ntIdx, rndTagIdx]++;
                if (refNtIdx == 0)
                    refNtIdx = GetNtIdx(mm.refNtInChrDir);
            }
            catch (Exception e)
            {
                Console.WriteLine("SnpRndTagData.Add() ERROR: RefNt=" + mm.refNtInChrDir + " AltNt=" + mm.ntInChrDir +
                                  " (local) ntIdx=" + ntIdx + " rndTagIdx=" + rndTagIdx + "\n  " + e);
            }
        }

        public void HasAllReadsInEachRndTagTheSameNt(out bool sameNtInEachRndTag, out int nUsedRndTags)
        {
            nUsedRndTags = 0;
            sameNtInEachRndTag = true;
            for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
            {
                int nAltNtsInRndTag = 0;
                for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                    if (countsByNtAndRndTagIdx[ntIdx, rndTagIdx] > 0)
                        nAltNtsInRndTag++;
                if (nAltNtsInRndTag > 1) sameNtInEachRndTag = false;
                if (nAltNtsInRndTag > 0) nUsedRndTags++;
            }
        }

        public List<char> GetValidNts()
        {
            int[] sumsPerNt = new int[5];
            for (int ntIdx = 0; ntIdx < 5; ntIdx++)
            {
                for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
                    sumsPerNt[ntIdx] += countsByNtAndRndTagIdx[ntIdx, rndTagIdx];
            }
            int maxPerNt = sumsPerNt.Max();
            int minValidPerNt = Math.Min(MinCountForValidAltNt, maxPerNt / MaxRatioToTopCountForValidAltNt);
            List<char> validNts = new List<char>();
            for (int ntIdx = 0; ntIdx < 5; ntIdx++)
            {
                if (sumsPerNt[ntIdx] > minValidPerNt)
                {
                    validNts.Add("0ACGT"[(ntIdx == 0) ? refNtIdx : ntIdx]);
                }
            }
            return validNts;
        }
    }

    /// <summary>
    /// Verifies that the same SNP occurs in all reads stemming from the same molecule (i.e. having equal chr, pos, bc, rndTag)
    /// Requires that props.AnalyzeSNPs is turned on.
    /// </summary>
    public class SnpRndTagVerifier
    {
        public Dictionary<int, SnpRndTagVerifierData[]> data = new Dictionary<int, SnpRndTagVerifierData[]>();
        public HashSet<int> snpPosOnChr = new HashSet<int>();
        public static string verificationChr;
        private Barcodes barcodes;
        private int minBowtieQAscii;
        /// <summary>
        /// Expecting the same chromosome for all data
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="nBarcodes"></param>
        public SnpRndTagVerifier(Props props, MapFileSnpFinder mfsf)
        {
            barcodes = props.Barcodes;
            minBowtieQAscii = props.SnpRndTagVerificationMinQAscii;
            verificationChr = props.SnpRndTagVerificationChr;
            foreach (LocatedSNPCounter locSNP in mfsf.IterSNPLocations(5))
                if (locSNP.chr == verificationChr)
                    snpPosOnChr.Add(locSNP.chrPos);
            Console.WriteLine("Found " + snpPosOnChr.Count + " potential SNPs to verify on chr " + verificationChr);
        }
        public SnpRndTagVerifier(Props props, StrtGenome genome)
        {
            barcodes = props.Barcodes;
            verificationChr = props.SnpRndTagVerificationChr;
            string GVFPath = PathHandler.GetGVFFile(genome);
            if (GVFPath == "")
                Console.WriteLine("Can not find a GVF file for " + genome.Build + ". Skipping SNP verification.");
            else
            {
                foreach (GVFRecord rec in GFF3CompatibleFile.Iterate(GVFPath, new GVFRecord()))
                {
                    if (rec.type == "SNV" && rec.AnyTranscriptEffect() && rec.seqid == verificationChr)
                        snpPosOnChr.Add(rec.start - 1);
                }
                Console.WriteLine("Registered " + snpPosOnChr.Count + " SNPs to verify on chr " + verificationChr + " using GVF file.");
            }
        }

        public void Add(MultiReadMappings mrm)
        {
            if (mrm.HasAltMappings || mrm[0].Chr != verificationChr)
                return;
            foreach (int snpPos in snpPosOnChr)
            {
                if (snpPos >= mrm[0].Position && snpPos < mrm[0].Position + mrm.SeqLen)
                {
                    int strandBit = (mrm[0].Strand == '+') ? 0 : 1;
                    int posStrand = (mrm[0].Position << 1) | strandBit;
                    if (!data.ContainsKey(posStrand))
                    {
                        data[posStrand] = new SnpRndTagVerifierData[barcodes.Count];
                        for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                            data[posStrand][bcIdx] = new SnpRndTagVerifierData(barcodes.RandomBarcodeCount);
                    }
                    data[posStrand][mrm.BarcodeIdx].Add(mrm.RandomBcIdx, GetMismatchAtPos(snpPos, mrm[0]));
                    break; // Ensure only count one snpPos per molecule
                }
            }
        }

        /// <summary>
        /// Parses the mismatch field in map file.
        /// </summary>
        /// <param name="snpPosOnChr"></param>
        /// <param name="mrm"></param>
        /// <returns>Mismatch at snpPosOnChr, or a mismatch with pos == 0 if there was no mismatch a snpPosOnChr</returns>
        private Mismatch GetMismatchAtPos(int snpPosOnChr, MultiReadMapping mrm)
        {
            foreach (Mismatch mm in mrm.IterMismatches(minBowtieQAscii))
            {
                if (mm.posInChr == snpPosOnChr)
                    return mm;
            }
            return new Mismatch(0, 0, '-', '-');
        }

        public void Verify(string fileNameBase)
        {
            int nMaxUsedRndTags = 50; // Errors may occur at rndTag saturation if two different molecules end up in the same rndTag
            string file = fileNameBase + "_SNP_RndTag_verification.tab";           
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Showing problematic positions on chr " + verificationChr + ". Summary at end.");
            writer.Write("ChrPos\tBarcode\tAltNts\t\tNt");
            for (int i = 0; i < barcodes.Count; i++)
                writer.Write("\t#InTag" + i);
            writer.WriteLine();
            int nCorrectHetero = 0, nCorrect = 0, nTotal = 0, nSkipped = 0;
            foreach (int pos in data.Keys)
            {
                for (int bcIdx = 0; bcIdx < data[pos].Length; bcIdx++)
                {
                    SnpRndTagVerifierData sData = data[pos][bcIdx];
                    bool sameNtInEachRndTag;
                    int nUsedRndTags;
                    sData.HasAllReadsInEachRndTagTheSameNt(out sameNtInEachRndTag, out nUsedRndTags);
                    if (nUsedRndTags > nMaxUsedRndTags)
                    {
                        nSkipped++;
                        continue;
                    }
                    nTotal++;
                    if (sameNtInEachRndTag)
                    {
                        if (sData.GetValidNts().Count > 1) nCorrectHetero++;
                        nCorrect++;
                    }
                    else
                    {
                        string[] ntLabels = new string[] { "0", "A", "C", "G", "T" };
                        int ntIdx = 0;
                        if (sData.refNtIdx > 0)
                        {
                            ntLabels[sData.refNtIdx] = "ref" + "0ACGT"[sData.refNtIdx];
                            ntIdx = 1;
                        }
                        string wStart = pos + "\t" + barcodes.Seqs[bcIdx] + "\t" + new string(sData.GetValidNts().ToArray());
                        for (; ntIdx < 5; ntIdx++)
                        {
                            int totCountInNt = 0;
                            StringBuilder sbNt = new StringBuilder();
                            sbNt.Append("\t" + ntLabels[ntIdx]);
                            for (int rndTagIdx = 0; rndTagIdx < sData.countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
                            {
                                int c = sData.countsByNtAndRndTagIdx[ntIdx, rndTagIdx];
                                totCountInNt += c;
                                sbNt.Append("\t" + c.ToString());
                            }
                            if (totCountInNt > 0)
                            {
                                writer.Write(wStart);
                                writer.WriteLine(sbNt.ToString());
                                wStart = "\t\t";
                            }
                        }
                    }
                }
            }
            writer.WriteLine("\n\n" + nSkipped + " chrPosition-barcode combinations with >" + nMaxUsedRndTags + " used rndTags were skipped.");
            writer.WriteLine(nCorrect + " out of totally " + nTotal +
                             " chrPosition-barcode combinations containing alternative Nts had the same Nt within every rndTag.");
            writer.WriteLine(nCorrectHetero + " of these were also found to be heterozygous (defined by min " + SnpRndTagVerifierData.MinCountForValidAltNt +
                             " reads and at least " + SnpRndTagVerifierData.MaxRatioToTopCountForValidAltNt + "X less reads than top Nt)");
            writer.Close();
        }
    }
}
