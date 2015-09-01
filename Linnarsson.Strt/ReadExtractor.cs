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
    public class ReadExtractor
    {
        private readonly static string tn5Seq = "CTGTCTCTTATACACATCTGACGC";
        private readonly static string tn5SeqStart = tn5Seq.Substring(0, 12);
        private readonly static string p1Seq = "AATGATACGGCGACC";

        private readonly static int ReadSegmentQualityControlIndicator = 2; // Phred score '2' corresponding to 'B' in fastQ read qualities
        private readonly static int maxExtraTSNts = 6; // Limit # of extra (G) Nts (in addition to min#==3) to remove from template switching
        private Barcodes barcodes;
        private int insertStartPos;
        private int minTotalReadLength;
        private int minInsertNonAs;
        private int minQualityInUMI;
        private string[] diNtPatterns;
        private string[] trailingPrimerSeqs;
        private string[] forbiddenReadInternalSeqs;
        private int minPrimerSeqLen = 5;
        char[] UMIChars; // Buffer for accumulated UMI bases
        string headerUMISection = "";

        public ReadExtractor(Barcodes barcodes)
        {
            this.barcodes = barcodes;
            insertStartPos = barcodes.InsertOrGGGPos;
            minTotalReadLength = barcodes.InsertOrGGGPos + Props.props.MinExtractionInsertLength;
            minInsertNonAs = Props.props.MinExtractionInsertNonAs;
            minQualityInUMI = Props.props.MinPhredScoreInRandomTag;
            trailingPrimerSeqs = Props.props.RemoveTrailingReadPrimerSeqs.Split(',').Where(s => s.Length >= minPrimerSeqLen).ToArray();
            forbiddenReadInternalSeqs = Props.props.ForbiddenReadInternalSeqs.Split(',').ToArray();
            UMIChars = new char[barcodes.UMILen];
        }

        /// <summary>
        /// Extracts the barcode and random barcode from rec.Sequence and puts in rec.Header.
        /// </summary>
        /// <param name="rec"></param>
        /// <param name="bcIdx">Set to -1 if no valid barcode could be detected</param>
        /// <returns>A ReadStatus that indicates if the read was valid</returns>
        public int Extract(ref FastQRecord rec, out int bcIdx)
        {
            bcIdx = -1;
            string rSeq = rec.Sequence;
            int trimmedLength = rSeq.Length; // barcodes.VerifyTotalLen(rSeq.Length);
            if (rec.Qualities != null)
            {
                while (trimmedLength > 0 && rec.Qualities[trimmedLength - 1] == ReadSegmentQualityControlIndicator) trimmedLength--;
            }
            if (trimmedLength <= minTotalReadLength)
                return ReadStatus.SEQ_QUALITY_ERROR;
            int insertStart;
            int bcStatus = barcodes.VerifyBarcodeAndTS(rSeq, maxExtraTSNts, out bcIdx, out insertStart);
            if (bcStatus == ReadStatus.NO_BC_OTHER)
                return AnalyzeNonBarcodeRead(rSeq);
            if (bcStatus == ReadStatus.TSSEQ_MISSING_OTHER)
                return AnalyzeMissingTSSeqRead(rSeq);
            int lenStatus = ReadStatus.VALID;
            trimmedLength = TrimTrailingNOrPrimerAndCheckAs(rSeq, trimmedLength, out lenStatus);
            if (trimmedLength < minTotalReadLength)
                return lenStatus;
            string headerUMISection = "";
            if (barcodes.HasUMIs)
            {
                int UMICharsPos = 0;
                foreach (int i in barcodes.ReadUMIPositions())
                {
                    if (rec.Qualities[i] < minQualityInUMI)
                        return ReadStatus.LOW_QUALITY_IN_UMI;
                    if (rSeq[i] == 'N')
                        return ReadStatus.N_IN_UMI;
                    UMIChars[UMICharsPos++] = rSeq[i];
                }
                headerUMISection = new string(UMIChars) + '.';
            }
            trimmedLength = barcodes.VerifyTotalLen(trimmedLength);
            int insertLength = trimmedLength - insertStart;
            int status = TestComplexity(rSeq, insertStart, insertLength);
            if (status != ReadStatus.VALID) return status;
            status = TestDinucleotideRepeats(rSeq, insertStart, insertLength);
            if (status != ReadStatus.VALID) return status;
            if (rec.Header[rec.Header.Length - 2] == '/')
                rec.Header = rec.Header.Replace('/', ':'); // Fix to prevent STAR aligner from stripping readIds at last '/' (GA2X reads)
            rec.Header = string.Format("{0}_{1}{2}", rec.Header, headerUMISection, barcodes.Seqs[bcIdx]);
            rec.Trim(insertStart, insertLength);
            return status;
        }

        public int ExtractBcIdx(FastQRecSet recSet, out int bcIdx)
        {
            bcIdx = recSet.BarcodeIdx;
            if (bcIdx == -1)
                return AnalyzeNonBarcodeRead(recSet.InsertRead.Sequence);
            return ReadStatus.VALID;
        }

        private int ExtractUMI(FastQRecSet recSet)
        {
            if (barcodes.HasUMIs)
            {
                FastQRecord umiRead = recSet.UMIRead;
                int UMICharsPos = 0;
                foreach (int i in barcodes.ReadUMIPositions())
                {
                    if (umiRead.Qualities[i] < minQualityInUMI)
                        return ReadStatus.LOW_QUALITY_IN_UMI;
                    if (umiRead.Sequence[i] == 'N')
                        return ReadStatus.N_IN_UMI;
                    UMIChars[UMICharsPos++] = umiRead.Sequence[i];
                }
                headerUMISection = "_" + new string(UMIChars);
            }
            return ReadStatus.VALID;
        }

        public int ExtractRecSet(FastQRecSet recSet)
        {
            FastQRecord rec = recSet.InsertRead;
            string rSeq = rec.Sequence;
            int trimmedLength = rSeq.Length;
            if (rec.Qualities != null)
            {
                while (trimmedLength > 0 && rec.Qualities[trimmedLength - 1] == ReadSegmentQualityControlIndicator) trimmedLength--;
            }
            if (trimmedLength <= minTotalReadLength)
                return ReadStatus.SEQ_QUALITY_ERROR;
            int insertStart;
            if (!barcodes.VerifyTS(rSeq, maxExtraTSNts, out insertStart))
                return AnalyzeMissingTSSeqRead(rSeq);
            int readStatus;
            trimmedLength = TrimTrailingNOrPrimerAndCheckAs(rSeq, trimmedLength, out readStatus);
            if (trimmedLength < minTotalReadLength) return readStatus;
            readStatus = ExtractUMI(recSet);
            if (readStatus != ReadStatus.VALID) return readStatus;
            trimmedLength = barcodes.VerifyTotalLen(trimmedLength);
            int insertLength = trimmedLength - insertStart;
            readStatus = TestComplexity(rSeq, insertStart, insertLength);
            if (readStatus != ReadStatus.VALID) return readStatus;
            readStatus = TestDinucleotideRepeats(rSeq, insertStart, insertLength);
            if (readStatus != ReadStatus.VALID) return readStatus;
            string newHeader = string.Format("{0}{1}", rec.Header.Replace('/', ':'), headerUMISection); // Prevent STAR aligner from stripping readIds at last '/' (GA2X reads)
            recSet.mappable = new FastQRecord(rec, insertStart, insertLength, newHeader);
            return readStatus;
        }

        /// <summary>
        /// Removes trailing N:s.
        /// If removing of trailing A:s leaves a sequence shorter than minReadLength, returns the truncated length,
        /// otherwise includes the trailing A:s in the returned length.
        /// Also, if the read ends with the (start) sequence of a pre-defined primer, that sequence is removed
        /// </summary>
        /// <param name="rSeq"></param>
        /// <param name="tailReadStatus">If returned length is doomed too short, this will is a ReadStatus value giving the reason</param>
        /// <returns>Length of remaining sequence</returns>
        private int TrimTrailingNOrPrimerAndCheckAs(string rSeq, int insertLength, out int tailReadStatus)
        {
            tailReadStatus = ReadStatus.VALID;
            while (insertLength >= minTotalReadLength && rSeq[insertLength - 1] == 'N')
                insertLength--;
            int lenWOPolyXTail = insertLength;
            while (lenWOPolyXTail >= minTotalReadLength && rSeq[lenWOPolyXTail - 1] == 'A')
                lenWOPolyXTail--;
            if (lenWOPolyXTail < minTotalReadLength)
            {
                tailReadStatus = ReadStatus.TOO_LONG_TRAILING_pApN;
                return lenWOPolyXTail;
            }
            foreach (string primerSeq in trailingPrimerSeqs)
            {
                int i = rSeq.LastIndexOf(primerSeq.Substring(0, minPrimerSeqLen));
                if (i >= 0)
                {
                    int restLen = rSeq.Length - i - minPrimerSeqLen;
                    if ((primerSeq.Length - minPrimerSeqLen) >= restLen
                        && rSeq.Substring(i + minPrimerSeqLen).Equals(primerSeq.Substring(minPrimerSeqLen, restLen)))
                    {
                        if (i < minTotalReadLength)
                        {
                            tailReadStatus = ReadStatus.TOO_LONG_TRAILING_PRIMER_SEQ;
                            return i;
                        }
                        insertLength = i;
                    }
                }
            }
            foreach (string intSeq in forbiddenReadInternalSeqs)
            {
                if (rSeq.Contains(intSeq))
                {
                    tailReadStatus = ReadStatus.FORBIDDEN_INTERNAL_SEQ;
                    return 0;
                }
            }
            return insertLength;
        }

        /// <summary>
        /// Check if the read is mainly polyN or polyA, or contains some known erratic sequence (e.g. primer)
        /// </summary>
        /// <param name="rSeq"></param>
        /// <param name="insertStart"></param>
        /// <param name="insertLength"></param>
        /// <returns></returns>
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
            if (Regex.Match(rSeq, p1Seq).Success)
                return ReadStatus.P1_IN_READ;
            return ReadStatus.VALID;
        }

        /// <summary>
        /// Sequences that are dinucleotide repeats are removed
        /// </summary>
        /// <param name="rSeq"></param>
        /// <param name="insertStart"></param>
        /// <param name="insertLength"></param>
        /// <returns></returns>
        private int TestDinucleotideRepeats(string rSeq, int insertStart, int insertLength)
        {
            string insertSeq = rSeq.Substring(insertStart, insertLength);
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

        /// <summary>
        /// Tries to classify reads with out barcode to some common artefact types
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        private static int AnalyzeNonBarcodeRead(string seq)
        {
            if (TestContainsOrEndsWithTn5(seq)) return ReadStatus.NO_BC_TN5;
            if (seq.StartsWith("CGACTTTTTTTTTTTTTTTTTTTTTTTTT")) return ReadStatus.NO_BC_CGACT25;
            if (seq.Contains("TTTTTTTTTTTTTTTTTTTT")) return ReadStatus.NO_BC_INTERNAL_T20;
            if (seq.Contains(p1Seq)) return ReadStatus.NO_BC_P1;
            if (seq.Contains("TAGTCACACAGTCCTTGACG")) return ReadStatus.NO_BC_PHIX;
            if (seq.Contains("ACCTCAGATCAGACGTGGCGACCCGCTGAA")) return ReadStatus.NO_BC_RNA45S;
            if (seq.Contains("AAAAAAAAAAAAAAAAAAAA")) return ReadStatus.NO_BC_INTERNAL_A20;
            return ReadStatus.NO_BC_OTHER;
        }

        private static int AnalyzeMissingTSSeqRead(string seq)
        {
            if (TestContainsOrEndsWithTn5(seq)) return ReadStatus.TSSEQ_MISSING_TN5;
            if (seq.Contains("TTTTTTTTTTTTTTTTTTTT")) return ReadStatus.TSSEQ_MISSING_INTERNAL_T20;
            if (seq.Contains("AAAAAAAAAAAAAAAAAAAA")) return ReadStatus.TSSEQ_MISSING_INTERNAL_A20;
            if (seq.Contains(p1Seq)) return ReadStatus.TSSEQ_MISSING_P1;
            return ReadStatus.TSSEQ_MISSING_OTHER;
        }

        private static bool TestContainsOrEndsWithTn5(string seq)
        {
            int p = seq.IndexOf(tn5SeqStart);
            if (p >= 0)
            {
                int l = Math.Min(tn5Seq.Length, seq.Length - p);
                if (seq.Substring(p, l).Equals(tn5Seq.Substring(0, l))) return true;
            }
            return false;
        }
    }

}
