using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class CompactWiggle
    {
        private static long maxAverageChrHitCount = 2000000;
        private static int annotMask = 1 << 29; // Non-annotated positions are marked with this bit set
        int[] hitLengths = new int[Props.props.LargestPossibleReadLength];
        Dictionary<string, int[]> fwHits;
        Dictionary<string, int> fwHitIdx;
        Dictionary<string, int[]> revHits;
        Dictionary<string, int> revHitIdx;
        Dictionary<string, int> chrLengths;

        public CompactWiggle(Dictionary<string, int> chrIdToLength)
        {
            chrLengths = new Dictionary<string, int>();
            foreach (string chrId in chrIdToLength.Keys)
                if (chrId != StrtGenome.chrCTRLId) chrLengths[chrId] = chrIdToLength[chrId];
            int nChr = chrLengths.Count;
            fwHits = new Dictionary<string, int[]>(nChr);
            fwHitIdx = new Dictionary<string, int>(nChr);
            revHits = new Dictionary<string, int[]>(nChr);
            revHitIdx = new Dictionary<string, int>(nChr);
            long averageChrLen = 0;
            foreach (int l in chrLengths.Values) averageChrLen += l;
            averageChrLen /= nChr;
            Console.WriteLine("Setting up Wiggle for " + chrLengths.Keys.Count + " chromosomes.");
            foreach (string chr in chrLengths.Keys)
            {
                int chrLength = chrLengths[chr];
                int maxChrHitCount = (int)(maxAverageChrHitCount * chrLength / averageChrLen);
                fwHits[chr] = new int[maxChrHitCount/2];
                fwHitIdx[chr] = 0;
                revHits[chr] = new int[maxChrHitCount/2];
                revHitIdx[chr] = 0;
            }
        }

        public void AddHit(string chr, char strand, int start, int len, int weight, bool annotatedPosition)
        {
            if (!fwHitIdx.ContainsKey(chr)) return;
            hitLengths[len]++;
            int hitPos = (annotatedPosition) ? start : start | annotMask;
            if (strand == '+')
            {
                int idx = fwHitIdx[chr];
                int[] hits = fwHits[chr];
                if (idx < hits.Length)
                {
                    fwHitIdx[chr]++;
                    hits[idx] = hitPos;
                }
            }
            else
            {
                int idx = revHitIdx[chr];
                int[] hits = revHits[chr];
                if (idx < hits.Length)
                {
                    revHitIdx[chr]++;
                    hits[idx] = hitPos;
                }
            }
        }

        public int[] GetHitLengthCounts()
        {
            return hitLengths;
        }

        public double GetAverageHitLength()
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < hitLengths.Length; i++)
            {
                sum += hitLengths[i] * i;
                count += hitLengths[i];
            }
            return sum / count;
        }

        public void WriteHotspots(string file, bool annotatedPositions, int maxCount)
        {
            string midName = (annotatedPositions) ? "annotated" : "nonannotated";
            var writer = (file + "_" + midName + "_hotspots.tab").OpenWrite();
            writer.WriteLine("Positions with local maximal counts that have no corresponding annotations (gene or repeat). Samples < 5 bp apart not shown.");
            writer.WriteLine("Chr\tPosition\tStrand\tCoverage");
            int averageReadLength = (int)Math.Round(GetAverageHitLength());
			foreach (string chr in fwHits.Keys)
            {
                FindHotspots(writer, chr, '+', fwHits[chr], fwHitIdx[chr], averageReadLength, 
                             annotatedPositions, maxCount);
            }

            foreach (string chr in revHits.Keys)
            {
                FindHotspots(writer, chr, '-', revHits[chr], revHitIdx[chr], averageReadLength, 
                                  annotatedPositions, maxCount);
            }
            writer.Close();
        }

        private void FindHotspots(StreamWriter writer, string chr, char strand, int[] hits, int maxIdx,
                                       int averageReadLength, bool annotatedPositions, int maxCount)
        {
            int maskTest = (annotatedPositions)? 0 : annotMask;
            int chrLength = chrLengths[chr];
            int[] positions = new int[maxIdx];
            HotspotFinder hFinder = new HotspotFinder(maxCount);
            int pIdx = 0;
            for (int p = 0; p < maxIdx; p++)
                if ((hits[p] & annotMask) == maskTest)
                    positions[pIdx++] = hits[p] & ~annotMask; // Remove annotation info bit
            Array.Resize(ref positions, pIdx);
            Array.Sort(positions);
            List<int> stops = new List<int>();
            int lastHit = 0;
            int hitIdx = 0;
            int i = 0;
            while (i < chrLength && hitIdx < pIdx)
            {
                i = positions[hitIdx++];
                stops.Add(i + averageReadLength);
                while (i < chrLength && stops.Count > 0)
                {
                    while (hitIdx < pIdx && positions[hitIdx] == i)
                    {
                        hitIdx++;
                        stops.Add(i + averageReadLength);
                    }
                    i++;
                    if (stops.Count > 0 && i == stops[0])
                    {
                        if (i - lastHit >= 5)
                        {
                            lastHit = i;
                            hFinder.Add(stops.Count, i - (averageReadLength / 2));
                        }
                        while (stops.Count > 0 && i == stops[0]) stops.RemoveAt(0);
                    }
                }
            }
            int[] counts, locations;
            hFinder.GetTop(out counts, out locations);
            for (int cI = 0; cI < counts.Length; cI++)
            {
                int start = locations[cI];
                writer.WriteLine("{0}\t{1}\t{2}\t{3}", 
                                 chr, start + averageReadLength/2, strand, counts[cI]);
            }
        }

        public void WriteWriggle(string file)
        {
            int averageReadLength = (int)Math.Round(GetAverageHitLength());
			var writer = (file + "_fw.wig.gz").OpenWrite();
			writer.WriteLine("track type=wiggle_0 name=\"{0} (+)\" description=\"{0} (+)\" visibility=full",
				Path.GetFileNameWithoutExtension(file) );
			foreach(string chr in fwHits.Keys)
			{
                if (!StrtGenome.IsSyntheticChr(chr))
    				WriteWiggleStrand(writer, chr, fwHits[chr], fwHitIdx[chr], averageReadLength, 1);
			}
			writer.Close();
			writer = (file + "_rev.wig.gz").OpenWrite();
			writer.WriteLine("track type=wiggle_0 name=\"{0} (-)\" description=\"{0} (-)\" visibility=full",
				Path.GetFileNameWithoutExtension(file) );
			foreach(string chr in revHits.Keys)
			{
                WriteWiggleStrand(writer, chr, revHits[chr], revHitIdx[chr], averageReadLength, -1);
            }
			writer.Close();
		}

		private void WriteWiggleStrand(StreamWriter writer, string chr, int[] hits, int maxIdx,
                                       int averageReadLength, int multiplier)
		{
            int chrLength = chrLengths[chr];
            int[] positions = new int[maxIdx];
            for (int p = 0; p < maxIdx; p++)
                positions[p] = hits[p] & ~annotMask; // Remove annotation info bit
            Array.Sort(positions);
            Queue<int> stops = new Queue<int>();
            int hitIdx = 0;
			int i = 0;
			while (i < chrLength && hitIdx < maxIdx)
			{
                i = positions[hitIdx++];
                stops.Enqueue(i + averageReadLength);
				writer.WriteLine("fixedStep chrom=chr{0} start={1} step=1 span=1", chr, i+1);
				while (i < chrLength && stops.Count > 0)
				{
                    while (hitIdx < maxIdx && positions[hitIdx] == i)
                    {
                        hitIdx++;
                        stops.Enqueue(i + averageReadLength);
                    }
                    writer.WriteLine(stops.Count * multiplier);
                    i++;
                    while (stops.Count > 0 && i == stops.Peek()) stops.Dequeue();
                }
			}
		}

        private void OLD_WriteWiggleStrand(StreamWriter writer, string chr, int[] hits, int maxIdx,
                                       int averageReadLength, int multiplier)
        {
            int chrLength = chrLengths[chr];
            int[] positions = new int[maxIdx];
            for (int p = 0; p < maxIdx; p++)
                positions[p] = hits[p] & ~annotMask; // Remove annotation info bit
            Array.Sort(positions);
            List<int> stops = new List<int>();
            int hitIdx = 0;
            int i = 0;
            while (i < chrLength && hitIdx < maxIdx)
            {
                i = positions[hitIdx++];
                stops.Add(i + averageReadLength);
                writer.WriteLine("fixedStep chrom=chr{0} start={1} step=1 span=1", chr, i + 1);
                while (i < chrLength && stops.Count > 0)
                {
                    while (hitIdx < maxIdx && positions[hitIdx] == i)
                    {
                        hitIdx++;
                        stops.Add(i + averageReadLength);
                    }
                    writer.WriteLine(stops.Count * multiplier);
                    i++;
                    while (stops.Count > 0 && i == stops[0]) stops.RemoveAt(0);
                }
            }
        }

    }
}
