using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Keeps track on and merges overlapping intervals on a chromosome
    /// </summary>
    public class ChrIntervals
    {
        private int minFlank;
        private int maxIntronToKeep;
        private int minIntronFlank;
        private List<int> maskStarts = new List<int>();
        private List<int> maskEnds = new List<int>();

        public int Count { get { return maskStarts.Count; } }

        public ChrIntervals(int minFlank, int minIntronFlank, int maxIntronToKeep)
        {
            this.maxIntronToKeep = maxIntronToKeep;
            this.minIntronFlank = minIntronFlank;
            this.minFlank = minFlank;
        }

        /// <summary>
        /// Add an interval to the series. If it overlapswith others, join them.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public void Add(int start, int end)
        {
            int insStartIdx = 0;
            for (; insStartIdx < maskStarts.Count; insStartIdx++)
            {
                if (start <= maskEnds[insStartIdx])
                    break;
            }
            int insEndIdx = insStartIdx;
            for (; insEndIdx < maskStarts.Count; insEndIdx++)
            {
                if (end + 1 < maskStarts[insEndIdx])
                    break;
            }
            if (insStartIdx == insEndIdx)
            {
                maskStarts.Insert(insStartIdx, start);
                maskEnds.Insert(insEndIdx, end);
            }
            else
            {
                int newStart = Math.Min(start, maskStarts[insStartIdx]);
                int newEnd = Math.Max(end, maskEnds[insEndIdx - 1]);
                maskStarts.RemoveRange(insStartIdx, insEndIdx - insStartIdx);
                maskEnds.RemoveRange(insStartIdx, insEndIdx - insStartIdx);
                maskStarts.Insert(insStartIdx, newStart);
                maskEnds.Insert(insStartIdx, newEnd);
            }
        }

        /// <summary>
        /// Add all exon intervals from a gene to the collection.
        /// </summary>
        /// <param name="exonStarts"></param>
        /// <param name="exonEnds">inclusive end positions</param>
        public void Add(int[] exonStarts, int[] exonEnds)
        {
            int maskStart = exonStarts[0] - minFlank;
            for (int i = 0; i < exonStarts.Length - 1; i++)
            {
                int intronLen = exonStarts[i + 1] - exonEnds[i];
                if (intronLen > maxIntronToKeep)
                {
                    Add(maskStart, exonEnds[i] + 1 + minIntronFlank);
                    maskStart = exonStarts[i + 1] - minIntronFlank;
                }
            }
            Add(maskStart, exonEnds[exonEnds.Length - 1] + minFlank + 1);
        }

        public IEnumerable<Pair<int, int>> IterIntervals()
        {
            for (int i = 0; i < maskStarts.Count; i++)
                yield return new Pair<int, int>(maskStarts[i], maskEnds[i]);
        }
        /// <summary>
        /// Iterates the spaces between the intervals
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Pair<int, int>> IterSpaces()
        {
            if (maskStarts.Count == 0)
            {
                yield return new Pair<int, int>(0, int.MaxValue);
                yield break;
            }
            yield return new Pair<int, int>(0, maskStarts[0] - 1); 
            for (int i = 0; i < maskStarts.Count - 1; i++)
                yield return new Pair<int, int>(maskEnds[i], maskStarts[i + 1] - 1);
            yield return new Pair<int, int>(maskEnds[maskEnds.Count - 1], int.MaxValue);
        }
    }

    /// <summary>
    /// Use to mask with N:s the nucleotides of the chromosomes that are repeats (indicated by lower case acgt) 
    /// and do not correspond to transcripts in the genome. 5' and 3' UTR:s can be kept unmasked as well as short introns.
    /// </summary>
    public class NonExonRepeatMasker
    {
        public void Mask(StrtGenome genome, string outputFolder, int minFlank, int minIntronFlank, int maxIntronToKeep)
        {
            if (maxIntronToKeep < minIntronFlank * 2)
                maxIntronToKeep = minIntronFlank * 2;
            Dictionary<string, string> chrIdToFileMap = ReadChrData(genome, outputFolder);
            Dictionary<string, ChrIntervals> chrIntervals = new Dictionary<string, ChrIntervals>();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (!StrtGenome.IsASpliceAnnotationChr(chrId))
                    chrIntervals[chrId] = new ChrIntervals(minFlank, minIntronFlank, maxIntronToKeep);
            }
            DefineChrIntervals(genome, chrIntervals);
            foreach (string chrId in chrIntervals.Keys)
            {
                int nChanged = 0;
                string infile = chrIdToFileMap[chrId];
                StreamReader fastaReader = new StreamReader(infile);
                string outfile = Path.Combine(outputFolder, Path.GetFileName(infile));
                StreamWriter writer = new StreamWriter(outfile);
                string line = fastaReader.ReadLine();
                writer.WriteLine(line); // Header
                StringBuilder seqBuilder = new StringBuilder();
                line = fastaReader.ReadLine();
                seqBuilder.Append(line);
                int lineLen = line.Length;
                while ((line = fastaReader.ReadLine()) != null)
                    seqBuilder.Append(line);
                char[] seq = new char[seqBuilder.Length];
                seqBuilder.CopyTo(0, seq, 0, seqBuilder.Length);
                fastaReader.Close();
                int n = 0;
                foreach (Pair<int, int> nonExons in chrIntervals[chrId].IterSpaces())
                {
                    n++;
                    for (int idx = nonExons.First; idx <= Math.Min(seq.Length - 1, nonExons.Second); idx++)
                    {
                        if ("acgt".IndexOf(seq[idx]) >= 0)
                        {
                            seq[idx] = 'N';
                            nChanged++;
                        }
                    }
                }
                WriteMaskedSequence(writer, seq, lineLen);
                Console.WriteLine("Chr" + chrId + ": " + nChanged + " 'N':s added using " + chrIntervals[chrId].Count + " intervals. Outfile: " + outfile); 
            }
        }

        private void WriteMaskedSequence(StreamWriter writer, char[] seq, int lineLen)
        {
            char[] subseq = new char[lineLen];
            int i = 0;
            for (; i < seq.Length - lineLen; i += lineLen)
            {
                Array.Copy(seq, i, subseq, 0, lineLen);
                writer.WriteLine(new string(subseq));
            }
            Array.Copy(seq, i, subseq, 0, seq.Length - i);
            Array.Resize(ref subseq, seq.Length - i);
            writer.WriteLine(new string(subseq));
            writer.Close();
        }

        private static void DefineChrIntervals(StrtGenome genome, Dictionary<string, ChrIntervals> chrIntervals)
        {
            string tryAnnotationsPath = genome.MakeAnnotationsPath();
            string annotationsPath = PathHandler.ExistsOrGz(tryAnnotationsPath);
            if (annotationsPath == null)
                throw new NoAnnotationsFileFoundException("Could not find annotation file: " + tryAnnotationsPath + " (or .gz)");
            foreach (LocusFeature f in new UCSCAnnotationReader(genome).IterAnnotationFile(annotationsPath))
                if (chrIntervals.ContainsKey(f.Chr))
                {
                    GeneFeature gf = (GeneFeature)f;
                    chrIntervals[gf.Chr].Add(gf.ExonStarts, gf.ExonEnds);
                }
        }

        private static Dictionary<string, string> ReadChrData(StrtGenome genome, string outputFolder)
        {
            Dictionary<string, string> chrIdToFileMap = PathHandler.GetGenomeFilesMap(genome);
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chrId)) continue;
                string outfile = Path.Combine(outputFolder, Path.GetFileName(chrIdToFileMap[chrId]));
                if (File.Exists(outfile))
                    throw new Exception("First delete outfile " + outfile);
            }
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            return chrIdToFileMap;
        }

    }
}
