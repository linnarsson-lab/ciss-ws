using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

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
        /// Molecules per barcode after UMI mutation filtering
        /// </summary>
        public int[] TotalMolsByBc;
        /// <summary>
        /// Always total reads per barcode
        /// </summary>
        public int[] TotalReadsByBc;

        /// <summary>
        /// Returns total molecules if UMIs are in use, else total reads
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <returns></returns>
        public int Hits(int bcIdx)
        {
            return (TotalMolsByBc != null)? TotalMolsByBc[bcIdx] : TotalReadsByBc[bcIdx];
        }
        public int Hits()
        {
            return (TotalMolsByBc != null) ? TotalMolsByBc.Sum(v => (int)v) : TotalReadsByBc.Sum();
        }

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
            if (Props.props.Barcodes.HasUMIs)
                TotalMolsByBc = new int[Barcodes.MaxCount];
            TotalReadsByBc = new int[Barcodes.MaxCount];
            C1DBTranscriptID = -1;
        }

        public void AddRegion(int start, int end)
        {
            Length += end - start + 1;
        }

        public int MarkHit(MappedTagItem item, int extraData, MarkStatus markType)
        {
            TotalHits += item.MolCount;
            if (TotalMolsByBc != null) TotalMolsByBc[item.bcIdx] += item.MolCount;
            TotalReadsByBc[item.bcIdx] += item.ReadCount;
            return AnnotType.REPT;
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo(((IFeature)obj).Name);
        }

    }

    public class RmskData
    {
        public string Name;
        public string Chr;
        public char Strand;
        public int Start;
        public int End;

        public static IEnumerable<RmskData> IterRmskFile(string rmskPath)
        {
            string[] record;
            int fileTypeOffset = 0;
            if (rmskPath.EndsWith("out"))
                fileTypeOffset = -1;
            using (StreamReader reader = rmskPath.OpenRead())
            {
                string line = reader.ReadLine();
                while (line == "" || !char.IsDigit(line.Trim()[0]))
                    line = reader.ReadLine();
                while (line != null)
                {
                    RmskData rmskData = new RmskData();
                    record = line.Split('\t');
                    rmskData.Chr = record[5 + fileTypeOffset].Substring(3); // Remove "chr"
                    rmskData.Start = int.Parse(record[6 + fileTypeOffset]);
                    rmskData.End = int.Parse(record[7 + fileTypeOffset]);
                    rmskData.Strand = record[9 + fileTypeOffset][0];
                    rmskData.Name = "r_" + record[10 + fileTypeOffset];
                    yield return rmskData;
                    line = reader.ReadLine();
                }
            }
        }
    }
}
