﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    public class SampleReadWriter
    {
        Barcodes barcodes;
        int prefixRead2, prefixRead3;
        string read2FilterPrefix = null;
        LaneInfo laneInfo;

        ReadExtractor readExtractor;
        ReadCounter readCounter;
        ExtractionWordCounter wordCounter;
        StreamWriter[] sws_barcoded;
        StreamWriter sw_slask;
        ExtractionQuality extrQ;
        double totLen = 0.0;
        long nRecords = 0;

        public SampleReadWriter(Barcodes barcodes, LaneInfo laneInfo)
        {
            this.barcodes = barcodes;
            this.prefixRead2 = barcodes.PrefixRead2;
            this.prefixRead3 = barcodes.PrefixRead3;
            this.laneInfo = laneInfo;
            if (laneInfo.idxSeqFilter.Length > 0)
                read2FilterPrefix = "^" + laneInfo.idxSeqFilter;
            readExtractor = new ReadExtractor();
            readCounter = new ReadCounter(barcodes.Count);
            wordCounter = new ExtractionWordCounter(Props.props.ExtractionCounterWordLength);
            sws_barcoded = OpenStreamWriters(laneInfo.extractedFilePaths);
            sw_slask = laneInfo.slaskFilePath.OpenWrite();
            extrQ = (Props.props.AnalyzeExtractionQualities) ? new ExtractionQuality(Props.props.LargestPossibleReadLength) : null;
        }

        public void Process(string hdrStart, string hdrEnd, char[][] readSeqs, char[][] readQuals)
        {
            int read1Len = readSeqs[0].Length;
            int seqLen = prefixRead2 + prefixRead3 + read1Len;
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
            FastQRecord rec = new FastQRecord(hdrStart + '1' + hdrEnd, seq, FastQRecord.QualitiesFromCharArray(qualChars, Props.props.QualityScoreBase));
            Process(rec);
        }

        public bool Process(FastQRecord rec)
        {
            int bcIdx;
            int readStatus = readExtractor.Extract(ref rec, out bcIdx);
            LimitTest testResult = readCounter.IsLimitReached(readStatus, bcIdx);
            if (testResult == LimitTest.Break)
                return false;
            if (testResult == LimitTest.UseThisRead)
            {
                if (extrQ != null) extrQ.Add(rec.Sequence, rec.Qualities);
                wordCounter.AddRead(rec.Sequence);
                readCounter.AddARead(readStatus, bcIdx);
                if (readStatus == ReadStatus.VALID)
                {
                    totLen += rec.Sequence.Length;
                    nRecords++;
                    sws_barcoded[bcIdx].WriteLine(rec.ToString(Props.props.QualityScoreBase));
                }
                else sw_slask.WriteLine(rec.ToString(Props.props.QualityScoreBase));
            }
            return true;
        }

        public void ProcessLane()
        {
            foreach (FastQRecord fastQRecord in
                     BarcodedReadStream.Stream(barcodes, laneInfo.readFilePath, Props.props.QualityScoreBase, laneInfo.idxSeqFilter))
                if (!Process(fastQRecord)) break;
            CloseAndWriteSummary();
        }

        public void CloseAndWriteSummary()
        {
            CloseStreamWriters(sws_barcoded);
            sw_slask.Close();
            using (StreamWriter sw_summary = new StreamWriter(laneInfo.summaryFilePath))
            {
                int averageReadLen = (int)Math.Round(totLen / nRecords);
                readCounter.AddReadFile(laneInfo.readFilePath, averageReadLen);
                sw_summary.WriteLine(readCounter.TotalsToTabString(barcodes.HasUMIs));
                sw_summary.WriteLine("#\tBarcode\tValidSTRTReads\tTotalBarcodedReads");
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    sw_summary.WriteLine("BARCODEREADS\t{0}\t{1}\t{2}",
                                         barcodes.Seqs[bcIdx], readCounter.ValidReadsByBarcode[bcIdx], readCounter.TotalReadsByBarcode[bcIdx]);
                sw_summary.WriteLine("\nBelow are the most common words among all reads.\n");
                sw_summary.WriteLine(wordCounter.GroupsToString(200));
            }
            if (extrQ != null)
                extrQ.Write(laneInfo);
            laneInfo.nReads = readCounter.PartialTotal;
            laneInfo.nValidReads = readCounter.PartialCount(ReadStatus.VALID);
        }

        private StreamWriter[] OpenStreamWriters(string[] extractedFilePaths)
        {
            StreamWriter[] sws_barcoded = new StreamWriter[extractedFilePaths.Length];
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
