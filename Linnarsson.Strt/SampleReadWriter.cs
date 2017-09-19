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

        LaneInfo laneInfo;

        ReadExtractor readExtractor;
        ReadCounter readCounter;
        Dictionary<int, StreamWriter> bcIdx2Writer;
        StreamWriter sw_slask_w_bc, sw_slask_no_bc;
        StreamWriter sw_read1, sw_read2, sw_read3;
        ExtractionQuality extrQ;

        public SampleReadWriter(Barcodes barcodes, LaneInfo laneInfo)
        {
            this.barcodes = barcodes;
            this.laneInfo = laneInfo;
            readExtractor = new ReadExtractor(barcodes);
            readCounter = new ReadCounter(barcodes);
            OpenStreamWriters(laneInfo);
            if (Props.props.WriteSlaskFiles)
            {
                sw_slask_w_bc = laneInfo.slaskWBcFilePath.OpenWrite();
                sw_slask_no_bc = laneInfo.slaskNoBcFilePath.OpenWrite();
            }
            if (Props.props.WritePlateReadFile)
            {
                sw_read1 = barcodes.NeedRead(1)? laneInfo.plateRead1FilePath.OpenWrite() : null;
                sw_read2 = barcodes.NeedRead(2) ? laneInfo.plateRead2FilePath.OpenWrite() : null;
                sw_read3 = barcodes.NeedRead(3) ? laneInfo.plateRead3FilePath.OpenWrite() : null;
            }
            extrQ = (Props.props.AnalyzeExtractionQualities) ? new ExtractionQuality(Props.props.LargestPossibleReadLength) : null;
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
            if (bcIdx > -1 && readStatus != ReadStatus.MIXIN_SAMPLE_BC)
                WritePlateReadFiles(recSet);
            bool useThisRead = (readCounter.IsLimitReached(readStatus, bcIdx) == LimitTest.UseThisRead);
            if (useThisRead)
            {
                recSet.mappable = recSet.InsertRead;
                if (readStatus == ReadStatus.VALID)
                    readStatus = readExtractor.ExtractRecSet(recSet);
                if (extrQ != null) extrQ.Add(recSet.InsertRead);
				if (readStatus == ReadStatus.VALID) {
					bcIdx2Writer[bcIdx].WriteLine(recSet.mappable.ToString (Props.props.QualityScoreBase));
				}
                else if (readStatus != ReadStatus.MIXIN_SAMPLE_BC && sw_slask_w_bc != null)
                {
                    recSet.InsertRead.Header += "_" + ReadStatus.GetName(readStatus);
                    if (ReadStatus.IsBarcodedCategory(readStatus))
                        sw_slask_w_bc.WriteLine(recSet.InsertRead.ToString(Props.props.QualityScoreBase));
                    else
                    { // Add the erronous barcode seq after status text
                        recSet.InsertRead.Header += ":" + recSet.BarcodeSeq;
                        sw_slask_no_bc.WriteLine(recSet.InsertRead.ToString(Props.props.QualityScoreBase));
                    }
                }
            }
            readCounter.AddARead(useThisRead, recSet.mappable, readStatus, bcIdx);
            return true;
        }

        public void CloseAndWriteSummary()
        {
            CloseStreamWriters();
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

        private void OpenStreamWriters(LaneInfo laneInfo)
        {
            bcIdx2Writer = new Dictionary<int, StreamWriter>();
            foreach (KeyValuePair<int, string> pathByBcIdx in laneInfo.IterExtractionFilePathsByBcIdx())
                bcIdx2Writer[pathByBcIdx.Key] = new StreamWriter(pathByBcIdx.Value);
        }

        private void CloseStreamWriters()
        {
            foreach (StreamWriter writer in bcIdx2Writer.Values)
            {
				writer.Close();
				writer.Dispose();
            }
        }

    }
}
