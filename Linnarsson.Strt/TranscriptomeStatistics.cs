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
        private CompactWiggle compactWiggle;
        public bool GenerateWiggle { get; set; }
        public bool DetermineMotifs { get; set; }
        public bool AnalyzeAllGeneVariants { get; set; }
        private RedundantExonHitMapper redundantExonHitMapper;
        private RandomTagFilterByBc randomTagFilter;
        public SyntReadReporter TestReporter { get; set; }

        Dictionary<string, int[]> TotalHitsByAnnotTypeAndChr; // Separates sense and antisense
        int[,] TotalHitsByAnnotTypeAndBarcode; // Separates sense and antisense
        int[] TotalHitsByAnnotType;            // Separates sense and antisense
        int[] TotalHitsByBarcode; // Number of hits to distinct annotations in each barcode
        AbstractGenomeAnnotations Annotations;
        Barcodes barcodes;
		DnaMotif[] motifs;
        int currentBcIdx = 0;
        string currentMapFilePath;
        int numReads = 0;                // Total number of mapped reads in input .map files
        int[] numReadsByBarcode;         // Total number of mapped reads in each barcode
        int[] numUniqueMolReadsByBarcode;   // Total number of unique molecule reads in each barcode
        int numAnnotatedReads = 0;       // Number of reads that map to some annotation
        int numExonAnnotatedReads = 0;   // Number of reads that map to (one or more) exons
        int nMaxAltMappingsReads = 0; // Number of reads that hit the limit of alternative genomic mapping positions in bowtie
        int nMaxAltMappingsReadsWOTrHit = 0; // Dito where the only hit in map file is not to a transcript

        int sampleDistForAccuStats = 5000000;
        private int statsSampleDistPerBarcode;

        Dictionary<int, List<int>> sampledDetectedTranscriptsByBcIdx = new Dictionary<int, List<int>>();
        // For non-rndTagged samples the following to will be identical:
        Dictionary<int, List<int>> sampledUniqueMoleculesByBcIdx = new Dictionary<int, List<int>>();
        Dictionary<int, List<int>> sampledUniqueHitPositionsByBcIdx = new Dictionary<int, List<int>>();
        int[] nReadsInSampleBin = new int[1];
        int sampleBinNumber = 0;

        Dictionary<string, int> redundantHits = new Dictionary<string, int>();
        List<Pair<MultiReadMapping, FtInterval>> exonsToMark;
        Dictionary<string, Pair<MultiReadMapping, FtInterval>> geneToExonToMark = new Dictionary<string,Pair<MultiReadMapping,FtInterval>>();
        List<string> exonHitGeneNames;
        string annotationChrId;

        public TranscriptomeStatistics(AbstractGenomeAnnotations annotations, Props props)
		{
            AnnotType.DirectionalReads = props.DirectionalReads;
            barcodes = props.Barcodes;
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
            numUniqueMolReadsByBarcode = new int[barcodes.Count];
            exonsToMark = new List<Pair<MultiReadMapping, FtInterval>>(100);
            exonHitGeneNames = new List<string>(100);
            annotationChrId = Annotations.Genome.Annotation;
            randomTagFilter = new RandomTagFilterByBc(barcodes, Annotations.GetChromosomeNames());
            statsSampleDistPerBarcode = sampleDistForAccuStats / barcodes.Count;
        }

        public void SetRedundantHitMapper(AbstractGenomeAnnotations annotations, int averageReadLen)
        {
            redundantExonHitMapper = RedundantExonHitMapper.GetRedundantHitMapper(annotations.Genome, averageReadLen, annotations.geneFeatures);
        }

        public void AnnotateSingleBarcodeMapFile(string mapFilePath, int bcIdx)
        {
            currentMapFilePath = mapFilePath;
            if (numReads > 0 && bcIdx != currentBcIdx)
                FinishBarcode(numReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode);
            currentBcIdx = bcIdx;
            MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, 20, barcodes);
            if (mapFileReader == null)
                throw new Exception("Unknown read map file type : " + mapFilePath);
            foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
            {
                if (mrm.BarcodeIdx != currentBcIdx)
                    Console.WriteLine("Error in read.BcIdx: " + mrm.BarcodeIdx + " detected for currentBcIdx" + currentBcIdx + " in " + mapFilePath);
                if (randomTagFilter.IsNew(mrm[0].Chr, mrm[0].Position, mrm[0].Strand, currentBcIdx, mrm.RandomBcIdx))
                    Add(mrm);
                numReads++;
                if ((++numReadsByBarcode[currentBcIdx]) % statsSampleDistPerBarcode == 0)
                    SampleStatistics(statsSampleDistPerBarcode);
            }
        }

        public void SampleStatistics(int numReadsInBin)
        {
            SampleDepths(numReadsInBin);
            //SampleForGeneCVs();
        }

        private void SampleForGeneCVs()
        {
            int[] speciesBcIndexes = barcodes.GenomeBarcodeIndexes(Annotations.Genome, true);
            List<int> totbcHits = new List<int>(TotalHitsByBarcode);
            totbcHits.Sort();
            int minTotBcHits = totbcHits[totbcHits.Count / 2];
            foreach (GeneFeature gf in Annotations.geneFeatures.Values)
            {
                gf.SampleVariation(TotalHitsByBarcode, minTotBcHits, speciesBcIndexes);
            }
        }

        private void SampleDepths(int numReadsInBin)
        {
            if (!sampledUniqueHitPositionsByBcIdx.ContainsKey(currentBcIdx))
            {
                sampledDetectedTranscriptsByBcIdx[currentBcIdx] = new List<int>();
                sampledUniqueMoleculesByBcIdx[currentBcIdx] = new List<int>();
                sampledUniqueHitPositionsByBcIdx[currentBcIdx] = new List<int>();
            }
            sampledDetectedTranscriptsByBcIdx[currentBcIdx].Add(Annotations.GetNumExpressedGenes(currentBcIdx));
            sampledUniqueMoleculesByBcIdx[currentBcIdx].Add(randomTagFilter.nUniqueByBarcode[currentBcIdx]);
            sampledUniqueHitPositionsByBcIdx[currentBcIdx].Add(randomTagFilter.GetNumDistinctMappings());
            if (nReadsInSampleBin.Length == sampleBinNumber)
                Array.Resize(ref nReadsInSampleBin, sampleBinNumber + 1);
            nReadsInSampleBin[sampleBinNumber++] += numReadsInBin;
        }
        public void FinishBarcode()        {
            int nReadsInBin = numReadsByBarcode[currentBcIdx] % statsSampleDistPerBarcode;
            FinishBarcode(nReadsInBin);        }        public void FinishBarcode(int nReadsInBin)
        {
            SampleStatistics(nReadsInBin);
            sampleBinNumber = 0;
            MakeWigglePlotsByMolecule();
        }

        private void MakeWigglePlotsByMolecule()
        {
            if (!barcodes.HasRandomBarcodes || !Props.props.GenerateBarcodedWiggle)
                return;
            int readLength = 0;
            WriteMoleculeWiggleStrand(readLength, '+');
            WriteMoleculeWiggleStrand(readLength, '-');
        }

        private void WriteMoleculeWiggleStrand(int readLength, char strand)
        {
            int strandSign = (strand == '+') ? 1 : -1;
            string fileNameHead = string.Format("{0}_{1}_bymolecule_{2}", currentBcIdx, Annotations.Genome.Build, ((strand == '+') ? "_fw" : "_rev"));
            string filePathHead = Path.Combine(Path.GetDirectoryName(currentMapFilePath), fileNameHead);
            string file = filePathHead + ".wig.gz";
            if (File.Exists(file))
                return;
            var writer = file.OpenWrite();
            writer.WriteLine("track type=wiggle_0 name=\"{0} ({1})\" description=\"{0} (+)\" visibility=full", fileNameHead, strand);
            foreach (KeyValuePair<string, RandomTagFilterByBc.ChrTagData> tagDataPair in randomTagFilter.chrTagDatas)
            {
                string chr = tagDataPair.Key;
                if (!StrtGenome.IsSyntheticChr(chr))
                {
                    int chrLen = Annotations.ChromosomeLengths[chr];
                    int[] positions, countAtEachPosition;
                    tagDataPair.Value.GetDistinctPositionsAndMoleculeCounts(strand, out positions, out countAtEachPosition);
                    CompactWiggle.DumpToWiggle(writer, chr, readLength, strandSign, chrLen, positions, countAtEachPosition);
                }
            }
            writer.Close();
        }

        public void Add(MultiReadMappings mappings)
        {
            exonHitGeneNames.Clear();
            int bcodeIdx = mappings.BarcodeIdx;
            int hitLen = mappings.SeqLen;
            int halfWidth = hitLen / 2;
            bool someAnnotationHit = false;
            bool someExonHit = false;
            MarkStatus markType = MarkStatus.TEST_EXON_MARK_OTHER;
            foreach (MultiReadMapping mapping in mappings.IterMappings())
            {
                bool currentMappingHitAnAnnotation = false;
                int hitStartPos = mapping.Position;
                string chr = mapping.Chr;
                char strand = mapping.Strand;
                int hitMidPos = hitStartPos + halfWidth;
                foreach (FtInterval ivl in Annotations.GetMatching(chr, hitMidPos))
                {
                    MarkResult res = ivl.Mark(hitMidPos, halfWidth, strand, bcodeIdx, ivl.ExtraData, markType);
                    if (res.annotType == AnnotType.NOHIT)
                        continue;
                    someAnnotationHit = true;
                    currentMappingHitAnAnnotation = true;
                    if (AnnotType.IsTranscript(res.annotType))
                    {
                        if (!exonHitGeneNames.Contains(res.feature.Name))
                        {
                            someExonHit = true;
                            exonHitGeneNames.Add(res.feature.Name);
                            exonsToMark.Add(new Pair<MultiReadMapping, FtInterval>(mapping, ivl));
                        }
                    }
                    else // hit is not to EXON or SPLC (neither AEXON/ASPLC for non-directional samples)
                    {
                        if (markType == MarkStatus.TEST_EXON_MARK_OTHER)
                        {
                            TotalHitsByAnnotTypeAndBarcode[res.annotType, bcodeIdx]++;
                            TotalHitsByAnnotTypeAndChr[chr][res.annotType]++;
                            TotalHitsByAnnotType[res.annotType]++;
                            TotalHitsByBarcode[bcodeIdx]++;
                            markType = MarkStatus.TEST_EXON_SKIP_OTHER;
                        }
                        //markType = MarkStatus.TEST_EXON_SKIP_OTHER;
                    }
                }
                if (chr != annotationChrId)
                {
                    if (compactWiggle != null)
                        compactWiggle.AddHit(chr, strand, hitStartPos, hitLen, 1, currentMappingHitAnAnnotation);
                    // Add to the motif (base 21 in the motif will be the first base of the read)
                    // Subtract one to make it zero-based
                    if (DetermineMotifs && someAnnotationHit && Annotations.HasChromosome(chr))
                        motifs[bcodeIdx].Add(Annotations.GetChromosome(chr), hitStartPos - 20 - 1, strand);
                }
            }
            // Now when the best alignments have been selected, mark these transcript hits
            MarkStatus markStatus = (exonsToMark.Count > 1) ? MarkStatus.ALT_MAPPINGS : MarkStatus.SINGLE_MAPPING;
            if (mappings.NMappings == 1 && mappings.AltMappings == Props.props.BowtieMaxNumAltMappings)
            {
                markStatus = MarkStatus.MARK_ALT_MAPPINGS;
                nMaxAltMappingsReads++;
                if (!someExonHit) nMaxAltMappingsReadsWOTrHit++;
                if (redundantExonHitMapper != null)
                    exonsToMark = redundantExonHitMapper.GetRedundantMappings(mappings[0].Chr, mappings[0].Position, mappings[0].Strand);
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
            }
            if (someAnnotationHit)
            {
                numAnnotatedReads++;
                if (someExonHit) numExonAnnotatedReads++;
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

		/// <summary>
		///  Save all the statistics to a set of files
		/// </summary>
        /// <param name="extractionSummaryPath">Full path to Lxxx_extraction_summary.txt file</param>
		/// <param name="fileNameBase">A path and a filename prefix that will used to create all output files, e.g. "/data/Sample12_"</param>
		public void SaveResult(ReadCounter readCounter, string fileNameBase)
		{
            if (TestReporter != null)
                TestReporter.Summarize(Annotations.geneFeatures);
            WriteRedundantExonHits(fileNameBase);
            WriteASExonDistributionHistogram(fileNameBase);
            WriteSummary(fileNameBase, Annotations.GetSummaryLines(), readCounter);
            Annotations.SaveResult(fileNameBase);
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
            string xmlPath = fileNameBase + "_summary.xml";
            var xmlFile = xmlPath.OpenWrite();
            xmlFile.WriteLine("<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>");
            xmlFile.WriteLine("<strtSummary project=\"{0}\">", Path.GetDirectoryName(fileNameBase));
            WriteReadStats(readCounter, xmlFile);
            WriteReadsBySpecies(xmlFile);
            WriteFeatureStats(xmlFile);
            WriteSenseAntisenseStats(xmlFile);
            WriteHitsByChromosome(xmlFile);
            WriteAccuBarcodedDetection(xmlFile);
            WriteMappingDepth(xmlFile);
            AddSpikes(xmlFile);
            Annotations.WriteSpikeDetection(xmlFile);
            AddHitProfile(xmlFile);
            //AddSampledCVs(xmlFile); Difficult when running barcode-by-barcode
            AddCVHistogram(xmlFile);
            WriteBarcodeStats(fileNameBase, xmlFile);
            WriteRandomFilterStats(xmlFile);
            xmlFile.WriteLine("</strtSummary>");
            xmlFile.Close();
            if (!Environment.OSVersion.VersionString.Contains("Microsoft"))
            {
                CmdCaller.Run("php", "make_html_summary.php " + xmlPath);
            }
        }

        private void WriteAccuBarcodedDetection(StreamWriter xmlFile)
        {
            //WriteAccuMolecules(xmlFile, "transcriptdepth", "Distinct (10^6) detected transcripts as fn. of reads processed",
            //                       sampledDetectedTranscriptsByBcIdx);
            WriteAccuMoleculesByBc(xmlFile, "transcriptdepthbybc", "Distinct detected transcripts in each barcode as fn. of reads processed", 
                                   sampledDetectedTranscriptsByBcIdx);
        }

        private void WriteMappingDepth(StreamWriter xmlFile)
        {
            //WriteAccuMolecules(xmlFile, "librarydepth", "Distinct (10^6) mappings as fn. of reads processed",
            //                       sampledUniqueHitPositionsByBcIdx);
            WriteAccuMoleculesByBc(xmlFile, "librarydepthbybc", "Distinct mappings in each barcode as fn. of reads processed",
                                   sampledUniqueHitPositionsByBcIdx);
        }

        private void WriteRandomFilterStats(StreamWriter xmlFile)
        {
            if (!barcodes.HasRandomBarcodes) return;
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
            xmlFile.WriteLine("    <nduplicatedrandomtagsperbarcodeidx>");
            //WriteAccuMolecules(xmlFile, "moleculedepth", "Distinct (10^6) detected molecules as fn. of reads processed",
            //                       sampledUniqueMoleculesByBcIdx);
            WriteAccuMoleculesByBc(xmlFile, "moleculedepthbybc", "Distinct detected molecules in each barcode as fn. of reads processed",
                                   sampledUniqueMoleculesByBcIdx);
        }

        private void WriteAccuMoleculesByBc(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data)
        {
            xmlFile.WriteLine("  <" + tag + ">");
            xmlFile.WriteLine("    <title>" + title + "</title>");
            xmlFile.WriteLine("    <xtitle>Millions of reads processed</xtitle>");
            foreach (int bcIdx in data.Keys)
            {
                List<int> curve = data[bcIdx];
                xmlFile.WriteLine("      <curve legend=\"{0}\" color=\"#{1:x2}{2:x2}{3:x2}\">",
                                  barcodes.Seqs[bcIdx], (bcIdx * 17) % 255, (bcIdx * 11) % 255, (255 - (43 * bcIdx % 255)));
                int nReads = 0;
                int i = 0;
                for (; i < curve.Count - 1; i++)
                {
                    nReads += statsSampleDistPerBarcode;
                    xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                }
                nReads = numReadsByBarcode[bcIdx];
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nReads / 1.0E6d, curve[i]);
                xmlFile.WriteLine("    </curve>");
            }
            xmlFile.WriteLine("  </" + tag + ">");
        }

        /* This does not work since same data in different barcodes will we counted as distinct.
        private void WriteAccuMolecules(StreamWriter xmlFile, string tag, string title, Dictionary<int, List<int>> data)
        {
            int nSamples0 = data.Values.ToArray()[0].Count;
            int[] valuesInBins = new int[nSamples0];
            foreach (int bcIdx in data.Keys)
            {
                int nSamples = data[bcIdx].Count;
                if (valuesInBins.Length < nSamples)
                    Array.Resize(ref valuesInBins, nSamples);
                for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                    valuesInBins[sampleIdx] += data[bcIdx][sampleIdx];
            }
            xmlFile.WriteLine("  <" + tag + ">");
            xmlFile.WriteLine("    <title>" + title + "</title>");
            xmlFile.WriteLine("	   <xtitle>Millions of reads processed</xtitle>");
            xmlFile.WriteLine("      <curve legend=\"Total accumulated (millions)\" color=\"#00FF00\">");
            int i = 0, nAccuReads = 0, nAccuValues = 0;
            for (; i < valuesInBins.Length - 1; i++)
            {
                nAccuReads += nReadsInSampleBin[i];
                nAccuValues += valuesInBins[i];
                xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nAccuReads / 1.0E6d, nAccuValues / 1.0E6d);
            }
            nAccuValues += valuesInBins[i];
            xmlFile.WriteLine("      <point x=\"{0:0.####}\" y=\"{1:0.####}\" />", nAccuReads / 1.0E6d, nAccuValues / 1.0E6d);
            xmlFile.WriteLine("    </curve>");
            xmlFile.WriteLine("  </" + tag + ">");
        }*/

        private void WriteReadStats(ReadCounter readCounter, StreamWriter xmlFile)
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
            xmlFile.WriteLine("    <point x=\"- over {0} hits [{1}] ({2:0%})\" y=\"{3}\" />",
                              Props.props.BowtieMaxNumAltMappings, spBcCount, nMaxAltMappingsReads / totalReads, nMaxAltMappingsReads / 1.0E6d);
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
            double nAllMappedReads = 0;
            double nUniqueMappedReads = 0;
            double nAnnotationsHit = 0;
            foreach (int bcIdx in speciesBcIndexes)
            {
                nAllMappedReads += numReadsByBarcode[bcIdx];
                nUniqueMappedReads += numUniqueMolReadsByBarcode[bcIdx];
                nAnnotationsHit += TotalHitsByBarcode[bcIdx];
            }
            xmlFile.WriteLine("  <reads species=\"{0}\">", speciesName);
            if (barcodes.HasRandomBarcodes)
            {
                xmlFile.WriteLine("    <title>Unique molecule distribution (10^6) by categories in {0} {1} wells</title>",
                              speciesBcIndexes.Length, speciesName);
                xmlFile.WriteLine("    <point x=\"All mapped reads(100%)\" y=\"{0}\" />", nAllMappedReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Unique molecules ({0:0%})\" y=\"{1}\" />", nUniqueMappedReads / nAllMappedReads, nUniqueMappedReads / 1.0E6d);
            }
            else
            {
                xmlFile.WriteLine("    <title>Mapped read distribution (10^6) by categories in {0} {1} wells</title>",
                              speciesBcIndexes.Length, speciesName);
                xmlFile.WriteLine("    <point x=\"All mapped (100%)\" y=\"{0}\" />", nAllMappedReads / 1.0E6d);
                xmlFile.WriteLine("    <point x=\"Annotations ({0:0%})\" y=\"{1}\" />", nAnnotationsHit / nAllMappedReads, nAnnotationsHit / 1.0E6d);
                foreach (int annotType in new int[] { AnnotType.EXON, AnnotType.INTR, AnnotType.AEXON, AnnotType.REPT })
                {
                    int nType = 0;
                    foreach (int bcIdx in speciesBcIndexes)
                        nType += TotalHitsByAnnotTypeAndBarcode[annotType, bcIdx];
                    xmlFile.WriteLine("    <point x=\"{0} ({1:0%})\" y=\"{2}\" />", AnnotType.GetName(annotType), nType / nAllMappedReads, nType / 1.0E6d);
                }
                xmlFile.WriteLine("  </reads>");
            }
        }
        
        private void WriteSenseAntisenseStats(StreamWriter xmlFile)
        {
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
                xmlFile.WriteLine("   <point x=\"{0}#br#{1}:1\" y=\"{2:0.##}\" y2=\"{3:0.##}\" />", AnnotType.GetName(t),
                                  ratio, 1000.0d * (totSense / (double)totLen), 1000.0d * (totASense / (double)totLen));
            }
            int reptLen = Annotations.GetTotalAnnotLength(AnnotType.REPT);
            int reptCount = TotalHitsByAnnotType[AnnotType.REPT];
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
                if (StrtGenome.IsASpliceAnnotationChr(chr))
                    continue;
                double nSense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.EXON];
                double nAsense = TotalHitsByAnnotTypeAndChr[chr][AnnotType.AEXON];
                string ratio = (nAsense == 0)? "1:0" : string.Format("{0:0}", nSense / (double)nAsense);
                xmlFile.WriteLine("    <point x=\"{0}#br#{1}\" y=\"{2:0.###}\" y2=\"{3:0.###}\" />",
                                  chr, ratio, 100.0d *(nSense / (double)numReads), 100.0d * (nAsense / (double)numReads));
            }
            xmlFile.WriteLine("  </senseantisensebychr>");
        }

        private void WriteFeatureStats(StreamWriter xmlFile)
        {
            xmlFile.WriteLine("  <features>");
            xmlFile.WriteLine("    <title>Overall detection of features</title>");
            if (!Annotations.noGeneVariants)
                xmlFile.WriteLine("    <point x=\"Detected tr. variants\" y=\"{0}\" />", Annotations.GetNumExpressedGenes());
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

        /*private void AddSampledCVs(StreamWriter xmlFile)
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
        }*/

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
            WriteDuplicateMoleculesByBarcode(xmlFile, barcodeStats, bCodeLines, genomeBcIndexes);
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

        private void WriteDuplicateMoleculesByBarcode(StreamWriter xmlFile, StreamWriter barcodeStats, StreamWriter bCodeLines, int[] genomeBcIndexes)
        {
            if (!barcodes.HasRandomBarcodes) return;
            barcodeStats.WriteLine("\nDuplicated reads filtered away due to same random tag and position, by barcode:\n");
            bCodeLines.Write("TOTAL");
            xmlFile.Write("    <barcodestat section=\"duplicated molecules (by position-random tag)\">");
            string[] counts = new string[barcodes.Count];
            for (int bcIdx = 0; bcIdx < counts.Length; bcIdx++)
            {
                counts[bcIdx] = randomTagFilter.nDuplicatesByBarcode[bcIdx].ToString();
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
