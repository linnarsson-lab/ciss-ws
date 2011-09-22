using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
	public class TranscriptomeStatistics
	{
        private readonly int bamFileWindowSize = 10000000;
        private CompactWiggle compactWiggle;
        public bool GenerateWiggle { get; set; }
        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private RedundantExonHitMapper redundantExonHitMapper;
        private RandomTagFilter randomTagFilter;
        public SyntReadReporter TestReporter { get; set; }

        Dictionary<string, int[]> TotalHitsByAnnotTypeAndChr; // Separates sense and antisense
        int[,] TotalHitsByAnnotTypeAndBarcode; // Separates sense and antisense
        int[] TotalHitsByAnnotType;            // Separates sense and antisense
        int[] TotalHitsByBarcode; // Number of hits to distinct annotations in each barcode
        AbstractGenomeAnnotations Annotations;
        Barcodes barcodes;
		DnaMotif[] motifs;
        int numReads = 0;                // Total number of mapped reads in input .map files
        int[] numReadsByBarcode;         // Total number of mapped reads in each barcode
        int numAltFeatureReads = 0;      // Number of reads that can stem from one of alternative features
        int numAnnotatedReads = 0;       // Number of reads that map to some annotation
        int numExonAnnotatedReads = 0;   // Number of reads that map to (one or more) exons
        int nMaxAltMappingsReads = 0; // Number of reads that hit the limit of alternative genomic mapping positions in bowtie
        int nMaxAltMappingsReadsWOTrHit = 0; // Dito where the only hit in map file is not to a transcript
        int sampleDistForUniqueReads = 500000;
        List<int> hitsAtSample = new List<int>();
        List<int> sampledNumUniqueReads = new List<int>();
        List<int[]> sampledBarcodeFeatures = new List<int[]>();
        Dictionary<string, int> redundantHits = new Dictionary<string, int>();
        List<Pair<MultiReadMapping, FtInterval>> exonsToMark;
        List<string> exonHitGeneNames;
        string annotationChrId;

        public TranscriptomeStatistics(AbstractGenomeAnnotations annotations, Props props)
		{
            AnnotType.DirectionalReads = props.DirectionalReads;
            this.barcodes = annotations.Barcodes;
            Annotations = annotations;
            if (props.GenerateWiggle) 
                compactWiggle = new CompactWiggle(Annotations.ChromosomeLengths);
            DetermineMotifs = props.DetermineMotifs;
            AnalyzeAllGeneVariants = !Annotations.noGeneVariants;
			motifs = new DnaMotif[barcodes.Count];
			for(int i = 0; i < motifs.Length; i++)
			{
				motifs[i] = new DnaMotif(40);
			}
            TotalHitsByBarcode = new int[barcodes.Count];
            TotalHitsByAnnotTypeAndBarcode = new int[AnnotType.Count, barcodes.Count];
            TotalHitsByAnnotTypeAndChr = new Dictionary<string, int[]>();
            foreach (string chr in Annotations.GetChromosomeNames())
                TotalHitsByAnnotTypeAndChr[chr] = new int[AnnotType.Count];
            TotalHitsByAnnotType = new int[AnnotType.Count];
            numReadsByBarcode = new int[barcodes.Count];
            exonsToMark = new List<Pair<MultiReadMapping, FtInterval>>(100);
            exonHitGeneNames = new List<string>(100);
            annotationChrId = Annotations.Genome.Annotation;
            if (barcodes.HasRandomBarcodes) randomTagFilter = new RandomTagFilter(barcodes, bamFileWindowSize);
        }

        public void SetRedundantHitMapper(AbstractGenomeAnnotations annotations, int averageReadLen)
        {
            redundantExonHitMapper = RedundantExonHitMapper.GetRedundantHitMapper(annotations.Genome, averageReadLen, annotations.geneFeatures);
        }

        public int AnnotateMapFile(string file, int[] genomeBcIndexes, bool useRandomBcFilter)
        {
            if (useRandomBcFilter)
                throw new InvalidOperationException("Random tags can only be filtered with .bam files as input!");
            int nReadsInValidBarcodes = 0;
            BowtieMapFile bmf = new BowtieMapFile(file, 12, barcodes);
            bool singleSpecies = !barcodes.HasSampleLayout();
            HashSet<int> validBcIndexes = new HashSet<int>(genomeBcIndexes);
            foreach (MultiReadMappings mappings in bmf.MultiMappings())
            {
                if (singleSpecies || validBcIndexes.Contains(mappings.BarcodeIdx))
                {
                    nReadsInValidBarcodes++;
                    Add(mappings, 1);
                }
            }
            return nReadsInValidBarcodes;
        }

        public int AnnotateBamFile(string file, int[] genomeBcIndexes, bool useRandomBcFilter)
        {
            int nReadsInValidBarcodes = 0;
            BamFile bamf = new BamFile(file);
            bool singleSpecies = !barcodes.HasSampleLayout();
            HashSet<int> validBcIndexes = new HashSet<int>(genomeBcIndexes);
            Console.Write("\nAnalyzing bam file reads for chr ");
            MultiReadMappings mappings = new MultiReadMappings(1, barcodes);
            foreach (string chrId in Annotations.ChrIdToFileMap.Keys)
            {
                string chrName = (StrtGenome.IsSpliceAnnotationChr(chrId))? chrId: "chr" + chrId;
                Console.Write(chrId + ".");
                for (int windowStart = 0; windowStart < Annotations.ChromosomeLengths[chrId]; windowStart += bamFileWindowSize)
                {
                    List<BamAlignedRead> bamReads = bamf.Fetch(chrName, windowStart, windowStart + bamFileWindowSize);
                    foreach (BamAlignedRead a in bamReads)
                    {
                        mappings.FromBamAlignedRead(a);
                        if (singleSpecies || validBcIndexes.Contains(mappings.BarcodeIdx))
                        {
                            nReadsInValidBarcodes++;
                            MultiReadMapping m = mappings[0];
                            if (!useRandomBcFilter || randomTagFilter.IsNew(m.Position, m.Strand, mappings.BarcodeIdx, mappings.RandomBcIdx))
                                Add(mappings, 1);
                        }
                    }
                }
            }
            Console.WriteLine();
            return nReadsInValidBarcodes;
        }

        public void Add(MultiReadMappings mappings, int weight)
        {
            exonHitGeneNames.Clear();
            numReads++;
            int bcodeIdx = mappings.BarcodeIdx;
            int hitLen = mappings.SeqLen;
            int halfWidth = hitLen / 2;
            numReadsByBarcode[bcodeIdx]++;
            bool someAnnotationHit = false;
            bool someExonHit = false;
            int nAltFeaturesHits = 0;
            MarkStatus markType = MarkStatus.TEST_EXON_MARK_OTHER;
            int recCount = 0;
            foreach (MultiReadMapping mapping in mappings.ValidMappings())
            {
                int hitStartPos = mapping.Position;
                if (hitStartPos == -1)
                    break;
                bool recSomeAnnotationHit = false;
                string chr = mapping.Chr;
                char strand = mapping.Strand;
                int hitMidPos = hitStartPos + halfWidth;
                foreach (FtInterval ivl in Annotations.GetMatching(chr, hitMidPos))
                {
                    MarkResult res = ivl.Mark(hitMidPos, halfWidth, strand, bcodeIdx, ivl.ExtraData, markType);
                    if (res.annotType == AnnotType.NOHIT)
                        continue;
                    someAnnotationHit = true;
                    recSomeAnnotationHit = true;
                    if (AnnotType.IsTranscript(res.annotType))
                    {
                        someExonHit = true;
                        string gfName = (AnalyzeAllGeneVariants)? res.feature.Name: res.feature.NonVariantName;
                        if (!exonHitGeneNames.Contains(gfName))
                        {
                            exonHitGeneNames.Add(gfName);
                            exonsToMark.Add(new Pair<MultiReadMapping, FtInterval>(mapping, ivl));
                        }
                    }
                    else // hit is not to EXON or SPLC (neither AEXON/ASPLC for non-directional samples)
                    {
                        nAltFeaturesHits++;
                        if (markType == MarkStatus.TEST_EXON_MARK_OTHER)
                        {
                            TotalHitsByAnnotTypeAndBarcode[res.annotType, bcodeIdx]++;
                            TotalHitsByAnnotTypeAndChr[chr][res.annotType]++;
                            TotalHitsByAnnotType[res.annotType]++;
                            TotalHitsByBarcode[bcodeIdx]++;
                        }
                        markType = MarkStatus.TEST_EXON_SKIP_OTHER;
                    }
                }
                if (chr != annotationChrId)
                {
                    if (compactWiggle != null)
                        compactWiggle.AddHit(chr, strand, hitStartPos, hitLen, 1, recSomeAnnotationHit);
                    // Add to the motif (base 21 in the motif will be the first base of the read)
                    // Subtract one to make it zero-based
                    if (DetermineMotifs && someAnnotationHit && Annotations.HasChromosome(chr))
                        motifs[bcodeIdx].Add(Annotations.GetChromosome(chr), hitStartPos - 20 - 1, strand);
                }
            }
            // Now when the best alignments have been selected, mark these transcript hits
            MarkStatus markStatus = (exonsToMark.Count > 1) ? MarkStatus.ALT_MAPPINGS : MarkStatus.SINGLE_MAPPING;
            if (recCount == 1 && mappings.AltMappings > 0)
            {
                if (redundantExonHitMapper != null)
                {
                    exonsToMark = redundantExonHitMapper.GetRedundantMappings(mappings[0].Chr, mappings[0].Position, mappings[0].Strand);
                }
                markStatus = MarkStatus.MARK_ALT_MAPPINGS;
            } 
            foreach (Pair<MultiReadMapping, FtInterval> exonToMark in exonsToMark)
            {
                MultiReadMapping rec = exonToMark.First;
                FtInterval ivl = exonToMark.Second;
                int chrStart = rec.Position;
                string chr = rec.Chr;
                char strand = rec.Strand;
                int hitMidPos = chrStart + halfWidth;
                MarkResult res = ivl.Mark(hitMidPos, halfWidth, rec.Strand, bcodeIdx, ivl.ExtraData, markStatus);
                if (rec.Mismatches != "")
                    ((GeneFeature)res.feature).MarkSNPs(rec.Position, bcodeIdx, rec.Mismatches);
                TotalHitsByAnnotTypeAndBarcode[res.annotType, bcodeIdx]++;
                TotalHitsByAnnotTypeAndChr[chr][res.annotType]++;
                TotalHitsByAnnotType[res.annotType]++;
                TotalHitsByBarcode[bcodeIdx]++;
                nAltFeaturesHits++;
            }
            if (someAnnotationHit)
            {
                numAnnotatedReads++;
                if (numAnnotatedReads % sampleDistForUniqueReads == 0)
                    SampleStatistics();
                if (someExonHit) numExonAnnotatedReads++;
            }
            if (nAltFeaturesHits > 1)
                numAltFeatureReads++;
            if (markStatus == MarkStatus.MARK_ALT_MAPPINGS)
            {
                nMaxAltMappingsReads++;
                if (!someExonHit) nMaxAltMappingsReadsWOTrHit++;
            }
            if (exonHitGeneNames.Count > 1)
            {
                exonHitGeneNames.Sort();
                string combNames = string.Join("#", exonHitGeneNames.ToArray());
                if (!redundantHits.ContainsKey(combNames))
                    redundantHits[combNames] = 1;
                else
                    redundantHits[combNames]++;
            }
            if (TestReporter != null)
                TestReporter.ReportHit(exonHitGeneNames, mappings, exonsToMark);
            exonsToMark.Clear();
        }

        public void SampleStatistics()
        {
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            List<int> totbcHits = new List<int>(TotalHitsByBarcode);
            totbcHits.Sort();
            int minTotBcHits = totbcHits[totbcHits.Count / 2];
            int n = 0;
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                n += CompactGenePainter.GetUniqueBarcodedHitPositionCount(gf);
                gf.SampleVariation(TotalHitsByBarcode, minTotBcHits, speciesBcIndexes);
            }
            hitsAtSample.Add(numAnnotatedReads);
            sampledNumUniqueReads.Add(n);
            sampledBarcodeFeatures.Add(Annotations.SampleBarcodeExpressedGenes());
        }

		/// <summary>
		///  Save all the statistics to a set of files
		/// </summary>
        /// <param name="extractionSummaryPath">Full path to Lxxx_extraction_summary.txt file</param>
		/// <param name="fileNameBase">A path and a filename prefix that will used to create all output files, e.g. "/data/Sample12_"</param>
		public void Save(ReadCounter readCounter, string fileNameBase)
		{
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            WriteRedundantExonHits(fileNameBase);
            WriteASExonDistributionHistogram(fileNameBase);
            WriteSampledVariation(fileNameBase);
            WriteSummary(fileNameBase, Annotations.GetSummaryLines(), readCounter);
            Annotations.WriteStats(fileNameBase);
            WriteSnps(fileNameBase);
            //WriteSnpsByBarcode(fileNameBase); // Large file & takes time
            if (DetermineMotifs)
                WriteSequenceLogos(fileNameBase);
            if (compactWiggle != null)
            {
                compactWiggle.WriteHotspots(fileNameBase, false, 50);
                compactWiggle.WriteWriggle(fileNameBase);
            }
        }

        /// <summary>
        /// Write sequence logo data for each barcode
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteSequenceLogos(string fileNameBase)
        {
            string logoDir = Directory.CreateDirectory(fileNameBase + "_sequence_logos").FullName;
            for (int i = 0; i < motifs.Length; i++)
            {
                motifs[i].Save(Path.Combine(logoDir, barcodes.Seqs[i] + "_motif.txt"));
            }
        }

        private void WriteSummary(string fileNameBase, List<string> summaryLines, ReadCounter readCounter)
        {
            var txtFile = (fileNameBase + "_summary.tab").OpenWrite();
            string xmlPath = fileNameBase + "_summary.xml";
            var xmlFile = xmlPath.OpenWrite();
            xmlFile.WriteLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            xmlFile.WriteLine("<strtSummary project=\"{0}\">", Path.GetDirectoryName(fileNameBase));
            WriteReadStats(readCounter, txtFile, xmlFile);
            WriteReadsBySpecies(xmlFile);
            WriteFeatureStats(txtFile, xmlFile);
            WriteSenseAntisenseStats(txtFile, xmlFile);
            WriteHitsByChromosome(xmlFile);
            WriteLibraryDepth(txtFile, xmlFile);
            WriteAccuBarcodedDetection(txtFile);
            txtFile.WriteLine();
            foreach (string line in summaryLines)
                txtFile.WriteLine(line);
            AddSpikes(xmlFile);
            Annotations.WriteSpikeDetection(txtFile, xmlFile);
            AddHitProfile(xmlFile);
            AddSampledCVs(xmlFile);
            AddCVHistogram(xmlFile);
            WriteBarcodeStats(fileNameBase, xmlFile);
            WriteRandomFilterStats(xmlFile);
            xmlFile.WriteLine("</strtSummary>");
            xmlFile.Close();
            txtFile.Close();
            if (!Environment.OSVersion.VersionString.Contains("Microsoft"))
            {
                CmdCaller.Run("php", "make_html_summary.php " + xmlPath);
            }
        }

        private void WriteAccuBarcodedDetection(StreamWriter txtFile)
        {
            txtFile.WriteLine("\nExpressed transcripts in each barcode accumulated as function of total read count:");
            txtFile.Write("ReadCount");
            foreach (string bcode in barcodes.Seqs) txtFile.Write("\t" + bcode);
            for (int i = 0; i < sampledBarcodeFeatures.Count; i++)
            {
                txtFile.Write("\n{0}", hitsAtSample[i]);
                foreach (int c in sampledBarcodeFeatures[i])
                    txtFile.Write("\t" + c);
            }
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (randomTagFilter == null) return;
            xmlFile.WriteLine("    <randomtagfrequence>");
            xmlFile.WriteLine("<title>Number of reads detected in each random barcode</title>");
            xmlFile.WriteLine("<xtitle>Random tag index (AAAA...TTTT)</xtitle>");
            for (int i = 0; i < randomTagFilter.nReadsByRandomTag.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", barcodes.MakeRandomTag(i), randomTagFilter.nReadsByRandomTag[i]);
            xmlFile.WriteLine("    </randomtagfrequence>");
            xmlFile.WriteLine("    <nuniqueateachrandomtagcoverage>");
            xmlFile.WriteLine("<title>Unique alignmentposition-barcodes as fn of # random tags they occur in</title>");
            xmlFile.WriteLine("<xtitle>Number of different random tags</xtitle>");
            for (int i = 0; i < randomTagFilter.nCasesPerRandomTagCount.Length; i++)
                xmlFile.WriteLine("      <point x=\"{0}\" y=\"{1}\" />", i, randomTagFilter.nCasesPerRandomTagCount[i]);
            xmlFile.WriteLine("    </nuniqueateachrandomtagcoverage>");
        }

        private void WriteReadStats(ReadCounter readCounter, StreamWriter txtFile, StreamWriter xmlFile)
        {
            int allBcCount = barcodes.Count;
            int spBcCount = barcodes.GenomeBarcodeIndexes(Annotations.Genome, false).Length;
            double totalReads = readCounter.GrandTotal;
            if (totalReads < 0.0001) totalReads = numReads;
            xmlFile.WriteLine("    <readfiles>");
            foreach (string path in readCounter.GetReadFiles())
                xmlFile.WriteLine("      <readfile path=\"{0}\" />", path); 
            xmlFile.WriteLine("    </readfiles>");
            xmlFile.WriteLine("  <reads>");
            xmlFile.WriteLine("    <title>Read distribution (10^6). [# samples/wells]</title>");
            xmlFile.WriteLine("    <point x=\"All reads [{0}] (100%)\" y=\"{1}\" />", allBcCount, totalReads / 1.0E6d);
            int validReads = readCounter.GrandCount(ReadStatus.VALID);
            xmlFile.WriteLine("    <point x=\"Valid STRT [{0}] ({1:0%})\" y=\"{2}\" />", allBcCount, validReads / totalReads, validReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Mapped [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, numReads / totalReads, numReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Annotated [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount,
                              numAnnotatedReads / totalReads, numAnnotatedReads / 1.0E6d);
            if (Props.props.DirectionalReads)
            {
                xmlFile.WriteLine("    <point x=\"Exon(sense) [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount,
                                  numExonAnnotatedReads / totalReads, numExonAnnotatedReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Intron(sense) [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, 
                                  TotalHitsByAnnotType[AnnotType.INTR] / totalReads, TotalHitsByAnnotType[AnnotType.INTR] / 1.0E6d);
            }
            else
            {
                int numIntronHits = TotalHitsByAnnotType[AnnotType.INTR] + TotalHitsByAnnotType[AnnotType.AINTR];
                xmlFile.WriteLine("    <point x=\"Exon [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount,
                                   numExonAnnotatedReads / totalReads, numExonAnnotatedReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Intron [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount, 
                                   numIntronHits / totalReads, numIntronHits / 1.0E6d);
            }
            xmlFile.WriteLine("    <point x=\"Repeat [{0}] ({1:0%})\" y=\"{2}\" />", spBcCount,
                               TotalHitsByAnnotType[AnnotType.REPT] / totalReads, TotalHitsByAnnotType[AnnotType.REPT] / 1.0E6d);
            xmlFile.WriteLine("  </reads>");

            txtFile.WriteLine(readCounter.TotalsToString());
            txtFile.WriteLine("Number of reads mapped to genome: {0} ({1:0%})", numReads, numReads / totalReads);
            if (numAltFeatureReads == 0)
                txtFile.WriteLine("Alternative mappings to genome were not analyzed.");
            else
                txtFile.WriteLine("Number of reads that can stem from one of alternative features: " + numAltFeatureReads);
            txtFile.WriteLine("Number of reads with > max (" + Props.props.BowtieMaxNumAltMappings + ") alt. genome mappings: " + nMaxAltMappingsReads);
            txtFile.WriteLine("Number of reads with > max alt. genome mappings and shown hit is not exon: " + nMaxAltMappingsReadsWOTrHit);
            txtFile.WriteLine("Number of reads mapped to some annotated position (gene locus or repeat): " + numAnnotatedReads);
            txtFile.WriteLine("Number of reads mapped only to position(s) without annotation: " + (numReads - numAnnotatedReads));
            txtFile.WriteLine("Number of reads mapped to a transcript: {0} ({1:0%})", numExonAnnotatedReads, numExonAnnotatedReads / totalReads);
        }

        private void WriteReadsBySpecies(StreamWriter xmlFile)
        {
            if (!barcodes.HasSampleLayout() || !Props.props.DirectionalReads) return;
            int[] genomeBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            WriteSpeciesReadSection(xmlFile, genomeBcIndexes, Annotations.Genome.Name);
            int[] emptyBcIndexes = barcodes.EmptyBarcodeIndexes();
            WriteSpeciesReadSection(xmlFile, emptyBcIndexes, "empty");
        }

        private void WriteSpeciesReadSection(StreamWriter xmlFile, int[] speciesBcIndexes, string speciesName)
        {
            double nMappedReads = 0;
            double nAnnotationsHit = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                nMappedReads += numReadsByBarcode[bcIdx];
                nAnnotationsHit += TotalHitsByBarcode[bcIdx];
            }
            xmlFile.WriteLine("  <reads species=\"{0}\">", speciesName);
            xmlFile.WriteLine("    <title>Mapped read distribution (10^6) by categories in {0} {1} wells</title>",
                              speciesBcIndexes.Length, speciesName);
            xmlFile.WriteLine("    <point x=\"Mapped (100%)\" y=\"{0}\" />", nMappedReads / 1.0E6d);
            xmlFile.WriteLine("    <point x=\"Annotations ({0:0%})\" y=\"{1}\" />", nAnnotationsHit / nMappedReads, nAnnotationsHit / 1.0E6d);
            foreach (int annotType in new int[] { AnnotType.EXON, AnnotType.INTR, AnnotType.AEXON, AnnotType.REPT })
            {
                int nType = 0;
                foreach (int bcIdx in speciesBcIndexes)
                    nType += TotalHitsByAnnotTypeAndBarcode[annotType, bcIdx];
                xmlFile.WriteLine("    <point x=\"{0} ({1:0%})\" y=\"{2}\" />", AnnotType.GetName(annotType), nType / nMappedReads, nType / 1.0E6d);
            }
            xmlFile.WriteLine("  </reads>");
        }
        
        private void WriteLibraryDepth(StreamWriter txtFile, StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <librarydepth>");
            xmlFile.WriteLine("    <title>Unique (10^6) barcoded feature hit positions as fn. of feature hits</title>");
            xmlFile.WriteLine("	<xtitle>Millions of feature hits processed</xtitle>");
            xmlFile.WriteLine("      <curve legend=\"\" color=\"#00FF00\">");
            txtFile.WriteLine("\nAccumulated number of unique barcode/hit position combinations in gene loci as function of read count:");
            for (int i = 0; i < sampledNumUniqueReads.Count; i++)
            {
                txtFile.WriteLine("{0}\t{1}", hitsAtSample[i], sampledNumUniqueReads[i]);
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", hitsAtSample[i] / 1.0E6d, sampledNumUniqueReads[i] / 1.0E6d);
            }
            xmlFile.WriteLine("    </curve>");
            xmlFile.WriteLine("  </librarydepth>");
        }

        private void WriteSenseAntisenseStats(StreamWriter txtFile, StreamWriter xmlFile)
        {
            txtFile.WriteLine("\nAbove every read is counted once. In table below every feature (e.g. transcript variant) hit by a read is counted.");
            txtFile.WriteLine("Hence, reads mapping to overlapping genes/gene variants and repeats inside genes will be counted more than once.");
            txtFile.WriteLine("Repeats are always non-directional and counted on both strands. TotLen of splices is the sum of all artifical junction lengths.");
            txtFile.WriteLine("Type\tTotLen\tSense\tAntisense\tRatio");
            xmlFile.WriteLine("  <senseantisense>");
            xmlFile.WriteLine("    <title>Sense and Antisense reads per kb feature length</title>");
            foreach (int t in AnnotType.GetSenseTypes())
            {
                int totSense = Annotations.GetTotalAnnotCounts(t, true);
                int at = AnnotType.MakeAntisense(t);
                int totASense = Annotations.GetTotalAnnotCounts(at, true);
                string ratio = "Inf";
                int totLen = Annotations.GetTotalAnnotLength(at, true);
                if (totASense > 0)
                    ratio = string.Format("{0:0.0}", totSense / (double)totASense);
                txtFile.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", AnnotType.GetName(t),
                               totLen, totSense, totASense, ratio);
                xmlFile.WriteLine("   <point x=\"{0}#br#{1}:1\" y=\"{2:0.##}\" y2=\"{3:0.##}\" />", AnnotType.GetName(t),
                                  ratio, 1000.0d * (totSense / (double)totLen), 1000.0d * (totASense / (double)totLen));
            }
            int reptLen = Annotations.GetTotalAnnotLength(AnnotType.REPT);
            int reptCount = TotalHitsByAnnotType[AnnotType.REPT];
            txtFile.WriteLine("REPT\t{0}\t{1}\t{2}\t{3}", reptLen, reptCount, reptCount, 1.0);
            xmlFile.WriteLine("   <point x=\"REPT#br#1:1\" y=\"{0:0.###}\" y2=\"{0:0.####}\" />", reptCount / (double)reptLen);
            xmlFile.WriteLine("  </senseantisense>");
        }

        private void WriteHitsByChromosome(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <senseantisensebychr>");
            xmlFile.WriteLine("    <title>% of reads mapped to Sense/Antisense exons [ratio below] by chromosome</title>");
            List<int> numkeys = new List<int>();
            List<string> alphakeys = new List<string>();
            int v;
            foreach (string key in TotalHitsByAnnotTypeAndChr.Keys)
                if (int.TryParse(key, out v)) numkeys.Add(v);
                else alphakeys.Add(key);
            numkeys.Sort();
            alphakeys.Sort();
            List<string> sortedkeys = numkeys.ConvertAll((n) => n.ToString());
            sortedkeys.AddRange(alphakeys);
            string[] chrs = TotalHitsByAnnotTypeAndChr.Keys.ToArray();
            foreach (string chr in sortedkeys)
            {
                if (StrtGenome.IsSpliceAnnotationChr(chr))
                    continue;
                double nSense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.EXON];
                double nAsense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.AEXON];
                string ratio = (nAsense == 0)? "1:0" : string.Format("{0:0}", nSense / (double)nAsense);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  chr, ratio, 100.0d *(nSense / (double)numReads), 100.0d * (nAsense / (double)numReads));
            }
            xmlFile.WriteLine("  </senseantisensebychr>");
        }

        private void WriteFeatureStats(StreamWriter txtFile, StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <features>");
            xmlFile.WriteLine("    <title>Overall detection of features</title>");
            if (!Annotations.noGeneVariants)
            {
                txtFile.WriteLine("Number of expressed genes including variants: " + Annotations.GetNumExpressedGenes());
                xmlFile.WriteLine("    <point x=\"Detected tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedGenes());
            }
            xmlFile.WriteLine("    <point x=\"Detected main tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedMainGeneVariants());
            int[] bcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            int nGenesInAllBarcodes = 0;
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                foreach (int bcIdx in bcIndexes)
                    if (gf.TranscriptHitsByBarcode[bcIdx] == 0) goto EXIT;
                nGenesInAllBarcodes++;
            EXIT: ;
            }
            xmlFile.WriteLine("    <point x=\"in every species well ({0})\" y=\"{1}\" />", bcIndexes.Length, nGenesInAllBarcodes);
            xmlFile.WriteLine("    <point x=\"Detected repeat classes\" y=\"{0}\" />", Annotations.GetNumExpressedRepeats());
            xmlFile.WriteLine("  </features>");
            txtFile.WriteLine("Number of expressed main gene variants: " + Annotations.GetNumExpressedMainGeneVariants());
            txtFile.WriteLine("Number of expressed repeats: " + Annotations.GetNumExpressedRepeats());
        }

        private void WriteSampledVariation(string fileNameBase)
        {
            var file = (fileNameBase + "_sampled_CV.tab").OpenWrite();
            file.WriteLine("Estimated coefficient of variation for each gene as a function of number of processed reads.");
            file.Write("Feature\tFinalTotalHits");
            for (int i = 0; i < sampledNumUniqueReads.Count; i++)
                file.Write("\t" + hitsAtSample[i]);
            file.WriteLine();
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                int trHits = gf.GetTranscriptHits();
                if (trHits == 0) continue;
                file.Write(gf.Name + "\t" + trHits);
                for (int i = 0; i < sampledNumUniqueReads.Count; i++)
                    file.Write("\t{0}%", (int)Math.Round(gf.VariationSamples[i] * 100, 0));
                file.WriteLine();
            }
            file.Close();
        }

        private void AddSpikes(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <spikes>");
            xmlFile.WriteLine("    <title>Normalized spike means and standard deviations</title>");
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                if (gf.Name.StartsWith("RNA_SPIKE_"))
                {
                    DescriptiveStatistics ds = new DescriptiveStatistics();
                    foreach (int bcIdx in barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome))
                    {
                        int c = TotalHitsByAnnotTypeAndBarcode[AnnotType.EXON, bcIdx];
                        if (!AnnotType.DirectionalReads) c += TotalHitsByAnnotTypeAndBarcode[AnnotType.AEXON, bcIdx];
                        if (c > 0)
                        {
                            double RPM = gf.TranscriptHitsByBarcode[bcIdx] * 1.0E+6 / (double)c;
                            ds.Add(RPM);
                        }
                    }
                    string spikeId = "#" + gf.Name.Replace("RNA_SPIKE_", "");
                    if (ds.Count > 0)
                        xmlFile.WriteLine("    <point x=\"{0}\" y=\"{1:0.###}\" error=\"{2:0.###}\" />", spikeId, ds.Mean(), ds.StandardDeviation());
                    else
                        xmlFile.WriteLine("    <point x=\"{0}\" y=\"0.0\" error=\"0.0\" />", spikeId);
                }
            }
            xmlFile.WriteLine("  </spikes>");
        }

        private void AddHitProfile(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <hitprofile>");
            xmlFile.WriteLine("  <title>5'->3' read distr. Green=Transcripts/Blue=Spikes</title>");
            xmlFile.WriteLine("	 <xtitle>Relative pos within transcript</xtitle>");
            int trLenBinSize = 500;
            int trLen1stBinMid = 500;
            int trLenBinStep = 1500;
            int trLenBinCount = 4;
            int nSections = 20;
            int minHitsPerGene = nSections * 10;
            DescriptiveStatistics[,] binnedEfficiencies = new DescriptiveStatistics[trLenBinCount, nSections];
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                for (int section = 0; section < nSections; section++)
                    binnedEfficiencies[trLenBinIdx, section] = new DescriptiveStatistics();
            }
            int[] geneCounts = new int[trLenBinCount];
            int spikeColor = 0x000080;
            int spikeColorStep = ((0xFF - 0x81) / 8);
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                bool isSpike = gf.Name.StartsWith("RNA_SPIKE_");
                if (isSpike && gf.GetTotalHits(true) < 50)
                    continue;
                if (!isSpike && gf.GetTranscriptHits() < minHitsPerGene)
                    continue;
                int trLen = gf.GetTranscriptLength();
                double sectionSize = trLen / (double)nSections;
                int[] trSectionCounts = CompactGenePainter.GetBinnedTranscriptHitsRelEnd(gf, sectionSize, Props.props.DirectionalReads);
                if (trSectionCounts.Length == 0) continue;
                double trTotalCounts = 0.0;
                foreach (int c in trSectionCounts) trTotalCounts += c;
                if (trTotalCounts == 0.0) continue;
                int trIdx = nSections - 1;
                if (!isSpike)
                {
                    if (Math.Abs((trLen - trLen1stBinMid) % trLenBinSize) > 250)
                        continue;
                    int trLenBin = (trLen - trLenBinSize / 2) / trLenBinStep;
                    if (trLenBin >= trLenBinCount) continue;
                    for (int section = 0; section < nSections; section++)
                        binnedEfficiencies[trLenBin, section].Add(trSectionCounts[trIdx--] / trTotalCounts);
                    geneCounts[trLenBin]++;
                }
                else
                {
                    string spikeId = gf.Name.Replace("RNA_SPIKE_", "");
                    xmlFile.WriteLine("    <curve legend=\"#{0} {1}bp\" color=\"#{2:X6}\">", spikeId, trLen, spikeColor);
                    spikeColor += spikeColorStep;
                    for (int section = 0; section < nSections; section++)
                    {
                        double eff = trSectionCounts[trIdx--] / trTotalCounts;
                        double fracPos = section / (double)nSections;
                        xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", fracPos, eff);
                    }
                    xmlFile.WriteLine("    </curve>");
                }
            }
            int geneColor = 0x008000;
            int geneColorStep = ((0xFF - 0x81) / trLenBinCount) * 0x0100;
            for (int trLenBinIdx = 0; trLenBinIdx < trLenBinCount; trLenBinIdx++)
            {
                if (geneCounts[trLenBinIdx] < 5) continue;
                int midLen = (trLenBinIdx * trLenBinStep) + trLen1stBinMid;
                xmlFile.WriteLine("    <curve legend=\"{0}-{1}bp\" color=\"#{2:X6}\">",
                                  midLen - (trLenBinSize / 2), midLen + (trLenBinSize / 2), geneColor);
                geneColor += geneColorStep;
                for (int section = 0; section < nSections; section++)
                {
                    double eff = binnedEfficiencies[trLenBinIdx, section].Mean();
                    double fracPos = section / (double)nSections;
                    xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", fracPos, eff);
                }
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </hitprofile>");
        }

        private void AddSampledCVs(StreamWriter xmlFile)
        {
            int nSamples = hitsAtSample.Count;
            int[] exprLevels = new int[] { 20, 100, 1000, 10000 };
            Dictionary<int, Dictionary<int, List<double>>> groupedCVs = new Dictionary<int, Dictionary<int, List<double>>>();
            foreach (int level in exprLevels)
            {
                groupedCVs[level] = new Dictionary<int, List<double>>();
                for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                    groupedCVs[level][sampleIdx] = new List<double>();
            }
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                int finalCount = gf.GetTranscriptHits();
                if (finalCount == 0) continue;
                foreach (int level in exprLevels)
                {
                    if (Math.Abs((level - finalCount)) < 0.2 * (double)level)
                    {
                        for (int sampleIdx = 0; sampleIdx < hitsAtSample.Count; sampleIdx++)
                        {
                            double cv = gf.VariationSamples[sampleIdx];
                            groupedCVs[level][sampleIdx].Add(cv);
                        }
                        break;
                    }
                }
            }
            int colorCode = 64;
            int colorStep = (255 - colorCode) / exprLevels.Length;
            xmlFile.WriteLine("  <variationbyreads>");
            xmlFile.WriteLine("    <title>Median %CV as function of feature hits at various transcript levels</title>");
            xmlFile.WriteLine("	   <xtitle>Millions of feature hits processed</xtitle>");
            foreach (int level in groupedCVs.Keys)
            {
                if (groupedCVs[level].ContainsKey(0) && groupedCVs[level][0].Count < 5) continue;
                double[] medianCVs = new double[nSamples];
                for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                    medianCVs[sampleIdx] = DescriptiveStatistics.Median(groupedCVs[level][sampleIdx]);
                if (!medianCVs.All(cv => cv < 0.000001))
                {
                    xmlFile.WriteLine("    <curve legend=\"{0} hits\" color=\"#00{1:X2}00\">", level, colorCode);
                    for (int i = 0; i < nSamples; i++)
                        if (medianCVs[i] > 0.0)
                            xmlFile.WriteLine("      <point x=\"{0:0.##}\" y=\"{1:#.#}\" />", hitsAtSample[i] / 1.0E6d, 100.0d * medianCVs[i]);
                }
                colorCode += colorStep;
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </variationbyreads>");
        }

        private void AddCVHistogram(StreamWriter xmlFile)
        {
            int[] genomeBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            if (genomeBcIndexes.Length < 3) return;
            int nPairs = 1000;
            int nBins = 40;
            List<double[]> validBcCountsByGene = new List<double[]>();
            List<int> totalCountsByGene = new List<int>();
            List<int> spikeIndices = new List<int>();
            int[] totalsByBarcode = new int[barcodes.Count];
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                double[] gfValidBcCounts = new double[barcodes.Count];
                int gfTotal = 0;
                foreach (int bcIdx in genomeBcIndexes)
                {
                    int c = gf.NonConflictingTranscriptHitsByBarcode[bcIdx];
                    gfValidBcCounts[bcIdx] = c;
                    gfTotal += c;
                    totalsByBarcode[bcIdx] += c;
                }
                totalCountsByGene.Add(gfTotal);
                validBcCountsByGene.Add(gfValidBcCounts);
            }
            double[] CVs = CalcCVs(genomeBcIndexes, nPairs, validBcCountsByGene, totalCountsByGene, totalsByBarcode);
            if (CVs != null)
                OutputCVBars(xmlFile, nPairs, nBins, CVs);
        }

        private double[] CalcCVs(int[] genomeBcIndexes, int nPairs, List<double[]> validBcCountsByGene, List<int> totalCountsByGene, int[] totalsByBarcode)
        {
            Linnarsson.Mathematics.Sort.HeapSort(totalCountsByGene, validBcCountsByGene);
            validBcCountsByGene.Reverse(); // Get indices of genes in order of most->least expressed
            int nGenes = Math.Min(validBcCountsByGene.Count - 1, nPairs);
            double[] CVs = new double[nGenes];
            int minValidWellCount = numReads / genomeBcIndexes.Length / 20;
            int[] usefulBcIndexes = genomeBcIndexes.Where(bcIdx => totalsByBarcode[bcIdx] > minValidWellCount).ToArray();
            if (usefulBcIndexes.Length < 3)
                return null;
            for (int geneIdx = 0; geneIdx < nGenes; geneIdx++)
            {
                double[] bcodeCounts = validBcCountsByGene[geneIdx];
                List<double> normedBcValues = new List<double>();
                foreach (int bcIdx in usefulBcIndexes)
                    normedBcValues.Add(bcodeCounts[bcIdx] / (double)totalsByBarcode[bcIdx]); // Normalized value
                CVs[geneIdx] = DescriptiveStatistics.CV(normedBcValues.ToArray());
            }
            return CVs;
        }

        private static void OutputCVBars(StreamWriter xmlFile, int nPairs, int nBins, double[] CVs)
        {
            double minCV = CVs.Min();
            double maxCV = CVs.Max() + 0.1;
            if (!double.IsNaN(minCV) && !double.IsNaN(maxCV))
            {
                double[] geneCVHisto = new double[nBins];
                foreach (double cv in CVs)
                    geneCVHisto[(int)(nBins * (cv - minCV) / (maxCV - minCV))]++;
                xmlFile.WriteLine("  <cvhistogram>");
                xmlFile.WriteLine("    <title>CV distribution of normalized counts of top {0} genes</title>", nPairs);
                xmlFile.WriteLine("    <xtitle>CV distribution</xtitle>");
                double binStep = (maxCV - minCV) / nBins;
                for (int bin = 0; bin < nBins; bin++)
                    xmlFile.WriteLine("    <point x=\"{0:0.###}\" y=\"{1}\" />", minCV + bin * binStep, geneCVHisto[bin]);
                xmlFile.WriteLine("  </cvhistogram>");
            }
        }

        /// <summary>
        /// Write plate layout formatted statistics for hits by barcodes.
        /// </summary>
        /// <param name="fileNameBase"></param>
        private void WriteBarcodeStats(string fileNameBase, StreamWriter xmlFile)
        {
            StreamWriter barcodeStats = new StreamWriter(fileNameBase + "_barcode_summary.tab");
            barcodeStats.WriteLine("Total annotated reads: {0}\n", numAnnotatedReads);
            StreamWriter bCodeLines = new StreamWriter(fileNameBase + "_barcode_oneliners.tab");
            xmlFile.WriteLine("  <barcodestats>");
            xmlFile.Write("    <barcodestat section=\"wellids\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", barcodes.GetWellId(bcIdx));
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            WriteBarcodes(xmlFile, barcodeStats, bCodeLines);
            int[] genomeBcIndexes = barcodes.GenomeAndEmptyBarcodeIndexes(Annotations.Genome);
            if (barcodes.SpeciesByWell != null)
                WriteSpeciesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            WriteTotalByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            WriteFeaturesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
            barcodeStats.WriteLine("Transcripts detected in each barcode:");
            string[] trCounts = Array.ConvertAll<int, string>(Annotations.SampleBarcodeExpressedGenes(), 
                                                              (x => x.ToString()));
            barcodeStats.WriteLine(MakeDataMatrix(trCounts, "0"));
            barcodeStats.Close();
            bCodeLines.Close();
            xmlFile.Write("    <barcodestat section=\"transcripts\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? trCounts[bcIdx] : "(" + trCounts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            xmlFile.WriteLine("  </barcodestats>");
        }

        private void WriteFeaturesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            for (var annotType = 0; annotType < AnnotType.Count; annotType++)
            {
                if (annotType == AnnotType.AREPT) continue;
                string annotName = AnnotType.GetName(annotType);
                barcodeStats.WriteLine("\nTotal mapped to {0}:\n", annotName);
                bCodeLines.Write(annotName);
                xmlFile.Write("    <barcodestat section=\"{0}\">", annotName);
                for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                {
                    string annotHits = TotalHitsByAnnotTypeAndBarcode[annotType, bcIdx].ToString();
                    bCodeLines.Write("\t" + annotHits);
                    if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                    string d = genomeBcIndexes.Contains(bcIdx) ? annotHits : "(" + annotHits.ToString() + ")";
                    xmlFile.Write("    <d>{0}</d>", d);
                }
                bCodeLines.WriteLine();
                xmlFile.WriteLine("\n    </barcodestat>");
                barcodeStats.WriteLine(MakeTotalMatrix(annotType));
                barcodeStats.WriteLine(MakeFracDevStatsMatrix(annotType));
            }
        }

        private void WriteTotalByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            barcodeStats.WriteLine("\nTotal annotated reads by barcode:\n");
            bCodeLines.Write("TOTAL");
            xmlFile.Write("    <barcodestat section=\"annotated reads\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = TotalHitsByBarcode[bcIdx].ToString();
                bCodeLines.Write("\t" + counts[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string d = genomeBcIndexes.Contains(bcIdx) ? counts[bcIdx] : "(" + counts[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", d);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine(MakeDataMatrix(counts, "0"));
            bCodeLines.WriteLine();
        }

        private void WriteBarcodes(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines)
        {
            xmlFile.Write("    <barcodestat section=\"barcodes\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                bCodeLines.Write("\t" + barcodes.Seqs[bcIdx]);
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                xmlFile.Write("    <d>{0}</d>", barcodes.Seqs[bcIdx]);
            }
            bCodeLines.WriteLine();
            xmlFile.WriteLine("\n    </barcodestat>");
            barcodeStats.WriteLine("Barcode by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(barcodes.Seqs, "-"));
        }

        private void WriteSpeciesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            foreach (string species in barcodes.SpeciesByWell) bCodeLines.Write("\t" + species);
            barcodeStats.WriteLine("Species by well:\n");
            barcodeStats.WriteLine(MakeDataMatrix(barcodes.SpeciesByWell, "empty"));
            xmlFile.Write("    <barcodestat section=\"species\">");
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                if ((bcIdx % 8) == 0) xmlFile.Write("\n      ");
                string species = genomeBcIndexes.Contains(bcIdx) ? barcodes.SpeciesByWell[bcIdx] : "(" + barcodes.SpeciesByWell[bcIdx] + ")";
                xmlFile.Write("    <d>{0}</d>", species);
            }
            xmlFile.WriteLine("\n    </barcodestat>");
        }

        private string MakeTotalMatrix(int annotType)
        {
            string[] counts = new string[barcodes.Count];
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
                counts[bcodeIdx] = TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx].ToString();
            return MakeDataMatrix(counts, "0");
        }

        private string MakeFracDevStatsMatrix(int annotType)
        {
            double fracSum = 0.0;
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
                fracSum += (double)TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx]
                                         / TotalHitsByBarcode[bcodeIdx];
            double meanFrac = fracSum / barcodes.Count;
            string[] values = new string[barcodes.Count];
            for (int bcodeIdx = 0; bcodeIdx < barcodes.Count; bcodeIdx++)
            {
                double ownFrac = (double)TotalHitsByAnnotTypeAndBarcode[annotType, bcodeIdx]
                                                    / TotalHitsByBarcode[bcodeIdx];
                values[bcodeIdx] = (meanFrac == 0)? "NoData" : string.Format("{0:0.000}", ownFrac / meanFrac);
            }
            string annotName = AnnotType.GetName(annotType);
            string result = string.Format("relative to average fraction {0:0.0000}:\n", meanFrac);
            result += MakeDataMatrix(values, "NoData");
            return result;
        }

        private string MakeDataMatrix(string[] values, string defaultValue)
        {
            StringBuilder result = new StringBuilder();
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 12; col++)
                {
                    int bcodeIdx = row + 8 * col;
                    if (bcodeIdx < values.Length)
                        result.Append(values[bcodeIdx].ToString());
                    else
                        result.Append(defaultValue.ToString());
                    if (col < 11) result.Append("\t");
                }
                result.Append("\n");
            }
            return result.ToString();
        }

        public int GetNumMappedReads()
        {
            return numAnnotatedReads;
        }

        private void WriteASExonDistributionHistogram(string fileNameBase)
        {
            var ASHistFile = (fileNameBase + "_ASRPM_Histo.tab").OpenWrite();
            ASHistFile.WriteLine("Distribution of Antisense RPM/bp transcript among all genes with any Antisense hit.");
            ASHistFile.WriteLine("BinStart\tCount");
            int[] histo;
            double firstBinStart, binWidth, median;
            MakeExonAntisenseHistogram(out histo, out median, out firstBinStart, out binWidth);
            for (int bin = 0; bin < histo.Length; bin++)
            {
                ASHistFile.WriteLine("{0}\t{1}", firstBinStart + bin * binWidth, histo[bin]);
            }
            ASHistFile.WriteLine("\n\nMedian:\t{0}", median);
            ASHistFile.Close();
        }

        private void MakeExonAntisenseHistogram(out int[] histo, out double median,
                       out double firstBinStart, out double binWidth)
        {
            int nGenesPerBin = 30;
            double totHits = TotalHitsByAnnotType[AnnotType.EXON] + TotalHitsByAnnotType[AnnotType.AEXON];
            if (totHits == 0)
            {
                histo = new int[1]; median = 0.0; firstBinStart = 0.0; binWidth = 1.0; return;
            }
            double normer = 1.0E+6 / totHits;
            double lastBinEnd = 0.0;
            int nGenesWithASHit = 0;
            firstBinStart = double.MaxValue;
            List<double> ASPBPM = new List<double>();
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                int antiHits = gf.NonMaskedHitsByAnnotType[AnnotType.AEXON];
                if (antiHits == 0) continue;
                nGenesWithASHit++;
                double antiHitsPerBasePerM = (double)antiHits / (double)gf.GetNonMaskedTranscriptLength() / normer;
                ASPBPM.Add(antiHitsPerBasePerM);
                lastBinEnd = Math.Ceiling(Math.Max(lastBinEnd, antiHitsPerBasePerM));
                firstBinStart = Math.Floor(Math.Min(firstBinStart, antiHitsPerBasePerM));
            }
            double valueRange = lastBinEnd - firstBinStart;
            int nBins = (int)((double)nGenesWithASHit / (double)nGenesPerBin);
            histo = new int[nBins + 1];
            binWidth = valueRange / nBins;
            foreach (double antiHitsPerBasePerM in ASPBPM)
            {
                int bin = (int)Math.Ceiling((antiHitsPerBasePerM - firstBinStart) / binWidth);
                histo[bin]++;
            }
            if (ASPBPM.Count == 0) 
                median = double.NaN;
            else
                median = DescriptiveStatistics.Median(ASPBPM);
        }

        private void WriteRedundantExonHits(string fileNameBase)
        {
            Dictionary<string, List<string>> byGene = new Dictionary<string, List<string>>();
            StreamWriter redFile = (fileNameBase + "_shared_hits.tab").OpenWrite();
            redFile.WriteLine("#Reads\tGene loci competing for these reads (not all gene variants spanning the same hit site are shown)");
            foreach (string combName in redundantHits.Keys)
            {
                int sharedHits = redundantHits[combName];
                string[] names = combName.Split('#');
                foreach (string n in names)
                {
                    string group = string.Format("{0}({1})", 
                                     string.Join("/", names.Where( (on) => (on != n)).ToArray()), sharedHits);
                    if (!byGene.ContainsKey(n))
                        byGene[n] = new List<string>();
                    byGene[n].Add(group);
                }
                string tabbedNames = string.Join("\t", names);
                redFile.WriteLine("{0}\t{1}", sharedHits, tabbedNames);
            }
            redFile.Close();
            StreamWriter sharedFile = (fileNameBase + "_shared_hits_by_gene.tab").OpenWrite();
            sharedFile.WriteLine("Gene\tMinHits\tMaxHits\tNon-unique hits in the difference, that also map to other known genes");
            foreach (string gene in byGene.Keys)
            {
                GeneFeature gf = Annotations.geneFeatures[gene];
                int ncHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                int allHits = gf.TranscriptHitsByBarcode.Sum();
                string altGenes = string.Join("\t", byGene[gene].ToArray());
                sharedFile.WriteLine("{0}\t{1}\t{2}\t{3}", gene, ncHits, allHits, altGenes);
            }
            sharedFile.Close();
        }

        private void WriteSnps(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs.tab").OpenWrite();
            snpFile.WriteLine("#Gene\tChr\tmRNAStartChrPos\tHZ_eSNPChrPos\tAlt_eSNPChrPos");
            int thres = (int)(SnpAnalyzer.thresholdFractionAltHitsForHZPos * 100);
            snpFile.WriteLine("#(>={0} AltNtReads/Pos required)\t\t\t({1}-{2}% AltNt)\t(>{2}% Alt Nt)", SnpAnalyzer.minAltHitsToTestSnpPos, thres, 100 - thres);
            int averageHitLen = (int)Math.Round(compactWiggle.GetAverageHitLength());
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                List<int> HZLocusPos, altLocusPos;
                SnpAnalyzer.GetSnpLocusPositions(gf, averageHitLen, out HZLocusPos, out altLocusPos);
                int nNeededOutputLines = Math.Max(HZLocusPos.Count, altLocusPos.Count);
                if (nNeededOutputLines == 0) continue;
                string first = gf.Name + "\t" + gf.Chr + "\t" + gf.Start + "\t";
                for (int i = 0; i < nNeededOutputLines; i++)
                {
                    snpFile.Write(first);
                    if (i < HZLocusPos.Count) snpFile.Write(HZLocusPos[i] + gf.LocusStart);
                    if (i < altLocusPos.Count) snpFile.Write("\t" + (altLocusPos[i] + gf.LocusStart));
                    snpFile.WriteLine();
                    first = "\t\t\t";
                }
            }
            snpFile.Close();
        }

        private void WriteSnpsByBarcode(string fileNameBase)
        {
            StreamWriter snpFile = (fileNameBase + "_SNPs_by_barcode.tab").OpenWrite();
            SnpAnalyzer sa = new SnpAnalyzer();
            int averageHitLength = (int)Math.Round(compactWiggle.GetAverageHitLength());
            sa.WriteSnpsByBarcode(snpFile, barcodes, Annotations.geneFeatures, averageHitLength);
            snpFile.Close();
        }

    }

}
