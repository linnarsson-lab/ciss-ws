using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class ReadFileEmptyException : Exception
    { }

    public enum LimitTest { UseThisRead, SkipThisRead, Break };

    public class ReadCounter
    {
        private int totalSum = 0;
        private int partialSum = 0;
        private int passedIlluminaFilterSum = 0;
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
        public ReadCounter(int nBarcodes)
        {
            validBarcodeReads = new List<int>(new int[nBarcodes]);
            totalBarcodeReads = new List<int>(new int[nBarcodes]);
            readLimitType = Props.props.ExtractionReadLimitType;
            readLimit = Props.props.ExtractionReadLimit;
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
        public void AddARead(int readStatus, int bcIdx, bool passedIlluminaFilter)
        {
            AddReads(readStatus, 1);
            if (bcIdx >= 0)
            {
                totalBarcodeReads[bcIdx] += 1;
                if (passedIlluminaFilter)
                    passedIlluminaFilterSum += 1;
                if (readStatus == ReadStatus.VALID)
                    validBarcodeReads[bcIdx] += 1;
            }
        }

        /// <summary>
        /// Add a number of reads (from statistics file) to the global statistics
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
                 "TOTAL_READS_IN_FILE\t" + GrandTotal + "\t100%\n" +
                 "PASSED_ILLUMINA_FILTER\t" + passedIlluminaFilterSum + "\t" + 
                                      ((GrandTotal == 0)? "0%\n" : string.Format("{0:0.#%}\n", passedIlluminaFilterSum / (double)GrandTotal));
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

}
