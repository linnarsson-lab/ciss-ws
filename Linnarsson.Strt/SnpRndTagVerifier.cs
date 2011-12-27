using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class SnpRndTagData
    {
        /// <summary>
        /// SNPNt in first index (-,A,C,G,T) and counts in each rndTag in second index 
        /// </summary>
        public short[,] countsByNtAndRndTagIdx;

        public SnpRndTagData(int nRndTags)
        {
            countsByNtAndRndTagIdx = new short[5, nRndTags];
        }

        public static int GetNtIdx(char nt)
        {
            int idx = "NACGT".IndexOf(nt);
            if (idx >= 0) return idx;
            idx = "nacgt".IndexOf(nt);
            if (idx == -1) idx = 0;
            return idx;
        }

        public void Add(int rndTagIdx, char nt)
        {
            int ntIdx = GetNtIdx(nt);
            try
            {
                countsByNtAndRndTagIdx[ntIdx, rndTagIdx]++;
            }
            catch (Exception e)
            {
                Console.WriteLine("AddERROR: Nt=" + nt + " ntIDx=" + ntIdx + " rndTagIdx=" + rndTagIdx);
            }
        }

        public bool HasAllReadsInEachRndTagTheSameNt()
        {
            for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
            {
                int nAltNtsInRndTag = 0;
                for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                    if (countsByNtAndRndTagIdx[ntIdx, rndTagIdx] > 0)
                        nAltNtsInRndTag++;
                if (nAltNtsInRndTag > 1) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Verifies that the same SNP occurs in all reads stemming from the same molecule (i.e. having equal chr, pos, bc, rndTag)
    /// Requires that props.AnalyzeSNPs is turned on.
    /// </summary>
    public class SnpRndTagVerifier
    {
        public Dictionary<int, SnpRndTagData[]> data = new Dictionary<int, SnpRndTagData[]>();
        public static string verificationChr;
        private Props props;
        /// <summary>
        /// Expecting the same chromosome for all data
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="nBarcodes"></param>
        public SnpRndTagVerifier(Props props, MapFileSnpFinder mfsf)
        {
            this.props = props;
            verificationChr = props.SnpRndTagVerificationChr;
            int nBarcodes = props.Barcodes.Count;
            int nRndTags = props.Barcodes.RandomBarcodeCount;
            foreach (LocatedSNPCounter locSNP in mfsf.IterSNPLocations())
            {
                if (locSNP.chr == verificationChr)
                {
                    data[locSNP.chrPos] = new SnpRndTagData[nBarcodes];
                    for (int i = 0; i < nBarcodes; i++)
                        data[locSNP.chrPos][i] = new SnpRndTagData(nRndTags);
                }
            }
        }

        public void Add(MultiReadMappings mrm)
        {
            if (!mrm.HasAltMappings && mrm[0].Chr == verificationChr)
                foreach (int snpPos in data.Keys)
                {
                    if (snpPos >= mrm[0].Position && snpPos < mrm[0].Position + mrm.SeqLen)
                        data[snpPos][mrm.BarcodeIdx].Add(mrm.RandomBcIdx, GetNtAtPos(snpPos, mrm[0]));
                }
        }

        private char GetNtAtPos(int snpPos, MultiReadMapping mrm)
        {
            foreach (Mismatch mm in mrm.IterMismatches())
            {
                if (mrm.Position + mm.relPosInChrDir == snpPos)
                    return mm.ntInChrDir;
            }
            return '-';
        }

        public void Verify(string fileNameBase)
        {
            int nMaxUsedRndTags = 50; // Errors may occur at rndTag saturation if two different molecules end up in the same rndTag
            string file = fileNameBase + "_SNP_RndTag_verification.tab";           
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Showing problematic positions on chr " + props.SnpRndTagVerificationChr +
                             ". (The genome, nonSNP, nt is represented by 0, since seq data is not kept.) Summary at end.");
            writer.Write("ChrPos\tBarcode\tNt");
            for (int i = 0; i < props.Barcodes.Count; i++)
                writer.Write("\t#InTag" + i);
            writer.WriteLine();
            int nCorrect = 0, nTotal = 0;
            foreach (int pos in data.Keys)
            {
                for (int bcIdx = 0; bcIdx < data[pos].Length; bcIdx++)
                {
                    SnpRndTagData sData = data[pos][bcIdx];
                    nTotal++;
                    if (sData.HasAllReadsInEachRndTagTheSameNt())
                        nCorrect++;
                    else
                    {
                        string wStart = pos + "\t" + props.Barcodes.Seqs[bcIdx];
                        for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                        {
                            int cInNt = 0;
                            StringBuilder sbNt = new StringBuilder();
                            sbNt.Append("\t" + "0ACGT"[ntIdx]);
                            for (int i = 0; i < sData.countsByNtAndRndTagIdx.GetLength(1); i++)
                            {
                                int c = sData.countsByNtAndRndTagIdx[ntIdx, i];
                                cInNt += c;
                                sbNt.Append("\t" + c.ToString());
                            }
                            if (cInNt > 0)
                            {
                                writer.Write(wStart);
                                writer.WriteLine(sbNt.ToString());
                                wStart = "\t";
                            }
                        }
                    }
                }
            }
            writer.WriteLine("\n\n" + nCorrect + " out of totally " + nTotal +
                             " chrPosition-barcode combinations containing alternative Nts had the same Nt within every rndTag.");
            writer.WriteLine("Max " + nMaxUsedRndTags + " busy rndTags were allowed for analysis.");
            writer.Close();
        }
    }
}
