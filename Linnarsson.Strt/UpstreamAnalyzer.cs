using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class UpstreamAnalyzer
    {
        private int[] upstreamTests;
        private int [,] upstreamEquals;
        private AbstractGenomeAnnotations Annotations;
        private Dictionary<string, int> barcodesWTSSeqMap;
        private Barcodes barcodes;

        public UpstreamAnalyzer(AbstractGenomeAnnotations annotations, Barcodes barcodes)
        {
            upstreamEquals = new int[barcodes.Count, barcodes.Count];
            upstreamTests = new int[barcodes.Count];
            Annotations = annotations;
            this.barcodes = barcodes;
            barcodesWTSSeqMap = barcodes.GetBcWTSSeqToBcIdxMap();
        }

        public void CheckSeqUpstreamTSSite(MultiReadMapping mrm, int currentBcIdx)
        {
            if (StrtGenome.IsSyntheticChr(mrm.Chr)) return;
            int l = barcodes.GetLengthOfBarcodesWithTSSeq();
            string actualSeq = barcodes.Seqs[mrm.BcIdx] + barcodes.TSSeq;
            DnaSequence chrSeq = Annotations.ChromosomeSequences[mrm.Chr];
            DnaSequence upSeq = null;
            if (mrm.Strand == '+')
            {
                if (mrm.Position - l < 0) return;
                upSeq = chrSeq.SubSequence(mrm.Position - l, actualSeq.Length);
            }
            else
            {
                if (mrm.Position + mrm.SeqLen + l > chrSeq.Count) return;
                upSeq = chrSeq.SubSequence(mrm.Position + mrm.SeqLen, l);
                upSeq.RevComp();
            }
            int upstreamBcIdx = -1;
            if (barcodesWTSSeqMap.TryGetValue(upSeq.ToString(), out upstreamBcIdx))
                upstreamEquals[currentBcIdx, mrm.BcIdx]++;
            upstreamTests[mrm.BcIdx]++;
        }

        public void WriteUpstreamStats(string fileNameBase)
        {
            string file = fileNameBase + "_upstream_barcodeGGG_matches.tab";
            using (StreamWriter writer = new StreamWriter(file))
            {
                writer.WriteLine("ActualBarcode\t#AnalyzedSingleReads");
                string[] actualBcs = barcodesWTSSeqMap.Keys.ToArray();
                foreach (string s in actualBcs)
                    writer.WriteLine("\t{0}", s);
                for (int actualBcIdx = 0; actualBcIdx < actualBcs.Length; actualBcIdx++)
                {
                    writer.WriteLine("{0}\t{1}", actualBcs[actualBcIdx], upstreamTests[actualBcIdx]);
                    for (int foundBcIdx = 0; foundBcIdx < actualBcs.Length; foundBcIdx++)
                        writer.WriteLine(upstreamEquals[actualBcIdx, foundBcIdx]);
                }
            }
        }
    }
}
