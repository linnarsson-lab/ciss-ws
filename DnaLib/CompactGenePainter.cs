using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.Dna
{
    public class CompactGenePainter
    {
        private static ushort[] locusProfile;

        public static void SetMaxLocusLen(int maxLocusLen)
        {
            locusProfile = new ushort[maxLocusLen];
        }

        public static ushort[,] GetTranscriptImageData(GeneFeature gf, int[] bcodeSortOrder)
        {
            return GetTranscriptImageData(gf.LocusHits, gf.Strand, gf.ExonStarts, gf.ExonEnds,
                                          gf.GetLocusLength(), gf.LocusStart, bcodeSortOrder);
        }

        /// <summary>
        /// The constructed matrix is always in 'strand' (5' to 3') orientation relative to chromosome,
        /// ordered by position in first index and desired barcodes in second.
        /// </summary>
        /// <param name="strand">Orientation of the transcript on chromosome</param>
        /// <param name="exonStarts"></param>
        /// <param name="exonEnds"></param>
        /// <param name="locusLen"></param>
        /// <param name="offset"></param>
        /// <param name="bcodeSortOrder">The barcodes in the desired order of rows of output.
        ///  If empty, one row of totals will be returned.</param>
        /// <returns></returns>
        private static ushort[,] GetTranscriptImageData(int[] hits, char strand, int[] exonStarts, int[] exonEnds, 
                                                int locusLen, int offset, int[] bcodeSortOrder)
        {
            ushort[,] locImgData = GetGeneImageData(hits, locusLen, locusLen, strand, 1);
            int trLen = 0;
            for (int i = 0; i < exonEnds.Length; i++)
                trLen += exonEnds[i] - exonStarts[i] + 1;
            int rowIncr = 1;
            ushort[,] trImgData;
            if (bcodeSortOrder == null)
            {
                rowIncr = 0; // Add all barcoded data to a total in row 0
                trImgData = new ushort[trLen, 1];
                bcodeSortOrder = new int[Barcodes.MaxCount];
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

        public static ushort[,] GetGeneImageData(GeneFeature gf)
        {
            return GetGeneImageData(gf.LocusHits, 1000, gf.GetLocusLength(), gf.Strand, 1);
        }

        /// <summary>
        /// Always makes profile in chromosome direction
        /// </summary>
        /// <param name="nSlots"></param>
        /// <param name="length"></param>
        /// <param name="strand"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        private static ushort[,] GetGeneImageData(int[] hits, int nSlots, int length, char strand, int weight)
        {
            ushort[,] imgData = new ushort[nSlots, Barcodes.MaxCount];
            double scaler = (double)nSlots / (double)length;
            int s = (strand == '+') ? 0 : 1;
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

        private static void MakeLocusHitProfile(char strand, int[] hits)
        {
            Array.Clear(locusProfile, 0, locusProfile.Length);
            int s = (strand == '+') ? 0 : 1;
            foreach (int hit in hits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    if (locusProfile[pos] < ushort.MaxValue)
                        locusProfile[pos]++;
                }
            }
        }

        public static int[] GetBarcodedTranscriptCounts(GeneFeature gf, int trFrom, int trTo)
        {
            int chrFrom = gf.GetChrPos(trFrom);
            int chrTo = gf.GetChrPos(trTo);
            if (chrFrom > chrTo)
            { int temp = chrFrom; chrFrom = chrTo; chrTo = temp; }
            int[] counts = new int[96];
            int s = (gf.Strand == '+') ? 0 : 1;
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
        /// <returns></returns>
        public static int[] GetBinnedTranscriptHitsRelEnd(GeneFeature gf, double binSize, bool senseOnly, int readLen)
        {
            char strand = (senseOnly) ? gf.Strand : '.';
            return GetIvlSpecificCountsInBinsRelEnd(gf.LocusHits, strand, binSize, readLen,
                                                    gf.ExonStarts, gf.ExonEnds, gf.LocusStart);
        }

        /// <summary>
        /// Make histogram of hits in bins relative to an end point,
        /// for hits within given intervals. Distance is calculated using these intervals,
        /// so that e.g. position within transcript is used if intervals define the exons.
        /// Intervals should be in order.
        /// </summary>
        /// <param name="strand">'+', '-', or '.' for both strands</param>
        /// <param name="binSize"></param>
        /// <param name="starts">starts of intervals</param>
        /// <param name="ends">ends of intervals</param>
        /// <param name="offset">reference point for intervals. Has to be consistent with pos in MarkHit() calls</param>
        /// <returns>histogram of counts</returns>
        private static int[] GetIvlSpecificCountsInBinsRelEnd(int[] hits, char strand, double binSize, int readLen,
                                                     int[] starts, int[] ends, int offset)
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
                        int pos = hit >> 8;
                        if ((hit & 1) == 0) // (strand == '+')
                        {
                            dist = rightIvlsLen[i] + (ivlEnd - pos);
                        }
                        else
                        {
                            dist = leftIvlsLen + (pos - ivlStart);
                        }
                        int bin = Math.Max(0, Math.Min(nBins - 1, (int)Math.Floor((dist - readLen/2) / binSize)));
                        result[bin]++;
                    }
                }
                leftIvlsLen += ends[i] - starts[i] + 1;
            }
            return result;
        }

        public static int[] GetCountsPerExon(GeneFeature gf, bool senseOnly)
        {
            char strand = (senseOnly) ? gf.Strand : '.';
            return GetCountsPerInterval(gf.LocusHits, strand, gf.ExonStarts, gf.ExonEnds, gf.LocusStart);
        }

        /// <summary>
        /// Assumes intervals are non-overlapping and in order
        /// </summary>
        /// <param name="strand">'+', '-', or '.' for both strands</param>
        /// <param name="starts">Start+offset positions in locus of intervals</param>
        /// <param name="ends">End+offset positions of intervals</param>
        /// <param name="offset">Offset between locus first pos and starts/ends (usually gene MatchStart)</param>
        /// <returns></returns>
        private static int[] GetCountsPerInterval(int[] hits, char strand, int[] starts, int[] ends, int offset)
        {
            int[] result = new int[starts.Length];
            int s = (strand == '-') ? 1 : 0;
            int strandMask = (strand != '.') ? 1 : 0;
            int idx = 0;
            for (int i = 0; i < starts.Length; i++)
            {
                int count = 0;
                int from = (starts[i] - offset) << 8;
                int to = ((ends[i] - offset) << 8) | 255;
                idx = Array.FindIndex(hits, idx, (v) => (v >= from));
                if (idx == -1) break;
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

        public static int[] GetHitPositions(GeneFeature gf, char strand)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            int s = (strand == '+') ? 0 : 1;
            foreach (int hit in gf.LocusHits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    if (!counts.ContainsKey(pos))
                        counts[pos] = 1;
                    else
                        counts[pos]++;
                }
            }
            return counts.Keys.ToArray<int>();
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
            int maxClearCount = 1; // 0;
            int peakWindowSize = 10;
            int clearWindowSize = 10;
            if (Math.Abs(searchEndLocusPos - searchStartLocusPos) < (peakWindowSize + clearWindowSize))
                return -1;
            MakeLocusHitProfile(strand, hits);
            int pWCount = 0;
            int cWCount = 0;
            int pWLastPos = searchStartLocusPos;
            int cWLastPos = searchStartLocusPos + dir * peakWindowSize;
            int cWNextPos = searchStartLocusPos + dir * (peakWindowSize + clearWindowSize);
            for (int p = pWLastPos; p != cWLastPos; p += dir)
                pWCount += locusProfile[p];
            //int sumCount = pWCount;
            for (int p = cWLastPos; p < cWNextPos; p += dir)
                cWCount += locusProfile[p];
            int acceptablePosition = -1;
            while (cWNextPos != searchEndLocusPos)
            {
                if (pWCount >= minPeakCount && cWCount <= maxClearCount)
                {
                    if (pWCount >= optimalMinCount) // (sumCount >= optimalMinCount)
                        return cWLastPos;
                    if (acceptablePosition == -1)
                        acceptablePosition = cWLastPos;
                }
                pWCount = pWCount + locusProfile[cWLastPos] - locusProfile[pWLastPos];
                cWCount = cWCount + locusProfile[cWNextPos] - locusProfile[cWLastPos];
                pWLastPos += dir;
                cWLastPos += dir;
                cWNextPos += dir;
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

        public static int[] GetLocusBinCountsRel3PrimeEnd(GeneFeature gf, char chrStrand)
        {
            if (gf.Strand == '+')
                return GetCountsInBins(gf.LocusHits, chrStrand, GeneFeature.LocusProfileBinSize, true, gf.End - gf.LocusStart);
            else
                return GetCountsInBins(gf.LocusHits, chrStrand, GeneFeature.LocusProfileBinSize, false, GeneFeature.LocusFlankLength);
        }

        /// <summary>
        /// Makes a histogram of counts
        /// </summary>
        /// <param name="strand">Strand of chromosome to analyze</param>
        /// <param name="binSize"></param>
        /// <param name="relativeToEndPos">If true, bins go from right to left on chromosome</param>
        /// <param name="offset">If relativeToEndPos==false, the first bin will start at offset into locus.
        ///                      If relativeToEndPos==true, the first bin will end at offset</param>
        /// <returns></returns>
        private static int[] GetCountsInBins(int[] hits, char chrStrand, int binSize,
                                             bool relativeToEndPos, int offset)
        {
            if (hits.Length == 0) return new int[0];
            int maxPos = (relativeToEndPos) ? (offset - (hits[0] >> 8)) : hits[hits.Length - 1] >> 8;
            int nBins = 1 + maxPos / binSize;
            if (nBins <= 0) return new int[0]; // No hits on right side of offset.
            int[] result = new int[nBins];
            int s = (chrStrand == '+') ? 0 : 1;
            foreach (int hit in hits)
            {
                if ((hit & 1) == s)
                {
                    int pos = hit >> 8;
                    if (relativeToEndPos) pos = offset - pos;
                    else pos = pos - offset;
                    int bin = pos / binSize;
                    if (bin >= 0)
                        result[bin]++;
                }
            }
            return result;
        }

    }
}
