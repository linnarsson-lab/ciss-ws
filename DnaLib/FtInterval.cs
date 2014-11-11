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
        public IFeature Feature;
        public ushort ExtraData; // The index of an exon, intron, or splice junctino
        public byte annotType;
        public char Strand;

        public bool IsTrDetectingStrand(char strand)
        {
            return !Props.props.DirectionalReads || ((strand == Strand) == Props.props.SenseStrandIsSequenced);
        }

        public override string ToString()
        {
            return "FtInterval: Start=" + Start + " End=" + End + " Strand=" + Strand + " Name=" + Feature.Name +
                   " AnnotType=" + AnnotType.GetName(annotType);
        }

        public FtInterval(int start, int end, NewMarkHit item, int extraData, IFeature feature, int annotType, char strand)
        {
            Start = start;
            End = end;
            Mark = item;
            Feature = feature;
            ExtraData = (ushort)extraData;
            this.annotType = (byte)annotType;
            Strand = strand;
        }

        public bool Contains(int pt)
        {
            if (Start <= pt && End >= pt) return true;
            return false;
        }

        public int GetTranscriptPos(int hitMidPos)
        {
            return ((TranscriptFeature)Feature).GetTranscriptPos(hitMidPos, ExtraData);
        }
    }
}
