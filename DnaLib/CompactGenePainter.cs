using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    /// <summary>
    /// Methods to make various histograms of read/molecule distributions along a locus or transcript
    /// </summary>
    public class CompactGenePainter
    {
        /// <summary>
        /// Array overload that expands in case of indexing past end.
        /// </summary>
        private class LocusProfile
        {
            private ushort[] data;

            public LocusProfile(int initLen)
            {
                data = new ushort[initLen];
            }
            public ushort this[int i]
            {
                get { return (i >= data.Length) ? (ushort)0 : data[i]; }
                set
                {
                    if (i >= data.Length)
                    {
                        Array.Resize(ref data, Math.Max(i + 1000, data.Length * 2));
                    }
                    data[i] = value;
                }
            }
            public void Clear()
            {
                Array.Clear(data, 0, data.Length);
            }
            public int Length
            {
                get { return data.Length; }
            }
        }

        private static LocusProfile locusProfile = new LocusProfile(Props.props.MaxFeatureLength + 1);

        /// <summary>
        /// The constructed matrix is always in 'strand' (5' to 3') orientation relative to chromosome,
        /// ordered by position in first index and desired barcodes in second.
        /// </summary>
        /// <param name="gf">Gene to analyze</param>
        /// <param name="bcodeSortOrder">The barcodes in the desired order of rows of output.
        ///  If null, one row of totals will be returned.</param>
        /// <returns></returns>
        public static ushort[,] GetTranscriptImageData(GeneFeature gf, int[] bcodeSortOrder)
        {
            return GetTranscriptImageData(gf.LocusHits, gf.Strand, gf.ExonStarts, gf.ExonEnds,
                                          gf.GetLocusLength(), gf.LocusStart, bcodeSortOrder);
        }

        private static ushort[,] GetTranscriptImageData(int[] hits, char strand, int[] exonStarts, int[] exonEnds, 
                                                int locusLen, int offset, int[] bcodeSortOrder)
        {
            ushort[,] locImgData = GetLocusProfilesByBarcode(hits, locusLen, locusLen, strand, 1);
            int trLen = 0;
            for (int i = 0; i < exonEnds.Length; i++)
                trLen += exonEnds[i] - exonStarts[i] + 1;
            int rowIncr = 1;
            ushort[,] trImgData;
            if (bcodeSortOrder == null || bcodeSortOrder.Length == 0)
            {
                rowIncr = 0; // Add all barcoded data to a total in row 0
                trImgData = new ushort[trLen, 1];
                bcodeSortOrder = new int[Props.props.Barcodes.Count];
                for (int idx = 0; idx < bcodeSortOrder.Length; idx++)
                    bcodeSortOrder[idx] = idx;
            }
            else
            {
                trImgData = new ushort[trLen, bcodeSortOrder.Length];
            }
            int trPos = (strand == '+')? 0 : trLen - 1;
            int trDir = (strand == '+')? 1 : -1;
            for (int i = 0; i < exonEnds.Length; i++)
            {
                for (int p = exonStarts[i]; p <= exonEnds[i]; p++)
                {
                    int rowIdx = 0;
                    foreach (int bcodeIdx in bcodeSortOrder)
                    {
                        trImgData[trPos, rowIdx] += locImgData[p - offset, bcodeIdx];
                        rowIdx += rowIncr;
                    }
                    trPos += trDir;
                }
            }
            return trImgData;
        }

        /// <summary>
        /// Make a histogram of hits across the whole gene locus, using 1000 bins.
        /// </summary>
        /// <param name="gf"></param>
        /// <returns></returns>
        public static ushort[,] GetLocusImageData(GeneFeature gf)
        {
            return GetLocusProfilesByBarcode(gf.LocusHits, 1000, gf.GetLocusLength(), gf.Strand, 1);
        }

        /// <summary>
        /// Always makes profile in chromosome direction
        /// </summary>
        /// <param name="nSlots"></param>
        /// <param name="length"></param>
        /// <param name="strand"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        private static ushort[,] GetLocusProfilesByBarcode(int[] hits, int nSlots, int length, char strand, int weight)
        {
            ushort[,] imgData = new ushort[nSlots, Props.props.Barcodes.Count];
            double scaler = (double)nSlots / (double)length;
            int s = GeneFeature.GetStrandAsInt(strand);
            foreach (int hit in hits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    int bin = (int)(pos * scaler);
                    int bcodeIdx = (hit >> 1) & 127;
                    int val = imgData[bin, bcodeIdx] + weight;
                    if (val < ushort.MaxValue)
                        imgData[bin, bcodeIdx] = (ushort)val;
                }
            }
            return imgData;
        }

        /// <summary>
        /// Make hit profile from 5' to 3' end in transcript direction
        /// </summary>
        /// <param name="gf"></param>
        /// <returns></returns>
        public static ushort[] GetTranscriptProfile(GeneFeature gf)
        {
            return GetTranscriptProfile(gf, -1);
        }
        public static ushort[] GetTranscriptProfile(GeneFeature gf, int bcIdx)
        {
            MakeLocusProfile(gf.Strand, gf.LocusHits, bcIdx);
            int trLen = gf.GetTranscriptLength();
            ushort[] trImgData = new ushort[trLen];
            int trPos = (gf.Strand == '+') ? 0 : trLen - 1;
            int trDir = (gf.Strand == '+') ? 1 : -1;
            for (int i = 0; i < gf.ExonEnds.Length; i++)
            {
                for (int p = gf.ExonStarts[i]; p <= gf.ExonEnds[i]; p++)
                {
                    if (trPos < 0 || trPos == trImgData.Length)
                    {
                        Console.WriteLine("ERROR: CompactGenePainter.GetTranscriptProfile(gf):");
                        Console.WriteLine("  gene=" + gf.Name + "Start=" + gf.Start + " End=" + gf.End + " LocusStart=" + gf.LocusStart + " next trPos=" + trPos +
                                           " trLen=" + gf.GetTranscriptLength() + " next chrPos=" + p + " exonIdx=" + i);
                        trPos -= trDir;
                    }
                    trImgData[trPos] = locusProfile[p - gf.LocusStart];
                    trPos += trDir;
                }
            }
            return trImgData;
        }

        /// <summary>
        /// Return the fraction GC of the read-covered DNA seq and the corresponding reads
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="chrSeq"></param>
        /// <param name="readLength">average read length</param>
        /// <returns>len of read-covered DNA, frac GC of read-covered DNA, frac GC of reads (both may be biased 3' of spliced-out exons)</returns>
        public static double[] GetTranscriptFractionGC(GeneFeature gf, DnaSequence chrSeq, int readLength)
        {
            MakeLocusProfile(gf.Strand, gf.LocusHits, -1);
            int trLen = gf.GetTranscriptLength();
            long nDNAGC = 0, nDNATot = 0;
            long nReadGC = 0, nReadTot = 0;
            int trPos = 0;
            Queue<int> stops = new Queue<int>();
            for (int exonIdx = 0; exonIdx < gf.ExonEnds.Length; exonIdx++)
            {
                for (int chrPos = gf.ExonStarts[exonIdx]; chrPos <= gf.ExonEnds[exonIdx]; chrPos++)
                {
                    int nReadsStartingAtPos = locusProfile[chrPos - gf.LocusStart];
                    for (int cc = 0; cc < nReadsStartingAtPos; cc++)
                        stops.Enqueue(trPos + readLength);
                    int coverageAtPos = stops.Count();
                    while (stops.Count > 0 && trPos == stops.Peek())
                        stops.Dequeue();
                    trPos += 1;
                    if (coverageAtPos > 0)
                    {
                        char nt = chrSeq.GetNucleotide(chrPos);
                        nDNATot += 1;
                        nReadTot += coverageAtPos;
                        if (nt == 'G' || nt == 'C')
                        {
                            nDNAGC += 1;
                            nReadGC += coverageAtPos;
                        }
                    }
                }
            }
            return new double[] { nDNATot, nDNAGC / (double)nDNATot, nReadGC / (double) nReadTot };
        }



        /// <summary>
        /// Paints a genes's molecules/reads onto an array of positions relative to ivlStart. Paints data from all barcodes if bcIdx==-1.
        /// </summary>
        /// <param name="gf">Gene to paint from</param>
        /// <param name="ivlStart"></param>
        /// <param name="ivlEnd"></param>
        /// <param name="bcIdx">Either specific barcode or -1</param>
        /// <param name="averageReadLen">To get paint stroke lengths correct</param>
        /// <returns>Hit counts at each position from ivlStart to ivlEnd, inclusive.</returns>
        public static int[] PaintHitsInInterval(GeneFeature gf, int ivlStart, int ivlEnd, int bcIdx, int averageReadLen)
        {
            int[] profile = new int[1 + ivlEnd - ivlStart];
            int bcMask = (bcIdx >= 0) ? 127 : 0;
            if (bcIdx == -1) bcIdx = 0;
            int s = GeneFeature.GetStrandAsInt(gf.Strand);
            int startOffset = - averageReadLen / 2;
            int endOffset = (averageReadLen - 1) / 2;
            foreach (int hit in gf.LocusHits)
            {
                int hitBcIdx = (hit >> 1) & 127;
                int hitMidPos = (hit >> 8) + gf.LocusStart;
                if ((hit & 1) == s && (hitBcIdx & bcMask) == bcIdx)
                {
                    int paintStart = Math.Max(ivlStart, hitMidPos + startOffset) - ivlStart;
                    int paintEnd = Math.Min(ivlEnd, hitMidPos + endOffset) - ivlStart;
                    for (int p = paintStart; p <= paintEnd; p++)
                        profile[p]++;
                }
            }
            return profile;
        }

        private static void MakeLocusProfile(char chrStrand, int[] hits, int bcIdx)
        {
            locusProfile.Clear();
            int s = GeneFeature.GetStrandAsInt(chrStrand);
            foreach (int hit in hits)
            {
                if ((hit & 1) == s && (bcIdx == -1 || ((hit >> 1) & 127) == bcIdx))
                {
                    int pos = hit >> 8;
                    if (locusProfile[pos] < ushort.MaxValue)
                        locusProfile[pos]++;
                }
            }
        }

        /// <summary>
        /// Makes histogram with given binSize of hits to gene locus. Data is always 5'->3' in transcript orientation.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="chrStrand">Strand on chr to pick counts from</param>
        /// <param name="binSize"></param>
        /// <param name="histo"></param>
        /// <returns>Index of last bin in histo with data</returns>
        public static int MakeLocusHistogram(GeneFeature gf, char chrStrand, int binSize, ref int[] histo)
        {
            Array.Clear(histo, 0, histo.Length);
            int locusLen = gf.GetLocusLength();
            bool inChrDir = (gf.Strand == '+');
            int s = GeneFeature.GetStrandAsInt(chrStrand);
            foreach (int hit in gf.LocusHits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    int bin = inChrDir ? (pos / binSize) : (locusLen - pos) / binSize;
                    histo[bin]++;
                }
            }
            int maxBin = histo.Length - 1;
            while (maxBin > 0 && histo[maxBin] == 0) maxBin--;
            return maxBin;
        }

        /// <summary>
        /// This method requires that gf.LocusHits come out sorted by position
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="strand"></param>
        /// <returns>Positions hit within transcript</returns>
        public static List<int> GetLocusHitPositions(GeneFeature gf, char strand)
        {
            List<int> hitPositions = new List<int>();
            int s = GeneFeature.GetStrandAsInt(strand);
            int lastPos = -1;
            foreach (int hit in gf.LocusHits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    if (pos != lastPos)
                    {
                        hitPositions.Add(pos);
                        lastPos = pos;
                    }
                }
            }
            return hitPositions;
        }

        /// <summary>
        /// Make an per-barcode array of counts of hits to the transcript
        /// in the region defined by trFrom - trTo.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="trFrom">Inclusive start position in transcript coordinates</param>
        /// <param name="trTo">Inclusive end position in transcript coordinates</param>
        /// <returns></returns>
        public static int[] GetBarcodedTranscriptCounts(GeneFeature gf, int trFrom, int trTo)
        {
            int chrFrom = gf.GetChrPos(trFrom);
            int chrTo = gf.GetChrPos(trTo);
            if (chrFrom > chrTo)
            { int temp = chrFrom; chrFrom = chrTo; chrTo = temp; }
            int[] counts = new int[96];
            int s = gf.GetStrandAsInt();
            foreach (int hit in gf.LocusHits)
            {
                if ((hit & 1) == s)
                {
                    int chrHitPos = (hit >> 8) + gf.LocusStart;
                    if (chrHitPos >= chrFrom && chrHitPos <= chrTo)
                    {
                        for (int i = 0; i < gf.ExonCount; i++)
                        {
                            if (chrHitPos >= gf.ExonStarts[i] && chrHitPos <= gf.ExonEnds[i])
                            {
                                int bcodeIdx = (hit >> 1) & 127;
                                counts[bcodeIdx]++;
                                break;
                            }
                        }
                    }
                }
            }
            return counts;
        }

        /// <summary>
        /// Makes histogram data of hits to gf transcript within each equally large section of size binSize.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="binSize"></param>
        /// <param name="senseOnly">If true, only hits to transcript sense will be counted</param>
        /// <param name="readLen">Used to exclude the transcript ends, where no hitMids can occur, from the bins</param>
        /// <returns>histogram of counts</returns>
        public static int[] GetBinnedTrHitsRelStart(GeneFeature gf, double binSize, bool senseOnly, int readLen)
        {
            return GetBinnedTrHitsRelStart(gf, binSize, senseOnly, readLen, -1);
        }
        public static int[] GetBinnedTrHitsRelStart(GeneFeature gf, double binSize, bool senseOnly, int readLen, int bcIdx)
        {
            char strand = (senseOnly) ? gf.Strand : '.';
            return GetIvlSpecificCountsInBinsRelStart(gf.LocusHits, strand, binSize, readLen,
                                                    gf.ExonStarts, gf.ExonEnds, gf.LocusStart, bcIdx);
        }

        /// <summary>
        /// Make histogram of hits in bins relative to a start point,
        /// for hits within given intervals. Distance is calculated using these intervals,
        /// so that e.g. position within transcript is used if intervals define the exons.
        /// Intervals should be in order.
        /// </summary>
        /// <param name="hits"></param>
        /// <param name="strand">'+', '-', or '.' for both strands</param>
        /// <param name="binSize">size of bins</param>
        /// <param name="readLen">takes care of that hits are mid positions, not start positions of reads</param>
        /// <param name="starts">starts of intervals from which data should be collected</param>
        /// <param name="ends">ends of intervals from which data should be collected</param>
        /// <param name="offset">reference point for intervals. Has to be consistent with pos in MarkHit() calls</param>
        /// <param name="bcIdx">Specific barcodeIdx or -1 for summation over all</param>
        /// <returns>histogram of counts</returns>
        private static int[] GetIvlSpecificCountsInBinsRelStart(int[] hits, char strand, double binSize, int readLen,
                                                     int[] starts, int[] ends, int offset, int bcIdx)
        {
            if (hits.Length == 0) return new int[0];
            int[] rightIvlsLen = new int[ends.Length];
            int accuLen = 0;
            for (int i = starts.Length - 1; i >= 0; i--)
            {
                rightIvlsLen[i] = accuLen;
                accuLen += ends[i] - starts[i] + 1;
            }
            if (accuLen <= readLen) return new int[0];
            int nBins = (int)Math.Ceiling((accuLen - readLen) / binSize);
            int[] result = new int[nBins];
            int s = (strand == '-') ? 1 : 0;
            int strandMask = (strand != '.') ? 1 : 0;
            int dist;
            int leftIvlsLen = 0;
            int idx = 0;
            for (int i = 0; i < starts.Length; i++)
            {
                int ivlStart = starts[i] - offset;
                int ivlEnd = ends[i] - offset;
                int from = ivlStart << 8;
                int to = (ivlEnd << 8) | 255;
                idx = Array.FindIndex(hits, idx, (v) => (v >= from));
                if (idx == -1) break;
                for (; idx < hits.Length; idx++)
                {
                    int hit = hits[idx];
                    if (hit > to) break;
                    if ((hit & strandMask) == s)
                    {
                        if (bcIdx == -1 || ((hit >> 1) & 127) == bcIdx)
                        {
                            int pos = hit >> 8;
                            if ((hit & 1) == 0) // (strand == '+')
                            {
                                dist = leftIvlsLen + (pos - ivlStart);
                            }
                            else
                            {
                                dist = rightIvlsLen[i] + (ivlEnd - pos);
                            }
                            int bin = Math.Max(0, Math.Min(nBins - 1, (int)Math.Floor((dist - readLen / 2) / binSize)));
                            result[bin]++;
                        }
                    }
                }
                leftIvlsLen += ends[i] - starts[i] + 1;
            }
            return result;
        }

        /// <summary>
        /// Make an array of hit counts to each of the exons in the transcript
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="senseOnly"></param>
        /// <returns></returns>
        public static int[] GetCountsPerExon(GeneFeature gf, bool senseOnly)
        {
            char strand = (senseOnly) ? gf.Strand : '.';
            return GetCountsPerInterval(gf.LocusHits, strand, gf.ExonStarts, gf.ExonEnds, gf.LocusStart);
        }

        /// <summary>
        /// The first and second values are for USTR and DSTR.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="senseOnly">True to only count on sense strand, otherwise adds anti-sense counts</param>
        /// <returns>Array of counts on USTR, DSTR, and intron 1,2,...</returns>
        public static int[] GetCountsPerIntron(GeneFeature gf, bool senseOnly)
        {
            char strand;
            int[] intronStarts;
            int[] intronEnds;
            GetIntronIvls(gf, senseOnly, out strand, out intronStarts, out intronEnds);
            return GetCountsPerInterval(gf.LocusHits, strand, intronStarts, intronEnds, gf.LocusStart);
        }

        /// <summary>
        /// Assumes intervals are non-overlapping and in order
        /// </summary>
        /// <param name="hits">LocusHits array from a GeneFeature.</param>
        /// <param name="strand">'+', '-', or '.' for both strands</param>
        /// <param name="starts">Start+offset positions in locus of intervals</param>
        /// <param name="ends">End+offset positions of intervals</param>
        /// <param name="offset">Offset between locus first pos and starts/ends (usually gene MatchStart)</param>
        /// <returns></returns>
        private static int[] GetCountsPerInterval(int[] hits, char strand, int[] starts, int[] ends, int offset)
        {
            int[] result = new int[starts.Length];
            int s = (strand != '.') ? GeneFeature.GetStrandAsInt(strand) : 0;
            int strandMask = (strand != '.') ? 1 : 0;
            for (int i = 0; i < starts.Length; i++)
            {
                int count = 0;
                int from = (starts[i] - offset) << 8;
                int to = ((ends[i] - offset) << 8) | 255;
                int idx = Array.FindIndex(hits, (v) => (v >= from));
                if (idx == -1) continue;
                for (; idx < hits.Length; idx++)
                {
                    int hit = hits[idx];
                    if (hit > to) break;
                    if ((hit & strandMask) == s)
                        count++;
                }
                result[i] = count;
            }
            return result;
        }

        /// <summary>
        /// Calculate hits per exon in each barcode.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="senseOnly"></param>
        /// <param name="nBarcodes"></param>
        /// <param name="result">counts[barcodeIdx, exonIdx along chromosome]</param>
        public static int GetCountsPerExonAndBarcode(GeneFeature gf, bool senseOnly, int nBarcodes, ref int[,] result)
        {
            char strand = (senseOnly) ? gf.Strand : '.';
            MakeCountsPerIntervalAndBarcode(gf.LocusHits, strand, gf.ExonStarts, gf.ExonEnds, gf.LocusStart, nBarcodes, ref result);
            return gf.ExonCount;
        }

        /// <summary>
        /// Calculate hits per intron, putting upstream and downstream first, in each barcode.
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="senseOnly">False to include also counts at anti-sense</param>
        /// <param name="nBarcodes"></param>
        /// <param name="result">counts[barcodeIdx, intronIdx along chromosome]</param>
        public static int GetCountsPerIntronAndBarcode(GeneFeature gf, bool senseOnly, int nBarcodes, ref int[,] result)
        {
            char strand;
            int[] intronStarts;
            int[] intronEnds;
            GetIntronIvls(gf, senseOnly, out strand, out intronStarts, out intronEnds);
            MakeCountsPerIntervalAndBarcode(gf.LocusHits, strand, intronStarts, intronEnds, gf.LocusStart, nBarcodes, ref result);
            return intronStarts.Length;
        }

        private static void GetIntronIvls(GeneFeature gf, bool senseOnly, out char strand, out int[] intronStarts, out int[] intronEnds)
        {
            strand = (senseOnly) ? gf.Strand : '.';
            intronStarts = new int[gf.ExonCount + 1];
            intronEnds = new int[gf.ExonCount + 1];
            intronStarts[0] = gf.LocusStart;
            intronEnds[0] = gf.ExonStarts[0] - 1;
            intronStarts[1] = gf.ExonEnds[gf.ExonEnds.Length - 1] + 1;
            intronEnds[1] = gf.LocusEnd;
            for (int i = 1; i < gf.ExonCount; i++)
            {
                intronStarts[i + 1] = gf.ExonEnds[i - 1] + 1;
                intronEnds[i + 1] = gf.ExonStarts[i] - 1;
            }
        }

        private static void MakeCountsPerIntervalAndBarcode(int[] hits, char strand, int[] starts, int[] ends, int offset,
                                                             int nBarcodes, ref int[,] result)
        {
            Array.Clear(result, 0, result.Length);
            int s = (strand != '.') ? GeneFeature.GetStrandAsInt(strand) : 0;
            int strandMask = (strand != '.') ? 1 : 0;
            for (int ivlIdx = 0; ivlIdx < starts.Length; ivlIdx++)
            {
                int from = (starts[ivlIdx] - offset) << 8;
                int to = ((ends[ivlIdx] - offset) << 8) | 255;
                int idx = Array.FindIndex(hits, (v) => (v >= from));
                if (idx == -1) continue;
                for (; idx < hits.Length; idx++)
                {
                    int hit = hits[idx];
                    if (hit > to) break;
                    if ((hit & strandMask) == s)
                    {
                        int bcIdx = (hit >> 1) & 127;
                        result[bcIdx, ivlIdx]++;
                    }
                }
            }
        }

        /// <summary>
        /// Search for a region from start up to end that has a distinct peak of hits.
        /// </summary>
        /// <param name="strand">Strand to search on</param>
        /// <param name="searchStartLocusPos">Start position in locus for search</param>
        /// <param name="dir">Direction to search in, either +1 or -1</param>
        /// <param name="searchEndLocusPos">Stop position of search</param>
        /// <returns>The position on direction side of peak, or -1 if none found.</returns>
        private static int FindHotspotStart(int[] hits, char strand, int searchStartLocusPos, int dir, 
                                           int searchEndLocusPos, int optimalMinCount)
        {
            int minPeakCount = 20;
            int maxClearCount = 1;
            int peakWindowSize = 10;
            int clearWindowSize = 10;
            if (Math.Abs(searchEndLocusPos - searchStartLocusPos) < (peakWindowSize + clearWindowSize))
                return -1;
            MakeLocusProfile(strand, hits, -1);
            int pWCount = 0;
            int cWCount = 0;
            int pWLastPos = searchStartLocusPos;
            int cWLastPos = searchStartLocusPos + dir * peakWindowSize;
            int cWNextPos = searchStartLocusPos + dir * (peakWindowSize + clearWindowSize);
            for (int p = pWLastPos; p != cWLastPos && p > 0 && p < locusProfile.Length - 1; p += dir)
                pWCount += locusProfile[p];
            for (int p = cWLastPos; p < cWNextPos && p > 0 && p < locusProfile.Length - 1; p += dir)
                cWCount += locusProfile[p];
            int acceptablePosition = -1;
            while (cWNextPos != searchEndLocusPos)
            {
                if (pWCount >= minPeakCount && cWCount <= maxClearCount)
                {
                    if (pWCount >= optimalMinCount)
                        return cWLastPos;
                    if (acceptablePosition == -1)
                        acceptablePosition = cWLastPos;
                }
                pWCount = pWCount + locusProfile[cWLastPos] - locusProfile[pWLastPos];
                cWCount = cWCount + locusProfile[cWNextPos] - locusProfile[cWLastPos];
                pWLastPos += dir;
                cWLastPos += dir;
                cWNextPos += dir;
                if (pWLastPos < 0 || cWLastPos < 0 || cWNextPos < 0 ||
                    pWLastPos >= locusProfile.Length || cWLastPos >= locusProfile.Length || cWNextPos >= locusProfile.Length)
                    break;
            }
            return acceptablePosition;
        }

        public static int FindUpstreamHotspotStart(GeneFeature gf, int flankHits)
        {
            int maxDistance = 200;
            int hotspotStart = -1;
            int minCount = flankHits / 2;
            if (gf.Strand == '+')
            {
                int searchEndPos = GeneFeature.LocusFlankLength - Math.Min(gf.LeftFlankLength, maxDistance);
                hotspotStart = FindHotspotStart(gf.LocusHits, gf.Strand, GeneFeature.LocusFlankLength - 1, -1, searchEndPos, minCount);
            }
            else
            {
                int searchEndPos = gf.End - gf.LocusStart + Math.Min(gf.LeftFlankLength, maxDistance);
                hotspotStart = FindHotspotStart(gf.LocusHits, gf.Strand, gf.End - gf.LocusStart + 1, 1, searchEndPos, minCount);
            }
            if (hotspotStart >= 0) hotspotStart += gf.LocusStart; // convert to Chr position
            return hotspotStart;
        }

    }
}
