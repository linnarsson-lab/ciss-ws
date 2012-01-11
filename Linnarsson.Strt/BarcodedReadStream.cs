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
        /// <param name="readsFqFilename"></param>
        /// <returns></returns>
        public static string ConvertToIndexFilename(string readsFqFilename)
        {
            Match m = Regex.Match(readsFqFilename, "Run[0-9]+_L[0-9]+_");
            return readsFqFilename.Substring(0, m.Length) + "2" + readsFqFilename.Substring(m.Length + 1);
        }

        public static IEnumerable<FastQRecord> Stream(Barcodes barcodes, string readsFqPath, byte qualityScoreBase)
        {
            if (!barcodes.BarcodesInIndexReads)
            {
                foreach (FastQRecord rec in FastQFile.Stream(readsFqPath, qualityScoreBase))
                    yield return rec;
                yield break;
            }
            int indexEndPos = barcodes.GetInsertStartPos();
            IEnumerator<FastQRecord> readStream = FastQFile.Stream(readsFqPath, qualityScoreBase, true).GetEnumerator();
            string indexFqPath = Path.Combine(Path.GetDirectoryName(readsFqPath), ConvertToIndexFilename(Path.GetFileName(readsFqPath)));
            IEnumerator<FastQRecord> indexStream = FastQFile.Stream(indexFqPath, qualityScoreBase, true).GetEnumerator();
            while (readStream.MoveNext())
            {
                FastQRecord read = readStream.Current;
                indexStream.MoveNext();
                FastQRecord index = indexStream.Current;
                if (read.PassedFilter && index.PassedFilter)
                {
                    if (read.Header != index.Header.Replace("_R2_", "_R1_"))
                        throw new FormatException("Read file and index file headers do not match at " + read.Header + " in " + readsFqPath + "!");
                    byte[] indexQs = new byte[indexEndPos];
                    Array.Copy(index.Qualities, indexQs, indexEndPos);
                    read.Insert(0, index.Sequence.Substring(0, indexEndPos), indexQs);
                    yield return read;
                }
            }
        }
    }
}
