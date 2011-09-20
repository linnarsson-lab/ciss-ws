using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
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
        private int maxNumMappings = Props.props.BowtieMaxNumAltMappings;

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

        public void ReportHit(List<string> exonHitGeneNames, ReadMapping[] recs, List<Pair<int, FtInterval>> exonsToMark)
        {
            string descr = "";
            if (recs[0].ReadId.StartsWith("Synt:BKG"))
            {
                if (exonHitGeneNames.Count > 0)
                {
                    string pat = "Synt:BKG:([^+-]+)([+-]):([0-9]+):([0-9]+)";
                    Match m = Regex.Match(recs[0].ReadId, pat);
                    string chrId = m.Groups[1].Value;
                    int pos = int.Parse(m.Groups[4].Value);
                    List<string> realGfHits = new List<string>();
                    bool realGeneRead = false;
                    foreach (string geneName in exonHitGeneNames)
                    {
                        GeneFeature gf = geneFeatures[geneName];
                        if (gf.Contains(pos, pos + recs[0].SeqLen))
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
                if (recs[0].AltMappings >= maxNumMappings)
                {
                    AddToRedundant(recs[0]);
                    descr = "---- No hit: Redundant: Many mappings to genome (" + recs[0].AltMappings + "): ----";
                }
            }
            else if (exonHitGeneNames.Count == 1)
            {
                string actualHitGene = exonHitGeneNames[0];
                foreach (ReadMapping rec in recs.TakeWhile(r => r.Position != -1))
                {
                    if (rec.ReadId.Contains(actualHitGene))
                        return;
                }
                nHitToWrongGene++;
                descr = "---- Got a wrong hit to " + actualHitGene + ": ----";
                if (recs[0].AltMappings >= maxNumMappings)
                {
                    AddToRedundant(recs[0]);
                    descr = "---- Wrong hit to " + actualHitGene + ": Redundant: Many mappings to genome (" + recs[0].AltMappings + "): ----";
                }
            }
            if (descr != "")
            {
                readReporter.WriteLine(descr);
                foreach (ReadMapping rec in recs.TakeWhile(r => r.Position != -1))
                {
                    readReporter.WriteLine(rec.ToString());
                }
            }
        }

        private void AddToRedundant(ReadMapping rec)
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
