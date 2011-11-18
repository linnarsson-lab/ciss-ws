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
        public short[,] countsByNtAndRndTagIdx;

        public SnpRndTagData(int nRndTags)
        {
            countsByNtAndRndTagIdx = new short[5, nRndTags];
        }

        public static int GetNtIdx(char nt)
        {
            return "-ACGT".IndexOf(nt);
        }

        public void Add(int rndTagIdx, char nt)
        {
            int ntIdx = GetNtIdx(nt);
            countsByNtAndRndTagIdx[ntIdx, rndTagIdx]++;
        }

        public bool VerifyAndCalcNtAndRndTagCounts(out int nAltNts, out int rndTagCount)
        {
            bool result = true;
            int[] ntUsed = new int[5];
            rndTagCount = 0;
            for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
            {
                int nAltNtsInRndTag = 0;
                for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                    if (countsByNtAndRndTagIdx[ntIdx, rndTagIdx] > 0)
                    {
                        ntUsed[ntIdx] = 1;
                        nAltNtsInRndTag++;
                    }
                if (nAltNtsInRndTag > 0) rndTagCount++;
                if (nAltNtsInRndTag > 1) result = false;
            }
            nAltNts = ntUsed.Sum();
            return result;
        }
    }

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
        public SnpRndTagVerifier(Props props)
        {
            this.props = props;
            verificationChr = props.SnpRndTagVerificationChr;
            int[] verPositions = props.SnpRndTagVerificationPositions;
            int nBarcodes = props.Barcodes.Count;
            int nRndTags = props.Barcodes.RandomBarcodeCount;
            foreach (int pos in verPositions)
            {
                data[pos] = new SnpRndTagData[nBarcodes];
                for (int i = 0; i < nBarcodes; i++)
                    data[pos][i] = new SnpRndTagData(nRndTags);
            }
        }

        public void Add(MultiReadMappings mrm)
        {
            foreach (MultiReadMapping m in mrm.IterMappings())
            {
                if (m.Chr == verificationChr)
                    foreach (int snpPos in data.Keys)
                    {
                        if (snpPos >= m.Position && snpPos <= m.Position + mrm.SeqLen)
                            data[snpPos][mrm.BarcodeIdx].Add(mrm.RandomBcIdx, GetNtAtPos(snpPos, m.Position, m.Mismatches));
                    }
            }
        }

        private char GetNtAtPos(int snpPos, int hitStartPos, string mismatches)
        {
            foreach (string snp in mismatches.Split(','))
            {
                int p = snp.IndexOf(':');
                if (p == -1)
                {
                    Console.WriteLine("Strange mismatches: " + mismatches + " at hitStartPos=" + hitStartPos);
                    continue;
                }
                int relPos = int.Parse(snp.Substring(0, p));
                if (hitStartPos + relPos == snpPos)
                    return snp[p + 3];
            }
            return '-';

        }

        public void Verify(string fileNameBase)
        {
            int nMaxUsedRndTags = 50;
            string file = fileNameBase + "_snp_rndTag_verification.tab";           
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Only showing problematic positions. (The genome, nonSNP, nt is represented by 0, since seq data is not kept.) Summary at end.");
            writer.WriteLine("ChrPos\tBarcode\tRndTagIndices...");
            writer.WriteLine("\tStd/SNPNt\tCounts...");
            int nWithSnpAndCorrectRndTags = 0, nTotal = 0, nWithSnp = 0;
            foreach (int pos in data.Keys)
            {
                for (int bcIdx = 0; bcIdx < data[pos].Length; bcIdx++)
                {
                    SnpRndTagData sData = data[pos][bcIdx];
                    nTotal++;
                    int nAltNts;
                    int rndTagCounts;
                    bool correct = sData.VerifyAndCalcNtAndRndTagCounts(out nAltNts, out rndTagCounts);
                    if (nAltNts < 2 || rndTagCounts > nMaxUsedRndTags) continue;
                    nWithSnp++;
                    if (correct) nWithSnpAndCorrectRndTags++;
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < sData.countsByNtAndRndTagIdx.GetLength(1); i++)
                            sb.Append("\t" + i);
                        writer.WriteLine(pos + "\t" + props.Barcodes.Seqs[bcIdx] + sb.ToString());
                        for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                        {
                            writer.Write("\t" + "0ACGT"[ntIdx]);
                            for (int i = 0; i < sData.countsByNtAndRndTagIdx.GetLength(1); i++)
                                writer.Write("\t" + sData.countsByNtAndRndTagIdx[ntIdx, i].ToString());
                        }
                    }
                }
            }
            writer.WriteLine("\n\n" + nWithSnpAndCorrectRndTags + " out of totally " + nWithSnp +
                             " chrPosition-barcode combinations containing alternative Nts had the same Nt within every rndTag.");
            writer.WriteLine("Max " + nMaxUsedRndTags + " busy rndTags were allowed for analysis.");
            writer.WriteLine("Totally " + nWithSnp + " chrPosition-barcode combinations were considered.");
            writer.Close();
        }
    }
}
