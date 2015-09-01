using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using System.IO;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Holder for up to three reads from same cluster
    /// </summary>
    public class FastQRecSet
    {
        private Barcodes bc;
        public FastQRecord read1 = null;
        public FastQRecord read2 = null;
        public FastQRecord read3 = null;
        public FastQRecord mappable = null;
        public bool PassedFilter = true;
        public FastQRecord InsertRead { get { return (bc.InsertRead == 1) ? read1 : (bc.InsertRead == 2) ? read2 : read3; } }
        public FastQRecord UMIRead { get { return (bc.UMIRead == 1)? read1 : (bc.UMIRead == 2)? read2 : read3; } }
        public FastQRecord IndexRead { get { return (bc.BarcodeRead == 1)? read1 : (bc.BarcodeRead == 2)? read2 : read3; } }
        public int BarcodeIdx { get { return bc.ExtractBcIdx(IndexRead.Sequence); } }

        public FastQRecSet(Barcodes bc)
        {
            this.bc = bc;
        }

        public override string ToString()
        {
            return "=== FastQRecSet ===\nInsertRead:\n" +
                ((InsertRead != null)? InsertRead.ToString(Props.props.QualityScoreBase) : "null") + "\nUMIRead:\n" +
                ((UMIRead != null) ? UMIRead.ToString(Props.props.QualityScoreBase) : "null") + "\nIndexRead:\n" +
                ((InsertRead != null)? IndexRead.ToString(Props.props.QualityScoreBase) : "null") + "\nmappable:\n" +
                ((mappable != null)? mappable.ToString(Props.props.QualityScoreBase) : "null") + "\n";
        }
    }

    /// <summary>
    /// FastQ read file streamer that can handle also TruSeq and similar runs where barcodes are separated,
    /// into the index file, by inserting barcodes from index file at start of reads before extraction.
    /// </summary>
    public class BarcodedReadStream
    {
        /// <summary>
        /// Iterates sets of matching reads (1,2, and/or 3) depending on barcode settings, while checks file integrity.
        /// </summary>
        /// <param name="barcodes"></param>
        /// <param name="read1Path"></param>
        /// <param name="read2FilterPrefix"></param>
        /// <param name="qualBaseFixed">If false, allow to adjust the Props.QualityScoreBase by examining input fastq files</param>
        /// <returns></returns>
        public static IEnumerable<FastQRecSet> RecSetStream(Barcodes barcodes, string read1Path, string read2FilterPrefix, bool qualBaseFixed)
        {
            FastQRecSet recSet = new FastQRecSet(barcodes);
            bool useRead1 = barcodes.NeedReed(1);
            bool useRead2 = (read2FilterPrefix.Length > 0 || barcodes.NeedReed(2));
            bool useRead3 = barcodes.NeedReed(3);
            IEnumerator<FastQRecord> read1Stream = useRead1? MkStream(read1Path, ref qualBaseFixed, 1) : null;
            IEnumerator<FastQRecord> read2Stream = useRead2 ? MkStream(read1Path, ref qualBaseFixed, 2) : null;
            IEnumerator<FastQRecord> read3Stream = useRead3 ? MkStream(read1Path, ref qualBaseFixed, 3) : null;
            bool moreRecs1 = false, moreRecs2 = false, moreRecs3 = false;
            while (true) {
                if (useRead1 && (moreRecs1 = read1Stream.MoveNext())) recSet.read1 = read1Stream.Current;
                if (useRead2 && (moreRecs2 = read2Stream.MoveNext())) recSet.read2 = read2Stream.Current;
                if (useRead3 && (moreRecs3 = read3Stream.MoveNext())) recSet.read3 = read3Stream.Current;
                if (!moreRecs1 && !moreRecs2 && !moreRecs3) break;
                bool uneqLen12 = (useRead1 && useRead2) && (moreRecs1 != moreRecs2);
                bool uneqLen13 = (useRead1 && useRead3) && (moreRecs1 != moreRecs3);
                bool uneqLen23 = (useRead2 && useRead3) && (moreRecs2 != moreRecs3);
                if (uneqLen12 || uneqLen13 || uneqLen23)
                    throw new FormatException("# of records in read files don't match: " + read1Path);
                bool uneqHeader12 = (useRead1 && useRead2) && (recSet.read1.Header != recSet.read2.Header.Replace("_R2_", "_R1_"));
                bool uneqHeader13 = (useRead1 && useRead3) && (recSet.read1.Header != recSet.read3.Header.Replace("_R3_", "_R1_"));
                bool uneqHeader23 = (useRead2 && useRead3) && (recSet.read2.Header != recSet.read3.Header.Replace("_R3_", "_R2_"));
                if (uneqHeader12 || uneqHeader13 || uneqHeader23)
                    throw new FormatException("File headers don't match: " + read1Path + " at " + (useRead1? recSet.read1.Header : recSet.read2.Header));
                if (read2FilterPrefix.Length > 0 && !Regex.IsMatch(recSet.read2.Sequence, "^" + read2FilterPrefix))
                    continue;
                yield return recSet;
            }
        }

        /// <summary>
        /// Reads fq records from a stream, with the option of extraction barcodes from a 2nd (and 3rd) read index file(s),
        /// and or filtering to yield only records having a specific 2nd read prefix sequence.
        /// </summary>
        /// <param name="barcodes"></param>
        /// <param name="read1FqPath"></param>
        /// <param name="qualityScoreBase"></param>
        /// <param name="read2FilterPrefix">If non-empty, only records with index (2nd) reads starting with the given seq will be returned</param>
        /// <returns></returns>
        public static IEnumerable<FastQRecord> Stream(Barcodes barcodes, string read1FqPath,
                                                      byte qualityScoreBase, string read2FilterPrefix)
        {
            if (!barcodes.NeedRead2Or3 && read2FilterPrefix.Length == 0)
            {
                foreach (FastQRecord rec in FastQFile.Stream(read1FqPath, qualityScoreBase))
                    yield return rec;
                yield break;
            }
            int read2CopyLen = barcodes.PrefixRead2;
            int read3CopyLen = barcodes.PrefixRead3;
            IEnumerator<FastQRecord> read1Stream = FastQFile.Stream(read1FqPath, qualityScoreBase, true).GetEnumerator();
            IEnumerator<FastQRecord> read2Stream = GetReadStream(read1FqPath, qualityScoreBase, read2CopyLen, 2);
            IEnumerator<FastQRecord> read3Stream = GetReadStream(read1FqPath, qualityScoreBase, read3CopyLen, 3);
            FastQRecord read1, read2 = null, read3 = null;
            while (read1Stream.MoveNext())
            {
                read1 = read1Stream.Current;
                if (read2Stream != null)
                {
                    read2Stream.MoveNext();
                    read2 = read2Stream.Current;
                }
                if (read3Stream != null)
                {
                    read3Stream.MoveNext();
                    read3 = read3Stream.Current;
                }
                if (read2FilterPrefix.Length > 0 && !Regex.IsMatch(read2.Sequence, "^" + read2FilterPrefix))
                        continue;
                if (true)
                {
                    CheckReadIdAndInsertPrefix(read1FqPath, read2CopyLen, read1, read2, "_R2_");
                    if (read3 != null)
                        CheckReadIdAndInsertPrefix(read1FqPath, read3CopyLen, read1, read3, "_R3_");
                    yield return read1;
                }
            }
        }

        private static void CheckReadIdAndInsertPrefix(string read1FqPath, int readNCopyLen, FastQRecord read1, 
                                                       FastQRecord readN, string idReplacer)
        {
            if (readNCopyLen > 0)
            {
                if (read1.Header != readN.Header.Replace(idReplacer, "_R1_"))
                    throw new FormatException("Read file and index file headers do not match at " + read1.Header + " in " + read1FqPath + "!");
                if (readN.Sequence.Length < readNCopyLen)
                    throw new BarcodeFileException("Index/PE read is shorter (" + readN.Sequence.Length + "bp) than #prefix spec. in barcode (" + readNCopyLen + 
                                                   "bp). file! You need to change barcode file or sequence more cycles!");
                read1.Insert(0, readN.Sequence.Substring(0, readNCopyLen), readN.Qualities);
            }
        }

        private static IEnumerator<FastQRecord> GetReadStream(string read1FqPath, byte qualityScoreBase, int readNCopyLen, int readNo)
        {
            IEnumerator<FastQRecord> readNStream = null;
            if (readNCopyLen > 0)
            {
                string readNPath = Path.Combine(Path.GetDirectoryName(read1FqPath),
                                                ConvertToReadNFilename(Path.GetFileName(read1FqPath), readNo));
                readNStream = FastQFile.Stream(readNPath, qualityScoreBase, true).GetEnumerator();
            }
            return readNStream;
        }

        private static IEnumerator<FastQRecord> MkStream(string read1Path, ref bool qualBaseFixed, int readNo)
        {
            string readNPath = Path.Combine(Path.GetDirectoryName(read1Path),
                                            ConvertToReadNFilename(Path.GetFileName(read1Path), readNo));
            byte qualBase = FastQFile.AutoDetectQualityScoreBase(readNPath, 50);
            if (qualBase != 0 && qualBase != Props.props.QualityScoreBase)
            {
                if (qualBaseFixed)
                    throw new Exception("ERROR: There seem to be different QualityScoreBases in input fastq files!");
                Console.WriteLine("WARNING: Changing fastq QualityScoreBase to " + qualBase + " from analysis of " + readNPath);
                Props.props.QualityScoreBase = qualBase;
                qualBaseFixed = true;
            }
            return FastQFile.Stream(readNPath, Props.props.QualityScoreBase, true).GetEnumerator();
        }

        private static string ConvertToReadNFilename(string read1Filename, int readNo)
        {
            Match m = Regex.Match(read1Filename, "Run[0-9]+_L[0-9]+_");
            return read1Filename.Substring(0, m.Length) + readNo + read1Filename.Substring(m.Length + 1);
        }
    }
}
