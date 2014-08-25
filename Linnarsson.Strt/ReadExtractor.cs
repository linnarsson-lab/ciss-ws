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

        public ReadExtractor()
        {
            barcodes = Props.props.Barcodes;
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
            if (bcStatus == ReadStatus.TSSEQ_MISSING)
                return bcStatus;
            if (bcStatus != ReadStatus.VALID)
            {
                bcIdx = -1;
                return AnalyzeNonBarcodeRead(rSeq);
            }
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
                        return ReadStatus.LOW_QUALITY_IN_RANDOM_TAG;
                    if (rSeq[i] == 'N')
                        return ReadStatus.N_IN_RANDOM_TAG;
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
            rec.Header = string.Format("{0}_{1}{2}", rec.Header, headerUMISection, barcodes.Seqs[bcIdx]);
            rec.Trim(insertStart, insertLength);
            return status;
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
                tailReadStatus = ReadStatus.LENGTH_ERROR;
                return lenWOPolyXTail;
            }
            foreach (string primerSeq in trailingPrimerSeqs)
            {
                int i = rSeq.LastIndexOf(primerSeq.Substring(0, minPrimerSeqLen));
                if (i >= minTotalReadLength)
                {
                    int restLen = rSeq.Length - i - minPrimerSeqLen;
                    if ((primerSeq.Length - minPrimerSeqLen) >= restLen
                        && rSeq.Substring(i + minPrimerSeqLen).Equals(primerSeq.Substring(minPrimerSeqLen, restLen)))
                    {
                        tailReadStatus = ReadStatus.TOO_SHORT_INSERT;
                        return i;
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
        /// Check if reads is mainly polyN or polyA, or contains a (previously) common SalI-containing erratic sequence
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
            if (Regex.Match(rSeq, "GTCGACTTTTTTTTTTTTTTTTTTTTTTTTT").Success)
                return ReadStatus.SAL1T25_IN_READ;
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
