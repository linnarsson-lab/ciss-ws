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
        public int End;
        //public DoMarkHit Mark;
        public NewMarkHit Mark;
        public int ExtraData;

        public FtInterval(int start, int end, NewMarkHit item, int extraData)
        {
            Start = start;
            End = end;
            Mark = item;
            ExtraData = extraData;
        }

        public bool Contains(int pt)
        {
            if (Start <= pt && End >= pt) return true;
            return false;
        }
    }
}
