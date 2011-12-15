using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    /// <summary>
    /// An interval with an associated call and int value
    /// </summary>
    public struct FtInterval
    {
        public int Start;
        /// <summary>
        /// Inclusive end position
        /// </summary>
        public int End;
        public NewMarkHit Mark;
        public int ExtraData;
        public IFeature Feature;
        public int annotType;
        public char Strand;

        public override string ToString()
        {
            return "FtInterval: Start=" + Start + " End=" + End + " Strand= " + Strand + "Name=" + Feature.Name + " AnnotType=" + AnnotType.GetName(annotType);
        }
        public FtInterval(int start, int end, NewMarkHit item, int extraData, IFeature feature, int annotType, char strand)
        {
            Start = start;
            End = end;
            Mark = item;
            ExtraData = extraData;
            Feature = feature;
            this.annotType = annotType;
            Strand = strand;
        }

        public bool Contains(int pt)
        {
            if (Start <= pt && End >= pt) return true;
            return false;
        }
    }
}
