using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Holds counts for one barcode of each Nt in every rnd label for one single known SNP position, of reads mapped at a specific position & strand 
    /// </summary>
    public class SnpRndTagVerChrPosBcData
    {
        public static readonly int MinCountForValidNtWithinRndTag = 5;
        public static readonly int MinCountFactorToLessCommonNtWithinRndTag = 10;

        /// <summary>
        /// SNPNt in first index (-,A,C,G,T) and counts in each rndTag in second index 
        /// </summary>
        public short[,] countsByNtAndRndTagIdx;

        public SnpRndTagVerChrPosBcData(int nRndTags)
        {
            countsByNtAndRndTagIdx = new short[5, nRndTags];
        }

        public static int GetNtIdx(char nt)
        {
            int idx = "-ACGT".IndexOf(char.ToUpper(nt));
            if (idx == -1) idx = 0;
            return idx;
        }

        /// <summary>
        /// Adds a count to the nt of the mismatch
        /// </summary>
        /// <param name="rndTagIdx"></param>
        /// <param name="mm">If null, adds to reference nt count</param>
        public void Add(int rndTagIdx, Mismatch mm)
        {
            int ntIdx = 0;
            if (mm != null)
                ntIdx = GetNtIdx(mm.ntInChrDir);
            countsByNtAndRndTagIdx[ntIdx, rndTagIdx]++;
        }

        /// <summary>
        /// Checks that within a random label (representing one original molecule) the same Nt is used at the SNP position in all reads.
        /// </summary>
        /// <param name="sameNtInEachRndTag">True indicates correctness</param>
        /// <param name="nUsedRndTags">Number of random labels containing data (i.e. number of original molecules)</param>
        /// <returns>Total number of reads</returns>
        public int HasAllReadsInEachRndTagTheSameNt(out bool sameNtInEachRndTag, out int nUsedRndTags)
        {
            int nTotalReads = 0;
            nUsedRndTags = 0;
            sameNtInEachRndTag = true;
            for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
            {
                int nAltNtsInRndTag = 0;
                for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                {
                    nTotalReads += countsByNtAndRndTagIdx[ntIdx, rndTagIdx];
                    if (countsByNtAndRndTagIdx[ntIdx, rndTagIdx] > 0)
                        nAltNtsInRndTag++;
                }
                if (nAltNtsInRndTag > 1) sameNtInEachRndTag = false;
                if (nAltNtsInRndTag > 0) nUsedRndTags++;
            }
            return nTotalReads;
        }

        /// <summary>
        /// Returns the Nts that appear correct alternative bases at this position in the barcode.
        /// They consist of the set of the clearly most dominant Nt in each random label.
        /// If more than one is returned, the barcode can be considered heterozygot at this position.
        /// </summary>
        /// <returns></returns>
        public HashSet<char> GetValidNtsInBarcode(char refNt)
        {
            HashSet<char> validNts = new HashSet<char>();
            int[] sumsPerNt = new int[5];
            for (int rndTagIdx = 0; rndTagIdx < countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
            {
                int maxCount = 0; char maxNt = '-';
                for (int ntIdx = 0; ntIdx < 5; ntIdx++)
                {
                    int n = countsByNtAndRndTagIdx[ntIdx, rndTagIdx];
                    if (n > maxCount * MinCountFactorToLessCommonNtWithinRndTag)
                    {
                        maxCount = n;
                        maxNt = (ntIdx == 0)? refNt : "-ACGT"[ntIdx];
                    }
                }
                if (maxNt != '-' && maxCount >= MinCountForValidNtWithinRndTag)
                    validNts.Add(maxNt);
            }
            return validNts;
        }
    }

    /// <summary>
    /// Holds data across all barcodes for one particular read position-strand mapping and its (maybe selected out of several) snp position
    /// </summary>
    public class SnpRndTagVerChrPosData
    {
        public int snpPos;
        /// <summary>
        /// Gives the nt which is on the reference chromosome
        /// </summary>
        public char refNt = '0';

        public SnpRndTagVerChrPosBcData[] dataByBc;
        public SnpRndTagVerChrPosData(int snpPos, Barcodes barcodes)
        {
            dataByBc = new SnpRndTagVerChrPosBcData[barcodes.Count];
            this.snpPos = snpPos;
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                dataByBc[bcIdx] = new SnpRndTagVerChrPosBcData(barcodes.RandomBarcodeCount);
        }
        public void Add(int bc, int rndTagIdx, Mismatch m)
        {
            if (refNt == '0' && m != null)
                refNt = m.refNtInChrDir;
            dataByBc[bc].Add(rndTagIdx, m);
        }

        /// <summary>
        /// Returns the valid alternative Nts at this position across all barcodes
        /// </summary>
        /// <param name="heterozygotBarcodes">Indexes of barcodes that are themselves heterozygot</param>
        /// <returns>List of Nts that occur in this position</returns>
        public List<char> GetValidNtsAtPos(out List<int> heterozygotBarcodes)
        {
            heterozygotBarcodes = new List<int>();
            HashSet<char> allValidNts = new HashSet<char>();
            for (int bcIdx = 0; bcIdx < dataByBc.Length; bcIdx++)
            {
                HashSet<char> validNtsInBc = dataByBc[bcIdx].GetValidNtsInBarcode(refNt);
                if (validNtsInBc.Count > 1)
                    heterozygotBarcodes.Add(bcIdx);
                allValidNts.UnionWith(validNtsInBc);
            }
            return allValidNts.ToList();
        }
    }

    /// <summary>
    /// Data for each chromosome
    /// </summary>
    public class SnpRndTagVerChrData
    {
        private Barcodes barcodes;
        /// <summary>
        /// SNPPos and count data indexed by position-strand of the reads on the single verification chromosome to analyze
        /// </summary>
        public Dictionary<int, SnpRndTagVerChrPosData> dataByPosStrand = new Dictionary<int, SnpRndTagVerChrPosData>();
        /// <summary>
        /// Holds all known SNP positions on the verification chromosome, read from GVF file
        /// </summary>
        public HashSet<int> snpPosOnChr = new HashSet<int>();

        public SnpRndTagVerChrData(Barcodes barcodes)
        {
            this.barcodes = barcodes;
        }

        public void AddSnpPos(int pos)
        {
            snpPosOnChr.Add(pos);
        }

        public void Add(MultiReadMappings mrm)
        {
            int strandBit = (mrm[0].Strand == '+') ? 0 : 1;
            int posStrand = (mrm[0].Position << 1) | strandBit;
            SnpRndTagVerChrPosData posDatas = null;
            if (!dataByPosStrand.TryGetValue(posStrand, out posDatas))
            {
                foreach (int snpPos in snpPosOnChr)
                {
                    if (mrm[0].Contains(snpPos, SnpRndTagVerifier.snpMargin))
                    {
                        posDatas = new SnpRndTagVerChrPosData(snpPos, barcodes);
                        dataByPosStrand[posStrand] = posDatas;
                        break; // We found one SNP, and only one SNP is analyzed per position-strand
                    }
                }
                if (posDatas == null) return;
            }
            if (mrm[0].GetQuality(posDatas.snpPos) >= SnpRndTagVerifier.minMismatchPhredAsciiVal)
            {
                Mismatch m = GetMismatchAtPos(posDatas.snpPos, mrm[0]);
                posDatas.Add(mrm.BarcodeIdx, mrm.RandomBcIdx, m);
            }

        }

        /// <summary>
        /// Parses the mismatch field in map file.
        /// </summary>
        /// <param name="snpPosOnChr"></param>
        /// <param name="mrm"></param>
        /// <returns>Mismatch at snpPosOnChr, or null if there was no mismatch a snpPosOnChr</returns>
        private Mismatch GetMismatchAtPos(int snpPosOnChr, MultiReadMapping mrm)
        {
            foreach (Mismatch mm in mrm.IterMismatches(SnpRndTagVerifier.minMismatchPhredAsciiVal))
            {
                if (mm.posInChr == snpPosOnChr)
                    return mm;
            }
            return null;
        }
    }

    /// <summary>
    /// Verifies that the same SNP occurs in all reads stemming from the same molecule (i.e. having equal chr, pos, bc, rnd label)
    /// Reads all known SNP positions from a GVF file that has to be in genome folder of species.
    /// </summary>
    public class SnpRndTagVerifier
    {
        private Dictionary<string, SnpRndTagVerChrData> dataByChr = new Dictionary<string, SnpRndTagVerChrData>();
        private Barcodes barcodes;
        public static int minMismatchPhredAsciiVal = 15 + 33; // Minimum quality of the SNP Nt for useful reads
        public static int minReads = 10; // Minumum number of reads in a position-barcode-strand to be worth analyzing
        public static int nMaxUsedRndTags = 50; // Errors may occur at rnd label saturation if two different molecules end up in the same label
        public static int snpMargin = 4;

        /// <summary>
        /// </summary>
        /// <param name="props"></param>
        /// <param name="genome">Used to find the GVF file that defines expressed SNP positions</param>
        public SnpRndTagVerifier(Props props, StrtGenome genome)
        {
            int n = 0;
            barcodes = props.Barcodes;
            string GVFPath = PathHandler.GetGVFFile(genome);
            if (GVFPath == "")
                Console.WriteLine("Can not find a GVF file for {0}. Skipping SNP verification.", genome.Build);
            else
            {
                foreach (GVFRecord rec in GFF3CompatibleFile.Iterate(GVFPath, new GVFRecord()))
                {
                    if (rec.type == "SNV" && rec.AnyTranscriptEffect())
                    {
                        n++;
                        SnpRndTagVerChrData chrData;
                        if (!dataByChr.TryGetValue(rec.seqid, out chrData))
                        {
                            chrData = new SnpRndTagVerChrData(barcodes);
                            dataByChr[rec.seqid] = chrData;
                        }
                        chrData.AddSnpPos(rec.start - 1);
                    }
                }
                Console.WriteLine("Registered {0} SNPs to verify read from GVF file.", n);
            }
        }

        /// <summary>
        /// Adds the read to verification data if it spans any of the pre-defined SNPs.
        /// Only reads that have unique mappings in the genome are considered.
        /// </summary>
        /// <param name="mrm"></param>
        public void Add(MultiReadMappings mrm)
        {
            if (!mrm.HasAltMappings && dataByChr.ContainsKey(mrm[0].Chr))
                dataByChr[mrm[0].Chr].Add(mrm);
        }

        public void Verify(string fileNameBase)
        {
            StreamWriter heterozygotSNPWriter = new StreamWriter(fileNameBase + "_RndTag_verification_heterozygot_positions.tab");
            InitOutfile(heterozygotSNPWriter, "Showing data for all heterozygot positions.");
            StreamWriter problemWriter = new StreamWriter(fileNameBase + "_RndTag_verification_problem_positions.tab");
            InitOutfile(problemWriter, "Showing problematic positions. Summary at end.");
            int nCorrectHetero = 0, nCorrect = 0, nTotal = 0, nSkipped = 0, nWrongHetero = 0;
            foreach (string chr in dataByChr.Keys)
            {
                SnpRndTagVerChrData chrData = dataByChr[chr];
                foreach (KeyValuePair<int, SnpRndTagVerChrPosData> p in chrData.dataByPosStrand)
                {
                    int posStrand = p.Key;
                    SnpRndTagVerChrPosData posDatas = p.Value;
                    List<int> heterzygotBcIndexes;
                    string validNtsAtPos = new string(posDatas.GetValidNtsAtPos(out heterzygotBcIndexes).ToArray());
                    bool heteroPosition = validNtsAtPos.Length > 1;
                    for (int bcIdx = 0; bcIdx < posDatas.dataByBc.Length; bcIdx++)
                    {
                        SnpRndTagVerChrPosBcData sData = posDatas.dataByBc[bcIdx];
                        bool sameNtInEachRndTag;
                        int nUsedRndTags;
                        int nTotalReads = sData.HasAllReadsInEachRndTagTheSameNt(out sameNtInEachRndTag, out nUsedRndTags);
                        if (nTotalReads < minReads || nUsedRndTags > nMaxUsedRndTags)
                        { // Do not analyze too crowded barcodes
                            nSkipped++;
                            continue;
                        }
                        nTotal++;
                        bool heteroInBc = heterzygotBcIndexes.Contains(bcIdx);
                        if (heteroPosition)
                            WriteOnePosData(heterozygotSNPWriter, chr, posStrand, posDatas, bcIdx, sameNtInEachRndTag, validNtsAtPos, heteroInBc);
                        if (sameNtInEachRndTag)
                        {
                            if (heteroPosition) nCorrectHetero++;
                            nCorrect++;
                        }
                        else
                        {
                            if (heteroPosition) nWrongHetero++;
                            WriteOnePosData(problemWriter, chr, posStrand, posDatas, bcIdx, sameNtInEachRndTag, validNtsAtPos, heteroInBc);
                        }
                    }
                }
            }
            FinishOutfile(problemWriter, nCorrectHetero, nCorrect, nTotal, nSkipped, nWrongHetero);
            FinishOutfile(heterozygotSNPWriter, nCorrectHetero, nCorrect, nTotal, nSkipped, nWrongHetero);
        }

        private void FinishOutfile(StreamWriter writer, 
                                   int nCorrectHetero, int nCorrect, int nTotal, int nSkipped, int nWrongHetero)
        {
            writer.WriteLine("\n\nKnown SNP position from GVF file and reads with PhredScore >=" + minMismatchPhredAsciiVal + " on the read Nt were considered.");
            writer.WriteLine("Skipped " + nSkipped + " pos-strand-barcodes with >" + nMaxUsedRndTags + " used rnd labels or <" + minReads + " reads.");
            writer.WriteLine(nCorrect + " / " + nTotal + " positions with mapped alternative Nts had correctly the same Nt within every rnd label.");
            writer.WriteLine(nCorrectHetero + " of these were also found to be heterozygous (altNt with >= " + SnpRndTagVerChrPosBcData.MinCountForValidNtWithinRndTag +
                             " reads.)");
            writer.WriteLine(nWrongHetero + " of the " + (nTotal - nCorrect) + " problematic read cases appear to be true heterozygous.");
            writer.Close();
        }

        private void InitOutfile(StreamWriter writer, string header)
        {
            writer.WriteLine(header);
            writer.Write("ReadPos\tChr\tStrand\tSNPPos\tAltNts\tBarcode\tProblem\tBcIsHeZ\tTotal\tNt");
            for (int i = 0; i < barcodes.RandomBarcodeCount; i++)
                writer.Write("\t#InTag" + i);
            writer.WriteLine();
        }

        private void WriteOnePosData(StreamWriter writer, string chr, int posStrand, SnpRndTagVerChrPosData posDatas, int bcIdx,
                                     bool correct, string validNtsAtPos, bool heteroInBc)
        {
            int pos = posStrand >> 1;
            char strand = ((posStrand & 1) == 0) ? '+' : '-';
            string wStart = pos + "\t" + chr + "\t" + strand + "\t" + posDatas.snpPos + "\t" + validNtsAtPos + "\t" + 
                barcodes.Seqs[bcIdx] + "\t" + (correct? "\t":"!!!\t") + (heteroInBc? "X\t" : "\t");
            for (int ntIdx = 0; ntIdx < 5; ntIdx++)
            {
                char nt = "0ACGT"[ntIdx];
                if (ntIdx > 0 && posDatas.refNt == nt)
                    continue;
                StringBuilder sbNt = new StringBuilder();
                sbNt.Append((ntIdx == 0)? ("\t" + posDatas.refNt + "(ref)") : ("\t" + nt));
                int totCountInNt = 0;
                SnpRndTagVerChrPosBcData sData = posDatas.dataByBc[bcIdx];
                for (int rndTagIdx = 0; rndTagIdx < sData.countsByNtAndRndTagIdx.GetLength(1); rndTagIdx++)
                {
                    int c = sData.countsByNtAndRndTagIdx[ntIdx, rndTagIdx];
                    totCountInNt += c;
                    sbNt.Append("\t" + c.ToString());
                }
                if (totCountInNt > 0 || ntIdx == 0)
                {
                    writer.Write(wStart + totCountInNt);
                    writer.WriteLine(sbNt.ToString());
                    wStart = "\t\t\t\t\t\t\t\t";
                }
            }
        }
    }
}
