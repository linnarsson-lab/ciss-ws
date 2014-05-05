using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public interface IFeature : IComparable
    {
        IFeature RealFeature { get; }
        string Name { get; set; }
        string NonVariantName { get; }
        int GetLocusLength();
        int GetTotalHits();
        int GetTotalHits(bool sense);
        bool IsExpressed();
        int MarkHit(MappedTagItem item, int extraData, MarkStatus markType);
    }
}
