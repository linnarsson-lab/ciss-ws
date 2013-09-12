using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using System.Text.RegularExpressions;

namespace Linnarsson.Strt
{
    public class ReadFileEmptyException : Exception
    { }

    public class ReadStatus
    {
        public readonly static int VALID = 0;
        public readonly static int LENGTH_ERROR = 1;
        public readonly static int SEQ_QUALITY_ERROR = 2;
        public readonly static int COMPLEXITY_ERROR = 3;
        public readonly static int SAL1T25_IN_READ = 4;
        public readonly static int N_IN_RANDOM_TAG = 5;
        public readonly static int LOW_QUALITY_IN_RANDOM_TAG = 6;
        public readonly static int NEGATIVE_BARCODE_ERROR = 7;
        public readonly static int NO_BC_CGACT25 = 8;
        public readonly static int NO_BC_NNNA25 = 9;
        public readonly static int NO_BC_SAL1 = 10;
        public readonly static int NO_BC_SOLEXA_ADP2 = 11;
        public readonly static int NO_BC_INTERNAL_T20 = 12;
        public readonly static int BARCODE_ERROR = 13;
        public readonly static int Length = 14;
        public readonly static string[] categories = new string[] { "VALID", "TOO_LONG_pA_pN_TAIL", "SEQ_QUALITY_ERROR",
                                                                   "COMPLEXITY_ERROR",  "SAL1-T25_IN_READ", "N_IN_RANDOM_TAG",
                                                                   "LOW_QUALITY_IN_RANDOM_TAG","NEGATIVE_BARCODE_ERROR",
                                                                   "NO_BARCODE-CGACT25", "NO_BARCODE-NNNA25", "NO_BARCODE-SAL1-T25",
                                                                   "NO_BARCODE-SOLEXA-ADP2_CONTAINING", "NO_BARCODE-INTERNAL-T20",
                                                                   "NO_VALID_BARCODE-UNCHARACTERIZED" };
        public static int Parse(string category) 
        {
            return Array.FindIndex(categories, (c) => category.Equals(c, StringComparison.CurrentCultureIgnoreCase));
        }

    }

    public enum LimitTest { UseThisRead, SkipThisRead, Break };

    public class ReadCounter
    {
        private int totalSum = 0;
        private int partialSum = 0;
        private int[] totalCounts = new int[ReadStatus.Length];
        private int[] partialCounts = new int[ReadStatus.Length];
        private List<string> readFiles = new List<string>();
        private List<int> meanReadLens = new List<int>();
        private List<int> validBarcodeReads = new List<int>();
        private List<int> totalBarcodeReads = new List<int>();
        private Dictionary<string, int> barcodeToReadsIdx = new Dictionary<string, int>();

        private ReadLimitType readLimitType = ReadLimitType.None; // By default all reads in fq files will be analyzed
        private int readLimit = 0; // Parameter to the read limiter

        public ReadCounter()
        { }
        public ReadCounter(Props props)
        {
            validBarcodeReads = new List<int>(new int[props.Barcodes.Count]);
            totalBarcodeReads = new List<int>(new int[props.Barcodes.Count]);
            readLimitType = props.ExtractionReadLimitType;
            readLimit = props.ExtractionReadLimit;
        }

        /// <summary>
        /// Average read length over all files
        /// </summary>
        public int AverageReadLen { get { return (int)Math.Floor(meanReadLens.Sum() / (double)meanReadLens.Count); } }
        /// <summary>
        /// Total valid read counts over all files, per barcode
        /// </summary>
        public List<int> ValidReadsByBarcode { get { return validBarcodeReads; } }
        public int ValidReads(int[] selectedBcIndexes)
        {
            int sum = 0;
            foreach (int idx in selectedBcIndexes)
                sum += validBarcodeReads[idx];
            return sum;
        }
        /// <summary>
        /// Total valid and invalid read counts over all files, per barcode
        /// </summary>
        public List<int> TotalReadsByBarcode { get { return totalBarcodeReads; } }
        public int TotalReads(int[] selectedBcIndexes)
        {
            int sum = 0;
            foreach (int idx in selectedBcIndexes)
                sum += totalBarcodeReads[idx];
            return sum;
        }
        public int GrandTotal { get { return totalSum; } }
        public int PartialTotal { get { return partialSum; } }
        public int GrandRejected { get { return totalSum - totalCounts[ReadStatus.VALID]; } }
        public int PartialRejected { get { return partialSum - partialCounts[ReadStatus.VALID]; } }
        public int GrandCount(int readStatus) { return totalCounts[readStatus]; }
        public int PartialCount(int readStatus) { return partialCounts[readStatus]; }

        public double GrandFraction(int readStatus)
        {
            return (totalSum > 0) ? (totalCounts[readStatus] / (double)totalSum) : 0.0;
        }

        /// <summary>
        /// Test if a read with readStatus and barcode can be added, or limit is reached
        /// </summary>
        /// <param name="readStatus">status of next read</param>
        /// <param name="bcIdx">barcode of next read</param>
        /// <returns>info wether to use read, skip read, or finish analysis</returns>
        public LimitTest IsLimitReached(int readStatus, int bcIdx)
        {
            switch (readLimitType)
            {
                case ReadLimitType.None:
                    return LimitTest.UseThisRead;
                case ReadLimitType.TotalReads:
                    return GrandTotal >= readLimit ? LimitTest.Break : LimitTest.UseThisRead;
                case ReadLimitType.TotalValidReads:
                    return GrandCount(ReadStatus.VALID) >= readLimit ? LimitTest.Break : LimitTest.UseThisRead;
                case ReadLimitType.TotalValidReadsPerBarcode:
                    if (bcIdx < 0 || ValidReadsByBarcode[bcIdx] < readLimit) return LimitTest.UseThisRead;
                    return (ValidReadsByBarcode.All(v => v >= readLimit)) ? LimitTest.Break : LimitTest.SkipThisRead;
                case ReadLimitType.TotalReadsPerBarcode:
                    if (bcIdx < 0 || TotalReadsByBarcode[bcIdx] < readLimit) return LimitTest.UseThisRead;
                    return (TotalReadsByBarcode.All(v => v >= readLimit)) ? LimitTest.Break : LimitTest.SkipThisRead;
                default:
                    return LimitTest.UseThisRead;
            }
        }

        /// <summary>
        /// Add a read (during extraction) to the statistics.
        /// </summary>
        /// <param name="readStatus">Status of the read</param>
        /// <param name="bcIdx">Barcode of the read</param>
        public void AddARead(int readStatus, int bcIdx)
        {
            AddReads(readStatus, 1);
            if (bcIdx >= 0)
            {
                totalBarcodeReads[bcIdx] += 1;
                if (readStatus == ReadStatus.VALID)
                    validBarcodeReads[bcIdx] += 1;
            }
        }

        /// <summary>
        /// A a number of reads (from statistics file) to the global statistics
        /// </summary>
        /// <param name="readStatus"></param>
        /// <param name="count"></param>
        public void AddReads(int readStatus, int count)
        {
            totalSum += count;
            partialSum += count;
            totalCounts[readStatus] += count;
            partialCounts[readStatus] += count;
        }

        public void AddReadFile(string path, int averageReadLen)
        {
            readFiles.Add(path);
            meanReadLens.Add(averageReadLen);
        }
        public List<string> GetReadFiles()
        {
            return readFiles;
        }

        public void ResetPartials()
        {
            partialSum = 0;
            partialCounts = new int[ReadStatus.Length];
        }

        public string TotalsToTabString(bool showRndTagCategories)
        {
            string s = "#Files included in this read summary, with average read lengths:\n";
            for (int i = 0; i < readFiles.Count; i++)
            {
                s += string.Format("READFILE\t{0}\t{1}\n", readFiles[i], meanReadLens[i]);
            }
            s += "#Category\tCount\tPercent\n" +
                 "TOTAL_PASSED_ILLUMINA_FILTER\t" + GrandTotal + "\t100%\n";
            for (int statusCat = 0; statusCat < ReadStatus.Length; statusCat++)
            {
                if (showRndTagCategories || !ReadStatus.categories[statusCat].Contains("RANDOM"))
                    s += string.Format("{0}\t{1}\t{2:0.#%}\n", ReadStatus.categories[statusCat], GrandCount(statusCat), GrandFraction(statusCat));
            }
            return s;
        }

        public void AddExtractionSummaries(List<string> extractionSummaryPaths)
        {
            foreach (string summaryPath in extractionSummaryPaths)
                AddExtractionSummary(summaryPath);
        }
        public void AddExtractionSummary(string extractionSummaryPath)
        {
            try
            {
                using (StreamReader extrFile = new StreamReader(extractionSummaryPath))
                {
                    string line = extrFile.ReadLine();
                    while (line != null)
                    {
                        if (!line.StartsWith("#"))
                        {
                            try
                            {
                                string[] fields = line.Split('\t');
                                if (line.StartsWith("READFILE"))
                                {
                                    int meanReadLen = int.Parse(fields[2]);
                                    if (meanReadLen == 0)
                                        throw new ReadFileEmptyException();
                                    readFiles.Add(fields[1]);
                                    meanReadLens.Add(meanReadLen);
                                }
                                else if (line.StartsWith("BARCODEREADS"))
                                {
                                    string bc = fields[1];
                                    int idx;
                                    if (!barcodeToReadsIdx.TryGetValue(bc, out idx))
                                    {
                                        idx = barcodeToReadsIdx.Count;
                                        barcodeToReadsIdx[bc] = idx;
                                        validBarcodeReads.Add(0);
                                        totalBarcodeReads.Add(0);
                                    }
                                    validBarcodeReads[idx] += int.Parse(fields[2]);
                                    if (fields.Length >= 4)
                                        totalBarcodeReads[idx] += int.Parse(fields[3]);
                                }
                                else
                                {
                                    int statusCategory = ReadStatus.Parse(fields[0]);
                                    if (statusCategory >= 0)
                                        AddReads(statusCategory, int.Parse(fields[1]));
                                }
                            }
                            catch (ReadFileEmptyException)
                            {
                                readFiles.Add(extractionSummaryPath + " - EMPTY: No valid reads in this file.");
                                break;
                            }
                            catch (Exception)
                            { }
                        }
                        line = extrFile.ReadLine();
                    }
                }
            }
            catch (FileNotFoundException)
            {
                readFiles.Add(extractionSummaryPath + " - MISSING: Read statistics omitted for this data.");
            }
        }
    }

    public class ReadExtractor
    {
        private readonly static int maxExtraTSNts = 6; // Limit # of extra (G) Nts (in addition to min#==3) to remove from template switching
        private Barcodes barcodes;
        private int insertStartPos;
        private int minTotalReadLength;
        private int UMIPos;
        private int UMILen;
        private int minInsertNonAs;
        private int minQualityInUMI;
        private string[] diNtPatterns;
        private string[] trailingPrimerSeqs;
        private int minPrimerSeqLen = 5;

        public ReadExtractor(Props props)
        {
            barcodes = props.Barcodes;
            insertStartPos = barcodes.InsertOrGGGPos;
            minTotalReadLength = barcodes.InsertOrGGGPos + props.MinExtractionInsertLength;
            UMIPos = barcodes.UMIPos;
            UMILen = barcodes.UMILen;
            minInsertNonAs = props.MinExtractionInsertNonAs;
            minQualityInUMI = props.MinPhredScoreInRandomTag;
            trailingPrimerSeqs = props.RemoveTrailingReadPrimerSeqs.Split(',').Where(s => s.Length >= minPrimerSeqLen).ToArray();
        }

        /// <summary>
        /// Extracts the barcode and random barcode from rec.Sequence and puts in rec.Header.
        /// </summary>
        /// <param name="rec"></param>
        /// <param name="bcIdx">Set to -1 if no valid barcode could be detected</param>
        /// <returns>A ReadStatus that indicates if the read was valid</returns>
        public int Extract(ref FastQRecord rec, out int bcIdx)
        {
            rec.TrimBBB();
            bcIdx = -1;
            string rSeq = rec.Sequence;
            if (rSeq.Length <= insertStartPos)
                return ReadStatus.SEQ_QUALITY_ERROR;
            int insertStart;
            if (!barcodes.VerifyBarcodeAndTS(rSeq, maxExtraTSNts, out bcIdx, out insertStart))
            {
                bcIdx = -1;
                return AnalyzeNonBarcodeRead(rSeq);
            }
            int insertLength = TrimTrailingNOrPrimerAndCheckAs(rSeq);
            if (insertLength < minTotalReadLength)
                return ReadStatus.LENGTH_ERROR;
            string headerUMISection = "";
            if (UMILen > 0)
            {
                for (int i = UMIPos; i < UMIPos + UMILen; i++)
                {
                    if (rec.Qualities[i] < minQualityInUMI)
                        return ReadStatus.LOW_QUALITY_IN_RANDOM_TAG;
                }
                headerUMISection = string.Format("{0}.", rSeq.Substring(UMIPos, UMILen));
                if (headerUMISection.Contains('N'))
                    return ReadStatus.N_IN_RANDOM_TAG;
            }

            insertLength -= insertStart;
            rec.Header = string.Format("{0}_{1}{2}", rec.Header, headerUMISection, barcodes.Seqs[bcIdx]);
            rec.Trim(insertStart, insertLength);
            int status = TestComplexity(rSeq, insertStart, insertLength);
            if (status != ReadStatus.VALID) return status;
            return TestDinucleotideRepeats(rec.Sequence);
        }

        /// <summary>
        /// Removes trailing N:s.
        /// If removing of trailing A:s leaves a sequence shorter than minReadLength, returns the truncated length,
        /// otherwise includes the trailing A:s in the returned length.
        /// Also, if the read ends with the (start) sequence of a pre-defined primer, that sequence is removed
        /// </summary>
        /// <param name="rSeq"></param>
        /// <returns>Length of remaining sequence</returns>
        private int TrimTrailingNOrPrimerAndCheckAs(string rSeq)
        {
            int insertLength = rSeq.Length;
            while (insertLength >= minTotalReadLength && rSeq[insertLength - 1] == 'N')
                insertLength--;
            int nonATailLen = insertLength;
            while (nonATailLen >= minTotalReadLength && rSeq[nonATailLen - 1] == 'A')
                nonATailLen--;
            if (nonATailLen < minTotalReadLength) return nonATailLen;
            foreach (string primerSeq in trailingPrimerSeqs)
            {
                int i = rSeq.LastIndexOf(primerSeq.Substring(0, minPrimerSeqLen));
                if (i >= minTotalReadLength)
                {
                    int restLen = rSeq.Length - i - minPrimerSeqLen;
                    if ((primerSeq.Length - minPrimerSeqLen) >= restLen 
                        && rSeq.Substring(i + minPrimerSeqLen).Equals(primerSeq.Substring(minPrimerSeqLen, restLen)))
                        return i;
                }
            }
            return insertLength;
        }

        private int TestComplexity(string rSeq, int insertStart, int insertLength)
        {
            int nNonAs = 0;
            for (int rp = insertStart; rp < insertStart + insertLength; rp++)
                if (!"AaNn".Contains(rSeq[rp]))
                {
                    if (++nNonAs >= minInsertNonAs)
                        break;
                }
            if (nNonAs < minInsertNonAs)
                return ReadStatus.COMPLEXITY_ERROR;
            if (Regex.Match(rSeq, "GTCGACTTTTTTTTTTTTTTTTTTTTTTTTT").Success)
                return ReadStatus.SAL1T25_IN_READ;
            return ReadStatus.VALID;
        }

        private int TestDinucleotideRepeats(string insertSeq)
        {
            if (diNtPatterns == null)
                SetupDiNtPatterns(insertSeq);
            foreach (string pattern in diNtPatterns)
            {
                if (insertSeq.Contains(pattern))
                    return ReadStatus.COMPLEXITY_ERROR;
            }
            return ReadStatus.VALID;
        }

        private static string[] diNtPairs = new string[] { "AC", "AG", "AT", "CG", "CT", "GT" };
        private void SetupDiNtPatterns(string insertSeq)
        {
            diNtPatterns = new string[diNtPairs.Length];
            for (int i = 0; i < diNtPairs.Length; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int np = 0; np < (insertSeq.Length - 6) / 2; np++)
                    sb.Append(diNtPairs[i]);
                diNtPatterns[i] = sb.ToString();
            }
        }

        private int AnalyzeNonBarcodeRead(string seq)
        {
            if (Regex.Match(seq, "GTCGACTTTTTTTTTTTTTTTTTTTTTTTTT").Success) return ReadStatus.NO_BC_SAL1;
            if (seq.StartsWith("CGACTTTTTTTTTTTTTTTTTTTTTTTTT")) return ReadStatus.NO_BC_CGACT25;
            if (Regex.Match(seq, "^...AAAAAAAAAAAAAAAAAAAAAAAAA").Success) return ReadStatus.NO_BC_NNNA25;
            if (seq.Contains("TCGGAAGAGCTCGTATG")) return ReadStatus.NO_BC_SOLEXA_ADP2;
            if (seq.Contains("TTTTTTTTTTTTTTTTTTTT")) return ReadStatus.NO_BC_INTERNAL_T20;
            return ReadStatus.BARCODE_ERROR;
        }

    }

}
