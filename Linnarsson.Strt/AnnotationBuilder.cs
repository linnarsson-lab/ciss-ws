﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{

    public class ExonCombination
    {
        /// <summary>
        /// 0-based idx of exons used in the junction, counting from 5' of chromosome.
        /// </summary>
        public List<int> ExonIndexes { get; set; }
        /// <summary>
        /// Total length of the middle exons if junction has >= 3 exons, otherwise 0.
        /// </summary>
        public int InternalExonsLen { get; set; }
        /// <summary>
        /// Total length of all exons in the junction (May be more than final junction length, if flank exons are truncated.)
        /// </summary>
        public int TotalLen { get; set; }
        /// <summary>
        /// Exact start position of each junction section ([partial] first or other exon) in the real chromosome
        /// </summary>
        public List<int> StartsInRealChr { get; set; }
        /// <summary>
        /// Start position of each junction section in the final junction
        /// </summary>
        public List<int> StartsInJunction { get; set; }
        /// <summary>
        /// Symbol of the exon combination, e.g. "3-5-6". Actual gene exon numbers.
        /// </summary>
        public string ExonsString { get; set; }
        /// <summary>
        /// Final sequence of the junction
        /// </summary>
        public DnaSequence Seq { get; set; }

        public ExonCombination(List<int> exonIndexes, int totalLen, int internalExonsLen)
        {
            ExonIndexes = exonIndexes;
            TotalLen = totalLen;
            InternalExonsLen = internalExonsLen;
        }
    }

    public class ExonCombinationGenerator
    {
        private static void MakeExonCombContinuations(int matchableLen, int totLen, int internalLen, int maxSkip, List<DnaSequence> exons,
                                                      int nextExonIdx, List<int> exonIndexes, List<ExonCombination> results)
        {
            int imax = Math.Min(nextExonIdx + maxSkip, exons.Count);
            for (int i = nextExonIdx; i < imax; i++)
            {
                List<int> nextExonIndexes = new List<int>(exonIndexes);
                nextExonIndexes.Add(i);
                int nextExonLen = (int)exons[i].Count;
                int newTotLen = totLen + nextExonLen;
                if (newTotLen >= matchableLen)
                    results.Add(new ExonCombination(nextExonIndexes, totLen, internalLen));
                int newInternalLen = internalLen + nextExonLen;
                if (newInternalLen < matchableLen)
                    MakeExonCombContinuations(matchableLen, newTotLen, newInternalLen, maxSkip, exons,
                                              i + 1, nextExonIndexes, results);
            }
        }

        public static List<ExonCombination> MakeAllExonCombinations(int matchableLen, int maxSkip, List<DnaSequence> exons)
        {
            List<ExonCombination> results = new List<ExonCombination>();
            for (int firstExonIdx = 0; firstExonIdx < exons.Count - 1; firstExonIdx++)
            {
                int firstExonLen = (int)exons[firstExonIdx].Count;
                int totLen = firstExonLen;
                int internalLen = 0;
                List<int> exonIndexes = new List<int>();
                exonIndexes.Add(firstExonIdx);
                MakeExonCombContinuations(matchableLen, totLen, internalLen, maxSkip, exons, firstExonIdx + 1, exonIndexes, results);
            }
            return results;
        }
    }

    public class AnnotationBuilder
    {
        public static AnnotationBuilder GetAnnotationBuilder(Props props, StrtGenome genome)
        {
            PathHandler ph = new PathHandler(props);
            if (genome.Annotation == "VEGA")
                return new AnnotationBuilder(props, new BioMartAnnotationReader(genome, "VEGA"));
            if (genome.Annotation.StartsWith("ENSE"))
                return new AnnotationBuilder(props, new BioMartAnnotationReader(genome, genome.Annotation));
            return new AnnotationBuilder(props, new UCSCAnnotationReader(genome));
        }
        private Props props;
        private PathHandler ph;
        private AnnotationReader annotationReader;

        private int ReadLen { get; set; }
        private int MaxAlignmentMismatches { get; set; }
        private int MatchableLen { get { return ReadLen - MaxAlignmentMismatches; } }
        private int MaxExonsSkip { get; set; }

        public AnnotationBuilder(Props props, AnnotationReader annotationReader)
        {
            ReadLen = props.StandardReadLen;
            MaxAlignmentMismatches = props.MaxAlignmentMismatches;
            MaxExonsSkip = props.MaxExonsSkip;
            this.annotationReader = annotationReader;
            this.props = props;
            ph = new PathHandler(props);
        }

        public IEnumerable<ExonCombination> GenerateSplices(GeneFeature gf, DnaSequence chrSeq)
        {
            List<DnaSequence> exonSeqsInChrDir = GetExonSequences(gf, chrSeq);
            foreach (ExonCombination ec in ExonCombinationGenerator.MakeAllExonCombinations(MatchableLen, MaxExonsSkip, exonSeqsInChrDir))
            {
                DnaSequence junction = new ShortDnaSequence();
                List<int> startsInJunction = new List<int>();
                List<int> startsInRealChr = new List<int>();
                int exonIdx0 = ec.ExonIndexes[0];
                DnaSequence firstExon = exonSeqsInChrDir[exonIdx0];
                int firstLen = Math.Min((int)firstExon.Count, MatchableLen - ec.InternalExonsLen);
                startsInJunction.Add((int)junction.Count);
                int posInFirstExon = (int)firstExon.Count - firstLen;
                startsInRealChr.Add(gf.ExonStarts[exonIdx0] + posInFirstExon);
                junction.Append(firstExon.SubSequence(posInFirstExon, firstLen));
                int i = 1;
                for (; i < ec.ExonIndexes.Count - 1; i++)
                {
                    int exonIdx = ec.ExonIndexes[i];
                    DnaSequence midExon = exonSeqsInChrDir[exonIdx];
                    startsInJunction.Add((int)junction.Count);
                    startsInRealChr.Add(gf.ExonStarts[exonIdx]);
                    junction.Append(midExon);
                }
                int exonIdxLast = ec.ExonIndexes[i];
                DnaSequence lastExon = exonSeqsInChrDir[exonIdxLast];
                int lastLen = Math.Min((int)lastExon.Count, MatchableLen - ec.InternalExonsLen);
                startsInJunction.Add((int)junction.Count);
                startsInRealChr.Add(gf.ExonStarts[exonIdxLast]);
                junction.Append(lastExon.SubSequence(0, lastLen));
                ec.StartsInJunction = startsInJunction;
                ec.StartsInRealChr = startsInRealChr;
                ec.Seq = junction;
                ec.ExonsString = MakeExonsString(gf, ec.ExonIndexes);
                yield return ec;
            }
        }

        /// <summary>
        /// Convert 0-based, chr-ordered indices of exons in a junction to a string representation
        /// with 1-based, transcript ordered exon numbers delimited by '-'
        /// </summary>
        /// <param name="gf"></param>
        /// <param name="exonIndexes"></param>
        /// <returns></returns>
        private static string MakeExonsString(GeneFeature gf, List<int> exonIndexes)
        {
            if (gf.Strand == '+')
                return string.Join("-", exonIndexes.ConvertAll(idx => (idx + 1).ToString()).ToArray());
            List<string> parts = exonIndexes.ConvertAll(idx => (gf.ExonCount - idx).ToString());
            parts.Reverse();
            return string.Join("-", parts.ToArray());
        }

        private static List<DnaSequence> GetExonSequences(GeneFeature gf, DnaSequence chrSeq)
        {
            List<DnaSequence> exonSeqsInChrDir = new List<DnaSequence>(gf.ExonCount);
            for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
                exonSeqsInChrDir.Add(chrSeq.SubSequence(gf.ExonStarts[exonIdx], gf.GetExonLength(exonIdx)));
            return exonSeqsInChrDir;
        }

        public void BuildExonSplices(StrtGenome genome)
        {
            BuildExonSplices(genome, "");
        }
        public void BuildExonSplices(StrtGenome genome, string newIndexName)
        {
            int smallestReadMatchDistFromEnd = Math.Max(0, props.MinExtractionInsertLength / 2 - MaxAlignmentMismatches);
            string junctionsChrId = "chr" + genome.Annotation;
            Console.WriteLine("Reading genes for genome {0} and annotations {1}...", genome.Annotation, genome.Build);
            Dictionary<string, List<GeneFeature>> gfByChr = annotationReader.BuildGeneModelsByChr();
            Console.WriteLine("...read data for {0} chromosomes from annotation files:", gfByChr.Count);
            Console.WriteLine(string.Join(",", gfByChr.Keys.ToArray()));
            Console.WriteLine(annotationReader.GetPseudogeneCount() + " genes are annotated as pseudogenes.");
            Console.WriteLine("ReadLen=" + ReadLen + " MaxMismatches=" + MaxAlignmentMismatches + " MaxExonsSkip=" + MaxExonsSkip);
            DnaSequence jChrSeq = new LongDnaSequence();
            Dictionary<string, string> chrIdToFileMap = PathHandler.GetGenomeFilesMap(genome);
            StreamWriter annotWriter = PrepareAnnotationsFile(genome, newIndexName);
            StreamWriter chrWriter = PrepareJunctionChrFile(genome, junctionsChrId, newIndexName);
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                if (!gfByChr.ContainsKey(chrId))
                    continue;
                DnaSequence chrSeq = AbstractGenomeAnnotations.readChromosomeFile(chrIdToFileMap[chrId]);
                List<GeneFeature> chrGfs = gfByChr[chrId];
                Console.WriteLine("Processing chr" + chrId + " (" + chrGfs.Count + " genes)...");
                /* We can not rely on that the versions of each gene are consecutive in input file.
                    One and the same junction-sequence could also potentially have two different gene names.
                    Thus, sort the genes along the chromosome and check if every new junction is already
                    in the junctionChr starting from the last gene with any overlap to current gene. */
                List<long> gfEnds = new List<long>();
                List<long> gfJPos = new List<long>();
                int lastOverlappingGfIdx = 0;
                chrGfs.Sort((gf1, gf2) => gf1.Start - gf2.Start);
                foreach (GeneFeature gf in chrGfs.Where(g => genome.GeneVariants || !g.IsVariant())) // GeneFeatures now sorted by position on chromosome
                {
                    gfEnds.Add(gf.End);
                    gfJPos.Add((int)jChrSeq.Count);
                    annotWriter.WriteLine(gf.ToString()); // Write the real chr annotations to output
                    if (gf.ExonCount < 2)
                        continue;
                    List<int> jStarts = new List<int>();
                    List<int> jEnds = new List<int>();
                    List<string> exonIdStrings = new List<string>();
                    List<int> realExonIds = new List<int>();
                    List<int> offsets = new List<int>();
                    foreach (ExonCombination ec in GenerateSplices(gf, chrSeq))
                    {
                        while (gf.Start > gfEnds[lastOverlappingGfIdx])
                            lastOverlappingGfIdx++;
                        int jStartInSpliceChr = (int)jChrSeq.Match(ec.Seq, gfJPos[lastOverlappingGfIdx]);
                        if (jStartInSpliceChr == -1)
                        {
                            jStartInSpliceChr = (int)jChrSeq.Count;
                            jChrSeq.Append(ec.Seq);
                            chrWriter.WriteLine(ec.Seq.ToString());
                        }
                        for (int i = 0; i < ec.StartsInJunction.Count; i++)
                        {
                            int maxEnd = (int)ec.Seq.Count - smallestReadMatchDistFromEnd;
                            int endInJunction = (i + 1 == ec.StartsInJunction.Count) ? maxEnd : Math.Min(maxEnd, ec.StartsInJunction[i + 1] - 1);
                            if (endInJunction < smallestReadMatchDistFromEnd) continue;
                            int startInJunction = Math.Max(smallestReadMatchDistFromEnd, ec.StartsInJunction[i]);
                            if (startInJunction > ec.Seq.Count - smallestReadMatchDistFromEnd) continue;
                            int startInJChr = jStartInSpliceChr + startInJunction;
                            jStarts.Add(startInJChr);
                            jEnds.Add(jStartInSpliceChr + endInJunction);
                            exonIdStrings.Add(ec.ExonsString);
                            realExonIds.Add(gf.GetRealExonId(ec.ExonIndexes[i]));
                            int seqStartInJChr = jStartInSpliceChr + ec.StartsInJunction[i];
                            offsets.Add(ec.StartsInRealChr[i] - seqStartInJChr);
                        }
                    }
                    if (jStarts.Count > 0)
                        annotWriter.WriteLine(MakeGenesFileLine(gf, junctionsChrId,
                                              jStarts, jEnds, offsets, realExonIds, exonIdStrings));
                }
                if (Background.CancellationPending) return;
            }
            annotWriter.Close();
            chrWriter.Close();
            Console.WriteLine("Length of artificial splice chromosome:" + jChrSeq.Count);
        }

        public string MakeGenesFileLine(GeneFeature gf, string junctionsChrId,
                                        List<int> jStarts, List<int> jEnds,
                                        List<int> offsets, List<int> realExonIds, List<string> exonIdStrings)
        {
            StringBuilder s = new StringBuilder();
            s.Append(gf.Name + "\t\t");
            s.Append(junctionsChrId + "\t");
            s.Append(gf.Strand + "\t");
            s.Append(jStarts[0] + "\t");
            s.Append((jEnds[jEnds.Count - 1] + 1) + "\t");
            s.Append("\t\t");
            s.Append(offsets.Count);
            s.Append("\t");
            foreach (int exonStart in jStarts)
                s.Append(exonStart.ToString() + ",");
            s.Append("\t");
            foreach (int exonEnd in jEnds)
                s.Append(exonEnd.ToString() + ",");
            s.Append("\t");
            foreach (int offset in offsets)
                s.Append(offset.ToString() + ",");
            s.Append("\t");
            foreach (int exonId in realExonIds)
                s.Append(exonId.ToString() + ",");
            s.Append("\t");
            foreach (string exonsString in exonIdStrings)
                s.Append(exonsString + ",");
            return s.ToString();
        }

        private StreamWriter PrepareJunctionChrFile(StrtGenome genome, string junctionChrId, string newIndexName)
        {
            string jChrPath = (string.IsNullOrEmpty(newIndexName)) ? PathHandler.GetJunctionChrPath(genome) :
                                            Path.Combine(PathHandler.GetGenomeSequenceFolder(genome), "chr_" + newIndexName + ".fa");
            Console.WriteLine("Artificial exon junction chromosome: " + jChrPath);
            if (File.Exists(jChrPath))
            {
                File.Delete(jChrPath + ".old");
                File.Move(jChrPath, jChrPath + ".old");
            }
            StreamWriter chrWriter = new StreamWriter(jChrPath, false);
            chrWriter.WriteLine(">" + junctionChrId);
            return chrWriter;
        }

        private StreamWriter PrepareAnnotationsFile(StrtGenome genome, string newIndexName)
        {
            string annotationsPath = (string.IsNullOrEmpty(newIndexName)) ? PathHandler.GetAnnotationsPath(genome) :
                                                Path.Combine(PathHandler.GetGenomeSequenceFolder(genome), "Annotations_" + newIndexName + ".txt");
            Console.WriteLine("Annotations file: " + annotationsPath);
            if (File.Exists(annotationsPath))
            {
                File.Delete(annotationsPath + ".old");
                File.Move(annotationsPath, annotationsPath + ".old");
            }
            StreamWriter annotWriter = new StreamWriter(annotationsPath, false);
            annotWriter.WriteLine("@ReadLen=" + ReadLen);
            annotWriter.WriteLine("@MaxAlignmentMismatches=" + MaxAlignmentMismatches);
            annotWriter.WriteLine("@MaxExonsSkip=" + MaxExonsSkip);
            CopyCTRLData(genome, annotWriter);
            return annotWriter;
        }

        private void CopyCTRLData(StrtGenome genome, StreamWriter refWriter)
        {
            string chrCTRLPath = ph.GetChrCTRLPath();
            if (File.Exists(chrCTRLPath))
            {
                Console.WriteLine("Adding control chromosome and annotations.");
                string chrDest = Path.Combine(PathHandler.GetGenomeSequenceFolder(genome), Path.GetFileName(chrCTRLPath));
                if (!File.Exists(chrDest))
                    File.Copy(chrCTRLPath, chrDest);
                using (StreamReader CTRLReader = ph.GetCTRLGenesPath().OpenRead())
                {
                    string CTRLData = CTRLReader.ReadToEnd();
                    foreach (string line in CTRLData.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        refWriter.WriteLine(line);
                }
            }
        }

        public void UpdateSilverBulletGenes(StrtGenome genome, string errorsPath)
        {
            if (!errorsPath.Contains(genome.GetBowtieIndexName()))
                throw new ArgumentException("The update has to be of the same build as the updated genome");
            Dictionary<string, int> geneToNewPos = ReadErrorsFile(errorsPath);
            Console.WriteLine("There are {0} genes to have their first/last extended.", geneToNewPos.Count);
            Background.Progress(5);
            string annotationPath = PathHandler.GetAnnotationsPath(genome);
            long fileSize = new FileInfo(annotationPath).Length;
            string updatedPath = annotationPath + ".extended";
            StreamWriter writer = updatedPath.OpenWrite();
            string lastOriginal = "";
            string originalFlag = GeneFeature.nonUTRExtendedIndicator;
            long nc = 0;
            Console.WriteLine("Updating annotation file...");
            foreach (LocusFeature gf in new UCSCAnnotationReader(genome).IterAnnotationFile(annotationPath))
            {
                string gfTxt = gf.ToString();
                nc += gfTxt.Length;
                Background.Progress((int)(100 * (nc + 2) / (double)fileSize));
                if (StrtGenome.IsSyntheticChr(gf.Chr))
                {
                    writer.WriteLine(gfTxt);
                    continue;
                }
                if (gf.Name.EndsWith(originalFlag))
                {
                    lastOriginal = gf.Name;
                    writer.WriteLine(gfTxt);
                }
                else
                {
                    try
                    {
                        int newPos = geneToNewPos[gf.Name];
                        if (!lastOriginal.StartsWith(gf.Name))
                        {
                            gf.Name += originalFlag;
                            writer.WriteLine(gf.ToString());
                            gf.Name = gf.Name.Substring(0, gf.Name.Length - originalFlag.Length);
                        }
                        if (newPos < 0)
                            gf.Start = -newPos;
                        else
                            gf.End = newPos;
                        writer.WriteLine(gf.ToString());
                    }
                    catch (KeyNotFoundException)
                    {
                        writer.WriteLine(gfTxt);
                    }
                    lastOriginal = "";
                }
            }
            writer.Close();
            Console.WriteLine("The updated annotations are stored in {0}.\n" +
                                "You need to replace the old file manually.", updatedPath);
        }

        private Dictionary<string, int> ReadErrorsFile(string errorsPath)
        {
            Dictionary<string, int> updates = new Dictionary<string, int>();
            StreamReader errReader = errorsPath.OpenRead();
            string line = errReader.ReadLine();
            string[] fields = line.Split('\t');
            int leftIdx = Array.FindIndex(fields, f => f == "NewLeftExonStart");
            int rightIdx = Array.FindIndex(fields, f => f == "NewRightExonStart");
            while (line != null)
            {
                fields = line.Split('\t');
                if (fields.Length >= rightIdx)
                {
                    string geneName = fields[0];
                    string newLeftStart = fields[leftIdx];
                    string newRightStart = fields[rightIdx];
                    int newPos;
                    if (int.TryParse(newLeftStart, out newPos)) updates[geneName] = -newPos;
                    else if (int.TryParse(newRightStart, out newPos)) updates[geneName] = newPos;
                }
                line = errReader.ReadLine();
            }
            errReader.Close();
            return updates;
        }
    }
}
