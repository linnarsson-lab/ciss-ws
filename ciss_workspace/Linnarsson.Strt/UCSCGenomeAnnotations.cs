﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using System.Security.AccessControl;

namespace Linnarsson.Strt
{
    public class UCSCGenomeAnnotations : AbstractGenomeAnnotations
    {
        Dictionary<string, int> antisensePairExons = new Dictionary<string, int>();
        private string lastLoadedGeneName;

        public UCSCGenomeAnnotations(Props props, StrtGenome genome)
            : base(props, genome)
        {
        }

        public override void Load()
        {
            PathHandler ph = new PathHandler(props);
            Dictionary<string, string> chrIdToFileMap = ph.GetGenomeFilesMap(genome);
            int nFiles = (int)(chrIdToFileMap.Count * 1.3);
            Background.Progress(0);
            foreach (string chrId in chrIdToFileMap.Keys)
                QuickAnnotations[chrId] = new QuickAnnotationMap(annotationBinSize);
            AssertGeneFile(ph);
            MarkUpOverlappingFeatures();
            foreach (GeneFeature gf in geneFeatures.Values) AddIntervals((GeneFeature)gf);
            if (needChromosomeSequences || needChromosomeLengths)
                ReadChromsomeSequences(chrIdToFileMap, nFiles);
            if (Background.CancellationPending) return;
            string[] rmskFiles = ph.GetRepeatMaskFiles(genome);
            Console.Write("Reading {0} masking files..", rmskFiles.Length);
            foreach (string rmskFile in rmskFiles)
            {
                Console.Write(".");
                LoadRepeatMaskFile(rmskFile);
            }
            Console.WriteLine("{0} annotated repeat types.", repeatFeatures.Count);
            summaryLines.Add(repeatFeatures.Count + " repeat types analyzed using " + rmskFiles.Length + " repeat mask files.\n");
            Background.Progress(100);
        }

        public override string[] GetChromosomeNames()
        {
 	        return ChromosomeSequences.Keys.ToArray();
        }

        private void MarkUpOverlappingFeatures()
        {
            int nMarkedExons = 0, nMarkedGenes = 0, totalMarkedLen = 0;
            foreach (string chrId in QuickAnnotations.Keys)
            {
                int nStrandExons, nStrandGenes, totalStrandLen;
                MarkUpOverlappingFeatures(chrId, '+', out nStrandExons, out nStrandGenes, out totalStrandLen);
                nMarkedExons += nStrandExons;
                nMarkedGenes += nStrandGenes;
                totalMarkedLen += totalStrandLen;
                MarkUpOverlappingFeatures(chrId, '-', out nStrandExons, out nStrandGenes, out totalStrandLen);
                nMarkedExons += nStrandExons;
                nMarkedGenes += nStrandGenes;
                totalMarkedLen += totalStrandLen;
            }
            Console.WriteLine("{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.",
                              nMarkedExons, nMarkedGenes, totalMarkedLen);
        }

        private void MarkUpOverlappingFeatures(string chrId, char strand, out int nMaskedExons,
                                              out int nMaskedGenes, out int totalMaskedLength)
        {
            int[] exonStarts = new int[30000];
            int[] exonEnds = new int[30000];
            GeneFeature[] gFeatureByExon = new GeneFeature[30000];
            int exonIdx = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.Chr == chrId && gf.Strand == strand)
                    for (int i = 0; i < gf.ExonCount; i++)
                    {
                        exonStarts[exonIdx] = gf.ExonStarts[i];
                        exonEnds[exonIdx] = gf.ExonEnds[i];
                        gFeatureByExon[exonIdx] = gf;
                        if (++exonIdx >= exonStarts.Length)
                        {
                            Array.Resize(ref exonStarts, exonIdx + 20000);
                            Array.Resize(ref exonEnds, exonIdx + 20000);
                            Array.Resize(ref gFeatureByExon, exonIdx + 20000);
                        }
                    }
            }
            Array.Resize(ref exonStarts, exonIdx);
            Array.Resize(ref exonEnds, exonIdx);
            Array.Resize(ref gFeatureByExon, exonIdx);
            Sort.QuickSort(exonStarts, exonEnds, gFeatureByExon);
            char revStrand = (strand == '+')? '-' : '+';
            nMaskedGenes = 0; nMaskedExons = 0; totalMaskedLength = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.Chr == chrId)
                {
                    gf.MaskInterExons(exonStarts, exonEnds, strand);
                    if (gf.Strand == revStrand)
                    {
                        List<int> indicesOfMasked = gf.MaskExons(exonStarts, exonEnds);
                        if (indicesOfMasked.Count > 0)
                        {
                            nMaskedExons += indicesOfMasked.Count;
                            nMaskedGenes++;
                            totalMaskedLength += gf.GetTranscriptLength() - gf.GetNonMaskedTranscriptLength();
                            foreach (int idx in indicesOfMasked)
                            {
                                string[] names = new string[] { gf.Name, gFeatureByExon[idx].Name };
                                Array.Sort(names);
                                string gfPair = string.Join("#", names);
                                if (!antisensePairExons.ContainsKey(gfPair))
                                    antisensePairExons[gfPair] = 1;
                                else
                                    antisensePairExons[gfPair]++;
                            }
                        }
                    }
                }
            }
        }

        private void AssertGeneFile(PathHandler ph)
        {
            string annotationsPath = ph.GetAnnotationsPath(genome);
            annotationsPath = PathHandler.ExistsOrGz(annotationsPath);
            if (annotationsPath == null)
                throw new NoAnnotationsFileFoundException("Could not find annotation file: " + annotationsPath);
            Console.WriteLine("Annotations read from " + Path.GetFileName(annotationsPath) + ".");
            LoadAnnotationsFile(annotationsPath);
        }

        private void ReadChromsomeSequences(Dictionary<string, string> chrIdToFileMap, int nFiles)
        {
            int nDone = 0;
            string[] selectedChrIds = props.SeqStatsChrIds;
            if (selectedChrIds == null || selectedChrIds[0] == "")
                selectedChrIds = chrIdToFileMap.Keys.ToArray();
            Console.Write("Reading {0} chromosomes...", selectedChrIds.Length);
            foreach (string chrId in selectedChrIds)
            {
                Console.Write("." + chrId);
                if (StrtGenome.IsSpliceAnnotationChr(chrId)) continue;
                try
                {
                    if (needChromosomeSequences)
                    {
                        DnaSequence chrSeq = readChromosomeFile(chrIdToFileMap[chrId]);
                        ChromosomeLengths.Add(chrId, (int)chrSeq.Count);
                        ChromosomeSequences.Add(chrId, chrSeq);
                    }
                    else
                    {
                        double fileLen = new FileInfo(chrIdToFileMap[chrId]).Length;
                        int chrLen = (int)(fileLen * 60.0 / 61.0); // Get an approximate length by removing \n:s
                        ChromosomeLengths.Add(chrId, chrLen);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("\nERROR: Could not read chromosome {0} - {1}", chrId, e.Message);
                }
                Background.Progress(++nDone * 100 / nFiles);
                if (Background.CancellationPending) return;
            }
            summaryLines.Add("Read " + ChromosomeSequences.Count + 
                     " sequence files for UCSC Wiggle plots (.wig files) and/or upstream hit motifs (sequence_logos.tab files).");
            Console.WriteLine();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="geneId"></param>
        /// <returns>Sequence of transcript in mRNA sense orientation,
        /// or null if chromosomes are not loaded.</returns>
        public DnaSequence GetTranscriptSequence(string geneName)
        {
            GeneFeature gf = geneFeatures[geneName];
            DnaSequence trSeq = null;
            if (ChromosomeSequences.ContainsKey(gf.Chr))
            {
                DnaSequence chrSeq = ChromosomeSequences[gf.Chr];
                trSeq = new ShortDnaSequence();
                for (int i = 0; i < gf.ExonCount; i++)
                {
                    trSeq.Append(chrSeq.SubSequence(gf.ExonStarts[i], gf.GetExonLength(i)));
                }
                if (gf.Strand == '-') trSeq.RevComp();
            }
            return trSeq;
        }

        private void LoadRepeatMaskFile(string rmskPath)
        {
            int nLines = 0;
            int nRepeatFeatures = 0;
            int nTooLongFeatures = 0;
            string[] record;
            int fileTypeOffset = 0;
            if (rmskPath.EndsWith("out"))
                fileTypeOffset = -1;
            StreamReader reader = rmskPath.OpenRead();
            string line = reader.ReadLine();
            while (line == "" || !char.IsDigit(line.Trim()[0]))
                line = reader.ReadLine();
            while (line != null)
            {
                nLines++;
                record = Regex.Split(line.Trim(), " +|\t");
                string chr = record[5 + fileTypeOffset].Substring(3);
                if (QuickAnnotations.ContainsKey(chr))
                {
                    int start = int.Parse(record[6 + fileTypeOffset]);
                    int end = int.Parse(record[7 + fileTypeOffset]);
                    nRepeatFeatures++;
                    string name = record[10 + fileTypeOffset];
                    RepeatFeature mappedFeature;
                    try
                    {
                        mappedFeature = repeatFeatures[name];
                        mappedFeature.Length += end - start + 1;
                    }
                    catch (KeyNotFoundException)
                    {
                        mappedFeature = new RepeatFeature(name, start, end);
                        repeatFeatures[name] = mappedFeature;
                    }
                    AddInterval(chr, start, end, mappedFeature.MarkHit, 0);
                }
                line = reader.ReadLine();
            }
            reader.Close();
            if (nRepeatFeatures < nLines || nTooLongFeatures > 0)
                Console.WriteLine("\n{0}: {1} repeats, {2} on defined chromosomes, but {3} larger than {4} were skipped.",
                  rmskPath, nLines, nRepeatFeatures, nTooLongFeatures, props.MaxFeatureLength);
        }

        private void LoadAnnotationsFile(string annotationsPath)
        {
            int nLines = 0;
            int nGeneFeatures = 0;
            int nTooLongFeatures = 0;
            foreach (LocusFeature gf in UCSCAnnotationReader.IterAnnotationFile(annotationsPath))
            {
                nLines++;
                if (noGeneVariants && gf.Name.Contains(GeneFeature.variantIndicator))
                    continue;
                if (gf.Length > props.MaxFeatureLength)
                    nTooLongFeatures++;
                else if (RegisterGeneFeature(gf))
                    nGeneFeatures++;
            }
            string exclV = noGeneVariants ? "main" : "complete";
            Console.WriteLine("{0} {1} gene variants will be mapped. (Excluding {2} spanning > {3} bp.)",
                              nGeneFeatures, exclV, nTooLongFeatures, props.MaxFeatureLength);
            summaryLines.Add("\n" + nGeneFeatures + " " + exclV + " gene variants were analyzed in this run.");
        }

        /// <summary>
        /// Adds a normal gene or a splice gene to the set of features
        /// </summary>
        /// <param name="gf"></param>
        /// <returns>true if gf represents a new gene, and not an artificial splice gene.</returns>
        private bool RegisterGeneFeature(LocusFeature gf)
        {
            if (StrtGenome.IsSpliceAnnotationChr(gf.Chr))
            { // Requires that real loci are registered before artificial splice loci.
                if (lastLoadedGeneName == gf.Name)
                {    // Link from artificial splice chromosome to real locus
                    ((SplicedGeneFeature)gf).BindToRealFeature(geneFeatures[gf.Name]);
                    AddIntervals((SplicedGeneFeature)gf);
                }
                lastLoadedGeneName = "";
                return false;
            }
            if (geneFeatures.ContainsKey(gf.Name))
            {
                Console.WriteLine("WARNING: Duplicated gene name in annotation file: " + gf.Name);
                lastLoadedGeneName = "";
                return false;
            }
            geneFeatures[gf.Name] = (GeneFeature)gf;
            lastLoadedGeneName = gf.Name;
            return true;
        }

        public override int GetTotalAnnotCounts(int annotType, bool excludeMasked)
        {
            int totCounts = 0;
            if (annotType == AnnotType.REPT || annotType == AnnotType.AREPT)
                foreach (RepeatFeature rf in repeatFeatures.Values)
                    totCounts += rf.GetTotalHits();
            else
                foreach (GeneFeature gf in geneFeatures.Values)
                    totCounts += gf.GetAnnotCounts(annotType, excludeMasked);
            return totCounts;
        }

        public override int GetTotalAnnotLength(int annotType, bool excludeMasked)
        {
            if (annotType == AnnotType.REPT || annotType == AnnotType.AREPT)
                return GetTotalRepeatLength();
            int totLen = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
                totLen += gf.GetAnnotLength(annotType, excludeMasked);
            return totLen;
        }
        public override int GetTotalAnnotLength(int annotType)
        {
            return GetTotalAnnotLength(annotType, false);
        }

        public int GetTotalRepeatLength()
        {
            int totLen = 0;
            foreach (RepeatFeature rf in repeatFeatures.Values)
                totLen += rf.GetLocusLength();
            return totLen;
        }

        public override void WriteStats(string fileNameBase)
        {
            WritePotentialErronousAnnotations(fileNameBase);
            WriteSplicesByGeneLocus(fileNameBase);
            WriteExpressionTable(fileNameBase);
            WriteBarcodedRPM(fileNameBase);
            if (GeneFeature.GenerateTranscriptProfiles)
                WriteTranscriptHitsByGeneLocus(fileNameBase);
            if (GeneFeature.GenerateLocusProfiles)
                WriteLocusHitsByGeneLocus(fileNameBase);
            WriteExpressedAntisenseGenes(fileNameBase);
            if (props.GenesToPaint != null && props.GenesToPaint.Length > 0)
            {
                WriteGeneImages(fileNameBase);
                WriteTranscriptImages(fileNameBase);
            }
            WriteUniquehits(fileNameBase);
            WriteAnnotTypeAndExonCounts(fileNameBase);
            WriteElongationEfficiency(fileNameBase);
        }

        private void WriteExpressedAntisenseGenes(string fileNameBase)
        {
            var file = (fileNameBase + "_expressed_antisense.tab").OpenWrite();
            file.WriteLine("Chr\tGeneA\tGeneB\tCountA\tCountB");
            int nPairs = 0;
            foreach (string gfPair in antisensePairExons.Keys)
            {
                string[] names = gfPair.Split('#');
                int aHits = geneFeatures[names[0]].GetTranscriptHits();
                int bHits = geneFeatures[names[1]].GetTranscriptHits();
                string chr = geneFeatures[names[0]].Chr;
                if (chr != geneFeatures[names[1]].Chr ||
                    geneFeatures[names[0]].Strand == geneFeatures[names[1]].Strand)
                    throw new Exception("Internal error in sense-antisense genes: " + names[0] + "-" + names[1]);
                if (aHits > 0 && bHits > 0)
                {
                    file.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", chr, names[0], names[1], aHits, bHits);
                    nPairs++;
                }
            }
            file.Close();
            summaryLines.Add(nPairs + " pairs of expressed overlapping counter-oriented genes were found." +
                             " For details, view the expressed_antisense.tab file.\n");
        }

        private void WritePotentialErronousAnnotations(string fileNameBase)
        {
            string warnFilename = fileNameBase + "_annot_errors_" + genome.GetBowtieIndexName() + ".tab";
            var warnFile = warnFilename.OpenWrite();
            warnFile.WriteLine("Feature\tExonHits\tPart\tPartHits\tPartLocation\tNewLeftExonStart\tNewRightExonStart");
            int nErr = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.GetTotalHits(true) == 0 || gf.Chr == StrtGenome.chrCTRLId 
                    || gf.Name.EndsWith(GeneFeature.nonUTRExtendedIndicator))
                    continue;
                if (gf.Strand == '+')
                {
                    nErr += TestErrorAnnotType(warnFile, gf, gf.LeftMatchStart, gf.Start - 1, AnnotType.USTR);
                    nErr += TestErrorAnnotType(warnFile, gf, gf.End, gf.RightMatchEnd, AnnotType.DSTR);
                }
                else
                {
                    nErr += TestErrorAnnotType(warnFile, gf, gf.LeftMatchStart, gf.Start - 1, AnnotType.DSTR);
                    nErr += TestErrorAnnotType(warnFile, gf, gf.End, gf.RightMatchEnd, AnnotType.USTR);
                }
            }
            warnFile.Close();
            if (nErr == 0)
                File.Delete(warnFilename);
            else
            {
                summaryLines.Add("Potentially erronous annotation of 5' and 3' transcript ends found in " +
                                 nErr + " cases. For details, view the annot_errors_X.tab file.\n");
            }
        }

        private int TestErrorAnnotType(StreamWriter warnFile, GeneFeature gf, 
                                        int start, int end, int annotType)
        {
            int flankHits = gf.HitsByAnnotType[annotType];
            int exonHits = gf.HitsByAnnotType[AnnotType.EXON];
            if (flankHits > 5 && flankHits > exonHits)
            {
                string newLeftExonStart = "", newRightExonStart = "";
                if (annotType == AnnotType.USTR)
                {
                    int newExonStart = CompactGenePainter.FindUpstreamHotspotStart(gf, flankHits);
                    if (newExonStart >= 0)
                    {
                        if (gf.Strand == '+') newLeftExonStart = newExonStart.ToString();
                        else newRightExonStart = newExonStart.ToString();
                    }
                }
                warnFile.WriteLine("{0}\t{1}\t{2}\t{3}\tChr{4}{5}:{6}-{7}\t{8}\t{9}",
                            gf.Name, exonHits, AnnotType.GetName(annotType),
                            flankHits, gf.Chr, gf.Strand, start, end,
                            newLeftExonStart, newRightExonStart);
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// For each feature, write the total (for genes, transcript) hit count for every barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteExpressionTable(string fileNameBase)
        {
            var matrixFile = (fileNameBase + "_expression.tab").OpenWrite();
            matrixFile.WriteLine("Length, total and per barcode maximum exon hits for transcripts, and total length, total and per barcode (both sense & antisense) hits for repeat regions grouped by type.");
            matrixFile.WriteLine("MinExonHits have a unique genome mapping, MaxExonHits includes hits with alternative mapping(s) to genome.");
            matrixFile.WriteLine("NOTE: Gene variants occupying the same locus share counts in the table.");
            if (!AnnotType.DirectionalReads)
                matrixFile.WriteLine("NOTE: This is non-STRT analysis with non-directional reads.");
            WriteBarcodeHeaders(matrixFile, 6);
            matrixFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tMinExonHits\tMaxExonHits");
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(genome);
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int ncHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                matrixFile.Write(gf.Name + "\t" + gf.Chr + "\t" + gf.Start + "\t" +
                                 gf.Strand + "\t" + gf.GetTranscriptLength() + "\t" +
                                 ncHits + "\t" + gf.GetTranscriptHits());
                foreach (int idx in speciesBcIndexes)
                    matrixFile.Write("\t" + gf.TranscriptHitsByBarcode[idx]);
                matrixFile.WriteLine();
            }
            foreach (RepeatFeature rf in repeatFeatures.Values)
            {
                matrixFile.Write("r_" + rf.Name + "\t\t\t\t" + rf.GetLocusLength() + "\t" +
                                 rf.GetTotalHits() + "\t");
                foreach (int idx in speciesBcIndexes)
                    matrixFile.Write("\t" + rf.TotalHitsByBarcode[idx]);
                matrixFile.WriteLine();
            }
            matrixFile.Close();
            summaryLines.Add("For raw counts of " + geneFeatures.Count + " genes/variants and " +
                             repeatFeatures.Count +  "expressed repeat types view the expression.tab file.");
        }

        /// <summary>
        /// For each barcode and locus, write the total hit count
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteExpressionList(string fileNameBase)
        {
            // Create a normal-form table of hit counts
            var tableFile = (fileNameBase + "_expression_list.tab").OpenWrite();
            tableFile.WriteLine("Barcode\tFeature\tLevel");
            tableFile.WriteLine();
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(genome);
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                foreach (int idx in speciesBcIndexes)
                {
                    tableFile.Write(barcodes.Seqs[idx] + "\t");
                    tableFile.Write(gf.Name + "\t");
                    tableFile.WriteLine(gf.TranscriptHitsByBarcode[idx].ToString());
                }
            }
            tableFile.Close();
        }

        private void WriteBarcodeHeaders(StreamWriter matrixFile, int nTabs)
        {
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(genome);
            matrixFile.Write(new String('\t', nTabs) + "Barcode:");
            foreach (int idx in speciesBcIndexes) matrixFile.Write("\t" + barcodes.Seqs[idx]);
            matrixFile.WriteLine();
            matrixFile.Write(new String('\t', nTabs) + "Sample:");
            foreach (int idx in speciesBcIndexes) matrixFile.Write("\t" + barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            foreach (string annotation in barcodes.GetAnnotationTitles())
            {
                matrixFile.Write(new String('\t', nTabs) + annotation + ":");
                foreach (int idx in speciesBcIndexes) matrixFile.Write("\t" + barcodes.GetAnnotation(annotation, idx));
                matrixFile.WriteLine();
            }
        }

        public override void WriteSpikeDetection(StreamWriter summaryFile, StreamWriter xmlFile)
        {
            summaryFile.WriteLine("\nFraction of wells where spikes have been detected:");
            summaryFile.WriteLine("Spike\tTotalCount\tFractionDetected");
            xmlFile.WriteLine("  <spikedetection>");
            xmlFile.WriteLine("    <title>Detection of spikes across all {0} wells</title>", barcodes.Count);
            StringBuilder sbt = new StringBuilder();
            StringBuilder sbf = new StringBuilder();
            foreach (GeneFeature gf in IterTranscripts(true))
            {
                int total = 0;
                int detected = 0;
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    int bcHits = gf.TranscriptHitsByBarcode[bcIdx];
                    total += bcHits;
                    if (bcHits > 0) detected++;
                }
                double fraction = (detected / (double)barcodes.Count);
                string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                summaryFile.WriteLine("{0}\t{1}\t{2:0.000}", gf.Name, total, fraction);
                sbt.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0}\" />\n", spikeId, total));
                sbf.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0.###}\" />\n", spikeId, fraction));
            }
            xmlFile.WriteLine("    <curve legend=\"total reads\" yaxis=\"right\" color=\"black\">");
            xmlFile.Write(sbt.ToString());
            xmlFile.WriteLine("    </curve>");
            xmlFile.WriteLine("    <curve legend=\"frac. of wells\" yaxis=\"left\" color=\"blue\">");
            xmlFile.Write(sbf.ToString());
            xmlFile.WriteLine("    </curve>");
            xmlFile.WriteLine("  </spikedetection>");
        }

        private void WriteBarcodedRPM(string fileNameBase)
        {
            string rpType = (props.UseRPKM) ? "RPKM" : "RPM";
            var matrixFile = (fileNameBase + "_" + rpType + ".tab").OpenWrite();
            if (props.DirectionalReads)
            {
                matrixFile.WriteLine("Estimated detection limits as {0} thresholds calculated from 99% and 99.9% of the global distribution ", rpType);
                matrixFile.WriteLine("of AntiSense Exon hits, and the normalized {0} values for main gene transcripts in each barcode.", rpType);
            }
            if (!props.UseRPKM)
                matrixFile.WriteLine("SingleRead is the RPM value that corresponds to a single read in each barcode.");
            WriteBarcodeHeaders(matrixFile, 9);
            matrixFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tTotExonHits\tP=0.01\tP=0.001\tAverage\tCV");
            WriteRPMSection(matrixFile, true, null);
            matrixFile.WriteLine();
            StreamWriter simpleTableFile = (fileNameBase + "_" + rpType + "_simple.txt").OpenWrite();
            foreach (int idx in barcodes.GenomeBarcodeIndexes(genome))
                simpleTableFile.Write("\t" + barcodes.GetWellId(idx));
            simpleTableFile.WriteLine();
            WriteRPMSection(matrixFile, false, simpleTableFile);
            simpleTableFile.Close();
            matrixFile.Close();
        }

        private void WriteRPMSection(StreamWriter matrixFile, bool selectSpikes, StreamWriter simpleTableFile)
        {
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(genome);
            int[] totCountsByBarcode = GetTotalTranscriptCountsByBarcode(selectSpikes);
            int totCount = totCountsByBarcode.Sum();
            List<double> ASReadsPerBase = GetExonASOrderedReadsPerBase(selectSpikes);
            Double RPBM999 = Double.NaN;
            Double RPBM99 = Double.NaN;
            if (!selectSpikes && totCount > 0 && ASReadsPerBase.Count > 0)
            {
                RPBM99 = 1.0E+6 * ASReadsPerBase[(int)Math.Floor(ASReadsPerBase.Count * 0.99)] / (double)totCount;
                RPBM999 = 1.0E+6 * ASReadsPerBase[(int)Math.Floor(ASReadsPerBase.Count * 0.999)] / (double)totCount;
            }
            double[] normFactors = CalcRPMNormFactors(totCountsByBarcode);
            if (!props.UseRPKM)
            {
                matrixFile.Write("SingleRead\t\t\t\t\t\t\t\t\t");
                foreach (int idx in speciesBcIndexes) matrixFile.Write("\t{0:G6}", normFactors[idx]);
                matrixFile.WriteLine();
            }
            foreach (GeneFeature gf in IterTranscripts(selectSpikes))
            {
                double kFactor = (props.UseRPKM) ? gf.GetTranscriptLength() : 1.0;
                string RPMThres01 = "-", RPMThres001 = "-";
                if (props.DirectionalReads)
                {
                    RPMThres01 = string.Format("{0:G6}", RPBM99 * gf.GetNonMaskedTranscriptLength() / kFactor);
                    RPMThres001 = string.Format("{0:G6}", RPBM999 * gf.GetNonMaskedTranscriptLength() / kFactor);
                }
                matrixFile.Write(gf.Name + "\t" + gf.Chr + "\t" + gf.Start + "\t" + gf.Strand + "\t" + gf.GetTranscriptLength() + "\t" +
                                 gf.GetTranscriptHits() + "\t" + RPMThres01 + "\t" + RPMThres001);
                StringBuilder sb = new StringBuilder();
                DescriptiveStatistics ds = new DescriptiveStatistics();
                foreach (int idx in speciesBcIndexes)
                {
                    double RPkM = (normFactors[idx] * gf.TranscriptHitsByBarcode[idx]) / kFactor;
                    ds.Add(RPkM);
                    sb.AppendFormat("\t{0:G6}", RPkM);
                }
                string CV = "N/A";
                if (ds.Count > 2) CV = string.Format("{0:G6}", (ds.StandardDeviation() / ds.Mean()));
                matrixFile.WriteLine("\t{0:G6}\t{1}{2}", ds.Mean(), CV, sb.ToString());
                if (simpleTableFile != null)
                    simpleTableFile.WriteLine(gf.Name + sb.ToString());
            }
        }

        private double[] CalcRPMNormFactors(int[] totCountsByBarcode)
        {
            double[] normFactors = new double[totCountsByBarcode.Length];
            for (int bcIdx = 0; bcIdx < totCountsByBarcode.Length; bcIdx++)
            {
                normFactors[bcIdx] = (totCountsByBarcode[bcIdx] > 0) ?
                                          (1.0E+6 / (double)totCountsByBarcode[bcIdx]) : 0;
            }
            return normFactors;
        }

        private List<double> GetExonASOrderedReadsPerBase(bool selectSpikes)
        {
            List<double> allASReadsPerBase = new List<double>();
            foreach (GeneFeature gf in IterTranscripts(selectSpikes))
            {
                int antiHits = gf.NonMaskedHitsByAnnotType[AnnotType.AEXON];
                //if (antiHits == 0) continue;
                double ASReadsPerBase = antiHits / (double)gf.GetNonMaskedTranscriptLength();
                allASReadsPerBase.Add(ASReadsPerBase);
            }
            if (allASReadsPerBase.Count > 0)
                allASReadsPerBase.Sort();
            return allASReadsPerBase;
        }

        /// <summary>
        /// For every gene, write the total hit count per annotation type (exon, intron, flank)
        /// and also hit count for each exon.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteAnnotTypeAndExonCounts(string fileNameBase)
        {
            int nExonsToShow = 50;
            var matrixFile = (fileNameBase + "_exons.tab").OpenWrite();
            matrixFile.Write("Gene\tChr\tStrand\tUSTRLen\tLocusLen\tDSTRLen\t#SHits\t#AHits\tExonLen\tIntrLen\t#Exons\tMixGene\tASGene\t");
            foreach (int i in AnnotType.GetGeneTypes())
                matrixFile.Write("#{0}Hits\t", AnnotType.GetName(i));
            for (int exonId = 1; exonId <= nExonsToShow; exonId++)
                matrixFile.Write("SEx{0}\t", exonId);
            matrixFile.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                string mixedSenseGene = "";
                if (gf.HitsByAnnotType[AnnotType.INTR] >= 5 ||
                    (gf.HitsByAnnotType[AnnotType.INTR] >= gf.HitsByAnnotType[AnnotType.EXON] * 0.10))
                    mixedSenseGene = OverlappingExpressedGene(gf, 10, true);
                string mixedASGene = "";
                if (gf.HitsByAnnotType[AnnotType.AINTR] >= 5 || gf.HitsByAnnotType[AnnotType.AEXON] >= 5)
                    mixedASGene = OverlappingExpressedGene(gf, 10, false);
                matrixFile.Write(gf.Name + "\t" + gf.Chr + "\t" + gf.Strand + "\t" + 
                                 gf.USTRLength + "\t" + gf.GetLocusLength() + "\t" + gf.DSTRLength + "\t" +
                                 gf.GetTotalHits(true) + "\t" + gf.GetTotalHits(false) + "\t" +
                                 gf.GetTranscriptLength() + "\t" + gf.GetIntronicLength() + "\t" + 
                                 gf.ExonCount + "\t" + mixedSenseGene + "\t" + mixedASGene + "\t");
                foreach (int i in AnnotType.GetGeneTypes())
                {
                    int nHits = gf.HitsByAnnotType[i];
                    matrixFile.Write(nHits.ToString() + "\t");
                }
                int[] counts = CompactGenePainter.GetCountsPerExon(gf, props.DirectionalReads);
                WriteCountsDirected(matrixFile, gf.Strand, counts);
                matrixFile.WriteLine();
            }
            matrixFile.Close();
        }

        private static void WriteCountsDirected(StreamWriter file, char strand, int[] counts)
        {
            if (strand == '-')
                for (int i = counts.Length - 1; i >= 0; i--)
                    file.Write(counts[i] + "\t");
            else
                foreach (int c in counts)
                    file.Write(c + "\t");
        }

        private string OverlappingExpressedGene(GeneFeature gf, int minIntrusion, bool sameStrand)
        {
            List<string> overlapNames = new List<string>();
            char searchStrand = gf.Strand;
            if (!sameStrand) searchStrand = (searchStrand == '+')? '-' : '+';
            foreach (GeneFeature otherGf in geneFeatures.Values)
            {
                if (otherGf.Chr == gf.Chr && otherGf.Strand == searchStrand &&
                    otherGf != gf && otherGf.GetTranscriptHits() > 0 &&
                    otherGf.Overlaps(gf.Start, gf.End, minIntrusion))
                    overlapNames.Add(otherGf.Name);
            }
            return string.Join(",", overlapNames.ToArray());
        }

        /// <summary>
        /// For every gene, write the differential splicing.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteSplicesByGeneLocus(string fileNameBase)
        {
            int nExonsToShow = 50;
            string fPath = fileNameBase + "_diff_splice.tab";
            var matrixFile = fPath.OpenWrite();
            matrixFile.WriteLine("Total 3' splice junction hits per exon, and, when variant splices were detected, hit counts of each.");
            matrixFile.Write("Gene\t#Exons\tJunctionHits\t");
            for (int i = 0; i < nExonsToShow; i++)
                matrixFile.Write("FromEx{0}\tSplices\t", (i+1));
            matrixFile.WriteLine();
            int totSpliceOutCount = 0;
            int numSpliceOuts = 0;
            int numGenesSpliceOut = 0;
            int numMainGenesWithSPLCHits = 0;
            int numGenesWithSPLCHits = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.GetJunctionHits() == 0)
                    continue;
                numGenesWithSPLCHits++;
                if (gf.IsMainVariant()) numMainGenesWithSPLCHits++;
                matrixFile.Write(gf.Name + "\t" + gf.ExonCount + "\t" + gf.GetJunctionHits() + "\t");
                int[][] countMatrix = gf.GetSpliceCounts();
                bool someSplicedOutExon = false;
                for (int e1 = 1; e1 < countMatrix.Length; e1++)
                {
                    int[] fromEx1SplcCounts = countMatrix[e1];
                    int splcOutCount = 0;
                    for (int e2 = e1 + 2; e2 < fromEx1SplcCounts.Length; e2++)
                    {
                        splcOutCount += fromEx1SplcCounts[e2];
                        if (fromEx1SplcCounts[e2] > 0) numSpliceOuts++;
                    }
                    int totSplcCount = fromEx1SplcCounts[e1 + 1] + splcOutCount;
                    matrixFile.Write("{0}\t", totSplcCount);
                    if (splcOutCount > 0)
                    {
                        totSpliceOutCount += splcOutCount;
                        someSplicedOutExon = true;
                        List<string> accData = new List<string>();
                        for (int accId = e1 + 1; accId < fromEx1SplcCounts.Length; accId++)
                        {
                            int c = fromEx1SplcCounts[accId];
                            if (c > 0)
                                accData.Add(string.Format("{0}>{1}({2})", e1, accId, c));
                        }
                        matrixFile.Write(string.Join(",", accData.ToArray()));
                    }
                    matrixFile.Write("\t");
                }
                if (someSplicedOutExon) numGenesSpliceOut++;
                matrixFile.WriteLine();
            }
            string summaryLine = string.Format("Totally {0} splice events that excluded some exon were detected in {1} genes by {2} reads.",
                                 numSpliceOuts, numGenesSpliceOut, totSpliceOutCount);
            matrixFile.WriteLine(summaryLine);
            summaryLines.Add(summaryLine);
            string vTxt = (noGeneVariants) ? "" : numGenesWithSPLCHits + " out of all and ";
            summaryLine = vTxt + numMainGenesWithSPLCHits + " out of main gene variants had hits to some splice junction.";
            matrixFile.WriteLine(summaryLine);
            summaryLine += " For details, view the diff_splice.tab file\n";
            summaryLines.Add(summaryLine);
            matrixFile.Close();
        }

        /// <summary>
        /// For every expressed gene, write the binned hit count profile across the transcript.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteTranscriptHitsByGeneLocus(string fileNameBase)
        {
            var file = (fileNameBase + "_transcript_profile.tab").OpenWrite();
            file.WriteLine("Binned number of hits to transcripts counting from 3' end");
            file.Write("Gene\tTrLen\tTotHits\tExonHits\t3'-");
            for (int i = 0; i < 100; i++)
                file.Write("{0}\t", i * GeneFeature.LocusProfileBinSize);
            file.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (!gf.IsExpressed()) continue;
                file.Write(gf.Name + "\t" + gf.GetTranscriptLength() + "\t" + 
                           gf.GetTotalHits() + "\t" + gf.GetTranscriptHits() + "\t");
                int[] trBinCounts = CompactGenePainter.GetBinnedTranscriptHitsRelEnd(gf, GeneFeature.LocusProfileBinSize, props.DirectionalReads);
                foreach (int c in trBinCounts)
                    file.Write(c + "\t");
                file.WriteLine();
            }
            file.Close();
        }

        /// <summary>
        /// For every locus, write the binned sense hit count profile across the chromosome.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteLocusHitsByGeneLocus(string fileNameBase)
        {
            var file = (fileNameBase + "_genelocus_profile.tab").OpenWrite();
            file.WriteLine("Binned number of hits to either strand of gene loci relative to 3' end including flank:");
            file.Write("Gene\tTrscrDir\tLen\tChrStrand\tTotHits\t3'End->");
            for (int i = 0; i < 100; i++)
                file.Write("{0}bp\t", i * GeneFeature.LocusProfileBinSize);
            file.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.GetTotalHits() == 0) continue;
                file.Write(gf.Name + "\t" + gf.Strand + "\t" + gf.Length + "\t+\t" + gf.GetTotalHits(true) + "\t");
                int[] fwCounts = CompactGenePainter.GetLocusBinCountsRel3PrimeEnd(gf, '+');
                foreach (int c in fwCounts)
                    file.Write(c + "\t");
                file.WriteLine();
                file.Write(gf.Name + "\t" + gf.Strand + "\t" + gf.Length + "\t-\t" + gf.GetTotalHits(false) + "\t");
                int[] revCounts = CompactGenePainter.GetLocusBinCountsRel3PrimeEnd(gf, '-');
                foreach (int c in revCounts)
                    file.Write(c + "\t");
                file.WriteLine();
            }
            file.Close();
        }

        private void WriteUniquehits(string fileNameBase)
        {
            var file = (fileNameBase + "_unique_hits.tab").OpenWrite();
            file.WriteLine("Unique hit positions in gene loci relative to first position of a {0} bp flank, counting in chr direction:",
                            GeneFeature.LocusFlankLength);
            file.Write("Gene\tTrscrDir\tChr\tLocusChrPos\tLeftFlankStart\tRightFlankEnd\tChrStrand\t#Positions\t");
            file.WriteLine();
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.GetTotalHits() == 0) continue;
                int chrRefPos = gf.Start - GeneFeature.LocusFlankLength;
                int[] cs = CompactGenePainter.GetHitPositions(gf, '+');
                file.Write(gf.Name + "\t" + gf.Strand + "\t" + gf.Chr + "\t" +
                           chrRefPos + "\t" +
                           (gf.LocusStart - chrRefPos) + "\t" + (gf.LocusEnd - chrRefPos) + "\t" +
                           "+" + "\t" + cs.Length + "\t");
                foreach (int p in cs)
                    file.Write(p + "\t");
                file.WriteLine();
                cs = CompactGenePainter.GetHitPositions(gf, '-');
                file.Write(gf.Name + "\t" + gf.Strand + "\t" + gf.Chr + "\t" +
                           chrRefPos + "\t" +
                           (gf.LocusStart - chrRefPos) + "\t" + (gf.LocusEnd - chrRefPos) + "\t" +
                           "-" + "\t" + cs.Length + "\t");
                foreach (int p in cs)
                    file.Write(p + "\t");
                file.WriteLine();
            }
        }

        /// <summary>
        /// Write the gene images
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteGeneImages(string fileNameBase)
        {
            string imgDir = Directory.CreateDirectory(fileNameBase + "_gene_images").FullName;
            foreach (string gene in props.GenesToPaint)
            {
                if (!geneFeatures.ContainsKey(gene))
                    continue;
                GeneFeature gf = geneFeatures[gene];
                ushort[,] imgData = CompactGenePainter.GetGeneImageData(gf);
                string safeGene = PathHandler.MakeSafeFilename(gene);
                string gifFile = Path.Combine(imgDir, safeGene + ".gif");
                WriteGifImage(imgData, gifFile, new int[] { 0, 10, 100, 1000, 10000 }, 
                              new Color[] { Color.Wheat, Color.Yellow, Color.Orange, Color.Red, Color.Purple });
            }
        }

        private static void WriteGifImage(ushort[,] imgData, string gifFile,
                                          int[] levels, Color[] colors)
        {
            Sort.QuickSort(levels, colors);
            int rows = imgData.GetLength(0);
            int columns = imgData.GetLength(1);
            Bitmap bm = new Bitmap(rows, columns);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int v = (int)(imgData[r, c]);
                    Color color = Color.Black;
                    for (int i = levels.Length - 1; i >= 0; i--)
                    {
                        if (v > levels[i])
                        {
                            color = colors[i];
                            break;
                        }
                    }
                    bm.SetPixel(r, c, color);
                }
            }
            bm.Save(gifFile, ImageFormat.Gif);
            int p = (int) Environment.OSVersion.Platform;
            if ((p == 4) || (p == 6) || (p == 128))
            {
                CmdCaller.Run("chmod", "a+rw " + gifFile);
            }
        }

        /// <summary>
        /// Write transcript images
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteTranscriptImages(string fileNameBase)
        {
            string imgDir = Directory.CreateDirectory(fileNameBase + "_transcript_images").FullName;
            foreach (string gene in props.GenesToPaint)
            {
                if (!geneFeatures.ContainsKey(gene))
                    continue;
                int[] bcodeCounts = new int[barcodes.Count];
                Array.Copy(geneFeatures[gene].TranscriptHitsByBarcode, bcodeCounts, barcodes.Count);
                int[] bcodesOrderedByCount = new int[bcodeCounts.Length];
                for (int i = 0; i < bcodeCounts.Length; i++)
                    bcodesOrderedByCount[i] = i;
                Sort.QuickSort(bcodeCounts, bcodesOrderedByCount);
                GeneFeature gf = geneFeatures[gene];
                ushort[,] imgData = CompactGenePainter.GetTranscriptImageData(gf, bcodesOrderedByCount);
                string safeGene = PathHandler.MakeSafeFilename(gene);
                string gifFile = Path.Combine(imgDir, safeGene + "_mRNA.gif");
                WriteGifImage(imgData, gifFile, new int[] {0, 3, 15},
                              new Color[] { Color.Orange, Color.Yellow, Color.Wheat} );
                string siteFile = Path.Combine(imgDir, safeGene + "_positions.tab");
                WriteSwitchSites(imgData, bcodesOrderedByCount, siteFile);
            }
        }

        private void WriteSwitchSites(ushort[,] imgData, int[] orderedBcIndexes, string txtFile)
        {
            int nPositions = imgData.GetLength(0);
            int nBarcodes = imgData.GetLength(1);
            StreamWriter file = txtFile.OpenWrite();
            for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                file.Write("\t" + barcodes.GetWellId(orderedBcIndexes[bcRow]));
            file.WriteLine();
            file.Write("PosFrom5'End");
            for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                file.Write("\t" + barcodes.Seqs[orderedBcIndexes[bcRow]]);
            file.WriteLine();
            for (int pos = 0; pos < nPositions; pos++)
            {
                file.Write(pos);
                for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                    file.Write("\t" + imgData[pos, bcRow]);
                file.WriteLine();
            }
            file.Close();
        }

        private void WriteElongationEfficiency(string fileNameBase)
        {
            int capRegionSize = props.CapRegionSize;
            int trLenBinSize = 500;
            int nSections = 20;
            var capHitsFile = (fileNameBase + "_cap_hits.tab").OpenWrite();
            WriteSpikeElongationHitCurve(capHitsFile, trLenBinSize, nSections);
            WriteElongationHitCurve(capHitsFile, trLenBinSize, nSections);
            // The old style hit statistics profile below:
            int minHitsPerGene = 50;
            capHitsFile.WriteLine("\n\nFraction hits that are to the 5' {0} bases of genes with >= {1} hits, grouped by transcript length (BinSize={2})",
                                 capRegionSize, minHitsPerGene, trLenBinSize);
            capHitsFile.WriteLine("\n\nSpike RNAs:");
            AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, capRegionSize, trLenBinSize, true);
            capHitsFile.WriteLine("\nOther RNAs:");
            AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, capRegionSize, trLenBinSize, false);
            capHitsFile.Close();
        }

        /// <summary>
        /// Old style analysis comparing a 5' region to the rest.
        /// </summary>
        /// <param name="capHitsFile"></param>
        /// <param name="minHitsPerGene"></param>
        /// <param name="capRegionSize"></param>
        /// <param name="binSize"></param>
        /// <param name="analyzeSpikes"></param>
        private void AnalyzeWriteElongationSection(StreamWriter capHitsFile, int minHitsPerGene, int capRegionSize, 
                                                   int binSize, bool analyzeSpikes)
        {
            DescriptiveStatistics allFracs = new DescriptiveStatistics();
            int nBins = 10000 / binSize;
            DescriptiveStatistics[] binnedEfficiencies = new DescriptiveStatistics[nBins];
            DescriptiveStatistics[,] bcBinnedEfficiencies = new DescriptiveStatistics[nBins, 96];
            int[] nGenes = new int[nBins];
            for (int di = 0; di < binnedEfficiencies.Length; di++)
            {
                binnedEfficiencies[di] = new DescriptiveStatistics();
                for (int bc = 0; bc < 96; bc++)
                    bcBinnedEfficiencies[di, bc] = new DescriptiveStatistics();
            }
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                int trLen = gf.GetTranscriptLength();
                if (trLen < capRegionSize * 2)
                    continue;
                if (analyzeSpikes != gf.Name.StartsWith("RNA_SPIKE")) 
                    continue;
                if (gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int bin = Math.Min(nBins - 1, trLen / binSize);
                int[] capCounts = CompactGenePainter.GetBarcodedTranscriptCounts(gf, 0, capRegionSize - 1);
                int[] allCounts = CompactGenePainter.GetBarcodedTranscriptCounts(gf, 0, trLen - 1);
                int capCountSum = 0;
                int allCountSum = 0;
                for (int bc = 0; bc < capCounts.Length; bc++)
                {
                    capCountSum += capCounts[bc];
                    allCountSum += allCounts[bc];
                    if (allCounts[bc] > 0)
                    {
                        double bcFrac = capCounts[bc] / (double)allCounts[bc];
                        bcBinnedEfficiencies[bin, bc].Add(bcFrac);
                    }
                }
                double frac = capCountSum / (double)allCountSum;
                binnedEfficiencies[bin].Add(frac);
                nGenes[bin]++;
                allFracs.Add(frac);
            }
            capHitsFile.WriteLine("Overall average fraction is " + allFracs.Mean());
            capHitsFile.Write("\nMidLength\tFraction\t#GenesInBin\t");
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(genome);
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t" + barcodes.Seqs[idx]);
            capHitsFile.WriteLine();
            capHitsFile.Write("\t\t\t");
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t" + barcodes.GetWellId(idx));
            capHitsFile.WriteLine();
            for (int di = 0; di < binnedEfficiencies.Length; di++)
            {
                int binMid = di * binSize + binSize / 2;
                capHitsFile.Write("{0}\t{1:0.####}\t{2}\t", binMid, binnedEfficiencies[di].Mean(), nGenes[di]);
                foreach (int idx in speciesBcIndexes)
                    capHitsFile.Write("\t{0:0.####}", bcBinnedEfficiencies[di, idx].Mean());
                capHitsFile.WriteLine();
            }
        }

        private void WriteElongationHitCurve(StreamWriter capHitsFile, int trLenBinSize, int nSections)
        {
            int nTrSizeBins = 10000 / trLenBinSize;
            int minHitsPerGene = nSections * 10;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[nTrSizeBins, nSections];
            for (int di = 0; di < nTrSizeBins; di++)
            {
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[di, section] = new DescriptiveStatistics();
            }
            int[] geneCounts = new int[nTrSizeBins];
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.Name.StartsWith("RNA_SPIKE"))
                    continue;
                if (gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                int trLenBin = Math.Min(nTrSizeBins - 1, trLen / trLenBinSize);
                double posBinSize = trLen / (double)nSections;
                int[] trBinCounts = CompactGenePainter.GetBinnedTranscriptHitsRelEnd(gf, posBinSize, props.DirectionalReads);
                double allCounts = 0.0;
                foreach (int c in trBinCounts) allCounts += c;
                int trIdx = nSections - 1;
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[trLenBin, section].Add(trBinCounts[trIdx--] / allCounts);
                geneCounts[trLenBin]++;
            }
            capHitsFile.WriteLine("Hit distribution across gene transcripts, group averages by transcript length classes.");
            capHitsFile.WriteLine("\nMidLength\tnGenes\t5' -> 3' hit distribution");
            for (int di = 0; di < nTrSizeBins; di++)
            {
                int binMid = di * trLenBinSize + trLenBinSize / 2;
                capHitsFile.Write(binMid + "\t" + geneCounts[di]);
                for (int section = 0; section < nSections; section++)
                    capHitsFile.Write("\t{0:0.####}", binnedEfficiencies[di, section].Mean());
                capHitsFile.WriteLine();
            }
        }

        private void WriteSpikeElongationHitCurve(StreamWriter capHitsFile, int trLenBinSize, int nSections)
        {
            int minHitsPerGene = nSections * 10;
            bool wroteHeader = false;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (!gf.Name.StartsWith("RNA_SPIKE") || gf.GetTotalHits(true) < 50)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double binSize = trLen / (double)nSections;
                int[] trBinCounts = CompactGenePainter.GetBinnedTranscriptHitsRelEnd(gf, binSize, props.DirectionalReads);
                if (trBinCounts.Length == 0) continue;
                double allCounts = 0.0;
                foreach (int c in trBinCounts) allCounts += c;
                int trIdx = nSections - 1;
                if (!wroteHeader)
                {
                    capHitsFile.WriteLine("Hit distribution across spike transcripts.");
                    capHitsFile.WriteLine("\nSpike\tLength\t5' -> 3' hit distribution");
                    wroteHeader = true;
                }
                capHitsFile.Write("{0}\t{1}", gf.Name, trLen);
                for (int section = 0; section < nSections; section++)
                {
                    double eff = trBinCounts[trIdx--] / allCounts;
                    capHitsFile.Write("\t{0:0.####}", eff);
                }
                capHitsFile.WriteLine();
            }
            capHitsFile.WriteLine();
        }

    }

}
