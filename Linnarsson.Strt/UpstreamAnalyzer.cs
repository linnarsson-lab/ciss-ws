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
        private int[,] upstreamEquals;
        private HashSet<string>[] hitsByBcIdx;
        private GenomeAnnotations Annotations;
        private Dictionary<string, int> barcodesWTSSeqMap;
        private Barcodes barcodes;

        public UpstreamAnalyzer(GenomeAnnotations annotations, Barcodes barcodes)
        {
            upstreamEquals = new int[barcodes.Count, barcodes.Count];
            upstreamTests = new int[barcodes.Count];
            Annotations = annotations;
            this.barcodes = barcodes;
            barcodesWTSSeqMap = barcodes.GetBcWTSSeqToBcIdxMap();
            hitsByBcIdx = new HashSet<string>[barcodes.Count];
            for (int i = 0; i < barcodes.Count; i++)
                hitsByBcIdx[i] = new HashSet<string>();
        }

        /// <summary>
        /// Use to analyze only exon annotated positions on molecule or read bases.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="currentBcIdx"></param>
        public void CheckSeqUpstreamTSSite(MappedTagItem item, int currentBcIdx)
        {
            if (item.hasAltMappings) return;
            CheckSeqUpstreamTSSite(item.chr, item.strand, item.hitStartPos, item.HitLen, currentBcIdx, item.MolCount);
        }
        /// <summary>
        /// Use to analyze raw reads
        /// </summary>
        /// <param name="mrm"></param>
        /// <param name="currentBcIdx"></param>
        public void CheckSeqUpstreamTSSite(MultiReadMapping mrm, int currentBcIdx)
        {
            CheckSeqUpstreamTSSite(mrm.Chr, mrm.Strand, mrm.Position, mrm.SeqLen, currentBcIdx, 1);
        }
        private void CheckSeqUpstreamTSSite(string chr, char strand, int pos, int readLen, int currentBcIdx, int count)
        {
            if (StrtGenome.IsSyntheticChr(chr) || !Annotations.HasChromosome(chr)) return;
            DnaSequence chrSeq = Annotations.ChromosomeSequences[chr];
            int l = barcodes.GetLengthOfBarcodesWithTSSeq();
            DnaSequence upSeq = null;
            if (strand == '+')
            {
                if (pos - l < 0) return;
                upSeq = chrSeq.SubSequence(pos - l, l);
            }
            else
            {
                if (pos + readLen + l > chrSeq.Count) return;
                upSeq = chrSeq.SubSequence(pos + readLen, l);
                upSeq.RevComp();
            }
            int upstreamBcIdx = -1;
            if (barcodesWTSSeqMap.TryGetValue(upSeq.ToString(), out upstreamBcIdx))
            {
                upstreamEquals[currentBcIdx, upstreamBcIdx] += count;
                if (currentBcIdx == upstreamBcIdx)
                {
                    string chrPos = chr + strand + pos.ToString();
                    hitsByBcIdx[upstreamBcIdx].Add(chrPos);
                }
            }
            upstreamTests[currentBcIdx] += count;
        }

        public void WriteUpstreamStats(string OutputPathbase)
        {
            string file = OutputPathbase + "_upstream_barcodeGGG_matches.tab";
            using (StreamWriter writer = new StreamWriter(file))
            {
                writer.WriteLine("ActualBarcode\t#AnalyzedCases");
                string[] actualBcs = barcodesWTSSeqMap.Keys.ToArray();
                foreach (string s in actualBcs)
                    writer.Write("\t{0}", s);
                writer.WriteLine("\tHits with same barcode upstream.");
                for (int actualBcIdx = 0; actualBcIdx < actualBcs.Length; actualBcIdx++)
                {
                    writer.Write("{0}\t{1}", actualBcs[actualBcIdx], upstreamTests[actualBcIdx]);
                    for (int foundBcIdx = 0; foundBcIdx < actualBcs.Length; foundBcIdx++)
                        writer.Write("\t{0}", upstreamEquals[actualBcIdx, foundBcIdx]);
                    writer.WriteLine("\t" + string.Join(",", hitsByBcIdx[actualBcIdx].ToArray()));
                }
            }
        }
    }
}
