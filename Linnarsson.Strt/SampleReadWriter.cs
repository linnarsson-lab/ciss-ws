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
        StreamWriter sw_read1, sw_read2, sw_read3;
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
            if (Props.props.WritePlateReadFile)
            {
                sw_read1 = barcodes.NeedReed(1)? laneInfo.plateRead1FilePath.OpenWrite() : null;
                sw_read2 = barcodes.NeedReed(2) ? laneInfo.plateRead2FilePath.OpenWrite() : null;
                sw_read3 = barcodes.NeedReed(3) ? laneInfo.plateRead3FilePath.OpenWrite() : null;
            }
            extrQ = (Props.props.AnalyzeExtractionQualities) ? new ExtractionQuality(Props.props.LargestPossibleReadLength) : null;
        }

        public void Setup(int read1Len, int read2Len, int read3Len)
        {
            this.read1Len = read1Len;
            prefixRead2 = Math.Min(prefixRead2, read2Len);
            prefixRead3 = Math.Min(prefixRead3, read3Len);
            seqLen = prefixRead2 + prefixRead3 + read1Len;
        }

        /// <summary>
        /// Experimental for parallell extraction to all samples in lane. Not yet in use
        /// </summary>
        /// <param name="hdrStart"></param>
        /// <param name="hdrEnd"></param>
        /// <param name="readSeqs"></param>
        /// <param name="readQuals"></param>
        /// <param name="passedFilter"></param>
        public void Process(string hdrStart, string hdrEnd, char[][] readSeqs, char[][] readQuals, bool passedFilter)
        {
            throw new NotImplementedException();
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
                if (extrQ != null) extrQ.Add(rec);
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

        public void ProcessLaneAsRecSets()
        {
            foreach (FastQRecSet recSet in
              BarcodedReadStream.RecSetStream(barcodes, laneInfo.PFReadFilePath, laneInfo.idxSeqFilter, false))
                if (!ProcessRecSet(recSet)) break;
            if (barcodes.IncludeNonPF)
            {
                foreach (FastQRecSet recSet in
                  BarcodedReadStream.RecSetStream(barcodes, laneInfo.nonPFReadFilePath, laneInfo.idxSeqFilter, false))
                {
                    recSet.PassedFilter = false;
                    if (!ProcessRecSet(recSet)) break;
                }
            }
            CloseAndWriteSummary();
        }

        private void WritePlateReadFiles(FastQRecSet recSet)
        {
            if (sw_read1 != null) sw_read1.WriteLine(recSet.read1.ToString(Props.props.QualityScoreBase));
            if (sw_read2 != null) sw_read2.WriteLine(recSet.read2.ToString(Props.props.QualityScoreBase));
            if (sw_read3 != null) sw_read3.WriteLine(recSet.read3.ToString(Props.props.QualityScoreBase));
        }

        private bool ProcessRecSet(FastQRecSet recSet)
        {
            int bcIdx;
            int readStatus = readExtractor.ExtractBcIdx(recSet, out bcIdx);
            if (bcIdx > -1)
                WritePlateReadFiles(recSet);
            FastQRecord insertRead = recSet.InsertRead;
            bool useThisRead = (readCounter.IsLimitReached(readStatus, bcIdx) == LimitTest.UseThisRead);
            if (useThisRead)
            {
                if (readStatus == ReadStatus.VALID)
                    readStatus = readExtractor.ExtractRecSet(recSet);
                //Console.WriteLine(recSet.ToString() + "-> bcIdx=" + bcIdx + "(" + ((bcIdx > -1)? barcodes.Seqs[bcIdx] : "None") + ") readStatus=" + ReadStatus.GetName(readStatus));
                if (extrQ != null) extrQ.Add(insertRead);
                if (readStatus == ReadStatus.VALID)
                    sws_barcoded[bcIdx].WriteLine(recSet.mappable.ToString(Props.props.QualityScoreBase));
                else if (sw_slask_w_bc != null)
                {
                    insertRead.Header += "_" + ReadStatus.GetName(readStatus);
                    if (ReadStatus.IsBarcodedCategory(readStatus))
                        sw_slask_w_bc.WriteLine(insertRead.ToString(Props.props.QualityScoreBase));
                    else
                        sw_slask_no_bc.WriteLine(insertRead.ToString(Props.props.QualityScoreBase));
                }
            }
            readCounter.AddARead(useThisRead, recSet.mappable, readStatus, bcIdx);
            return true;
        }

        public void CloseAndWriteSummary()
        {
            CloseStreamWriters(sws_barcoded);
            if (sw_slask_w_bc != null) sw_slask_w_bc.Close();
            if (sw_slask_no_bc != null) sw_slask_no_bc.Close();
            if (sw_read1 != null) sw_read1.Close();
            if (sw_read2 != null) sw_read2.Close();
            if (sw_read3 != null) sw_read3.Close();
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
