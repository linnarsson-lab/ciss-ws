using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class RepeatFeature : IFeature
    {
        public IFeature RealFeature { get { return this; } }

        private string m_Name;
        public string Name { get { return m_Name; } set { m_Name = value; } }
        public string NonVariantName { get { return m_Name; } }
        public int C1DBTranscriptID { get; set; }
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
        public bool IsExpressed()
        {
            return GetTotalHits() > 0;
        }

        /// <summary>
        /// Either molecules per barcode after rndTag mutation filtering, or total reads per barcode when no rndTag are used.
        /// </summary>
        public int[] TotalHitsByBarcode;
        /// <summary>
        /// Always total reads per barcode
        /// </summary>
        public int[] TotalReadsByBarcode;

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public RepeatFeature(string name)
        {
            Name = name;
            Length = 0;
            TotalHits = 0;
            TotalHitsByBarcode = new int[Barcodes.MaxCount];
            TotalReadsByBarcode = new int[Barcodes.MaxCount];
            C1DBTranscriptID = -1;
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

        public void AddRegion(int start, int end)
        {
            Length += end - start + 1;
        }

        //public MarkResult MarkHit(MappedTagItem item, int extraData, MarkStatus markType)
        public int MarkHit(MappedTagItem item, int extraData, MarkStatus markType)
        {
            TotalHits += item.MolCount;
            TotalHitsByBarcode[item.bcIdx] += item.MolCount;
            TotalReadsByBarcode[item.bcIdx] += item.ReadCount;
            return AnnotType.REPT; // return new MarkResult(AnnotType.REPT, this); // Do not care about orientation for repeats
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo(((IFeature)obj).Name);
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
