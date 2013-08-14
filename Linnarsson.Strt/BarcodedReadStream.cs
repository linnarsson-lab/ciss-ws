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
    /// FastQ read file streamer that can handle also TrueSeq and similar runs where barcodes are separated,
    /// into the index file, by inserting barcodes from index file at start of reads before extraction.
    /// </summary>
    public class BarcodedReadStream
    {
        /// <summary>
        /// Expect that sequence read are number 1 and index reads are number 2 using standard STRT read file naming.
        /// </summary>
        /// <param name="read1Filename">Original 1st read filename</param>
        /// <param name="readNo">Either '2' or '3' for respective read</param>
        /// <returns></returns>
        public static string ConvertToReadNFilename(string read1Filename, char readNo)
        {
            Match m = Regex.Match(read1Filename, "Run[0-9]+_L[0-9]+_");
            return read1Filename.Substring(0, m.Length) + readNo + read1Filename.Substring(m.Length + 1);
        }

        /// <summary>
        /// Reads fq records from a stream, with the option of extraction barcodes from a 2nd (& 3rd) read index file(s),
        /// and/or filtering to yield only records having a specific 2nd read prefix sequence.
        /// </summary>
        /// <param name="barcodes"></param>
        /// <param name="read1FqPath"></param>
        /// <param name="qualityScoreBase"></param>
        /// <param name="read2PrefixFilter">If non-empty, only records with index (2nd) reads starting with the given seq will be returned</param>
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
            IEnumerator<FastQRecord> read2Stream = GetReadStream(read1FqPath, qualityScoreBase, read2CopyLen, '2');
            IEnumerator<FastQRecord> read3Stream = GetReadStream(read1FqPath, qualityScoreBase, read3CopyLen, '3');
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
                if (read1.PassedFilter && (read2 == null || read2.PassedFilter) && (read3 == null || read3.PassedFilter))
                {
                    CheckReadIdAndInsertPrefix(read1FqPath, read2CopyLen, read1, read2, "_R2_");
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
                read1.Insert(0, readN.Sequence.Substring(0, readNCopyLen), readN.Qualities);
            }
        }

        private static IEnumerator<FastQRecord> GetReadStream(string read1FqPath, byte qualityScoreBase, int read2CopyLen, char readNo)
        {
            IEnumerator<FastQRecord> read2Stream = null;
            if (read2CopyLen > 0)
            {
                string read2Path = Path.Combine(Path.GetDirectoryName(read1FqPath),
                                                ConvertToReadNFilename(Path.GetFileName(read1FqPath), readNo));
                read2Stream = FastQFile.Stream(read2Path, qualityScoreBase, true).GetEnumerator();
            }
            return read2Stream;
        }
    }
}
