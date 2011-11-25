using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class LocusFeature : IFeature
    {
        public readonly static string variantIndicator = "_v";

        public bool IsVariant()
        {
            return Name.Contains(variantIndicator);
        }
        public bool IsMainVariant()
        {
            return !Name.Contains(variantIndicator);
        }

        private string m_Name;
        public string Name
        {
            get { return m_Name; }
            set
            {
                m_Name = value;
                int pos = value.IndexOf(variantIndicator);
                if (pos > 0) m_NonVariantName = value.Substring(0, pos);
                else m_NonVariantName = value;
            }
        }
        private string m_NonVariantName;
        public string NonVariantName { get { return m_NonVariantName; } }
        public string Chr { get; set; }
        public char Strand { get; set; }
        protected int m_Start;
        public virtual int Start
        {
            get { return m_Start; }
            set { m_Start = value; }
        }
        protected int m_End;
        public virtual int End
        {
            get { return m_End; }
            set { m_End = value; }
        }
        public int Length { get { return End - Start + 1; } }

        private int TotalSenseHits;     // Total hits to sense strand of locus (independent of masked Exons etc.)
        private int TotalAntiSenseHits; // Total hits to antisense strand of locus (independent of masked Exons etc.)
        /// <summary>
        /// Total hits to the locus on both strand (independent of any masked regions)
        /// </summary>
        /// <returns></returns>
        public virtual int GetTotalHits()
        {
            return TotalSenseHits + TotalAntiSenseHits;
        }
        public virtual int GetTotalHits(bool sense)
        {
            return (sense)? TotalSenseHits : TotalAntiSenseHits;
        }
        public void IncrTotalHits(bool sense)
        {
            if (sense) TotalSenseHits++;
            else TotalAntiSenseHits++;
        }
        public virtual bool IsExpressed()
        {
            return GetTotalHits() > 0;
        }
        public virtual int GetLocusLength()
        {
            return End - Start + 1;
        }

        public LocusFeature (string name, string chr, char strand, int start, int end)
        {
            Name = name;
            Chr = (chr.StartsWith("chr"))? chr.Substring(3) : chr;
            Strand = strand;
            m_Start = start;
            m_End = end;
        }

        public virtual MarkResult MarkHit(MappedTagItem item, int junk, MarkStatus markType)
        {
            if (markType != MarkStatus.TEST_EXON_MARK_OTHER)
                return new MarkResult(AnnotType.NOHIT, this);
            int annotType = AnnotType.USTR;
            if (item.strand == Strand)
                TotalSenseHits += item.MolCount;
            else
            {
                TotalAntiSenseHits += item.MolCount;
                annotType = AnnotType.AUSTR;
            }
            return new MarkResult(annotType, this);
        }

        public virtual bool Contains(int pos)
        {
            return (Start <= pos && End >= pos);
        }

        public virtual IEnumerable<FtInterval> IterIntervals()
        {
            yield return new FtInterval(Start, End, MarkHit, 0);
            yield break;
        }

    }
}
