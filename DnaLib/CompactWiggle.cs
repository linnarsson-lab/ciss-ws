using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Wiggle plot of reads and molecules for a single strand
    /// </summary>
    public class Wiggle
    {
        /// <summary>
        /// Total counts (all barcodes) of molecules for each hit start position on one strand of the chromosome
        /// </summary>
        private SortedDictionary<int, int> molWiggle = new SortedDictionary<int, int>();
        /// <summary>
        /// Total counts (all barcodes) of reads for each hit start position on one strand of the chromosome
        /// </summary>
        private SortedDictionary<int, int> readWiggle = new SortedDictionary<int, int>();

        /// <summary>
        /// Add (after every barcode) the molecule and read counts at all hit positions
        /// </summary>
        /// <param name="hitStartPositions">Array of hit start positions</param>
        /// <param name="molCounts">Corresponding molecule counts</param>
        /// <param name="readCounts">Corresponding read counts</param>
        public void AddCounts(int[] hitStartPositions, int[] molCounts, int[] readCounts)
        {
            for (int i = 0; i < hitStartPositions.Length; i++)
            {
                int hitStartPos = hitStartPositions[i];
                if (readCounts[i] > 0)
                    AddCount(readWiggle, hitStartPos, readCounts[i]);
                if (molCounts[i] > 0)
                    AddCount(molWiggle, hitStartPos, molCounts[i]);
            }
        }

        private void AddCount(SortedDictionary<int, int> wData, int hitStartPos, int count)
        {
            if (!wData.ContainsKey(hitStartPos))
                wData[hitStartPos] = count;
            else
                wData[hitStartPos] += count;
        }

        /// <summary>
        /// Add some reads at a hit position
        /// </summary>
        /// <param name="readStartPos"></param>
        /// <param name="count"></param>
        public void AddReads(int readStartPos, int count)
        {
            AddCount(readWiggle, readStartPos, count);
        }

        /// <summary>
        /// Get number of reads spanning across a position.
        /// Note that this implementation requires all reads to be of almost same length for correct wiggle.
        /// </summary>
        /// <param name="pos">position on chromosome</param>
        /// <param name="averageReadLength">Since each read's length is not stored, call with Ceiling(the average readLen) for complete coverage</param>
        /// <param name="margin">Sometimes you only want to count reads that are surely spanning the position, then specifiy min overhang here</param>
        /// <returns>Number of reads covering position</returns>
        public int GetReadCount(int pos, int averageReadLength, int margin)
        {
            return GetCount(readWiggle, pos, averageReadLength, margin);
        }
        private int GetCount(SortedDictionary<int, int> wData, int pos, int averageReadLength, int margin)
        {
            int count = 0;
            for (int p = pos - averageReadLength + 1 + margin; p <= pos - margin; p++)
                if (wData.ContainsKey(p)) count += wData[p];
            return count;
        }

        /// <summary>
        /// Get ordered hit start positions and respective read count for all reads added to the Wiggle instance
        /// </summary>
        /// <param name="sortedPositions">Ordered hit start positions</param>
        /// <param name="countAtEachSortedPosition"></param>
        public void GetReadPositionsAndCounts(out int[] sortedPositions, out int[] countAtEachSortedPosition)
        {
            sortedPositions = readWiggle.Keys.ToArray();
            countAtEachSortedPosition = readWiggle.Values.ToArray();
        }

        public void WriteMolWiggle(StreamWriter writer, string chr, char strand, int averageReadLength, int chrLength)
        {
            int[] sortedHitStartPositions = molWiggle.Keys.ToArray();
            int[] countAtEachSortedPosition = molWiggle.Values.ToArray();
            WriteToWigFile(writer, chr, averageReadLength, strand, chrLength, sortedHitStartPositions, countAtEachSortedPosition);
        }
        public void WriteReadWiggle(StreamWriter writer, string chr, char strand, int averageReadLength, int chrLength)
        {
            int[] sortedHitStartPositions = readWiggle.Keys.ToArray();
            int[] countAtEachSortedPosition = readWiggle.Values.ToArray();
            WriteToWigFile(writer, chr, averageReadLength, strand, chrLength, sortedHitStartPositions, countAtEachSortedPosition);
        }

        /// <summary>
        /// Output wiggle formatted data for one strand of a chromosome. N.B.: Positions/counts MUST be sorted on position!
        /// </summary>
        /// <param name="writer">output file handler</param>
        /// <param name="chr">id of chromosome</param>
        /// <param name="readLength">average read length</param>
        /// <param name="strand"></param>
        /// <param name="chrLength">(approximate) length of chromosome</param>
        /// <param name="sortedHitStartPositions">SORTED! start positions of reads on chromomsome</param>
        /// <param name="countAtEachSortedPosition">number of reads at every corresponding SORTED start position</param>
        public static void WriteToWigFile(StreamWriter writer, string chr, int readLength, char strand, int chrLength,
                                           int[] sortedHitStartPositions, int[] countAtEachSortedPosition)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            Queue<int> stops = new Queue<int>();
            int hitIdx = 0;
            int i = 0;
            while (i < chrLength && hitIdx < sortedHitStartPositions.Length)
            {
                int c0 = countAtEachSortedPosition[hitIdx];
                i = sortedHitStartPositions[hitIdx++];
                for (int cc = 0; cc < c0; cc++)
                    stops.Enqueue(i + readLength);
                if (i < chrLength && stops.Count > 0)
                    writer.WriteLine("fixedStep chrom=chr{0} start={1} step=1 span=1", chr, i + 1);
                while (i < chrLength && stops.Count > 0)
                {
                    while (hitIdx < sortedHitStartPositions.Length && sortedHitStartPositions[hitIdx] == i)
                    {
                        int c = countAtEachSortedPosition[hitIdx++];
                        for (int cc = 0; cc < c; cc++)
                            stops.Enqueue(i + readLength);
                    }
                    writer.WriteLine(stops.Count * strandSign);
                    i++;
                    while (stops.Count > 0 && i == stops.Peek()) stops.Dequeue();
                }
            }
        }

        public void WriteReadBed(StreamWriter writer, string chr, char strand, int averageReadLength)
        {
            int[] sortedHitStartPositions = readWiggle.Keys.ToArray();
            int[] countAtEachSortedPosition = readWiggle.Values.ToArray();
            WriteToBedFile(writer, chr, averageReadLength, strand, sortedHitStartPositions, countAtEachSortedPosition);
        }

        public void WriteMolBed(StreamWriter writer, string chr, char strand, int averageReadLength)
        {
            int[] sortedHitStartPositions = molWiggle.Keys.ToArray();
            int[] countAtEachSortedPosition = molWiggle.Values.ToArray();
            WriteToBedFile(writer, chr, averageReadLength, strand, sortedHitStartPositions, countAtEachSortedPosition);
        }

        /// <summary>
        /// Output BED formatted data for one strand of a chromosome. N.B.: Positions/counts MUST be sorted on position!
        /// </summary>
        /// <param name="writer">output file handler</param>
        /// <param name="chr">id of chromosome</param>
        /// <param name="readLength">average read length</param>
        /// <param name="strand"></param>
        /// <param name="sortedHitStartPositions">SORTED! start positions of reads on chromomsome</param>
        /// <param name="countAtEachSortedPosition">number of reads at every corresponding SORTED start position</param>
        private static void WriteToBedFile(StreamWriter writer, string chr, int readLength, char strand,
                                           int[] sortedHitStartPositions, int[] countAtEachSortedPosition)
        {
            for (int i = 0; i < sortedHitStartPositions.Length; i++)
            {
                string id = string.Format("{0}{1}{2}", chr, strand, sortedHitStartPositions[i]);
                writer.WriteLine("chr{0}\t{1}\t{2}\t{3}\t{4}\t{5}", chr, sortedHitStartPositions[i], sortedHitStartPositions[i] + readLength - 1,
                    id, countAtEachSortedPosition[i], strand);
            }
        }

    }
}
