using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Handles writing of for per lane-extracted reads 1,2,3, and summaries into separate files
    /// </summary>
    public class SampleReadWriter
    {
        Barcodes barcodes;
        int read1Len, prefixRead2, prefixRead3, seqLen;

        string read2FilterPrefix = null;
        LaneInfo laneInfo;

        ReadExtractor readExtractor;
        ReadCounter readCounter;
        StreamWriter[] sws_barcoded;
        StreamWriter sw_slask_w_bc, sw_slask_no_bc;
        ExtractionQuality extrQ;

        public SampleReadWriter(Barcodes barcodes, LaneInfo laneInfo)
        {
            this.barcodes = barcodes;
            this.prefixRead2 = barcodes.PrefixRead2;
            this.prefixRead3 = barcodes.PrefixRead3;
            this.laneInfo = laneInfo;
            if (laneInfo.idxSeqFilter.Length > 0)
                read2FilterPrefix = "^" + laneInfo.idxSeqFilter;
            readExtractor = new ReadExtractor(barcodes);
            readCounter = new ReadCounter(barcodes);
            sws_barcoded = OpenStreamWriters(laneInfo.extractedFilePaths);
            if (Props.props.WriteSlaskFiles)
            {
                sw_slask_w_bc = laneInfo.slaskWBcFilePath.OpenWrite();
                sw_slask_no_bc = laneInfo.slaskNoBcFilePath.OpenWrite();
            }
            extrQ = (Props.props.AnalyzeExtractionQualities) ? new ExtractionQuality(Props.props.LargestPossibleReadLength) : null;
        }

        public void Setup(int read1Len, int read2Len, int read3Len)
        {
            this.read1Len = read1Len;
            prefixRead2 = Math.Min(prefixRead2, read2Len);
            prefixRead3 = Math.Min(prefixRead3, read3Len);
            seqLen = prefixRead2 + prefixRead3 + read1Len;
            //Console.WriteLine("SampleReadWriter.Setup(): read1Len=" + read1Len, " read2Len=" + read2Len + " read3Len=" + read3Len);
        }

        public void Process(string hdrStart, string hdrEnd, char[][] readSeqs, char[][] readQuals, bool passedFilter)
        {
            char[] seqChars = new char[seqLen];
            char[] qualChars = new char[seqLen];
            if (prefixRead2 > 0)
            {
                Array.Copy(readSeqs[1], seqChars, prefixRead2);
                Array.Copy(readQuals[1], qualChars, prefixRead2);
            }
            if (prefixRead3 > 0)
            {
                Array.Copy(readSeqs[2], 0, seqChars, prefixRead2, prefixRead3);
                Array.Copy(readQuals[2], 0, qualChars, prefixRead2, prefixRead3);
            }
            Array.Copy(readSeqs[0], 0, seqChars, prefixRead2 + prefixRead3, read1Len);
            Array.Copy(readQuals[0], 0, qualChars, prefixRead2 + prefixRead3, read1Len);
            string seq = new string(seqChars);
            if (read2FilterPrefix != null && !Regex.IsMatch(seq, read2FilterPrefix))
                return;
            FastQRecord rec = new FastQRecord(hdrStart + '1' + hdrEnd, seq, FastQRecord.QualitiesFromCharArray(qualChars, Props.props.QualityScoreBase), passedFilter);
            Process(rec);
        }

        private bool Process(FastQRecord rec)
        {
            int bcIdx;
            int readStatus = readExtractor.Extract(ref rec, out bcIdx);
            bool useThisRead = (readCounter.IsLimitReached(readStatus, bcIdx) == LimitTest.UseThisRead);
            if (useThisRead)
            {
                if (extrQ != null) extrQ.Add(rec.Sequence, rec.Qualities);
                if (readStatus == ReadStatus.VALID)
                    sws_barcoded[bcIdx].WriteLine(rec.ToString(Props.props.QualityScoreBase));
                else if (sw_slask_w_bc != null)
                {
                    rec.Header += "_" + ReadStatus.GetName(readStatus);
                    if (ReadStatus.IsBarcodedCategory(readStatus))
                        sw_slask_w_bc.WriteLine(rec.ToString(Props.props.QualityScoreBase));
                    else
                        sw_slask_no_bc.WriteLine(rec.ToString(Props.props.QualityScoreBase));
                }
            }
            readCounter.AddARead(useThisRead, rec, readStatus, bcIdx);
            return true;
        }

        public void ProcessLane()
        {
            foreach (FastQRecord fastQRecord in
                     BarcodedReadStream.Stream(barcodes, laneInfo.PFReadFilePath, Props.props.QualityScoreBase, laneInfo.idxSeqFilter))
                if (!Process(fastQRecord)) break;
            if (barcodes.IncludeNonPF)
            {
                string nonPFFilename = Path.GetFileName(laneInfo.PFReadFilePath).Replace(".fq", "_nonPF.fq");
                string nonPFDir = Path.Combine(Path.GetDirectoryName(laneInfo.PFReadFilePath), "nonPF");
                string nonPFPath = Path.Combine(nonPFDir, nonPFFilename);
                laneInfo.nonPFReadFilePath = nonPFPath;
                foreach (FastQRecord fastQRecord in
                         BarcodedReadStream.Stream(barcodes, nonPFPath, Props.props.QualityScoreBase, laneInfo.idxSeqFilter))
                {
                    fastQRecord.PassedFilter = false;
                    if (!Process(fastQRecord)) break;
                }
            }
            CloseAndWriteSummary();
        }

        public void CloseAndWriteSummary()
        {
            CloseStreamWriters(sws_barcoded);
            if (sw_slask_w_bc != null)
            {
                sw_slask_w_bc.Close();
                sw_slask_no_bc.Close();
            }
            using (StreamWriter sw_summary = new StreamWriter(laneInfo.summaryFilePath))
            {
                FileReads fr = readCounter.FinishLane(laneInfo);
                laneInfo.nReads = fr.readCount;
                laneInfo.nValidReads = fr.validReadCount;
                sw_summary.WriteLine(readCounter.ToSummarySection());
            }
            if (extrQ != null)
                extrQ.Write(laneInfo);
        }

        private StreamWriter[] OpenStreamWriters(string[] extractedFilePaths)
        {
            StreamWriter[] sws_barcoded = new StreamWriter[extractedFilePaths.Length];
            //Console.WriteLine("SampleReadWriter.OpenStreamWriters() " + extractedFilePaths[0] + "...");
            for (int i = 0; i < extractedFilePaths.Length; i++)
                sws_barcoded[i] = new StreamWriter(extractedFilePaths[i]);
            return sws_barcoded;
        }

        private static void CloseStreamWriters(StreamWriter[] sws_barcoded)
        {
            for (int i = 0; i < sws_barcoded.Length; i++)
            {
                sws_barcoded[i].Close();
                sws_barcoded[i].Dispose();
            }
        }

    }
}
