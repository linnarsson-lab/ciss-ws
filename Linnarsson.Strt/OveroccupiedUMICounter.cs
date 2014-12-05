using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class OverOccupiedUMICounter
    {
        int UMICountLimit;
        Dictionary<int, List<IFeature>> gfsByBcIdx;
        Barcodes barcodes;

        public OverOccupiedUMICounter(Barcodes barcodes)
        {
            this.barcodes = barcodes;
            UMICountLimit = (int)Math.Floor(Props.props.CriticalOccupiedUMIFraction * barcodes.UMICount);
            gfsByBcIdx = new Dictionary<int, List<IFeature>>(barcodes.Count);
        }

        public void Check(MappedTagItem item, List<IFeature> exonHitFeatures)
        {
            if (item.ObservedMolCount > UMICountLimit)
            {
                List<IFeature> gfs;
                if (!gfsByBcIdx.TryGetValue(item.bcIdx, out gfs))
                {
                    gfs = new List<IFeature>();
                    gfsByBcIdx[item.bcIdx] = gfs;
                }
                foreach (IFeature gf in exonHitFeatures)
                    if (!gfs.Contains(gf))
                        gfs.Add(gf);
            }
        }

        public void WriteOutput(string outputPathbase)
        {
            using (StreamWriter writer = new StreamWriter(outputPathbase + "_overoccupied_UMI_warnings.tab"))
            {
                writer.WriteLine("The following wells have more than " + UMICountLimit +
                                 " UMIs occupied at some position in the given genes, which may result in bad molecule count estimates.");
                writer.WriteLine("Sample\tBarcode\tGenes");
                int[] bcs = gfsByBcIdx.Keys.ToArray();
                Array.Sort(bcs);
                foreach (int bcIdx in bcs)
                {
                    writer.Write("{0}\t{1}\t", barcodes.WellIds[bcIdx], barcodes.Seqs[bcIdx]);
                    foreach (IFeature gf in gfsByBcIdx[bcIdx])
                        writer.Write(gf.Name + ", ");
                    writer.WriteLine();
                }
            }
        }
    }
}
