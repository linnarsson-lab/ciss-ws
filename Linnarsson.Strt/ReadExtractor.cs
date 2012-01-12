using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    public class ReadStatus
    {
        public readonly static int VALID = 0;
        public readonly static int BARCODE_ERROR = 1;
        public readonly static int LENGTH_ERROR = 2;
        public readonly static int COMPLEXITY_ERROR = 3;
        public readonly static int N_IN_RANDOM_TAG = 4;
        public readonly static int NEGATIVE_BARCODE_ERROR = 5;
        public readonly static int LOW_QUALITY_IN_RANDOM_TAG = 6;
        public readonly static int Length = 7;
        public readonly static string[] categories = new string[] { "VALID", "BARCODE_ERROR", "LENGTH_ERROR", 
                                                                   "COMPLEXITY_ERROR", "N_IN_RANDOM_TAG", "NEGATIVE_BARCODE_ERROR",
                                                                   "LOW_QUALITY_IN_RANDOM_TAG" };
        public static int Parse(string category) { return Array.IndexOf(categories, category.ToUpper()); }
    }

    public class ReadCounter
    {
        private int totalSum = 0;
        private int partialSum = 0;
        private int[] totalCounts = new int[ReadStatus.Length];
        private int[] partialCounts = new int[ReadStatus.Length];
        private List<string> readFiles = new List<string>();
        private List<int> meanReadLens = new List<int>();
        private List<int> barcodeReads = new List<int>();
        private Dictionary<string, int> barcodeToReadsIdx = new Dictionary<string, int>();

        /// <summary>
        /// Average read length over all files
        /// </summary>
        public int AverageReadLen { get { return (int)Math.Floor(meanReadLens.Sum() / (double)meanReadLens.Count); } }
        /// <summary>
        /// Total read counts over all files, per barcode
        /// </summary>
        public int[] TotalBarcodeReads { get { return barcodeReads.ToArray(); } }

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
        public void Add(int readStatus)
        {
            Add(readStatus, 1);
        }
        public void Add(int readStatus, int count)
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
        public string PartialsToString()
        {
            string stats = PartialTotal + " reads: " +
                           PartialCount(ReadStatus.VALID) + " accepted, " + PartialRejected + " rejected. (" +
                           PartialCount(ReadStatus.BARCODE_ERROR) + " wrong barcode, " + PartialCount(ReadStatus.LENGTH_ERROR) + " too short, " +
                           PartialCount(ReadStatus.COMPLEXITY_ERROR) + " polyA-like";
            if (PartialCount(ReadStatus.N_IN_RANDOM_TAG) > 0)
                stats += ", " + PartialCount(ReadStatus.N_IN_RANDOM_TAG) + " unparseable random barcodes";
            stats += ").";
            return stats;
        }

        public string TotalsToTabString()
        {
            string s = "#Files included in this read summary, with average read lengths:\n";
            for (int i = 0; i < readFiles.Count; i++)
            {
                s += string.Format("READFILE\t{0}\t{1}\n", readFiles[i], meanReadLens[i]);
            }
            s += "#Category\tCount\tPercent\n" +
                 "TOTAL_PASSED_ILLUMINA_FILTER\t" + GrandTotal + "\t100%\n";
            for (int statusCat = 0; statusCat < ReadStatus.Length; statusCat++)
                s += string.Format("{0}\t{1}\t{2:0.#%}\n", ReadStatus.categories[statusCat], GrandCount(statusCat), GrandFraction(statusCat));
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
                StreamReader extrFile = extractionSummaryPath.OpenRead();
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
                                readFiles.Add(fields[1]);
                                meanReadLens.Add(int.Parse(fields[2]));
                            }
                            else if (line.StartsWith("BARCODEREADS"))
                            {
                                string bc = fields[1];
                                int idx;
                                if (!barcodeToReadsIdx.TryGetValue(bc, out idx))
                                {
                                    idx = barcodeToReadsIdx.Count;
                                    barcodeToReadsIdx[bc] = idx;
                                    barcodeReads.Add(0);
                                }
                                barcodeReads[idx] += int.Parse(fields[2]);
                            }
                            else
                            {
                                int statusCategory = ReadStatus.Parse(fields[0]);
                                if (statusCategory >= 0)
                                    Add(statusCategory, int.Parse(fields[1]));
                            }
                        }
                        catch (Exception)
                        { }
                    }
                    line = extrFile.ReadLine();
                }
                extrFile.Close();
            }
            catch (FileNotFoundException)
            {
                readFiles.Add(extractionSummaryPath + " - MISSING: Read statistics omitted for this data.");
            }
        }
    }

    public class ReadExtractor
    {
        private int bcWithTSSeqLen;
        private readonly static int maxExtraTSNts = 6; // Limit # of extra (G) Nts (in addition to min#==3) to remove from template switching
        private int barcodePos;
        private int insertStartPos;
        private int rndBcPos;
        private int rndBcLen;
        private int minReadLength;
        private int minInsertNonAs;
        private string[] barcodeSeqs;
        private Dictionary<string, int> barcodesWithTSSeq;
        private char lastNtOfTSSeq;
        private int firstNegBarcodeIndex;

        public ReadExtractor(Props props)
        {
            Barcodes barcodes = props.Barcodes;
            bcWithTSSeqLen = barcodes.GetLengthOfBarcodesWithTSSeq();
            barcodePos = barcodes.BarcodePos;
            insertStartPos = barcodes.GetInsertStartPos();
            rndBcPos = barcodes.RandomTagPos;
            rndBcLen = barcodes.RandomTagLen;
            minReadLength = barcodes.GetInsertStartPos() + props.MinExtractionInsertLength;
            minInsertNonAs = props.MinExtractionInsertNonAs;
            barcodeSeqs = barcodes.Seqs;
            int sIdx = 0;
            barcodesWithTSSeq = new Dictionary<string, int>();
            foreach (string s in barcodes.GetBarcodesWithTSSeq())
                barcodesWithTSSeq[s] = sIdx++;
            lastNtOfTSSeq = barcodes.TSTrimNt;
            firstNegBarcodeIndex = barcodes.FirstNegBarcodeIndex;
        }

        /// <summary>
        /// Extracts the barcode and random barcode from rec.Sequence and puts in rec.Header.
        /// </summary>
        /// <param name="rec"></param>
        /// <returns>A ReadStatus that indicates if the read was valid</returns>
        public int Extract(ref FastQRecord rec, out int bcIdx)
        {
            int minQualityInRandomTag = Props.props.MinPhredScoreInRandomTag;
            bcIdx = 0;
            rec.TrimBBB();
            string rSeq = rec.Sequence;
            int insertLength = TrimTrailingNAndCheckAs(rSeq);
            if (insertLength < minReadLength)
                return ReadStatus.LENGTH_ERROR;
            if (!barcodesWithTSSeq.TryGetValue(rSeq.Substring(barcodePos, bcWithTSSeqLen), out bcIdx))
                return ReadStatus.BARCODE_ERROR;
            if (bcIdx >= firstNegBarcodeIndex)
                return ReadStatus.NEGATIVE_BARCODE_ERROR;
            string bcRandomPart = "";
            if (rndBcLen > 0)
            {
                for (int i = rndBcPos; i < rndBcPos + rndBcLen; i++)
                {
                    if (rec.Qualities[i] < minQualityInRandomTag)
                        return ReadStatus.LOW_QUALITY_IN_RANDOM_TAG;
                }
                bcRandomPart = rSeq.Substring(rndBcPos, rndBcLen) + ".";
                if (bcRandomPart.Contains('N'))
                    return ReadStatus.N_IN_RANDOM_TAG;
            }
            int insertStart = insertStartPos;
            insertLength -= insertStart;
            int nTsNt = maxExtraTSNts;
            while (nTsNt-- > 0 && rSeq[insertStart] == lastNtOfTSSeq)
            {
                insertStart++;
                insertLength--;
            }
            if (!HasComplexity(rSeq, insertStart, insertLength))
                return ReadStatus.COMPLEXITY_ERROR;
            rec.Header = rec.Header + "_" + bcRandomPart + barcodeSeqs[bcIdx];
            rec.Trim(insertStart, insertLength);
            return ReadStatus.VALID;
        }

        /// <summary>
        /// Removes trailing N:s.
        /// If removing of trailing A:s leaves a sequence shorter than minReadLength, returns the truncated length,
        /// otherwise includes the trailing A:s in the returned length
        /// </summary>
        /// <param name="rSeq"></param>
        /// <returns>Length of remaining sequence</returns>
        private int TrimTrailingNAndCheckAs(string rSeq)
        {
            int insertLength = rSeq.Length;
            while (insertLength >= minReadLength && rSeq[insertLength - 1] == 'N')
                insertLength--;
            int nonATailLen = insertLength;
            while (nonATailLen >= minReadLength && rSeq[nonATailLen - 1] == 'A')
                nonATailLen--;
            if (nonATailLen < minReadLength) return nonATailLen;
            return insertLength;
        }

        private bool HasComplexity(string rSeq, int insertStart, int insertLength)
        {
            int nNonAs = 0;
            for (int rp = insertStart; rp < insertStart + insertLength; rp++)
                if (!"AaNn".Contains(rSeq[rp]))
                {
                    if (++nNonAs >= minInsertNonAs)
                        break;
                }
            return nNonAs >= minInsertNonAs;
        }

    }

}
