using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Maker of synthetic data for pipeline test.
    /// Output consists of a FastQ reads file, and files giving the true expression levels (molecules and reads),
    /// as well as positions where SNPs are simulated.
    /// </summary>
    public class SyntReadMaker
    {
        private Barcodes barcodes;
        private StrtGenome genome;
        private int nRndTags;
        Random rnd = new Random();

        int meanMolExprLevelPerBc = 10;
        int meanReadsPerMolecule = 50;
        double ProbGeneExpressed = 0.05;
        int hotspotPos;
        int hotspotCount;
        /// <summary>
        /// Probability of generating a SNP at each Nt in transcripts
        /// </summary>
        double snpProb = 0.001;
        /// <summary>
        /// The higher the value, the more 5' will reads be
        /// </summary>
        double trPosTiltPower = 8.0;
        /// <summary>
        /// To mimic truncated reads
        /// </summary>
        public int[] readLengthSamples = new int [4] { 50, 50, 50, 50 };
        private int maxReadLength;
        private int readNumber;
        private string[] barcodesGGG;
        private string firstFiller;
        private string midFiller;

        public SyntReadMaker(Barcodes barcodes, StrtGenome genome)
        {
            this.barcodes = barcodes;
            this.genome = genome;
            nRndTags = 1 << (2 * barcodes.RandomTagLen);
            barcodesGGG = barcodes.GetBarcodesWithTSSeq();
            firstFiller = new string('T', barcodes.RandomTagPos);
            midFiller = new string('T', barcodes.BarcodePos - barcodes.RandomTagLen - barcodes.RandomTagPos);
            maxReadLength = DescriptiveStatistics.Max(readLengthSamples);
        }

        public string SettingsString()
        {
            return "Genome: " + genome.Build + " source: " + genome.Annotation + ((genome.GeneVariants) ? "all" : "single") + " transcript models\r\n" +
                   "Barcode set: " + barcodes.Name + (barcodes.HasRandomTags ? (" (" + nRndTags.ToString()) : " (no") + " random labels)\r\n" +
                   "Read lengths sampled from: " + string.Join(",", Array.ConvertAll(readLengthSamples, v => v.ToString())) + "\r\n" +
                   "Average #reads per molecule: " + meanReadsPerMolecule + " Average expr level of a gene within a barcode: " + meanMolExprLevelPerBc +
                   "Probility of a gene to be expressed: " + ProbGeneExpressed + "\r\n" +
                   "Probability of SNP per base: " + snpProb + " Background reads per base: " + Props.props.SyntheticReadsBackgroundFreq;
        }

        /// <summary>
        /// Generates synthetic transcript read data as a FastQ file for testing of the analysis pipeline.
        /// Generates random mutations, SNPs, barcodes and rndTags depending on the settings.
        /// Output will end up in the Reads folder, as Run #0, lane 1, read 1, with specified Id.
        /// Both .fq data and files specifying SNPs and expression levels will be written.
        /// </summary>
        /// <param name="dataId">Identifier of output files - will be put in place of Illumina machine Id</param>
        public void SynthetizeReads(string dataId)
        {
            int nMols = 0;
            readNumber = 1;
            bool variantGenes = genome.GeneVariants;
            Dictionary<string, string> chrIdToFileMap = genome.GetOriginalGenomeFilesMap();
            Dictionary<string, List<GeneFeature>> chrIdToFeature = ReadGenesByChr(genome, chrIdToFileMap.Keys.ToList());
            string outFileHead = PathHandler.MakeSyntLevelFileHead(dataId);
            string fqOutput = outFileHead + ".fq";
            StreamWriter fqWriter = fqOutput.OpenWrite();
            string snpOutput = outFileHead + ".snps";
            StreamWriter snpWriter = snpOutput.OpenWrite();
            string readOutput = outFileHead + ".readlevels";
            StreamWriter readWriter = readOutput.OpenWrite();
            string molOutput = outFileHead + ".mollevels";
            StreamWriter molWriter = null;
            if (barcodes.HasRandomTags)
                molWriter = SetupWriter(genome, molOutput);
            int nReads = 0; int nBkgSeqs = 0, nSNPs = 0;
            foreach (string chrId in chrIdToFeature.Keys)
            {
                Console.Write(chrId + ".");
                Console.Out.Flush();
                DnaSequence chrSeq = AbstractGenomeAnnotations.ReadChromosomeFile(chrIdToFileMap[chrId]);
                int nChrBkgSeqs = rnd.Next((int)(chrSeq.Count * Props.props.SyntheticReadsBackgroundFreq));
                nBkgSeqs += nChrBkgSeqs;
                for (; nChrBkgSeqs > 0; nChrBkgSeqs--)
                {
                    string fqBlock = MakeBkgRead(chrId, chrSeq);
                    fqWriter.WriteLine(fqBlock);
                }
                Console.WriteLine("Wrote " + nBkgSeqs + " for chr" + chrId);
                foreach (GeneFeature gf in chrIdToFeature[chrId])
                {
                    if ((!variantGenes && gf.IsVariant()) || (gf.Length > Props.props.MaxFeatureLength) || (gf.Length <= maxReadLength))
                        continue;
                    if (rnd.NextDouble() > ProbGeneExpressed)
                        continue;
                    DnaSequence origTrSeq = ExtractTranscriptSeq(chrSeq, gf);
                    int maxPos = (int)origTrSeq.Count - maxReadLength;
                    int[] nMolsPerBc = new int[barcodes.Count];
                    int[] nDistinctRndTagsPerBc = new int[barcodes.Count];
                    int[] nReadsPerBc = new int[barcodes.Count];
                    DnaSequence snpTrSeq = new ShortDnaSequence(origTrSeq);
                    List<Pair<int, char>> SNPs = new List<Pair<int, char>>();
                    for (int p = 0; p < snpTrSeq.Count; p++)
                    {
                        if (rnd.NextDouble() < snpProb)
                        {
                            char oldNt = snpTrSeq.GetNucleotide(p);
                            char newNt = MutateNt(oldNt);
                            snpTrSeq.SetNucleotide(p, newNt);
                            SNPs.Add(new Pair<int, char>(p, newNt));
                            snpWriter.WriteLine(gf.Name + "\t" + p.ToString() + "\t" + oldNt + ">" + newNt);
                            snpWriter.Flush();
                            nSNPs++;
                        }
                    }
                    double thisGfMeanMolsPerBc = new GammaDistribution(2.0, meanMolExprLevelPerBc).Sample();
                    for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
                    {
                        int nGfMols = (int)(Math.Max(0.0, new ExponentialDistribution(3.0).Sample() * 3.0 * (2.0 + thisGfMeanMolsPerBc) - 2.0));
                        nMolsPerBc[bcIdx] = nGfMols;
                        double useSNPSeqForThisBcP = 0.0;
                        if (SNPs.Count > 0 && rnd.NextDouble() > 0.9)
                        {
                            double p = rnd.NextDouble();
                            if (p > 0.75) useSNPSeqForThisBcP = 1.0;
                            else if (p > 0.5) useSNPSeqForThisBcP = 0.5;
                        }
                        int[] rndTagsUsed = new int[nRndTags];
                        for (int molIdx = 0; molIdx < nGfMols; molIdx++)
                        {
                            double useSNPSeqForThisMolP = (rnd.NextDouble() < useSNPSeqForThisBcP) ? 0.99 : 0.01; 
                            int nReadsFromMol = new PoissonDistribution(meanReadsPerMolecule).Sample();
                            nReadsPerBc[bcIdx] += nReadsFromMol;
                            int rndTagIdx = rnd.Next(nRndTags);
                            rndTagsUsed[rndTagIdx] = 1;
                            int trPos = GetNextHitPosition(maxPos, nGfMols - molIdx);
                            WriteExonReadsForOneMolecule(fqWriter, bcIdx, rndTagIdx, gf, origTrSeq, snpTrSeq, trPos,
                                                         nReadsFromMol, useSNPSeqForThisMolP, SNPs);
                        }
                        nDistinctRndTagsPerBc[bcIdx] = rndTagsUsed.Sum();
                    }
                    if (barcodes.HasRandomTags)
                    {
                        WriteExprReportLine(molWriter, gf.Name, nMolsPerBc);
                        WriteExprReportLine(molWriter, "(distinct RndTags)", nDistinctRndTagsPerBc);
                    }
                    WriteExprReportLine(readWriter, gf.Name, nReadsPerBc);
                    Console.WriteLine(gf.Name + ":" + nReadsPerBc.Sum() + " reads in " + nMolsPerBc.Sum() + " molecules.");
                    nReads += nReadsPerBc.Sum();
                    nMols += nMolsPerBc.Sum();
                }
            }
            fqWriter.Close();
            snpWriter.Close();
            readWriter.Close();
            Console.WriteLine("\nWrote totally " + nReads + " exon reads from " + nMols + " and " + nBkgSeqs + " background reads to fasta file " + fqOutput);
            Console.WriteLine("Wrote parameters and read levels to " + readOutput);
            Console.WriteLine("Wrote " + nSNPs + " SNPs to " + snpOutput);
            if (molWriter != null)
            {
                molWriter.Close();
                Console.WriteLine("Wrote parameters and molecule counts to " + molOutput);
            }
        }

        private StreamWriter SetupWriter(StrtGenome genome, string outputFile)
        {
            StreamWriter reportWriter = outputFile.OpenWrite();
            reportWriter.WriteLine("Synthetic data - parameters:\nBarcodeSet\t{0}\nGenome\t{1}\nMutationProb\t{2}",
                                   barcodes.Name, genome.GetBowtieMainIndexName(), Props.props.SyntheticReadsRandomMutationProb);
            reportWriter.WriteLine("MaxExprLevel\t{0}BackgroundFreq\t{1}\n\nGeneFeature\tExprLevel",
                                   meanMolExprLevelPerBc, Props.props.SyntheticReadsBackgroundFreq);
            return reportWriter;
        }

        private static DnaSequence ExtractTranscriptSeq(DnaSequence chrSeq, GeneFeature gf)
        {
            DnaSequence gfTrFwSeq = new LongDnaSequence(gf.Length);
            for (int exonIdx = 0; exonIdx < gf.ExonCount; exonIdx++)
            {
                int exonLen = 1 + gf.ExonEnds[exonIdx] - gf.ExonStarts[exonIdx];
                gfTrFwSeq.Append(chrSeq.SubSequence(gf.ExonStarts[exonIdx], exonLen));
            }
            if (gf.Strand == '-')
                gfTrFwSeq.RevComp();
            return gfTrFwSeq;
        }

        private static void WriteExprReportLine(StreamWriter reportWriter, string name, int[] nPerBc)
        {
            reportWriter.WriteLine(name + "\t" + nPerBc.Sum() + "\t");
            foreach (int n in nPerBc)
                reportWriter.Write("\t" + n);
            reportWriter.WriteLine();
        }

        private static Dictionary<string, List<GeneFeature>> ReadGenesByChr(StrtGenome genome, List<string> chrIds)
        {
            string annotationsPath = genome.VerifyAnAnnotationPath();
            Dictionary<string, List<GeneFeature>> chrIdToFeature = new Dictionary<string, List<GeneFeature>>();
            foreach (string chrId in chrIds)
            {
                if (StrtGenome.IsASpliceAnnotationChr(chrId)) continue;
                chrIdToFeature[chrId] = new List<GeneFeature>();
            }
            foreach (LocusFeature gf in AnnotationReader.IterAnnotationFile(annotationsPath))
                if (chrIdToFeature.ContainsKey(gf.Chr))
                    chrIdToFeature[gf.Chr].Add((GeneFeature)gf);
            return chrIdToFeature;
        }

        private void WriteExonReadsForOneMolecule(StreamWriter fqWriter, int bcIdx, int rndTagIdx, GeneFeature gf,
                                                  DnaSequence origTrSeq, DnaSequence snpTrSeq, int trPos, int nReads, double useSNPSeqP,
                                                  List<Pair<int, char>> SNPs)
        {
            string bcPart = MakeBarcodePart(bcIdx, rndTagIdx);
            int readLen = 1 + readLengthSamples[rnd.Next(readLengthSamples.Length)] - bcPart.Length;
            DnaSequence trSeq = origTrSeq;
            string snpTxt = "";
            List<string> snpTxts = new List<string>();
            if (rnd.NextDouble() < useSNPSeqP)
            {
                foreach (Pair<int, char> SNP in SNPs)
                {
                    int relPos = SNP.First - trPos;
                    if (relPos >= 0 && relPos < readLen)
                        snpTxts.Add(relPos.ToString() + ':' + origTrSeq.GetNucleotide(SNP.First) + '>' + snpTrSeq.GetNucleotide(SNP.First));
                }
                if (snpTxts.Count > 0)
                {
                    trSeq = snpTrSeq;
                    snpTxt = string.Join(",", snpTxts.ToArray());
                }
            }
            string exonReadSeq = trSeq.SubSequence(trPos, readLen).ToString().Replace("-", "N");
            string nonMutReadSeq = bcPart + exonReadSeq;
            for (int n = 0; n < nReads; n++)
            {
                string readSeq = nonMutReadSeq;
                string mutations = Mutate(ref readSeq);
                string hdr = string.Format("Synt:{0}/{1}/{2}/{3}/{4}/{5}/{6}.{7}",
                                           gf.Name, gf.Chr, gf.Strand, gf.Start, trPos, snpTxt, mutations, readNumber++);
                string fqBlock = "@" + hdr + "\n" + readSeq + "\n+\n" + new string('b', readSeq.Length);
                fqWriter.WriteLine(fqBlock);
            }
        }

        private string MakeBkgRead(string chrId, DnaSequence chrSeq)
        {
            string bcPart = MakeBarcodePart(-1, -1);
            int bkgPos = rnd.Next((int)chrSeq.Count - maxReadLength);
            int seqPartLen = 1 + readLengthSamples[rnd.Next(readLengthSamples.Length)] - bcPart.Length;
            while (chrSeq.CountCases('N', bkgPos, bkgPos + seqPartLen) > 2)
                bkgPos = rnd.Next((int)chrSeq.Count - maxReadLength);
            DnaSequence bkgSeq = chrSeq.SubSequence(bkgPos, seqPartLen);
            char strand = (rnd.NextDouble() < 0.5) ? '+' : '-';
            if (strand == '-') bkgSeq.RevComp();
            string bkgReadSeq = bkgSeq.ToString().Replace("-", "N");
            string mutations = Mutate(ref bkgReadSeq);
            string hdr = string.Format("Synt:{0}/{1}/{2}/{3}/{4}/{5}/{6}.{7}", 
                                       "BKG", chrId, strand, 0, bkgPos, "", mutations, readNumber++);
            string readSeq = bcPart + bkgReadSeq;
            string fqBlock = "@" + hdr + "\n" + readSeq + "\n+\n" + new string('b', readSeq.Length);
            return fqBlock;
        }

        private string MakeBarcodePart(int bcIdx, int rndTagIdx)
        {
            if (bcIdx < 0) bcIdx = rnd.Next(barcodesGGG.Length);
            string extraGs = new String('G', Math.Max(0, rnd.Next(11) - 7));
            string bcGGGSeq = barcodesGGG[bcIdx] + extraGs;
            string rndTag = "";
            if (barcodes.HasRandomTags)
            {
                if (rndTagIdx == -1)
                    rndTagIdx = rnd.Next(nRndTags);
                rndTag = barcodes.MakeRandomTag(rndTagIdx);
            }
            return firstFiller + rndTag + midFiller + bcGGGSeq;
        }

        /// <summary>
        /// Simulate random mismatches in read sequence
        /// </summary>
        /// <param name="readSeq"></param>
        /// <returns></returns>
        private string Mutate(ref string readSeq)
        {
            List<string> mutations = new List<string>();
            while (rnd.NextDouble() < Props.props.SyntheticReadsRandomMutationProb)
            {
                int mPos = rnd.Next(readSeq.Length);
                char oldNt = readSeq[mPos];
                char newNt = MutateNt(oldNt);
                mutations.Add(mPos + ":" + oldNt + ">" + newNt);
                readSeq = readSeq.Substring(0, mPos) + newNt + readSeq.Substring(mPos + 1);
            }
            return string.Join(",", mutations.ToArray());
        }

        private char MutateNt(char nt)
        {
            int i = "ACGT".IndexOf(nt);
            char newNt = "ACGT"[(i + 1 + rnd.Next(3)) % 4];
            return newNt;
        }

        private int GetNextHitPosition(int maxPos, int maxCount)
        {
            if (hotspotCount == 0)
            {
                double relPos = Math.Pow(rnd.NextDouble(), trPosTiltPower);
                hotspotPos = (int)(maxPos * relPos);
                hotspotCount = rnd.Next(1, maxCount);
            }
            hotspotCount--;
            return hotspotPos;
        }
    }

    public class SyntReadReporter
    {
        private StreamWriter readReporter;
        private bool geneVariants;
        private string filenameBase;
        private string syntLevelFile;
        private Dictionary<string, GeneFeature> geneFeatures;
        private Dictionary<string, int> realGeneBkgCounts;
        private Dictionary<string, int> realGeneTooRedundantSeq;
        private int nBkgHitOnExon = 0;
        private int nBkgReadIsActuallyExon = 0;
        private int nTooManyMappingPositions = 0;
        private int nNoHitToGene = 0;
        private int nHitToWrongGene = 0;
        private int maxNumMappings = 1;

        public SyntReadReporter(string syntLevelFile, bool analyzeGeneVariants, string filenameBase, Dictionary<string, GeneFeature> geneFeatures)
        {
            geneVariants = analyzeGeneVariants;
            if (!Directory.Exists(Path.GetDirectoryName(filenameBase)))
                Directory.CreateDirectory(Path.GetDirectoryName(filenameBase));
            readReporter = new StreamWriter(filenameBase + "_" + Props.props.TestAnalysisFileMarker + "_analysis.txt");
            this.filenameBase = filenameBase;
            this.geneFeatures = geneFeatures;
            this.syntLevelFile = syntLevelFile;
            realGeneBkgCounts = new Dictionary<string, int>();
            realGeneTooRedundantSeq = new Dictionary<string, int>();
            foreach (GeneFeature gf in this.geneFeatures.Values)
            {
                realGeneBkgCounts[gf.Name] = 0;
                realGeneTooRedundantSeq[gf.Name] = 0;
            }
        }

        public void ReportHit(List<string> exonHitGeneNames, MultiReadMappings recs, List<Pair<MultiReadMapping, FtInterval>> exonsToMark)
        {
            string descr = "";
            if (recs.ReadId.StartsWith("Synt:BKG"))
            {
                if (exonHitGeneNames.Count > 0)
                {
                    string pat = "Synt:BKG:([^+-]+)([+-]):([0-9]+):([0-9]+)";
                    Match m = Regex.Match(recs.ReadId, pat);
                    string chrId = m.Groups[1].Value;
                    int pos = int.Parse(m.Groups[4].Value);
                    List<string> realGfHits = new List<string>();
                    bool realGeneRead = false;
                    foreach (string geneName in exonHitGeneNames)
                    {
                        GeneFeature gf = geneFeatures[geneName];
                        if (gf.Contains(pos, pos + recs.SeqLen))
                        {
                            realGfHits.Add(gf.Name);
                            realGeneBkgCounts[gf.Name]++;
                            nBkgReadIsActuallyExon++;
                            realGeneRead = true;
                        }
                    }
                    if (realGeneRead)
                    {
                        descr = "---- BKG read is not true bkg, but really mapping to " + string.Join("/", realGfHits.ToArray()) + ": ----";
                    }
                    else
                    {
                        nBkgHitOnExon++;
                        descr = "---- BKG read wrongly mapped to " + string.Join("/", exonHitGeneNames.ToArray()) + ": ----";
                    }
                }
            }
            else if (exonHitGeneNames.Count == 0)
            {
                descr = "---- No hit to annotated exon: ----";
                nNoHitToGene++;
                if (recs.AltMappings >= maxNumMappings)
                {
                    AddToRedundant(recs);
                    descr = "---- No hit: Redundant: Many mappings to genome (" + recs.AltMappings + "): ----";
                }
            }
            else if (exonHitGeneNames.Count == 1)
            {
                string actualHitGene = exonHitGeneNames[0];
                foreach (MultiReadMapping rec in recs.IterMappings())
                {
                    if (recs.ReadId.Contains(actualHitGene))
                        return;
                }
                nHitToWrongGene++;
                descr = "---- Got a wrong hit to " + actualHitGene + ": ----";
                if (recs.AltMappings >= maxNumMappings)
                {
                    AddToRedundant(recs);
                    descr = "---- Wrong hit to " + actualHitGene + ": Redundant: Many mappings to genome (" + recs.AltMappings + "): ----";
                }
            }
            if (descr != "")
            {
                readReporter.WriteLine(descr);
                foreach (MultiReadMapping rec in recs.IterMappings())
                {
                    readReporter.WriteLine(rec.ToString());
                }
            }
        }

        private void AddToRedundant(MultiReadMappings rec)
        {
            int pos = rec.ReadId.IndexOf(":", 5);
            string geneName = rec.ReadId.Substring(5, pos - 5);
            try
            {
                realGeneTooRedundantSeq[geneName]++;
            }
            catch (Exception e)
            {
                Console.WriteLine(e + " " + geneName);
            }
        }

        public void Summarize(Dictionary<string, GeneFeature> geneFeatures)
        {
            readReporter.Close();
            StreamWriter testSummary = new StreamWriter(filenameBase + "_" + Props.props.TestAnalysisFileMarker +"_summary.txt");
            testSummary.WriteLine("Exonic read with hit only to the wrong gene: " + nHitToWrongGene);
            testSummary.WriteLine("Exonic read without hit to any gene: " + nNoHitToGene);
            testSummary.WriteLine("Too many (>" +  maxNumMappings + ") redundant read mappings: " + nTooManyMappingPositions);
            testSummary.WriteLine("Background read somehow mapped to a real gene: " + nBkgHitOnExon);
            testSummary.WriteLine("Background read is not true bkg, but is actually and mapped to an exon: " + nBkgReadIsActuallyExon);
            Dictionary<string, int> geneToLevel = new Dictionary<string, int>();
            if (geneFeatures != null)
            {
                StreamReader lReader = new StreamReader(syntLevelFile);
                string line = lReader.ReadLine();
                while (!line.StartsWith("GeneFeature"))
                    line = lReader.ReadLine();
                line = lReader.ReadLine();
                while (line != null)
                {
                    string[] fields = line.Trim().Split('\t');
                    geneToLevel[fields[0]] = int.Parse(fields[1]);
                    line = lReader.ReadLine();
                }
                lReader.Close();
                int nWithinRange = 0, nTooLow = 0, nTooHigh = 0, n10PcLow = 0, n10PcHigh = 0, nDetectedWhenZero = 0;
                int nTooHighDueToAltMappings = 0, n10PcTooHighDueToAltMappings = 0;
                testSummary.WriteLine("\nGenes with measured counts that do not match the actual level:");
                testSummary.WriteLine("Gene\tActualCount\tKnownBkg\tMissedRedundant\tMeasuredMin\tMeasuredMax");
                foreach (GeneFeature gf in geneFeatures.Values)
                {
                    int minHits = gf.NonConflictingTranscriptHitsByBarcode.Sum();
                    int maxHits = gf.HitsByAnnotType[AnnotType.EXON];
                    int actualLevel = 0;
                    int levelInclKnownBkg = realGeneBkgCounts[gf.Name];
                    try
                    {
                        actualLevel = geneToLevel[gf.Name];
                        levelInclKnownBkg += actualLevel;
                    }
                    catch (KeyNotFoundException)
                    {
                        if (minHits > levelInclKnownBkg) nDetectedWhenZero++;
                    }
                    if (levelInclKnownBkg * 1.1 < minHits) n10PcLow++;
                    if (levelInclKnownBkg < minHits) nTooLow++;
                    if (actualLevel * 0.9 > maxHits)
                    {
                        n10PcHigh++;
                        if (actualLevel * 0.9 <= maxHits + realGeneTooRedundantSeq[gf.Name])
                            n10PcTooHighDueToAltMappings++;
                    }
                    if (actualLevel > maxHits)
                    {
                        nTooHigh++;
                        if (actualLevel <= maxHits + realGeneTooRedundantSeq[gf.Name])
                            nTooHighDueToAltMappings++;
                    }
                    if (actualLevel >= minHits && actualLevel <= maxHits)
                        nWithinRange++;
                    else
                        testSummary.WriteLine(gf.Name + "\t" + actualLevel + "\t" + realGeneBkgCounts[gf.Name] + "\t" +
                                              realGeneTooRedundantSeq[gf.Name] + "\t" + minHits + "\t" + maxHits);
                }
                testSummary.WriteLine("\nGenes where measured Min-Max spans actual level: " + nWithinRange);
                testSummary.WriteLine("Genes where measured Min is above actual level + known bkg: " + nTooLow);
                testSummary.WriteLine("Genes where measured Min > 10% above actual level + known bkg: " + n10PcLow);
                testSummary.WriteLine("Genes where measured Max is below actual level: " + nTooHigh);
                testSummary.WriteLine("Cases where this is due to too many redundant mappings: " + nTooHighDueToAltMappings);
                testSummary.WriteLine("Genes where measured Max is > 10% below actual level: " + n10PcHigh);
                testSummary.WriteLine("Cases where this is due to too many redundant mappings: " + n10PcTooHighDueToAltMappings);
                testSummary.WriteLine("Genes that were detected above known bkg when actual level=0: " + nDetectedWhenZero);
                testSummary.WriteLine("\nNote that measured levels may be higher if random background is defined in test data.");
            }
            testSummary.Close();
        }
    }
}
