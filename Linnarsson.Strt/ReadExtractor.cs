﻿using System;
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
        public readonly static int N_IN_RANDOM_BC = 4;
        public readonly static int NEGATIVE_BARCODE_ERROR = 5;
        public readonly static int Length = 6;
        public readonly static string[] categories = new string[] { "VALID", "BARCODE_ERROR", "LENGTH_ERROR", 
                                                                   "COMPLEXITY_ERROR", "N_IN_RANDOM_BC", "NEGATIVE_BARCODE_ERROR" };
        public static int Parse(string category) { return Array.IndexOf(categories, category.ToUpper()); }
    }

    public class ReadCounter
    {
        private int totalSum = 0;
        private int partialSum = 0;
        private int[] totalCounts = new int[ReadStatus.Length];
        private int[] partialCounts = new int[ReadStatus.Length];
        private List<string> readFiles = new List<string>();

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
        public void AddReadFilename(string path)
        {
            readFiles.Add(path);
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
            if (PartialCount(ReadStatus.N_IN_RANDOM_BC) > 0)
                stats += ", " + PartialCount(ReadStatus.N_IN_RANDOM_BC) + " unparseable random barcodes";
            stats += ").";
            return stats;
        }
        public string TotalsToString()
        {
            string s = "The following read files are included in this read summary:\n" +
                       string.Join("\n", readFiles.ToArray());
            s += "\n\nTotal reads that passed Illumina filters: " + GrandTotal +
                 "\nAccepted reads: " + GrandCount(ReadStatus.VALID) + " (" + string.Format("{0:0.#%}",  GrandFraction(ReadStatus.VALID)) + ")" +
                 "\nRejected reads (barcode error/Gs missing/too short/polyA): " + GrandRejected +
                 "\n- wrong barcode/Gs missing: " + GrandCount(ReadStatus.BARCODE_ERROR) +
                 "\n- too short: " + GrandCount(ReadStatus.LENGTH_ERROR) +
                 "\n- polyA/low complexity: " + GrandCount(ReadStatus.COMPLEXITY_ERROR);
            if (GrandCount(ReadStatus.N_IN_RANDOM_BC) > 0)
                s += "\nRejected reads due to unparseable random tags: " + GrandCount(ReadStatus.N_IN_RANDOM_BC);
            if (GrandCount(ReadStatus.NEGATIVE_BARCODE_ERROR) > 0)
                s += "\nRejected reads due to negative barcodes: " + GrandCount(ReadStatus.NEGATIVE_BARCODE_ERROR);
            return s;
        }
        public string TotalsToTabString()
        {
            string s = "#Files included in this read summary:\n";
            foreach (string readFile in readFiles)
                s += string.Format("READFILE\t{0}\n", readFile);
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
        private void AddExtractionSummary(string extractionSummaryPath)
        {
            try
            {
                StreamReader extrFile = extractionSummaryPath.OpenRead();
                string line = extrFile.ReadLine();
                while (line != null)
                {
                    if (!line.StartsWith("#"))
                    {
                        string[] fields = line.Split('\t');
                        if (line.StartsWith("READFILE"))
                            readFiles.Add(fields[1]);
                        else
                        {
                            int statusCategory = ReadStatus.Parse(fields[0]);
                            if (statusCategory >= 0)
                                Add(statusCategory, int.Parse(fields[1]));
                        }
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
            bcIdx = 0;
            rec.TrimBBB();
            string rSeq = rec.Sequence;
            int insertLength = TrimTrailingNAndCheckAs(rSeq);
            if (insertLength < minReadLength)
                return ReadStatus.LENGTH_ERROR;
             //int bcIdx = Array.IndexOf(barcodesWithTSSeq, rSeq.Substring(barcodePos, bcWithTSSeqLen));
            if (!barcodesWithTSSeq.TryGetValue(rSeq.Substring(barcodePos, bcWithTSSeqLen), out bcIdx))
                return ReadStatus.BARCODE_ERROR;
            if (bcIdx >= firstNegBarcodeIndex)
                return ReadStatus.NEGATIVE_BARCODE_ERROR;
            string bcRandomPart = "";
            if (rndBcLen > 0)
            {
                bcRandomPart = rSeq.Substring(rndBcPos, rndBcLen) + ".";
                if (bcRandomPart.Contains('N'))
                    return ReadStatus.N_IN_RANDOM_BC;
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
