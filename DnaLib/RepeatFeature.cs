using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RepeatFeature : IFeature
    {
        public string Name { get; set; }

        public int Length;
        public int GetLocusLength()
        {
            return Length;
        }

        private int TotalHits;
        public int GetTotalHits()
        {
            return TotalHits; // Total from both strands
        }
        public int GetTotalHits(bool sense)
        {
            return TotalHits; // Total from both strands
        }
        public void IncrTotalHits(bool sense)
        {
            TotalHits++;
        }
        public bool IsExpressed()
        {
            return GetTotalHits() > 0;
        }

        public int[] TotalHitsByBarcode;

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public RepeatFeature(string name, int start, int end)
        {
            Name = name;
            Length = end - start + 1;
            TotalHits = 0;
            TotalHitsByBarcode = new int[Barcodes.MaxCount];
        }

        public static RmskLine rmskLine = new RmskLine();
 
        public static RmskLine ParseRmskLine(string line)
        {
            string[] record = line.Split('\t');
            rmskLine.Chr = record[5].Substring(3); // Remove "chr"
            rmskLine.Start = int.Parse(record[6]);
            rmskLine.End = int.Parse(record[7]);
            rmskLine.Strand = record[9][0];
            rmskLine.Name = "r_" + record[10];
            return rmskLine;
        }

        public static RepeatFeature NewFromParsed()
        {
            return new RepeatFeature(rmskLine.Name, rmskLine.Start, rmskLine.End);
        }

        public void AddLength(int length)
        {
            Length += length;
        }

        public MarkResult MarkHit(int chrHitPos, int halfWidth, char strand, int bcodeIdx,
                                  int extraData, MarkStatus markType)
        {
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(AnnotType.NOHIT, this);
            TotalHits++;
            TotalHitsByBarcode[bcodeIdx]++;
            return new MarkResult(AnnotType.REPT, this); // Do not care about orientation for repeats
        }

    }

    public struct RmskLine
    {
        public string Name;
        public string Chr;
        public char Strand;
        public int Start;
        public int End;
    }
}
