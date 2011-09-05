using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public interface IFeature
    {
        string Name { get; set; }
        int GetLocusLength();
        int GetTotalHits();
        int GetTotalHits(bool sense);
        void IncrTotalHits(bool sense);
        bool IsExpressed();
        MarkResult MarkHit(int chrPos, int halfWidth, char strand, int barcodeIdx, 
                           int extraData, MarkStatus markType);
    }
}
