using System;
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
        /// <summary>
        /// Keeps number of overlapping exons for counter-orientation overlapping genes.
        /// The Keys follow the pattern "GeneName1#GeneName2"
        /// </summary>
        Dictionary<string, int> antisensePairExons = new Dictionary<string, int>();
        /// <summary>
        /// Used during gene loading to bind splices correctly to genes
        /// </summary>
        private string lastLoadedGeneName;

        public UCSCGenomeAnnotations(Props props, StrtGenome genome)
            : base(props, genome)
        {
        }

        public override void Load()
        {
            ChrIdToFileMap = genome.GetStrtChrFilesMap();
            foreach (string chrId in ChrIdToFileMap.Keys)
            {
                ExonAnnotations[chrId] = new QuickAnnotationMap(annotationBinSize);
                NonExonAnnotations[chrId] = new QuickAnnotationMap(annotationBinSize);
            }
            if (needChromosomeSequences || needChromosomeLengths)
                ReadChromsomeSequences(ChrIdToFileMap);
            RegisterGenesAndIntervals();
            if (Background.CancellationPending) return;
            string[] rmskFiles = PathHandler.GetRepeatMaskFiles(genome);
            Console.Write("Reading {0} masking files..", rmskFiles.Length);
            foreach (string rmskFile in rmskFiles)
            {
                Console.Write(".");
                LoadRepeatMaskFile(rmskFile);
            }
            Console.WriteLine("{0} annotated repeat types.", repeatFeatures.Count);
        }

        public override string[] GetChromosomeIds()
        {
 	        return ExonAnnotations.Keys.ToArray();
        }

        private void AdjustEndsAndMarkUpOverlaps()
        {
            int nMarkedExons = 0, nMarkedGenes = 0, totalMarkedLen = 0, totalFullyExtended5Primes = 0, totalMaskedIntronicFeatures = 0;
            foreach (string chrId in GetChromosomeIds())
            {
                if (StrtGenome.IsSyntheticChr(chrId))
                    continue;
                int nStrandExons, nStrandGenes, totalStrandLen, nFullyExtended5Primes, nMaskedIntronicFeatures;
                AdjustEndAndMarkUpOverlapsOnChr(chrId, out nStrandExons, out nStrandGenes, out totalStrandLen,
                                          out nFullyExtended5Primes, out nMaskedIntronicFeatures);
                nMarkedExons += nStrandExons;
                nMarkedGenes += nStrandGenes;
                totalMarkedLen += totalStrandLen;
                totalFullyExtended5Primes += nFullyExtended5Primes;
                totalMaskedIntronicFeatures += nMaskedIntronicFeatures;

            }
            Console.WriteLine("{0} genes had the 5' exon extended {1} bp as specified by GeneFeature5PrimeExtension property.",
                              totalFullyExtended5Primes, props.GeneFeature5PrimeExtension);
            if (nMarkedExons > 0)
                Console.WriteLine("{0} overlapping anti-sense exons from {1} genes ({2} bps) were masked from statistics calculations.",
                              nMarkedExons, nMarkedGenes, totalMarkedLen);
            Console.WriteLine("{0} USTR/DSTR/INTR features that overlap with an exon were masked from statistics calculations.", totalMaskedIntronicFeatures);
        }

        private void AdjustEndAndMarkUpOverlapsOnChr(string chrId, out int nMaskedASExons, out int nMaskedASGenes,
                             out int totalMaskedASLength, out int nFullyExtended5Primes, out int nMaskedIntronicFeatures)
        {
            int[] sortedExonStarts;
            int[] exonEnds;
            bool[] startSortedExonStrands;
            GeneFeature[] geneFeatureByExon;
            CollectExonsOfAllGenes(chrId, out sortedExonStarts, out exonEnds, out startSortedExonStrands, out geneFeatureByExon);
            int[] sortedExonEnds = (int[])exonEnds.Clone();
            bool[] endSortedExonStrands = (bool[])startSortedExonStrands.Clone();
            Sort.QuickSort(sortedExonEnds, endSortedExonStrands);
            nMaskedASGenes = 0; nMaskedASExons = 0; totalMaskedASLength = 0; nFullyExtended5Primes = 0; nMaskedIntronicFeatures = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.Chr == chrId)
                {
                    int extension = gf.AdjustFlanksAnd5PrimeExtend(sortedExonStarts, startSortedExonStrands, exonEnds, endSortedExonStrands);
                    if (extension == props.GeneFeature5PrimeExtension) nFullyExtended5Primes++;
                    nMaskedIntronicFeatures += gf.MaskOverlappingUSTRDSTRINTR(sortedExonStarts, exonEnds);
                    List<int> indicesOfMasked = gf.MaskOverlappingAntisenseExons(sortedExonStarts, exonEnds, startSortedExonStrands);
                    if (indicesOfMasked.Count > 0)
                    {
                        nMaskedASExons += indicesOfMasked.Count;
                        nMaskedASGenes++;
                        totalMaskedASLength += gf.GetTranscriptLength() - gf.GetNonMaskedTranscriptLength();
                        foreach (int idx in indicesOfMasked)
                        {
                            string[] names = new string[] { gf.Name, geneFeatureByExon[idx].Name };
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

        /// <summary>
        /// All four arrays will come out sorted on the exon Starts
        /// </summary>
        /// <param name="chrId"></param>
        /// <param name="sortedExonStarts"></param>
        /// <param name="exonEnds"></param>
        /// <param name="exonStrands"></param>
        /// <param name="gFeatureByExon"></param>
        private void CollectExonsOfAllGenes(string chrId, out int[] sortedExonStarts, out int[] exonEnds,
                                             out bool[] exonStrands, out GeneFeature[] gFeatureByExon)
        {
            sortedExonStarts = new int[30000];
            exonEnds = new int[30000];
            exonStrands = new bool[30000];
            gFeatureByExon = new GeneFeature[30000];
            int exonIdx = 0;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.Chr == chrId)
                    for (int i = 0; i < gf.ExonCount; i++)
                    {
                        sortedExonStarts[exonIdx] = gf.ExonStarts[i];
                        exonEnds[exonIdx] = gf.ExonEnds[i];
                        exonStrands[exonIdx] = (gf.Strand == '+')? true : false;
                        gFeatureByExon[exonIdx] = gf;
                        if (++exonIdx >= sortedExonStarts.Length)
                        {
                            Array.Resize(ref sortedExonStarts, exonIdx + 20000);
                            Array.Resize(ref exonEnds, exonIdx + 20000);
                            Array.Resize(ref exonStrands, exonIdx + 20000);
                            Array.Resize(ref gFeatureByExon, exonIdx + 20000);
                        }
                    }
            }
            Array.Resize(ref sortedExonStarts, exonIdx);
            Array.Resize(ref exonEnds, exonIdx);
            Array.Resize(ref exonStrands, exonIdx);
            Array.Resize(ref gFeatureByExon, exonIdx);
            Sort.QuickSort(sortedExonStarts, exonEnds, exonStrands, gFeatureByExon);
        }

        private void ReadChromsomeSequences(Dictionary<string, string> chrIdToFileMap)
        {
            string[] selectedChrIds = props.SeqStatsChrIds;
            if (selectedChrIds == null || selectedChrIds[0] == "")
                selectedChrIds = chrIdToFileMap.Keys.ToArray();
            Console.Write("Reading {0} chromosomes...", selectedChrIds.Length);
            foreach (string chrId in chrIdToFileMap.Keys)
            {
                Console.Write(".{0}", chrId);
                if (StrtGenome.IsASpliceAnnotationChr(chrId)) continue;
                try
                {
                    int chrLen;
                    if (needChromosomeSequences && Array.IndexOf(selectedChrIds, chrId) >= 0)
                    {
                        DnaSequence chrSeq = readChromosomeFile(chrIdToFileMap[chrId]);
                        chrLen = (int)chrSeq.Count;
                        ChromosomeSequences.Add(chrId, chrSeq);
                    }
                    else
                    {
                        double fileLen = new FileInfo(chrIdToFileMap[chrId]).Length;
                        chrLen = (int)(fileLen * 80.0 / 81.0); // Get an approximate length by removing \n:s
                    }
                    ChromosomeLengths.Add(chrId, chrLen);
                }
                catch (Exception e)
                {
                    Console.WriteLine("\nERROR: Could not read chromosome {0} - {1}", chrId, e.Message);
                }
                if (Background.CancellationPending) return;
            }
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
            string[] record;
            RepeatFeature reptFeature;
            QuickAnnotationMap annotMap;
            int fileTypeOffset = 0;
            if (rmskPath.EndsWith("out"))
                fileTypeOffset = -1;
            using (StreamReader reader = rmskPath.OpenRead())
            {
                string line = reader.ReadLine();
                while (line == "" || !char.IsDigit(line.Trim()[0]))
                    line = reader.ReadLine();
                while (line != null)
                {
                    nLines++;
                    record = line.Split('\t'); // Regex.Split(line.Trim(), " +|\t");
                    string chr = record[5 + fileTypeOffset].Substring(3);
                    if (NonExonAnnotations.TryGetValue(chr, out annotMap))
                    {
                        int start = int.Parse(record[6 + fileTypeOffset]);
                        int end = int.Parse(record[7 + fileTypeOffset]);
                        string name = record[10 + fileTypeOffset];
                        nRepeatFeatures++;
                        if (!repeatFeatures.TryGetValue(name, out reptFeature))
                        {
                            repeatFeatures[name] = new RepeatFeature(name);
                            reptFeature = repeatFeatures[name];
                        }
                        reptFeature.AddRegion(start, end);
                        annotMap.Add(new FtInterval(start, end, reptFeature.MarkHit, 0, reptFeature, AnnotType.REPT, '0'));
                    }
                    line = reader.ReadLine();
                }
            }
        }

        private void RegisterGenesAndIntervals()
        {
            string annotationsPath = genome.VerifyAnAnnotationPath();
            LoadAnnotationsFile(annotationsPath);
            AdjustEndsAndMarkUpOverlaps();
            foreach (GeneFeature gf in geneFeatures.Values) 
                AddGeneIntervals((GeneFeature)gf);
        }

        private void LoadAnnotationsFile(string annotationsPath)
        {
            int nLines = 0;
            int nGeneFeatures = 0;
            int nTooLongFeatures = 0;
            foreach (LocusFeature gf in new UCSCAnnotationReader(genome).IterAnnotationFile(annotationsPath))
            {
                nLines++;
                if (noGeneVariants && gf.IsVariant())
                    continue;
                if (gf.Length > props.MaxFeatureLength)
                    nTooLongFeatures++;
                else if (RegisterGeneFeature(gf))
                {
                    nGeneFeatures++;
                }
            }
            string exlTxt = (nTooLongFeatures == 0) ? "" : string.Format(" (Excluding {0} spanning > {1} bp.)",
                                                                         nTooLongFeatures, props.MaxFeatureLength);
            string exclV = noGeneVariants ? "main" : "complete";
            Console.WriteLine("{0} {1} gene variants will be mapped.{2}", nGeneFeatures, exclV, exlTxt);
        }

        /// <summary>
        /// Adds a normal gene or a splice gene to the set of features
        /// </summary>
        /// <param name="gf"></param>
        /// <returns>true if gf represents a new gene, and not an artificial splice gene.</returns>
        private bool RegisterGeneFeature(LocusFeature gf)
        {
            if (genome.Annotation == gf.Chr) // I.e., we are on the splice chromosome
            { // Requires that real loci are registered before artificial splice loci.
                if (lastLoadedGeneName == gf.Name)
                {    // Link from artificial splice chromosome to real locus
                    ((SplicedGeneFeature)gf).BindToRealFeature(geneFeatures[gf.Name]);
                    AddGeneIntervals((SplicedGeneFeature)gf);
                }
                lastLoadedGeneName = "";
                return false;
            }
            if (geneFeatures.ContainsKey(gf.Name))
            {
                Console.WriteLine("WARNING: Duplicated gene name in annotation file: {0}", gf.Name);
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

        public override void SaveResult(string fileNameBase, int averageReadLen)
        {
            WriteIntronCounts(fileNameBase);
            if (props.GenerateGeneProfilesByBarcode)
            {
                WriteExonCountsPerBarcode(fileNameBase);
                WriteIntronCountsPerBarcode(fileNameBase);
            }
            if (props.GenerateTranscriptProfiles)
            {
                WriteTranscriptProfiles(fileNameBase);
                WriteTranscriptHistograms(fileNameBase, averageReadLen);
            }
            WriteSharedGenes(fileNameBase);
            WritePotentialErronousAnnotations(fileNameBase);
            WriteSplicesByGeneLocus(fileNameBase);
            if (props.AnalyzeSpliceHitsByBarcode)
                WriteSplicesByGeneLocusAndBc(fileNameBase);
            if (barcodes.HasRandomBarcodes)
            {
                WriteTrueMolsTable(fileNameBase);
                WriteReadsTable(fileNameBase);
            }
            string expressionFile = WriteExpressionTable(fileNameBase);
            WriteMinExpressionTable(fileNameBase);
            if (props.DirectionalReads)
            {
                WriteCAPHitsTable(fileNameBase);
                WriteExpressedAntisenseGenes(fileNameBase);
                WriteUniquehits(fileNameBase);
            }
            string rpmFile = WriteNormalizedExpression(fileNameBase);
            if (!Environment.OSVersion.VersionString.Contains("Microsoft"))
            {
                CmdCaller.Run("php", "strt2Qsingle.php " + rpmFile);
                CmdCaller.Run("php", "expression2forR.php " + expressionFile);
            }
            if (props.GenerateGeneLocusProfiles)
                WriteLocusHitsByGeneLocus(fileNameBase);
            if (props.GenesToPaint != null && props.GenesToPaint.Length > 0)
            {
                PaintSelectedGeneLocusImages(fileNameBase);
                PaintSelectedGeneTranscriptImages(fileNameBase);
            }
            WriteAnnotTypeAndExonCounts(fileNameBase);
            WriteElongationEfficiency(fileNameBase, averageReadLen);
        }

        private void WriteExpressedAntisenseGenes(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_expressed_antisense.tab"))
            {
                file.WriteLine("Expressed gene pairs transcribed in opposite direction that have overlapping exons. " +
                               "Numbers of overlapping exons and the expression from the non-overlapping exons is shown.");
                file.WriteLine("Chr\tGeneA\tGeneB\t#OverlappingExons\tCountA\tCountB\t" +
                               "OverlappingExonsA\tNonoverlappingCountA\tOverlappingExonsB\tNonoverlappingCountB");
                int nPairs = 0;
                foreach (KeyValuePair<string, int> gfPair in antisensePairExons)
                {
                    string[] names = gfPair.Key.Split('#');
                    GeneFeature gfA = geneFeatures[names[0]];
                    GeneFeature gfB = geneFeatures[names[1]];
                    if (gfA.Chr != gfB.Chr || gfA.Strand == gfB.Strand)
                        throw new Exception("Internal error in sense-antisense genes: " + names[0] + "-" + names[1]);
                    int aHits = gfA.GetTranscriptHits();
                    int bHits = gfB.GetTranscriptHits();
                    if (aHits > 0 && bHits > 0)
                    {
                        List<int> freeExonsA = GetNonOverlappingExons(gfA, gfB);
                        int freeHitsA = gfA.GetExpressionFromExons(freeExonsA);
                        List<int> freeExonsB = GetNonOverlappingExons(gfB, gfA);
                        int freeHitsB = gfB.GetExpressionFromExons(freeExonsB);
                        int nCommonExons = gfPair.Value;
                        string freeExonsAList = MakeExonNumberList(freeExonsA);
                        string freeExonsBList = MakeExonNumberList(freeExonsB);
                        file.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}", gfA.Chr, names[0], names[1], nCommonExons,
                                       aHits, bHits, freeExonsAList, freeHitsA, freeExonsBList, freeHitsB);
                        nPairs++;
                    }
                }
            }
        }

        private string MakeExonNumberList(List<int> exonIdxs)
        {
            if (exonIdxs.Count == 0)
                return "-";
            StringBuilder sb = new StringBuilder();
            sb.Append((exonIdxs[0] + 1));
            for (int i = 1; i < exonIdxs.Count; i++)
            {
                sb.Append(',');
                sb.Append(exonIdxs[i] + 1);
            }
            return sb.ToString();
        }

        private List<int> GetNonOverlappingExons(GeneFeature gf, GeneFeature maskGf)
        {
            List<int> freeExons = new List<int>();
            for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
            {
                if (!maskGf.ExonsWithin(gf.ExonStarts[exonIdx], gf.ExonEnds[exonIdx], 0))
                    freeExons.Add(exonIdx);
            }
            return freeExons;
        }

        private void WritePotentialErronousAnnotations(string fileNameBase)
        {
            string warnFilename = fileNameBase + "_annot_errors_" + genome.GetBowtieMainIndexName() + ".tab";
            int nErr = 0;
            using (StreamWriter warnFile = new StreamWriter(warnFilename))
            {
                warnFile.WriteLine("Feature\tExonHits\tPart\tPartHits\tPartLocation\tNewLeftExonStart\tNewRightExonStart");
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
            }
            if (nErr == 0)
                File.Delete(warnFilename);
        }

        private int TestErrorAnnotType(StreamWriter warnFile, GeneFeature gf, int start, int end, int annotType)
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

        private void WriteReadsTable(string fileNameBase)
        {
            string readFile = fileNameBase + "_reads.tab";
            Func<GeneFeature, int[]> getReads = x => x.TranscriptReadsByBarcode;
            WriteBasicDataTable(readFile, "Total maximal read counts in barcodes for each gene and repeat.", getReads);
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            using (StreamWriter outFile = new StreamWriter(readFile, true))
            {
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (int idx in speciesBcIndexes)
                    {
                        sb.Append("\t");
                        sb.Append(rf.TotalReadsByBarcode[idx]);
                    }
                    outFile.WriteLine("r_{0}\t\t\t\t{1}\t{2}{3}", rf.Name, rf.GetLocusLength(), rf.TotalReadsByBarcode.Sum(), sb);
                }
            }
        }

        private void WriteTrueMolsTable(string fileNameBase)
        {
            Func<GeneFeature, int[]> getTrueMols = x => x.EstimatedTrueMolsByBarcode;
            WriteBasicDataTable(fileNameBase + "_true_counts.tab", "Estimated true molecule counts.", getTrueMols);
        }

        private void WriteBasicDataTable(string fileName, string header, Func<GeneFeature, int[]> dataGetter)
        {
            using (StreamWriter outFile = new StreamWriter(fileName))
            {
                outFile.WriteLine(header);
                WriteExtraDataTableHeaders(outFile);
                WriteBarcodeHeaders(outFile, 5, "");
                outFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tExonHits");
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int[] data = dataGetter(gf);
                    StringBuilder sbDatarow = new StringBuilder();
                    foreach (int idx in speciesBcIndexes)
                    {
                        sbDatarow.Append("\t");
                        sbDatarow.Append(data[idx]);
                    }
                    outFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}{6}",
                                   gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), data.Sum(), sbDatarow);

                }
            }
        }

        /// <summary>
        /// For each feature, write the total (for genes, transcript) hit count for every barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        /// <returns>Path to output file</returns>
        private string WriteExpressionTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_expression.tab";
            Func<GeneFeature, int[]> getMaxHits = x => x.TranscriptHitsByBarcode;
            return WriteExtendedDataTable(exprPath, "maximum (including multireads)", getMaxHits);
        }
        private string WriteMinExpressionTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_expression_singlereads.tab";
            Func<GeneFeature, int[]> getMaxHits = x => x.NonConflictingTranscriptHitsByBarcode;
            return WriteExtendedDataTable(exprPath, "minimum (uniquely mapped)", getMaxHits);
        }
        private string WriteExtendedDataTable(string fileName, string dataType, Func<GeneFeature, int[]> dataGetter)
        {
            using (StreamWriter matrixFile = new StreamWriter(fileName))
            {
                matrixFile.WriteLine("Length, total and per barcode {0} transcript hits for transcripts, and total length, total and per barcode (both sense & antisense) hits for repeat regions grouped by type.", dataType);
                matrixFile.WriteLine("The totals under MinExonHits correspond to single reads, having a unique genome mapping.");
                WriteExtraDataTableHeaders(matrixFile);
                string matrixValType = barcodes.HasRandomBarcodes ? "(Values are molecule counts)" : "(Values are read counts)";
                WriteBarcodeHeaders(matrixFile, 6, matrixValType);
                matrixFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tMinExonHits\tExonHits");
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int ncHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                    int maxHits = gf.TranscriptHitsByBarcode.Sum();
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                                     gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), ncHits, maxHits);
                    int[] data = dataGetter(gf);
                    foreach (int idx in speciesBcIndexes)
                        matrixFile.Write("\t{0}", data[idx]);
                    matrixFile.WriteLine();
                }
                foreach (RepeatFeature rf in repeatFeatures.Values)
                {
                    matrixFile.Write("r_{0}\t\t\t\t{1}\t{2}\t{2}", rf.Name, rf.GetLocusLength(), rf.GetTotalHits());
                    foreach (int idx in speciesBcIndexes)
                        matrixFile.Write("\t{0}", rf.TotalHitsByBarcode[idx]);
                    matrixFile.WriteLine();
                }
            }
            return fileName;
        }

        private void WriteExtraDataTableHeaders(StreamWriter tableOutFile)
        {
            if (props.UseMost5PrimeExonMapping && props.DirectionalReads)
            {
                tableOutFile.WriteLine("Multireads have been assigned to their most 5' exonic hit and included in totals under ExonHits.");
            }
            else
            {
                tableOutFile.WriteLine("Multireads are multiply assigned to all their exonic hits and included in totals under ExonHits.");
                tableOutFile.WriteLine("NOTE: Also gene variants occupying the same locus share ExonHits and table counts.");
            }
            if (!props.DirectionalReads)
                tableOutFile.WriteLine("NOTE: This is a non-STRT analysis with non-directional reads.");
        }

        private void WriteCAPHitsTable(string fileNameBase)
        {
            string exprPath = fileNameBase + "_CAPRegionHits.tab";
            using (StreamWriter matrixFile = new StreamWriter(exprPath))
            {
                string hitType = barcodes.HasRandomBarcodes ? "molecule" : "read";
                matrixFile.WriteLine("Total and per barcode uniquely mapping {0} within +/- {1} of CAP site.", hitType, props.CAPRegionSpan);
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                matrixFile.Write("Feature\tChr\tCAPPos\tStrand\tAllTrscrHits\tSumCAPHits");
                foreach (int idx in speciesBcIndexes)
                    matrixFile.Write("\t{0}", barcodes.GetWellId(idx));
                matrixFile.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int totalHits = gf.CAPRegionHitsByBarcode.Sum();
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                     gf.Name, gf.Chr, gf.SavedCAPPos, gf.Strand, gf.GetTranscriptHits(), totalHits);
                    foreach (int idx in speciesBcIndexes)
                        matrixFile.Write("\t{0}", gf.CAPRegionHitsByBarcode[idx]);
                    matrixFile.WriteLine();
                }
            }
        }

        /// <summary>
        /// For each barcode and locus, write the total hit count
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteExpressionList(string fileNameBase)
        {
            // Create a normal-form table of hit counts
            using (StreamWriter tableFile = new StreamWriter(fileNameBase + "_expression_list.tab"))
            {
                string exonHitType = (props.UseMost5PrimeExonMapping && props.DirectionalReads) ? "ExonHits" : "MaxExonHits";
                tableFile.WriteLine("Barcode\tFeature\t{0}", exonHitType);
                tableFile.WriteLine();
                int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    foreach (int idx in speciesBcIndexes)
                    {
                        tableFile.Write("{0}\t", barcodes.Seqs[idx]);
                        tableFile.Write("{0}\t", gf.Name);
                        tableFile.WriteLine(gf.TranscriptHitsByBarcode[idx]);
                    }
                }
            }
        }

        private void WriteBarcodeHeaders(StreamWriter matrixFile, int nTabs, string firstField)
        {
            String tabs = new String('\t', nTabs);
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            matrixFile.Write("{0}{1}Barcode:", firstField, tabs);
            foreach (int idx in speciesBcIndexes) matrixFile.Write("\t{0}", barcodes.Seqs[idx]);
            matrixFile.WriteLine();
            matrixFile.Write("{0}Sample:", tabs);
            foreach (int idx in speciesBcIndexes)
                matrixFile.Write("\t{0}", barcodes.GetWellId(idx));
            matrixFile.WriteLine();
            foreach (string annotation in barcodes.GetAnnotationTitles())
            {
                matrixFile.Write("{0}{1}:", tabs, annotation);
                foreach (int idx in speciesBcIndexes)
                    matrixFile.Write("\t{0}", barcodes.GetAnnotation(annotation, idx));
                matrixFile.WriteLine();
            }
        }

        private void WriteSharedGenes(string fileNameBase)
        {
            using (StreamWriter trShareFile = new StreamWriter(fileNameBase + "_sharing_genes.tab"))
            {
                trShareFile.WriteLine("Transcripts/variants competing for reads (# shared reads within parenthesis)");
                trShareFile.WriteLine("Feature\t#Assigned Reads\tCompetes with genes...");
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    List<string> sGfGroup = new List<string>();
                    foreach (KeyValuePair<IFeature, int> pair in gf.sharingGenes)
                    {
                        string sGfName = pair.Key.Name;
                        if (!sGfGroup.Contains(sGfName) && sGfName != gf.Name)
                            sGfGroup.Add(string.Format("{0}({1})", sGfName, pair.Value));
                    }
                    sGfGroup.Sort();
                    if (sGfGroup.Count > 0)
                        trShareFile.WriteLine("{0}\t{1}\t{2}", gf.Name, gf.TranscriptReadsByBarcode.Sum(), string.Join("\t", sGfGroup.ToArray()));
                }
            }
        }

        public override void WriteSpikeDetection(StreamWriter xmlFile)
        {
            StringBuilder sbt = new StringBuilder();
            StringBuilder sbf = new StringBuilder();
            foreach (GeneFeature gf in IterTranscripts(true))
            {
                if (!gf.IsExpressed())
                    continue;
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
                sbt.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0}\" />\n", spikeId, total));
                sbf.Append(string.Format("      <point x=\"#{0}\" y=\"{1:0.###}\" />\n", spikeId, fraction));
            }
            if (sbt.Length > 0)
            {
                xmlFile.WriteLine("  <spikedetection>");
                xmlFile.WriteLine("    <title>Detection of spikes across all {0} wells</title>", barcodes.Count);
                xmlFile.WriteLine("    <curve legend=\"total reads\" yaxis=\"right\" color=\"black\">");
                xmlFile.Write(sbt.ToString());
                xmlFile.WriteLine("    </curve>");
                xmlFile.WriteLine("    <curve legend=\"frac. of wells\" yaxis=\"left\" color=\"blue\">");
                xmlFile.Write(sbf.ToString());
                xmlFile.WriteLine("    </curve>");
                xmlFile.WriteLine("  </spikedetection>");
            }
        }

        private string WriteNormalizedExpression(string fileNameBase)
        {
            bool molCounts = barcodes.HasRandomBarcodes;
            string ROrM = molCounts ? "Mols" : "Reads";
            string rpType = ROrM + (props.UseRPKM ? "PerKBases" : "") + (molCounts ? "Normalized" : "PerMillion");
            string rpmPath = fileNameBase + "_" + rpType + ".tab";
            using (StreamWriter matrixFile = new StreamWriter(rpmPath))
            using (StreamWriter simpleTableFile = new StreamWriter(fileNameBase + "_" + rpType + "_simple.txt"))
            {
                string rpDescr = (molCounts ? "molecules " : "reads ") + (props.UseRPKM ? "per kilobase transcript and " : "") +
                                 (molCounts ? "normalized to the average across all samples" : "million");
                matrixFile.WriteLine("Values in the table are " + rpDescr);
                matrixFile.WriteLine("Note that added spikes and transcripts are normalized separately.");
                if (props.DirectionalReads)
                {
                    matrixFile.WriteLine("The given estimated detection thresholds ('P') are calculated from 99% and 99.9% of the global distribution ");
                    matrixFile.WriteLine("of AntiSense Exon hits, and the normalized values for main transcripts in each barcode.");
                }
                if (!props.UseRPKM)
                    matrixFile.WriteLine("Single{0} is the value (in that barcode and spike or sample section) that corresponds to a single {1}.",
                                         (molCounts ? "Mol" : "Read"), (molCounts ? "molecule" : "read"));
                WriteBarcodeHeaders(matrixFile, (props.DirectionalReads ? 9 : 7), "");
                matrixFile.WriteLine("Feature\tChr\tPos\tStrand\tTrLen\tTotExonHits\t{0}Average\tCV", (props.DirectionalReads ? "P=0.01\tP=0.001\t" : ""));
                WriteNormalizedExprSection(matrixFile, true, null);
                matrixFile.WriteLine();
                foreach (int idx in barcodes.GenomeAndEmptyBarcodeIndexes(genome))
                    simpleTableFile.Write("\t{0}", barcodes.GetWellId(idx));
                simpleTableFile.WriteLine();
                WriteNormalizedExprSection(matrixFile, false, simpleTableFile);
            }
            return rpmPath;
        }

        private void WriteNormalizedExprSection(StreamWriter matrixFile, bool selectSpikes, StreamWriter simpleTableFile)
        {
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            int[] totalByBarcode = GetTotalTranscriptCountsByBarcode(selectSpikes);
            int totCount = totalByBarcode.Sum();
            List<double> ASReadsPerBase = GetExonASOrderedReadsPerBase(selectSpikes);
            Double RPkbM999 = Double.NaN;
            Double RPkbM99 = Double.NaN;
            if (!selectSpikes && totCount > 0 && ASReadsPerBase.Count > 0)
            {
                RPkbM99 = 1000 * 1.0E+6 * ASReadsPerBase[(int)Math.Floor(ASReadsPerBase.Count * 0.99)] / (double)totCount;
                RPkbM999 = 1000 * 1.0E+6 * ASReadsPerBase[(int)Math.Floor(ASReadsPerBase.Count * 0.999)] / (double)totCount;
            }
            double[] normFactors = CalcNormalizationFactors(totalByBarcode);
            string normName = (props.UseRPKM) ? "Normalizer" : (barcodes.HasRandomBarcodes) ? "SingleMol" : "SingleRead";
            matrixFile.Write("{0}\t\t\t\t\t\t\t", normName);
            if (props.DirectionalReads) matrixFile.Write("\t\t");
            foreach (int idx in speciesBcIndexes)
                matrixFile.Write("\t{0:G6}", normFactors[idx]);
            matrixFile.WriteLine();
            foreach (GeneFeature gf in IterTranscripts(selectSpikes))
            {
                double trLenFactor = (props.UseRPKM)? gf.GetTranscriptLength() : 1000.0;
                matrixFile.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                 gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), gf.GetTranscriptHits());
                if (props.DirectionalReads)
                {
                    string RPkbMThres01 = string.Format("{0:G6}", RPkbM99 * gf.GetNonMaskedTranscriptLength() / trLenFactor);
                    string RPkbMThres001 = string.Format("{0:G6}", RPkbM999 * gf.GetNonMaskedTranscriptLength() / trLenFactor);
                    matrixFile.Write("\t{0}\t{1}", RPkbMThres01, RPkbMThres001);
                }
                StringBuilder sb = new StringBuilder();
                DescriptiveStatistics ds = new DescriptiveStatistics();
                foreach (int idx in speciesBcIndexes)
                {
                    double normedValue = (normFactors[idx] * gf.TranscriptHitsByBarcode[idx]) * 1000.0 / trLenFactor;
                    ds.Add(normedValue);
                    sb.AppendFormat("\t{0:G6}", normedValue);
                }
                string CV = "N/A";
                if (ds.Count > 2 && gf.GetTranscriptHits() > 0)
                    CV = string.Format("{0:G6}", (ds.StandardDeviation() / ds.Mean()));
                matrixFile.WriteLine("\t{0:G6}\t{1}{2}", ds.Mean(), CV, sb.ToString());
                if (simpleTableFile != null)
                    simpleTableFile.WriteLine(gf.Name + sb.ToString());
            }
        }

        private double[] CalcNormalizationFactors(int[] totByBarcode)
        {
            double normalizer = 1.0E+6;
            if (barcodes.HasRandomBarcodes)
            {
                double nValidBarcodes = totByBarcode.Count(v => v > 0);
                normalizer = totByBarcode.Sum() / nValidBarcodes;
            }
            double[] normFactors = Array.ConvertAll(totByBarcode, v => ((v > 0) ? (normalizer / (double)v) : 0.0));
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
            int nExonsToShow = MaxExonCount();
            using (StreamWriter file = new StreamWriter(fileNameBase + "_exons.tab"))
            {
                file.WriteLine("Shows hits to all gene loci divided by type of annotation, direction, and exon.");
                file.WriteLine("MixGene shows overlapping genes in the same, ASGene in the opposite orientation.");
                file.Write("Gene\tChr\tPos\tStrand\tTrLen\tUSTRLen\tLocusLen\tDSTRLen\t#SenseHits\t#AntiHits\tIntronLen\t#Exons\tMixGene\tASGene");
                foreach (int i in AnnotType.GetGeneTypes())
                {
                    string annotName = AnnotType.GetName(i);
                    if (annotName == "EXON") annotName = "EXON+SPLC";
                    file.Write("\t#{0}Hits", annotName);
                }
                string exonTitle = props.DirectionalReads ? "SExon" : "Exon";
                for (int exonId = 1; exonId <= nExonsToShow; exonId++)
                    file.Write("\t{0}{1}", exonTitle, exonId);
                file.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    string mixedSenseGene = "";
                    if (gf.HitsByAnnotType[AnnotType.INTR] >= 5 ||
                        (gf.HitsByAnnotType[AnnotType.INTR] >= gf.HitsByAnnotType[AnnotType.EXON] * 0.10))
                        mixedSenseGene = OverlappingExpressedGene(gf, 10, true);
                    string mixedASGene = "";
                    if (gf.HitsByAnnotType[AnnotType.AINTR] >= 5 || gf.HitsByAnnotType[AnnotType.AEXON] >= 5)
                        mixedASGene = OverlappingExpressedGene(gf, 10, false);
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}",
                                     gf.Name, gf.Chr, gf.Start, gf.Strand, gf.GetTranscriptLength(), gf.USTRLength,
                                     gf.GetLocusLength(), gf.DSTRLength, gf.GetTotalHits(true), gf.GetTotalHits(false),
                                     gf.GetIntronicLength(), gf.ExonCount, mixedSenseGene, mixedASGene);
                    foreach (int i in AnnotType.GetGeneTypes())
                    {
                        int nHits = gf.HitsByAnnotType[i];
                        file.Write("\t{0}", nHits);
                    }
                    int[] counts = CompactGenePainter.GetCountsPerExon(gf, props.DirectionalReads);
                    WriteCountsDirected(file, gf.Strand, counts);
                }
            }
        }

        private static void WriteCountsDirected(StreamWriter file, char strand, int[] counts)
        {
            if (strand == '-')
                for (int i = counts.Length - 1; i >= 0; i--)
                    file.Write("\t{0}", counts[i]);
            else
                for (int i = 0; i < counts.Length; i++)
                    file.Write("\t{0}", counts[i]);
            file.WriteLine();
        }

        private void WriteIntronCounts(string fileNameBase)
        {
            int nIntronsToShow = MaxExonCount() + 1;
            using (StreamWriter file = new StreamWriter(fileNameBase + "_introns.tab"))
            {
                file.Write("Gene\tChr\tStrand\tUSTRStart\tLocusLen\tDSTREnd\tUSTR({0}bp)\tDSTR({0}bp)", GeneFeature.LocusFlankLength);
                for (int intronId = 1; intronId <= nIntronsToShow; intronId++)
                    file.Write("\tIntr{0}", intronId);
                file.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", gf.Name, gf.Chr, gf.Strand, gf.LocusStart, gf.GetLocusLength(), gf.LocusEnd);
                    int[] counts = CompactGenePainter.GetCountsPerIntron(gf, props.DirectionalReads);
                    if (gf.Strand == '-')
                    {
                        file.Write("\t{0}\t{1}", counts[1], counts[0]);
                        for (int i = counts.Length - 1; i >= 2; i--)
                            file.Write("\t{0}", counts[i]);
                    }
                    else
                    {
                        file.Write("\t{0}\t{1}", counts[0], counts[1]);
                        for (int i = 2; i < counts.Length; i++)
                            file.Write("\t{0}", counts[i]);
                    }
                    file.WriteLine();
                }
            }
        }

        private void WriteIntronCountsPerBarcode(string fileNameBase)
        {
            int nIntronsToShow = MaxExonCount() + 1;
            using (StreamWriter file = new StreamWriter(fileNameBase + "_introns_by_bc.tab"))
            {
                file.WriteLine("Only genes with any intronic hits are shown. Rows are truncated when only zeroes remain.");
                file.Write("Gene\tChr\tStrand\tUSTRStart\tLocusLen\tDSTREnd\tBarcode\tUSTR({0}bp\tDSTR({0}bp))",
                           GeneFeature.LocusFlankLength);
                for (int intronId = 1; intronId <= nIntronsToShow - 2; intronId++)
                    file.Write("\tIntr{0}", intronId);
                file.WriteLine();
                int[,] counts = new int[barcodes.Count, nIntronsToShow];
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int n = CompactGenePainter.GetCountsPerIntronAndBarcode(gf, props.DirectionalReads, barcodes.Count, ref counts);
                    string firstCols = string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                                                     gf.Name, gf.Chr, gf.Strand, gf.LocusStart, gf.GetLocusLength(), gf.LocusEnd);
                    string nextFirstCols = string.Format("{0}\t\t\t\t\t", gf.Name);
                    WriteCountsByBarcodeDirected(file, counts, gf, firstCols, nextFirstCols, 2, n - 1);
                }
            }
        }

        private void WriteCountsByBarcodeDirected(StreamWriter file, int[,] counts, GeneFeature gf,
                                                    string firstCols, string followingFirstCols, int startAt, int last)
        {
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                string bc = barcodes.Seqs[bcIdx];
                file.Write("{0}\t{1}", firstCols, bc);
                if (gf.Strand == '-')
                {
                    for (int i = startAt - 1; i >= 0; i--)
                        file.Write("\t{0}", counts[bcIdx, i]);
                    int firstNonZero = startAt;
                    while (firstNonZero < last && counts[bcIdx, firstNonZero] == 0) firstNonZero++;
                    for (int i = last; i >= firstNonZero; i--)
                        file.Write("\t{0}", counts[bcIdx, i]);
                }
                else
                {
                    for (int i = 0; i < startAt; i++)
                        file.Write("\t{0}", counts[bcIdx, i]);
                    int lastNonZero = last;
                    while (lastNonZero > startAt && counts[bcIdx, lastNonZero] == 0) lastNonZero--;
                    for (int i = startAt; i <= lastNonZero; i++)
                        file.Write("\t{0}", counts[bcIdx, i]);
                }
                file.WriteLine();
                firstCols = followingFirstCols;
            }
        }

        private void WriteExonCountsPerBarcode(string fileNameBase)
        {
            int nExonsToShow = MaxExonCount();
            using (StreamWriter file = new StreamWriter(fileNameBase + "_exons_by_bc.tab"))
            {
                file.WriteLine("Only expressed genes are shown. Rows are truncated when only zeroes remain.");
                file.Write("Gene\tChr\tStrand\tPos\tTrLen\tBarcode");
                for (int exonId = 1; exonId <= nExonsToShow; exonId++)
                    file.Write("\tExon{0}", exonId);
                file.WriteLine();
                int[,] counts = new int[barcodes.Count, nExonsToShow];
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int n = CompactGenePainter.GetCountsPerExonAndBarcode(gf, props.DirectionalReads, barcodes.Count, ref counts);
                    string firstCols = string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                                                     gf.Name, gf.Chr, gf.Strand, gf.Start, gf.GetTranscriptLength());
                    string followingFirstCols = string.Format("{0}\t\t\t\t", gf.Name);
                    WriteCountsByBarcodeDirected(file, counts, gf, firstCols, followingFirstCols, 0, n - 1);
                }
            }
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
            string fPath = fileNameBase + "_diff_splice.tab";
            using (StreamWriter matrixFile = new StreamWriter(fPath))
            {
                matrixFile.WriteLine("Total hits to individual exons and splice junctions.");
                matrixFile.WriteLine("Gene\t#Exons\t#TrHits\t#JunctionHits\tExon/Junction IDs...");
                matrixFile.WriteLine("\t\t\t\tCounts...");
                matrixFile.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}", gf.Name, gf.ExonCount, gf.GetTranscriptHits(), gf.GetJunctionHits());
                    List<Pair<string, int>> spliceCounts = gf.GetSpliceCounts();
                    foreach (Pair<string, int> spliceAndCount in spliceCounts)
                        matrixFile.Write("\t{0}", spliceAndCount.First);
                    matrixFile.WriteLine();
                    matrixFile.Write("{0}\t\t\t", gf.Name);
                    foreach (Pair<string, int> spliceAndCount in spliceCounts)
                        matrixFile.Write("\t{0}", spliceAndCount.Second);
                    matrixFile.WriteLine();
                }
            }
        }

        private void WriteSplicesByGeneLocusAndBc(string fileNameBase)
        {
            string fPath = fileNameBase + "_diff_splice_by_bc.tab";
            using (StreamWriter matrixFile = new StreamWriter(fPath))
            {
                matrixFile.WriteLine("Hits to individual splice junctions by barcode.");
                matrixFile.WriteLine("Gene\t#Exons\t#TrHits\t#JunctionHits\t\tJunctionIDs...");
                matrixFile.WriteLine("\t\t\t\tBarcode\tCounts...");
                matrixFile.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    matrixFile.Write("{0}\t{1}\t{2}\t{3}\t", gf.Name, gf.ExonCount, gf.GetTranscriptHits(), gf.GetJunctionHits());
                    List<Pair<string, int[]>> splicesAndBcCounts = gf.GetSpliceCountsPerBarcode();
                    foreach (Pair<string, int[]> spliceAndBcCounts in splicesAndBcCounts)
                        matrixFile.Write("\t{0}", spliceAndBcCounts.First);
                    matrixFile.WriteLine();
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    {
                        matrixFile.Write("{0}\t\t\t\t{1}", gf.Name, barcodes.Seqs[bcIdx]);
                        foreach (Pair<string, int[]> spliceAndBcCounts in splicesAndBcCounts)
                            matrixFile.Write("\t{0}", spliceAndBcCounts.Second[bcIdx]);
                        matrixFile.WriteLine();
                    }
                }
            }
        }

        /// <summary>
        /// For every expressed gene, write the binned hit count profile across the transcript.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteTranscriptHistograms(string fileNameBase, int averageReadLen)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_transcript_histograms.tab"))
            {
                file.WriteLine("Binned number of hits to transcripts counting from 5' end of transcript");
                file.Write("Gene\tTrLen\tTotHits\tExonHits\tFrom5'-");
                for (int i = 0; i < 100; i++)
                    file.Write("{0}\t", i * GeneFeature.LocusProfileBinSize);
                file.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    if (!gf.IsExpressed()) continue;
                    file.Write("{0}\t{1}\t{2}\t{3}\t", gf.Name, gf.GetTranscriptLength(), gf.GetTotalHits(), gf.GetTranscriptHits());
                    int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, GeneFeature.LocusProfileBinSize,
                                                                                         props.DirectionalReads, averageReadLen);
                    foreach (int c in trBinCounts)
                        file.Write("{0}\t", c);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// For every locus, write the binned hit count profiles across the chromosome.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteLocusHitsByGeneLocus(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_locus_histograms.tab"))
            {
                file.WriteLine("Binned number of hits to either strand of gene loci (including flanks) relative to 5' end of gene:");
                file.Write("Gene\tTrscrDir\tLen\tChrStrand\tTotHits\t5'End->");
                for (int i = 0; i < 100000 / GeneFeature.LocusProfileBinSize; i++)
                    file.Write("{0}bp\t", i * GeneFeature.LocusProfileBinSize);
                file.WriteLine();
                int histoSize = 0;
                foreach (GeneFeature gf in geneFeatures.Values)
                    histoSize = Math.Max(histoSize, gf.GetLocusLength());
                int[] histo = new int[histoSize];
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    if (gf.GetTotalHits() == 0) continue;
                    WriteLocusHistogramLine(file, ref histo, gf, true);
                    WriteLocusHistogramLine(file, ref histo, gf, false);
                }
            }
        }

        private static void WriteLocusHistogramLine(StreamWriter file, ref int[] histo, GeneFeature gf, bool sense)
        {
            char chrStrand = ((gf.Strand == '+') ^ sense) ? '-' : '+';
            file.Write("{0}\t{1}\t{2}\t{3}\t{4}", gf.Name, gf.Strand, gf.Length, chrStrand, gf.GetTotalHits(sense));
            int maxBin = CompactGenePainter.MakeLocusHistogram(gf, chrStrand, GeneFeature.LocusProfileBinSize, ref histo);
            for (int c = 0; c < maxBin; c++)
                file.Write("\t{0}", histo[c]);
            file.WriteLine();
        }

        private void WriteUniquehits(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_unique_hits.tab"))
            {
                file.WriteLine("Unique hit positions in gene loci relative to the first position of a {0} bp LeftFlank, counting in chr direction:",
                                GeneFeature.LocusFlankLength);
                file.Write("Gene\tTrscrDir\tChr\tLeftFlankStart\tRightFlankEnd\tStrand\t#Positions\t");
                file.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    List<int> hisPoss = CompactGenePainter.GetLocusHitPositions(gf, '+');
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                               gf.Name, gf.Strand, gf.Chr, gf.LocusStart, gf.LocusEnd, "+", hisPoss.Count);
                    foreach (int p in hisPoss)
                        file.Write("\t{0}", p);
                    file.WriteLine();
                    hisPoss = CompactGenePainter.GetLocusHitPositions(gf, '-');
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                               gf.Name, gf.Strand, gf.Chr, gf.LocusStart, gf.LocusEnd, "-", hisPoss.Count);
                    foreach (int p in hisPoss)
                        file.Write("\t{0}", p);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write the gene images
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void PaintSelectedGeneLocusImages(string fileNameBase)
        {
            if (props.GenesToPaint.Length == 0) return;
            string imgDir = Directory.CreateDirectory(fileNameBase + "_gene_images").FullName;
            foreach (string gene in props.GenesToPaint)
            {
                if (!geneFeatures.ContainsKey(gene))
                    continue;
                GeneFeature gf = geneFeatures[gene];
                ushort[,] imgData = CompactGenePainter.GetLocusImageData(gf);
                string safeGene = PathHandler.MakeSafeFilename(gene);
                string gifFile = Path.Combine(imgDir, safeGene + ".gif");
                WriteGifImage(imgData, gifFile, new int[] { 0, 10, 100, 1000, 10000 }, 
                              new Color[] { Color.Wheat, Color.Yellow, Color.Orange, Color.Red, Color.Purple });
            }
        }

        private static void WriteGifImage(ushort[,] imgData, string gifFile, int[] levels, Color[] colors)
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
        private void PaintSelectedGeneTranscriptImages(string fileNameBase)
        {
            if (props.GenesToPaint.Length == 0) return;
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
            using (StreamWriter file = new StreamWriter(txtFile))
            {
                for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                    file.Write("\t{0}", barcodes.GetWellId(orderedBcIndexes[bcRow]));
                file.WriteLine();
                file.Write("PosFrom5'End");
                for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                    file.Write("\t{0}", barcodes.Seqs[orderedBcIndexes[bcRow]]);
                file.WriteLine();
                for (int pos = 0; pos < nPositions; pos++)
                {
                    file.Write(pos);
                    for (int bcRow = nBarcodes - 1; bcRow >= 0; bcRow--)
                        file.Write("\t{0}", imgData[pos, bcRow]);
                    file.WriteLine();
                }
            }
        }

        private void WriteTranscriptProfiles(string fileNameBase)
        {
            using (StreamWriter file = new StreamWriter(fileNameBase + "_transcript_profiles.tab"))
            {
                file.WriteLine("All hit counts to expressed transcripts from 5' to 3' end. Each data row truncated at last position > 0.");
                file.Write("Gene\tChr\tTrDir\tTr5'Pos\tTr3'Pos\tTrLen");
                for (int p = 1; p < 10000; p++)
                    file.Write("\tPos{0}", p);
                file.WriteLine();
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    if (gf.GetTranscriptHits() == 0) continue;
                    file.Write("{0}\t{1}\t{2}\t{3}\t{4}\t{5}",
                               gf.Name, gf.Chr, gf.Strand, gf.Start, gf.End, gf.GetTranscriptLength());
                    ushort[] trProfile = CompactGenePainter.GetTranscriptProfile(gf);
                    int i = trProfile.Length - 1;
                    while (i > 0 && trProfile[i] == 0) i--;
                    for (int p = 0; p <= i; p++)
                        file.Write("\t{0}", trProfile[p]);
                    file.WriteLine();
                }
            }
        }

        /// <summary>
        /// Write the "cap_hits.tab" containing ratios of 5' to rest-of-transcript hits for spikes and transcripts
        /// </summary>
        /// <param name="fileNameBase"></param>
        /// <param name="averageReadLen"></param>
        private void WriteElongationEfficiency(string fileNameBase, int averageReadLen)
        {
            int capRegionSize = props.CapRegionSize;
            int trLenBinSize = 500;
            int nSections = 20;
            using (StreamWriter capHitsFile = new StreamWriter(fileNameBase + "_5to3_profiles.tab"))
            {
                WriteSpikeElongationHitCurve(capHitsFile, trLenBinSize, nSections, averageReadLen);
                WriteElongationHitCurve(capHitsFile, trLenBinSize, nSections, averageReadLen);
                // The old style hit statistics profile below:
                int minHitsPerGene = 50;
                capHitsFile.WriteLine("\n\nFraction hits that are to the 5' {0} bases of genes with >= {1} hits, grouped by transcript length (BinSize={2})",
                                     capRegionSize, minHitsPerGene, trLenBinSize);
                capHitsFile.WriteLine("\n\nSpike RNAs:");
                AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, capRegionSize, trLenBinSize, true);
                capHitsFile.WriteLine("\nOther RNAs:");
                AnalyzeWriteElongationSection(capHitsFile, minHitsPerGene, capRegionSize, trLenBinSize, false);
            }
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
                if (analyzeSpikes != gf.IsSpike()) 
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
            capHitsFile.WriteLine("Overall average fraction is {0}", allFracs.Mean());
            capHitsFile.Write("\nMidLength\tFraction\t#GenesInBin\t");
            int[] speciesBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(genome);
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t{0}", barcodes.Seqs[idx]);
            capHitsFile.WriteLine();
            capHitsFile.Write("\t\t\t");
            foreach (int idx in speciesBcIndexes) capHitsFile.Write("\t{0}", barcodes.GetWellId(idx));
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

        private void WriteElongationHitCurve(StreamWriter capHitsFile, int trLenBinSize, int nSectionsOverTranscipt, int averageReadLen)
        {
            int nTrSizeBins = 10000 / trLenBinSize;
            int minHitsPerGene = nSectionsOverTranscipt * 10;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[nTrSizeBins, nSectionsOverTranscipt];
            for (int di = 0; di < nTrSizeBins; di++)
            {
                for (int section = 0; section < nSectionsOverTranscipt; section++)
                    binnedEfficiencies[di, section] = new DescriptiveStatistics();
            }
            int[] nGenesPerSizeClass = new int[nTrSizeBins];
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (gf.IsSpike())
                    continue;
                if (gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                int trLenBin = Math.Min(nTrSizeBins - 1, trLen / trLenBinSize);
                double posBinSize = (trLen - averageReadLen) / (double)nSectionsOverTranscipt;
                int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, posBinSize, props.DirectionalReads, averageReadLen);
                for (int section = 0; section < Math.Min(nSectionsOverTranscipt, trBinCounts.Length); section++)
                    binnedEfficiencies[trLenBin, section].Add(trBinCounts[section] / (double)trBinCounts.Sum());
                nGenesPerSizeClass[trLenBin]++;
            }
            capHitsFile.WriteLine("Hit distribution across gene transcripts, group averages by transcript length classes.");
            capHitsFile.WriteLine("\nMidLength\tnGenes\t5' -> 3' hit distribution");
            for (int di = 0; di < nTrSizeBins; di++)
            {
                int binMid = averageReadLen + di * trLenBinSize + trLenBinSize / 2;
                capHitsFile.Write("{0}\t{1}", binMid, nGenesPerSizeClass[di]);
                for (int section = 0; section < nSectionsOverTranscipt; section++)
                    capHitsFile.Write("\t{0:0.####}", binnedEfficiencies[di, section].Mean());
                capHitsFile.WriteLine();
            }
        }

        private void WriteSpikeElongationHitCurve(StreamWriter capHitsFile, int trLenBinSize, int nSections, int averageReadLen)
        {
            int minHitsPerGene = nSections * 10;
            bool wroteHeader = false;
            foreach (GeneFeature gf in geneFeatures.Values)
            {
                if (!gf.IsSpike() || gf.GetTranscriptHits() < 25)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double binSize = (trLen - averageReadLen) / (double)nSections;
                int[] trBinCounts = CompactGenePainter.GetBinnedTrHitsRelStart(gf, binSize, props.DirectionalReads, averageReadLen);
                if (trBinCounts.Length == 0) continue;
                double allCounts = 0.0;
                foreach (int c in trBinCounts) allCounts += c;
                if (!wroteHeader)
                {
                    capHitsFile.WriteLine("Hit distribution across spike transcripts.");
                    capHitsFile.WriteLine("\nSpike\tLength\t5' -> 3' hit distribution");
                    wroteHeader = true;
                }
                capHitsFile.Write("{0}\t{1}", gf.Name, trLen);
                for (int section = 0; section < nSections; section++)
                {
                    double eff = trBinCounts[section] / allCounts;
                    capHitsFile.Write("\t{0:0.####}", eff);
                }
                capHitsFile.WriteLine();
            }
            capHitsFile.WriteLine();
        }

    }

}
