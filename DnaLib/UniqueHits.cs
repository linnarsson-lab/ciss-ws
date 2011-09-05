using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Utilities;
using System.IO;

namespace Linnarsson.Dna
{
    public class UniqueHits
    {
        public int MaxReadCountOnChromosome = 10000000;
        public int MinValidHitCount = 1;
        private long[] barcodedPositions;
        private int[] totalHits;
        private int currIdx;
        public string Chromosome { get; set; }
        public DnaStrand Strand { get; set; }

        public UniqueHits(string selectedChromosome, DnaStrand selectedStrand)
        {
            Chromosome = selectedChromosome;
            Strand = selectedStrand;
            barcodedPositions = new long[MaxReadCountOnChromosome];
            totalHits = new int[Barcodes.STRT_v1.Length];
        }

        public void AddHit(string chr, DnaStrand strand, int chrPos, int bcodeIdx)
        {
            if (chr == Chromosome & strand == Strand)
            {
                barcodedPositions[currIdx++] = (chrPos << 8) | bcodeIdx;
                totalHits[bcodeIdx]++;
            }
        }

        public void WriteResult(string filenameBase)
        {
            Array.Resize(ref barcodedPositions, currIdx);
            Array.Sort(barcodedPositions);
            string file = filenameBase + "_unique_hits_" + Chromosome + ".txt";
            StreamWriter fw = file.OpenWrite();
            fw.WriteLine("Counts of barcode unique hit positions in genes on " + Chromosome);
            fw.WriteLine("Barcode\tBCUniquePos\tDistinctPos\tTotalReads");
            int [] uHits = GetBarcodeUniqueHitPos();
            int[] dHits = GetDistinctHitPosPerBarcode();
            for (int i = 0; i < uHits.Length; i++)
            {
                fw.WriteLine("{0}\t{1}\t{2}\t{3}", Barcodes.STRT_v1[i], uHits[i], dHits[i], totalHits[i]);
            }
            fw.Close();
        }

        private int[] GetDistinctHitPosPerBarcode()
        {
            int[] distinctPos = new int[Barcodes.STRT_v1.Length];
            long currHitPos = -1;
            int currBcodeIdx = -1;
            long hitPos = barcodedPositions[0] >> 8;
            int bcodeIdx = (int)(barcodedPositions[0] & 255);
            foreach (long v in barcodedPositions)
            {
                if (hitPos != currHitPos || bcodeIdx != currBcodeIdx)
                {
                    distinctPos[bcodeIdx]++;
                    currHitPos = hitPos;
                    currBcodeIdx = bcodeIdx;
                }
                hitPos = v >> 8;
                bcodeIdx = (int)(v & 255);
            }
            if (hitPos != currHitPos || bcodeIdx != currBcodeIdx)
                distinctPos[bcodeIdx]++;
            return distinctPos;
        }

        private int[] GetBarcodeUniqueHitPos()
        {
            int[] uniqueCounts = new int[Barcodes.STRT_v1.Length];
            int i = 0;
            while (i < currIdx)
            {
                long currHitPos = barcodedPositions[i] >> 8;
                int currBcodeIdx = (int)(barcodedPositions[i] & 255);
                int count = 1;
                i++;
                while (i < currIdx && (barcodedPositions[i] >> 8) == currHitPos
                                   && (barcodedPositions[i] & 255) == currBcodeIdx)
                {
                    count++;
                    i++;
                }
                if ((i == currIdx || (barcodedPositions[i] >> 8) != currHitPos)
                     && count >= MinValidHitCount) uniqueCounts[currBcodeIdx]++;
                if (i == currIdx) break;
                while (i < currIdx && (barcodedPositions[i] >> 8) == currHitPos)
                    i++;
            }
            return uniqueCounts;
        }
    }

}