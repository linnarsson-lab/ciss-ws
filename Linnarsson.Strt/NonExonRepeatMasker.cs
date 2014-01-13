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

        /// <summary>
        /// holder for intervals on chromosome that should not have their repeats masked
        /// </summary>
        /// <param name="minFlank">save this amount of nts upstream and downstream of each gene</param>
        /// <param name="minIntronFlank">save this amount of nts on each side of every exon</param>
        /// <param name="maxIntronToKeep">introns smaller that this will be not be masked at all</param>
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
    /// Use to mask with N:s the nucleotides of the chromosomes that are repeats, indicated by lower case acgt in fasta sequences.
    /// Optionally keep repeats that correspond to transcripts in the genome, as well as 5' and 3' UTR:s and short introns.
    /// </summary>
    public class NonExonRepeatMasker
    {
        private int minFlank = 500;
        private int minIntronFlank = 50;
        private int maxIntronToKeep = 400;
        private RepeatMaskingType maskingType = Props.props.GenomeBuildRepeatMaskingType;

        public NonExonRepeatMasker()
        { }
        public NonExonRepeatMasker(int minFlank, int minIntronFlank, int maxIntronToKeep)
        {
            this.minFlank = minFlank;
            this.minIntronFlank = minIntronFlank;
            this.maxIntronToKeep = Math.Max(maxIntronToKeep, 2 * minIntronFlank);
        }

        public void Mask(StrtGenome genome, string outputFolder)
        {
            ReportMethod();
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            Dictionary<string, ChrIntervals> chrIntervals = SetupIntervals(genome, chrIdToFileMap);
            foreach (string chrId in chrIntervals.Keys)
            {
                string infile = chrIdToFileMap[chrId];
                StreamReader fastaReader = new StreamReader(infile);
                string headerLine = fastaReader.ReadLine();
                StringBuilder seqBuilder = new StringBuilder();
                string line = fastaReader.ReadLine();
                seqBuilder.Append(line);
                int lineLen = line.Length;
                while ((line = fastaReader.ReadLine()) != null)
                    seqBuilder.Append(line);
                fastaReader.Close();
                char[] seq = new char[seqBuilder.Length];
                seqBuilder.CopyTo(0, seq, 0, seqBuilder.Length);
                if (maskingType != RepeatMaskingType.None)
                {
                    ChrIntervals chrIvls = chrIntervals[chrId];
                    int nChanged = MaskByIntervals(seq, chrIvls);
                    Console.WriteLine("Chr{0}: {1} 'N':s added using {2} intervals.", chrId, nChanged, chrIvls.Count);
                }
                string outfile = Path.Combine(outputFolder, genome.MakeMaskedChrFileName(chrId));
                WriteSequence(outfile, headerLine, seq, lineLen);
            }
        }
         
        private Dictionary<string, ChrIntervals> SetupIntervals(StrtGenome genome, Dictionary<string, string> chrIdToFileMap)
        {
            Dictionary<string, ChrIntervals> chrIntervals = new Dictionary<string, ChrIntervals>();
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (!StrtGenome.IsASpliceAnnotation(chrId))
                    chrIntervals[chrId] = new ChrIntervals(minFlank, minIntronFlank, maxIntronToKeep);
            }
            if (maskingType == RepeatMaskingType.Exon)
                DefineProtectedChrIntervals(genome, chrIntervals);

            return chrIntervals;
        }

        private void ReportMethod()
        {
            switch (maskingType)
            {
                case RepeatMaskingType.Exon:
                    Console.WriteLine("*** Making STRT genome by masking non-exonic repeat sequences ***");
                    break;
                case RepeatMaskingType.All:
                    Console.WriteLine("*** Making STRT genome by masking all repeat sequences ***");
                    break;
                default:
                    Console.WriteLine("*** No masking of repeats is made ***");
                    break;
            }
        }

        private static int MaskByIntervals(char[] seq, ChrIntervals chrIvls)
        {
            int nChanged = 0;
            foreach (Pair<int, int> nonExons in chrIvls.IterSpaces())
            {
                for (int idx = nonExons.First; idx <= Math.Min(seq.Length - 1, nonExons.Second); idx++)
                {
                    if ("acgt".IndexOf(seq[idx]) >= 0)
                    {
                        seq[idx] = 'N';
                        nChanged++;
                    }
                }
            }
            return nChanged;
        }

        private void WriteSequence(string outfile, string headerLine, char[] seq, int lineLen)
        {
            using (StreamWriter writer = new StreamWriter(outfile))
            {
                writer.WriteLine(headerLine);
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
            }
        }

        /// <summary>
        /// Setup all exonic intervals that should be protected from repeat masking
        /// </summary>
        /// <param name="genome"></param>
        /// <param name="chrIntervals"></param>
        private static void DefineProtectedChrIntervals(StrtGenome genome, Dictionary<string, ChrIntervals> chrIntervals)
        {
            string tryAnnotationsPath = genome.MakeAnnotationsPath();
            string STRTAnnotationsPath = PathHandler.ExistsOrGz(tryAnnotationsPath);
            if (STRTAnnotationsPath == null)
                throw new Exception("Could not find annotation file: " + tryAnnotationsPath + " (or .gz)");
            foreach (LocusFeature f in AnnotationReader.IterSTRTAnnotationsFile(STRTAnnotationsPath))
                if (chrIntervals.ContainsKey(f.Chr))
                {
                    GeneFeature gf = (GeneFeature)f;
                    chrIntervals[gf.Chr].Add(gf.ExonStarts, gf.ExonEnds);
                }
        }

    }
}
