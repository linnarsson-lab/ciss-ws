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
        private static readonly int readShift = 14; // Can handle up to 16k molecules and 256k reads per position.
        private static uint molMask = (uint)(1 << readShift) - 1;
        private static uint maxReads = (uint)(1 << (32 - readShift)) - 1;

        private static readonly int QSize = 500;
        private static int[] posQ = new int[QSize];
        private static int[] countQ = new int[QSize];

        /// <summary>
        /// Total counts (all barcodes) of reads and molecules for each hit start position on one strand of the chromosome
        /// Read count is shifted up readShift bits, and molecule count is kept in lower half.
        /// </summary>
        private Dictionary<int, uint> wiggle = new Dictionary<int, uint>();
        public int NumWiggleEntries { get { return wiggle.Count; } }

        public void AddCount(int hitStartPos, int nReads, int nMols)
        {
            if (!wiggle.ContainsKey(hitStartPos))
            {
                wiggle[hitStartPos] = (Math.Min(maxReads, (uint)nReads) << readShift) | Math.Min(molMask, (uint)nMols);
            }
            else
            {
                uint newReads = (uint)nReads + (wiggle[hitStartPos] >> readShift);
                if (newReads > maxReads) newReads = maxReads;
                uint newMols = (uint)nMols + (wiggle[hitStartPos] & molMask);
                if (newMols > molMask) newMols = molMask;
                wiggle[hitStartPos] = (newReads << readShift) | newMols;
            }
        }

        public void AddARead(int readStartPos)
        {
            AddCount(readStartPos, 1, 0);
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
            int nReads = 0;
            for (int p = pos - averageReadLength + 1 + margin; p <= pos - margin; p++)
                if (wiggle.ContainsKey(p)) nReads += (int)(wiggle[p] >> readShift);
            return nReads;
        }

        /// <summary>
        /// Get sorted hit start positions and respective read count for all reads added to the Wiggle instance
        /// </summary>
        /// <param name="sortedHitStartPositions">Ordered hit start positions</param>
        /// <param name="countAtEachSortedPosition"></param>
        public void GetPositionsAndCounts(out int[] sortedHitStartPositions, out int[] countAtEachSortedPosition, bool byRead)
        {
            sortedHitStartPositions = wiggle.Keys.ToArray();
            Array.Sort(sortedHitStartPositions);
            if (byRead)
                countAtEachSortedPosition = Array.ConvertAll(sortedHitStartPositions, (p => (int)(wiggle[p] >> readShift)));
            else
                countAtEachSortedPosition = Array.ConvertAll(sortedHitStartPositions, (p => (int)(wiggle[p] & molMask)));
        }

        public void WriteWiggle(StreamWriter writer, string chr, char strand, int averageReadLength, int chrLength, bool byRead)
        {
            int[] positions, counts;
            GetPositionsAndCounts(out positions, out counts, byRead);
            WriteToWigFile(writer, chr, averageReadLength, strand, chrLength, positions, counts);
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
            int inQIdx = 0, outQIdx = 0;
            int hitIdx = 0;
            int i = 0;
            int countAtPos = 0, coverage = 0;
            while (i < chrLength && hitIdx < sortedHitStartPositions.Length)
            {
                countAtPos = countAtEachSortedPosition[hitIdx];
                coverage += countAtPos;
                i = sortedHitStartPositions[hitIdx++];
                if (i < chrLength && countAtPos > 0)
                        writer.WriteLine("fixedStep chrom=chr{0} start={1} step=1 span=1", chr, i + 1);
                while (i < chrLength && coverage > 0)
                {
                    while (hitIdx < sortedHitStartPositions.Length && sortedHitStartPositions[hitIdx] == i)
                    {
                        countAtPos += countAtEachSortedPosition[hitIdx++];
                        coverage += countAtPos;
                    }
                    if (countAtPos > 0)
                    {
                        posQ[inQIdx] = i + readLength;
                        countQ[inQIdx] = countAtPos;
                        inQIdx = (inQIdx + 1) % QSize;
                        countAtPos = 0;
                    }
                    writer.WriteLine(coverage * strandSign);
                    i++;
                    if (i == posQ[outQIdx])
                    {
                        coverage -= countQ[outQIdx];
                        outQIdx = (outQIdx + 1) % QSize;
                    }
                }
            }
        }

        public void WriteBed(StreamWriter writer, string chr, char strand, int averageReadLength, bool byRead)
        {
            int[] positions, counts;
            GetPositionsAndCounts(out positions, out counts, byRead);
            WriteToBedFile(writer, chr, averageReadLength, strand, positions, counts);
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
        public static void WriteToBedFile(StreamWriter writer, string chr, int readLength, char strand,
                                           int[] sortedHitStartPositions, int[] countAtEachSortedPosition)
        {
            for (int i = 0; i < sortedHitStartPositions.Length; i++)
            {
                if (countAtEachSortedPosition[i] == 0)
                    continue;
                string id = string.Format("{0}{1}{2}", chr, strand, sortedHitStartPositions[i]);
                writer.WriteLine("chr{0}\t{1}\t{2}\t{3}\t{4}\t{5}", chr, sortedHitStartPositions[i], sortedHitStartPositions[i] + readLength - 1,
                    id, countAtEachSortedPosition[i], strand);
            }
        }

    }
}
