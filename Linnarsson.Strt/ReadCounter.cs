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

    public class FileReads
    {
        public string path;
        public int readCount = 0;
        public int validReadCount = 0;
        public long validReadTotLen = 0;
        public double AverageValidReadLen { get { return (validReadTotLen / (double)validReadCount); } }

        public FileReads()
        { }
        public FileReads(string path, int readCount, int validReadCount, long validReadTotLen)
        {
            this.path = path;
            this.readCount = readCount;
            this.validReadCount = validReadCount;
            this.validReadTotLen = validReadTotLen;
        }

        public void AddAValidRead(int readLen)
        {
            validReadCount += 1;
            validReadTotLen += readLen;
        }

        public static FileReads FromSummaryLine(string line)
        {
            FileReads fr = new FileReads();
            string[] fields = line.Split('\t');
            fr.path = fields[1];
            double averageReadLen = double.Parse(fields[2]);
            if (averageReadLen < 0.01)
                throw new ReadFileEmptyException();
            fr.validReadCount = int.Parse(fields[3]);
            fr.validReadTotLen = (long)Math.Round(averageReadLen * fr.validReadCount);
            fr.readCount = int.Parse(fields[4]);
            return fr;
        }

        public static readonly string SummaryHeader = "#\tExtractedFile\tMeanValidReadLen\tValidReadCount\tTotalReadCount\n";

        public string ToSummaryLine()
        {
            return string.Format("READFILE\t{0}\t{1:0.0000}\t{2}\t{3}\n", path, AverageValidReadLen, validReadCount, readCount);
        }
    }

    public class ReadCounter
    {
        private ExtractionWordCounter wordCounter;
        private Barcodes barcodes;

        private List<FileReads> fileReads = new List<FileReads>();
        private FileReads PFFileReads = new FileReads();
        private FileReads NonPFFileReads = new FileReads();

        private int totalLimitedReads = 0; // If the user extracted with an upper read count limit
        private int totAnalyzedCount = 0;
        public int PassedIlluminaFilter { get; private set; }
        private int[] countByStatus = new int[ReadStatus.Length];

        private int[] validBarcodeReads;
        private int[] totalBarcodeReads;

        private ReadLimitType readLimitType = ReadLimitType.None; // By default all reads in fq files will be analyzed
        private int readLimit = 0; // Parameter to the read limiter

        public ReadCounter(Barcodes barcodes)
        {
            this.barcodes = barcodes;
            validBarcodeReads = new int[barcodes.Count];
            totalBarcodeReads = new int[barcodes.Count];
            readLimitType = Props.props.ExtractionReadLimitType;
            readLimit = Props.props.ExtractionReadLimit;
            wordCounter = new ExtractionWordCounter(Props.props.ExtractionCounterWordLength);
        }

        /// <summary>
        /// Average read length over the valid extraced reads in all files
        /// </summary>
        public int AverageReadLen
        {
            get
            {
                return (int)Math.Floor(fileReads.ConvertAll(fr => fr.validReadTotLen).Sum() / 
                                         (double) fileReads.ConvertAll(fr => fr.validReadCount).Sum());
            }
        }
        /// <summary>
        /// Total valid read counts per barcode over all files
        /// </summary>
        public int[] ValidReadsByBarcode { get { return validBarcodeReads; } }
        /// <summary>
        /// Count of valid reads over all files in specific barcodes
        /// </summary>
        public int ValidReads(int[] selectedBcIndexes)
        {
            int sum = 0;
            foreach (int idx in selectedBcIndexes)
                sum += validBarcodeReads[idx];
            return sum;
        }

        /// <summary>
        /// Count of all analyzed reads over all files in specific barcodes
        /// </summary>
        public int TotalReads(int[] selectedBcIndexes)
        {
            int sum = 0;
            foreach (int idx in selectedBcIndexes)
                sum += totalBarcodeReads[idx];
            return sum;
        }
        public int TotalAnalyzedReads { get { return totAnalyzedCount; } }
        public int ReadCount(int readStatus) { return countByStatus[readStatus]; }

        private double ReadCountFraction(int readStatus)
        {
            return (totAnalyzedCount > 0) ? (countByStatus[readStatus] / (double)totAnalyzedCount) : 0.0;
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
                    return TotalAnalyzedReads >= readLimit ? LimitTest.Break : LimitTest.UseThisRead;
                case ReadLimitType.TotalValidReads:
                    return ReadCount(ReadStatus.VALID) >= readLimit ? LimitTest.Break : LimitTest.UseThisRead;
                case ReadLimitType.TotalValidReadsPerBarcode:
                    if (bcIdx < 0 || validBarcodeReads[bcIdx] < readLimit) return LimitTest.UseThisRead;
                    return (ValidReadsByBarcode.All(v => v >= readLimit)) ? LimitTest.Break : LimitTest.SkipThisRead;
                case ReadLimitType.TotalReadsPerBarcode:
                    if (bcIdx < 0 || totalBarcodeReads[bcIdx] < readLimit) return LimitTest.UseThisRead;
                    return (totalBarcodeReads.All(v => v >= readLimit)) ? LimitTest.Break : LimitTest.SkipThisRead;
                default:
                    return LimitTest.UseThisRead;
            }
        }

        /// <summary>
        /// Add a read (during extraction) to the statistics.
        /// </summary>
        /// <param name="readIsUsed">If false, the read is not used due to a use-defined upper limit on number of reads</param>
        /// <param name="readStatus">Status of the read</param>
        /// <param name="bcIdx">Barcode of the read</param>
        /// <param name="passedIlluminaFilter"></param>
        public void AddARead(bool readIsUsed, FastQRecord rec, int readStatus, int bcIdx)
        {
            FileReads fr = rec.PassedFilter ? PFFileReads : NonPFFileReads;
            fr.readCount++;
            if (!readIsUsed)
                totalLimitedReads++;
            else
            {
                wordCounter.AddRead(rec.Sequence);
                AddReads(readStatus, 1);
                if (rec.PassedFilter)
                    PassedIlluminaFilter++;
                if (bcIdx >= 0)
                {
                    totalBarcodeReads[bcIdx]++;
                    if (readStatus == ReadStatus.VALID)
                    {
                        fr.AddAValidRead(rec.Sequence.Length);
                        validBarcodeReads[bcIdx]++;
                    }
                }
            }
        }

        /// <summary>
        /// Add a number of reads (from statistics file) to the global statistics
        /// </summary>
        /// <param name="readStatus"></param>
        /// <param name="count"></param>
        public void AddReads(int readStatus, int count)
        {
            totAnalyzedCount += count;
            countByStatus[readStatus] += count;
        }

        /// <summary>
        /// Summarize the extracted reads of the lane and prepare for counting a new lane.
        /// </summary>
        /// <param name="laneInfo">Info with path to PF (and nonPF) of the recently extracted lane correctly set</param>
        /// <returns></returns>
        public FileReads FinishLane(LaneInfo laneInfo)
        {
            PFFileReads.path = laneInfo.PFReadFilePath;
            NonPFFileReads.path = laneInfo.nonPFReadFilePath;
            FileReads PFResult = PFFileReads;
            fileReads.Add(PFResult);
            if (laneInfo.nonPFReadFilePath != null)
                fileReads.Add(NonPFFileReads);
            PFFileReads = new FileReads();
            NonPFFileReads = new FileReads();
            return PFResult;
        }

        public List<FileReads> GetReadFiles()
        {
            return fileReads;
        }

        public string ToSummarySection()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(FileReads.SummaryHeader);
            foreach (FileReads fr in fileReads)
                sb.Append(fr.ToSummaryLine());
            if (totalLimitedReads > 0)
                sb.Append("#A limiter condition was used during extraction and some reads were skipped:\n" +
                     "LIMITER_EXCLUDED_READS\t" + totalLimitedReads + "\n#Below figures refers to non-limiter filtered reads:\n");
            sb.Append("#Category\tCount\tPercent\n");
            sb.Append("TOTAL_ANALYZED_READS\t" + TotalAnalyzedReads + "\t100%\n");
            string PFFrac = ((TotalAnalyzedReads == 0)? "0%\n" : string.Format("{0:0.#%}\n", PassedIlluminaFilter / (double)TotalAnalyzedReads));
            sb.Append(string.Format("PASSED_ILLUMINA_FILTER\t{0}\t{1}\n", PassedIlluminaFilter, PFFrac));
            for (int statusCat = 0; statusCat < ReadStatus.Length; statusCat++)
            {
                if (barcodes.HasUMIs || !ReadStatus.categories[statusCat].Contains("RANDOM"))
                    sb.Append(string.Format("{0}\t{1}\t{2:0.#%}\n", ReadStatus.categories[statusCat], ReadCount(statusCat), ReadCountFraction(statusCat)));
            }
            sb.Append("#\tBarcode\tValidSTRTReads\tTotalBarcodedReads\n");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                sb.Append(string.Format("BARCODEREADS\t{0}\t{1}\t{2}\n", barcodes.Seqs[bcIdx], validBarcodeReads[bcIdx], totalBarcodeReads[bcIdx]));
            sb.Append("\nBelow are the most common words among all reads.\n\n");
            sb.Append(wordCounter.GroupsToString(200));
            sb.Append("\n");
            return sb.ToString();
        }

        public void AddExtractionSummary(string extractionSummaryPath)
        {
            try
            {
                AddExtractionSummaryFile(extractionSummaryPath);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("WARNING: " + extractionSummaryPath + " missing! Maybe the read extraction was interrupted?");
                MakeExtractionSummaryFromFqFiles(Path.GetDirectoryName(extractionSummaryPath));
            }
            if (fileReads.Count == 0)
                throw new ReadFileEmptyException();
        }

        private void AddExtractionSummaryFile(string extractionSummaryPath)
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
                                fileReads.Add(FileReads.FromSummaryLine(line));
                            else if (line.StartsWith("PASSED_ILLUMINA_FILTER"))
                            {
                                PassedIlluminaFilter += int.Parse(fields[1]);
                            }
                            else if (line.StartsWith("BARCODEREADS"))
                            {
                                int bcIdx = barcodes.GetBcIdxFromBarcode(fields[1]);
                                validBarcodeReads[bcIdx] += int.Parse(fields[2]);
                                if (fields.Length >= 4)
                                    totalBarcodeReads[bcIdx] += int.Parse(fields[3]);
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
                            Console.WriteLine("WARNING: Skipping empty readfile:\n" + line);
                            break;
                        }
                    }
                    line = extrFile.ReadLine();
                }
            }
        }

        private void MakeExtractionSummaryFromFqFiles(string laneExtractionFolder)
        {
            Console.WriteLine("WARNING: Calculating valid read counts and average read length from extracted .fq files in " + laneExtractionFolder + ".");
            long totValidReadsLen = 0;
            int totValidReads = 0;
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                string fqFile = Path.Combine(laneExtractionFolder, bcIdx.ToString() + ".fq");
                int bcReads = 0;
                if (File.Exists(fqFile))
                {
                    foreach (FastQRecord rec in FastQFile.Stream(fqFile, 64))
                    {
                        bcReads++;
                        totValidReadsLen += rec.Sequence.Length;
                    }
                } 
                totalBarcodeReads[bcIdx] = validBarcodeReads[bcIdx] = bcReads;
                totValidReads += bcReads;
            }
            if (totValidReads > 0)
                fileReads.Add(new FileReads(laneExtractionFolder, totValidReads, totValidReads, totValidReadsLen));
        }
    }

}
