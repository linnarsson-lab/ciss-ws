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
        /// Wiggle data, i.e. total counts (all barcodes) of reads and molecules for each position on the chromosome
        /// </summary>
        private SortedDictionary<int, int> molWiggle = new SortedDictionary<int, int>();
        private SortedDictionary<int, int> readWiggle = new SortedDictionary<int, int>();

        /// <summary>
        /// Add (after every barcode) the molecule and read counts at all hit positions
        /// </summary>
        /// <param name="positions">Array of positions</param>
        /// <param name="molCounts">Corresponding molecule counts</param>
        /// <param name="readCounts">Corresponding read counts</param>
        public void AddCounts(int[] positions, int[] molCounts, int[] readCounts)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                int pos = positions[i];
                if (readCounts[i] > 0)
                    AddCount(readWiggle, pos, readCounts[i]);
                if (molCounts[i] > 0)
                    AddCount(readWiggle, pos, molCounts[i]);
            }
        }

        /// <summary>
        /// Add some molecules at a hit position
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="count"></param>
        public void AddMolecules(int pos, int count)
        {
            AddCount(molWiggle, pos, count);
        }

        /// <summary>
        /// Add some reads at a hit position
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="count"></param>
        public void AddReads(int pos, int count)
        {
            AddCount(readWiggle, pos, count);
        }

        private void AddCount(SortedDictionary<int, int> wData, int pos, int count)
        {
            if (!wData.ContainsKey(pos))
                wData[pos] = count;
            else
                wData[pos] += count;
        }

        /// <summary>
        /// Get number of reads spanning across a position.
        /// Note that this implementation requires all reads to be of almost same length for correct wiggle.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="averageReadLength">Since each read's length is not stored, use Ceiling of average read len for complete coverage</param>
        /// <param name="margin">Sometimes you only want to count reads that are surely spanning the position</param>
        /// <returns></returns>
        public int GetReadCount(int pos, int averageReadLength, int margin)
        {
            return GetCount(readWiggle, pos, averageReadLength, margin);
        }
        public int GetMolCount(int pos, int averageReadLength)
        {
            return GetCount(molWiggle, pos, averageReadLength, 0);
        }
        private int GetCount(SortedDictionary<int, int> wData, int pos, int averageReadLength, int margin)
        {
            int count = 0;
            for (int p = pos - averageReadLength + 1 + margin; p <= pos - margin; p++)
                if (wData.ContainsKey(p)) count += wData[p];
            return count;
        }

        public void GetReadPositionsAndCounts(out int[] positions, out int[] countAtEachPosition)
        {
            positions = readWiggle.Keys.ToArray();
            countAtEachPosition = readWiggle.Keys.ToArray();
        }

        public void WriteMolWiggle(StreamWriter writer, string chr, char strand, int averageReadLength, int chrLength)
        {
            WriteToWigFile(writer, chr, strand, averageReadLength, chrLength, molWiggle);
        }
        public void WriteReadWiggle(StreamWriter writer, string chr, char strand, int averageReadLength, int chrLength)
        {
            WriteToWigFile(writer, chr, strand, averageReadLength, chrLength, readWiggle);
        }
        private void WriteToWigFile(StreamWriter writer, string chr, char strand, int readLength, int chrLength, SortedDictionary<int, int> wData)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            int[] positions = wData.Keys.ToArray();
            int[] countAtEachPosition = wData.Keys.ToArray();
            WriteToWigFile(writer, chr, readLength, strandSign, chrLength, positions, countAtEachPosition);
        }

        public static void WriteToWigFile(StreamWriter writer, string chr, int readLength, int strandSign, int chrLength,
                                           int[] positions, int[] countAtEachPosition)
        {
            Array.Sort(positions, countAtEachPosition);
            Queue<int> stops = new Queue<int>();
            int hitIdx = 0;
            int i = 0;
            while (i < chrLength && hitIdx < positions.Length)
            {
                int c = countAtEachPosition[hitIdx];
                i = positions[hitIdx++];
                for (int cc = 0; cc < c; cc++)
                    stops.Enqueue(i + readLength);
                writer.WriteLine("fixedStep chrom=chr{0} start={1} step=1 span=1", chr, i + 1);
                while (i < chrLength && stops.Count > 0)
                {
                    while (hitIdx < positions.Length && positions[hitIdx] == i)
                    {
                        hitIdx++;
                        stops.Enqueue(i + readLength);
                    }
                    writer.WriteLine(stops.Count * strandSign);
                    i++;
                    while (stops.Count > 0 && i == stops.Peek()) stops.Dequeue();
                }
            }
        }

    }
}
