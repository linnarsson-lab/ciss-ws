using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    public class SenseAntisense
    {
        public string Type { get; set; }
        public int TotLen { get; set; }
        public int SenseHits { get; set; }
        public int AntisenseHits { get; set; }
        public double SASRatio { get { return (double)SenseHits / (double)AntisenseHits; } }
        public double SPM { get { return (TotLen == 0)? 0 : (double)SenseHits / (double)TotLen * 1.0E+6; } }
        public double ASPM { get { return (TotLen == 0) ? 0 : (double)AntisenseHits / (double)TotLen * 1.0E+6; } }

        public SenseAntisense(string type, string totLen, string sHits, string asHits)
        {
            Type = type;
            try
            {
                TotLen = int.Parse(totLen);
                SenseHits = int.Parse(sHits);
                AntisenseHits = int.Parse(asHits);
            }
            catch {}
        }
    }

    public class SilverBulletSummary
    {
        private int expressionFileFirstBcCol = 7;

        public string annotationFolder;
        public string projectName;
        public Dictionary<string, int> groupedHitCounts = new Dictionary<string, int>();
        public int spliceHits;
        public int trHits = -1;
        public List<SenseAntisense> senseAntisense = new List<SenseAntisense>();
        public List<int> sampledReads = new List<int>();
        public List<int> sampledUniqueHits = new List<int>();
        public List<int[]> sampledBarcodeTranscripts = new List<int[]>();
        public Dictionary<int, double[]> sampledMedianCVs = new Dictionary<int, double[]>();
        public Dictionary<string,string[,]> bcodeData = new Dictionary<string,string[,]>();
        public double[] geneCorrelationHisto;
        public double[] geneCVHisto;
        public double minCV, maxCV;
        public List<string> spikeNames = new List<string>();
        public List<double> spikeRPMMeans = new List<double>();
        public List<double> spikeRPMSDs = new List<double>();
        public List<double[]> spikeHitProfiles = new List<double[]>();
        public List<double[]> geneHitProfiles = new List<double[]>();
        public int TotalReads { get { return groupedHitCounts["total"]; } }
        public int MappedReads { get { return groupedHitCounts["aligned"]; } }
        public int AnnotatedHits { get { return groupedHitCounts["annotated"]; } }
        public int ExonHits { get { return groupedHitCounts["exon"]; } }
        public int NonAnnotatedHits { get { return groupedHitCounts["non-annotated"]; } }

        public void Load(string annotFolder)
        {
            string[] bcFiles = Directory.GetFiles(annotFolder, "*_barcode_summary.*");
            if (bcFiles.Length == 0)
                bcFiles = Directory.GetFiles(annotFolder, "*_bcodes.txt");
            string[] files = Directory.GetFiles(annotFolder, "*_summary.tab");
            if (files.Length == 0)
                files = Directory.GetFiles(annotFolder, "*_sense_antisense.tab");
            Array.Sort(files, (x, y) => (x.Length - y.Length));
            string sFile = files[0];
            AddSummaryFile(sFile);
            projectName = Path.GetFileName(sFile).Split('_')[0];
            annotationFolder = annotFolder;
            AddBarcodeDataFile(bcFiles[0]);
            files = Directory.GetFiles(annotFolder, "*_cap_hits.tab");
            if (files.Length == 1)
                AddCapHits(files[0]);
            files = Directory.GetFiles(annotFolder, "*_sampled_CV.tab");
            if (files.Length == 1)
                AddSampledCVs(files[0]);
            files = Directory.GetFiles(annotFolder, "*_expression.tab");
            if (files.Length == 1)
                AddGeneCV(files[0]);
        }

        private void AddSummaryFile(string summaryFile)
        {
            StreamReader reader = new StreamReader(summaryFile);
            string repCount = "";
            string line = reader.ReadLine();
            while (line != null)
            {
                if (line.StartsWith("Type"))
                {
                    line = reader.ReadLine();
                    while (line.Length > 0)
                    {
                        string[] f = (line.Trim() + "\t\t\t").Split('\t');
                        SenseAntisense sas = new SenseAntisense(f[0], f[1], f[2], f[3]);
                        senseAntisense.Add(sas);
                        if (f[0] == "SPLC") spliceHits = int.Parse(f[2]);
                        line = reader.ReadLine();
                    }
                }
                else if (line.StartsWith("Accu"))
                {
                    line = reader.ReadLine();
                    while (line != null && line.Length > 0)
                    {
                        string[] f = line.Split('\t');
                        int readCount = int.Parse(f[0]);
                        int unique = int.Parse(f[1]);
                        sampledReads.Add(readCount);
                        sampledUniqueHits.Add(unique);
                        line = reader.ReadLine();
                    }
                }
                else if (line.StartsWith("Expressed transcripts in each barcode"))
                {
                    line = reader.ReadLine(); // Header
                    line = reader.ReadLine();
                    while (line != null && line.Length > 0)
                    {
                        string[] f = line.Split('\t');
                        int readCount = int.Parse(f[0]);
                        int[] counts = new int[f.Length - 1];
                        for (int i = 1; i < f.Length; i++)
                            counts[i - 1] = int.Parse(f[i]);
                        sampledBarcodeTranscripts.Add(counts);
                        line = reader.ReadLine();
                    }
                }
                else if (line.StartsWith("Total number of mapped")
                         || line.StartsWith("Number of reads mapped to a feature")
                         || line.StartsWith("Number of reads mapped to features")
                         || line.StartsWith("Number of reads mapped to some annotated position"))
                    groupedHitCounts["annotated"] = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Number of mapped reads without") ||
                         line.StartsWith("Number of reads mapped to a position without") ||
                         line.StartsWith("Number of reads mapped to positions without") ||
                         line.StartsWith("Number of reads mapped only to position(s) without annotation"))
                    groupedHitCounts["non-annotated"] = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Number of reads mapped to an exon") ||
                         line.StartsWith("Number of reads mapped to exons") ||
                         line.StartsWith("Number of reads mapped to a transcript"))
                    groupedHitCounts["exon"] = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Total number of reads mapped to repeats"))
                     repCount = ParseIntAtEndBeforeAnyPercent(line).ToString();
                else if (line.StartsWith("Number of expressed main gene variants"))
                    trHits = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Total read"))
                    groupedHitCounts["total"] = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Accepted reads"))
                    groupedHitCounts["accepted"] = ParseIntAtEndBeforeAnyPercent(line);
                else if (line.StartsWith("Number of reads mapped to genome"))
                    groupedHitCounts["aligned"] = ParseIntAtEndBeforeAnyPercent(line);
                line = reader.ReadLine();
            }
            if (repCount != "") senseAntisense.Add(new SenseAntisense("REPT", "1", repCount, repCount));
            reader.Close();
        }

        private static int ParseIntAtEndBeforeAnyPercent(string line)
        {
            int pos = line.IndexOfAny("0123456789".ToCharArray());
            int endPos = pos;
            while (endPos < line.Length && "0123456789".Contains(line[endPos])) endPos++;
            try
            {
                return int.Parse(line.Substring(pos, endPos - pos));
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private void AddGeneCV(string expressionFile)
        {
            int nPairs = 1000;
            int nBins = 40;
            List<double[]> geneBcodeCounts = new List<double[]>();
            List<int> geneTotalCounts = new List<int>();
            List<int> spikeIndices = new List<int>();
            StreamReader reader = new StreamReader(expressionFile);
            int junk;
            string line = reader.ReadLine();
            while (line.Trim().Split('\t').Length <= expressionFileFirstBcCol ||
                   !int.TryParse(line.Trim().Split('\t')[expressionFileFirstBcCol], out junk))
                line = reader.ReadLine();
            string[] headerFields = line.Trim().Split('\t');
            int bcCount = headerFields.Length - expressionFileFirstBcCol;
            int[] colTotals = new int[bcCount];
            while (line != null)
            {
                string[] fields = line.Trim().Split('\t');
                double[] rowCounts = new double[bcCount];
                int rowTotal = 0;
                for (int bcIdx = 0; bcIdx < bcCount; bcIdx++)
                {
                    int c = int.Parse(fields[bcIdx + expressionFileFirstBcCol]);
                    rowCounts[bcIdx] = c;
                    rowTotal += c;
                    colTotals[bcIdx] += c;
                }
                if (!fields[0].Contains(GeneFeature.variantIndicator))
                {
                    if (fields[0].StartsWith("RNA_SPIKE"))
                    {
                        spikeIndices.Add(geneTotalCounts.Count);
                        spikeNames.Add(fields[0]);
                    }
                    geneTotalCounts.Add(rowTotal);
                    geneBcodeCounts.Add(rowCounts);
                }
                line = reader.ReadLine();
            }
            reader.Close();
            foreach (int spikeIdx in spikeIndices)
            {
                List<double> bcodeRPMs = new List<double>();
                double[] counts = geneBcodeCounts[spikeIdx];
                for (int bcodeIdx = 0; bcodeIdx < counts.Length; bcodeIdx++)
                    if (colTotals[bcodeIdx] > 0)
                        bcodeRPMs.Add( counts[bcodeIdx] * 1.0E+6 / (double)colTotals[bcodeIdx] );
                spikeRPMMeans.Add(DescriptiveStatistics.Mean(bcodeRPMs.ToArray()));
                spikeRPMSDs.Add(DescriptiveStatistics.Stdev(bcodeRPMs.ToArray()));
            }
            List<int> indices = new List<int>();
            for (int geneIdx = 0; geneIdx < geneTotalCounts.Count; geneIdx++) indices.Add(geneIdx);
            Linnarsson.Mathematics.Sort.HeapSort(geneTotalCounts, indices);
            indices.Reverse(); // Get indices of genes in order of most->least expressed
            int nGenes = Math.Min(indices.Count - 1, nPairs);
            double[] CVs = new double[nGenes];
            int minValidWellCount = groupedHitCounts["total"] / bcCount / 20;
            for (int geneIdx = 0; geneIdx < nGenes; geneIdx++)
            {
                double[] bcodeCounts = geneBcodeCounts[indices[geneIdx]];
                List<double> normedBcodeValues = new List<double>();
                for (int bcodeIdx = 0; bcodeIdx < bcodeCounts.Length; bcodeIdx++)
                    if (colTotals[bcodeIdx] > minValidWellCount)
                        normedBcodeValues.Add(bcodeCounts[bcodeIdx] / (double)colTotals[bcodeIdx]); // Normalized value
                CVs[geneIdx] = DescriptiveStatistics.CV(normedBcodeValues.ToArray());
            }
            minCV = CVs.Min();
            maxCV = CVs.Max() + 0.0001;
            if (!double.IsNaN(minCV) && !double.IsNaN(maxCV))
            {
                geneCVHisto = new double[nBins];
                foreach (double cv in CVs)
                    geneCVHisto[(int)(nBins * (cv - minCV) / (maxCV - minCV))]++;
            }
        }

        private void AddGeneCorrelation(string expressionFile)
        {
            int nPairs = 1000;
            List<double[]> geneCounts = new List<double[]>();
            List<int> totalCounts = new List<int>();
            StreamReader reader = new StreamReader(expressionFile);
            string line = reader.ReadLine();
            line = reader.ReadLine();
            line = reader.ReadLine(); //Skip headers
            string[] headerFields = line.Trim().Split('\t');
            int bcCount = headerFields.Length - expressionFileFirstBcCol;
            while (line != null)
            {
                string[] fields = line.Trim().Split('\t');
                if (!fields[0].Contains('_'))
                {
                    double[] counts = new double[bcCount];
                    int total = 0;
                    int bcCol0 = fields.Length - bcCount;
                    for (int bcIdx = 0; bcIdx < bcCount; bcIdx++)
                    {
                        int c = int.Parse(fields[bcIdx + expressionFileFirstBcCol]);
                        counts[bcIdx] = c;
                        total += c;
                    }
                    totalCounts.Add(total);
                    geneCounts.Add(counts);
                }
                line = reader.ReadLine();
            }
            reader.Close();
            List<int> indices = new List<int>();
            for (int i = 0; i < totalCounts.Count; i++) indices.Add(i);
            Linnarsson.Mathematics.Sort.HeapSort(totalCounts, indices);
            indices.Reverse();
            geneCorrelationHisto = new double[40];
            for (int i = 0; i < Math.Min(indices.Count - 1, nPairs); i++)
            {
                double[] v1 = geneCounts[indices[i]];
                double[] v2 = geneCounts[indices[i + 1]];
                double cor = Correlation.pearsoncorrelation(ref v1, ref v2, 96);
                int bin = (int)Math.Min(39, Math.Max(0.0, cor * 40.0));
                geneCorrelationHisto[bin] += 1.0;
            }
        }

        private void AddBarcodeDataFile(string barcodeDataFile)
        {
            StreamReader reader = new StreamReader(barcodeDataFile);
            string line = reader.ReadLine(); // Skip Total mapped reads line
            line = reader.ReadLine(); // Skip blank line
            line = reader.ReadLine();
            string[,] bcodes = null;
            if (line.Split('\t').Length > 11)
                bcodes = ReadBcodeDataMatrix(reader, line); // Handle old style file
            string[,] speciesByWell = null;
            string[,] totalHits = new string[8, 12];
            string[,] exonHits = new string[8,12];
            string[,] intronHits = new string[8,12];
            string[,] transcriptCounts = null;
            while (line != null)
            {
                if (line.StartsWith("Barcode by Well"))
                    bcodes = ReadBcodeDataMatrix(reader, "");
                if (line.StartsWith("Total mapped reads by barcode") ||
                    line.StartsWith("Total annotated reads by barcode"))
                    totalHits = ReadBcodeDataMatrix(reader, "");
                else if (line.StartsWith("Total mapped to EXON"))
                    exonHits = ReadBcodeDataMatrix(reader, "");
                else if (line.StartsWith("Total mapped to INTR"))
                    intronHits = ReadBcodeDataMatrix(reader, "");
                else if (line.StartsWith("Transcripts detected in each barcode"))
                    transcriptCounts = ReadBcodeDataMatrix(reader, "");
                else if (line.StartsWith("Species by well"))
                    speciesByWell = ReadBcodeDataMatrix(reader, "");
                line = reader.ReadLine();
            }
            reader.Close();
            bcodeData["Barcodes"] = bcodes;
            if (speciesByWell != null)
                bcodeData["Species"] = speciesByWell;
            bcodeData["Total annotated reads"] = totalHits;
            bcodeData["Reads mapped to exons"] = exonHits;
            bcodeData["Reads mapped to introns"] = intronHits;
            if (transcriptCounts != null)
                bcodeData["Transcripts detected"] = transcriptCounts;
        }

        private string[,] ReadBcodeDataMatrix(StreamReader reader, string lastLine)
        {
            string[,] result = new string[8, 12];
            string line = lastLine.Trim();
            while (line == "") line = reader.ReadLine().Trim();
            for (int row = 0; row < 8; row++)
            {
                string[] fields = line.Split('\t');
                for (int col = 0; col < 12; col++)
                    result[row, col] = fields[col];
                line = reader.ReadLine().Trim();
            }
            return result;
        }

        private void AddSampledCVs(string sampledCVFile)
        {
            int[] exprLevels = new int[] { 20, 100, 1000, 10000 };
            Dictionary<int, Dictionary<int, List<double>>> groupedCVs = new Dictionary<int, Dictionary<int, List<double>>>();
            StreamReader reader = new StreamReader(sampledCVFile);
            int nSamples = 0;
            string line = reader.ReadLine();
            while (!line.Contains('%'))
                line = reader.ReadLine();
            while (line != null)
            {
                string[] fields = line.Trim().Split('\t');
                try
                {
                    if (nSamples == 0) // Set up data structure using first data line
                    {
                        nSamples = fields.Length - 2;
                        foreach (int level in exprLevels)
                        {
                            groupedCVs[level] = new Dictionary<int,List<double>>();
                            for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                                groupedCVs[level][sampleIdx] = new List<double>();
                        }
                    }
                    int finalCount = int.Parse(fields[1]);
                    foreach (int level in exprLevels)
                    {
                        if (Math.Abs((level - finalCount)) < 0.2 * (double)level)
                        {
                            for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                            {
                                string v = fields[sampleIdx + 2];
                                double cv = double.Parse(v.Substring(0, v.Length - 1)); // Remove "%" before parsing
                                groupedCVs[level][sampleIdx].Add(cv);
                            }
                            break;
                        }
                    }
                }
                catch (Exception)
                {}
                line = reader.ReadLine();
            }
            reader.Close();
            foreach (int level in groupedCVs.Keys)
            {
                if (groupedCVs[level].ContainsKey(0) && groupedCVs[level][0].Count < 5) continue;
                double[] medianCVs = new double[nSamples];
                for (int sampleIdx = 0; sampleIdx < nSamples; sampleIdx++)
                    medianCVs[sampleIdx] = DescriptiveStatistics.Median(groupedCVs[level][sampleIdx]);
                if (!medianCVs.All(cv => cv < 0.000001))
                    sampledMedianCVs[level] = medianCVs;
            }
        }

        private void AddCapHits(string capHitsFile)
        {
            StreamReader reader = new StreamReader(capHitsFile);
            string line = reader.ReadLine();
            bool ok = false;
            if (line.StartsWith("Average hit distribution across transcripts"))
            {  // Complicated handling of old output file format.
                ok = true;
                while (!line.StartsWith("750"))
                    line = reader.ReadLine();
            }
            else if (line.StartsWith("Hit distribution across spike transcripts"))
            {
                ok = true;
                while (!line.StartsWith("RNA_SPIKE"))
                    line = reader.ReadLine();
            }
            else if (line.StartsWith("Hit distribution across gene transcripts"))
            { // Sometimes no spike transcripts are included
                ok = true;
            }
            if (ok)
            {
                while (!line.StartsWith("MidLength") && line.Split('\t').Length > 2)
                {
                    AddCapHitLine(line, ref spikeHitProfiles);
                    line = reader.ReadLine();
                }
                while (!line.StartsWith("MidLength"))
                    line = reader.ReadLine();
                line = reader.ReadLine();
                while (line.Split('\t').Length > 2)
                {
                    AddCapHitLine(line, ref geneHitProfiles);
                    line = reader.ReadLine();
                }
            }
            reader.Close();
        }

        private void AddCapHitLine(string line, ref List<double[]> profiles)
        {
            string[] fields = line.Trim().Split('\t');
            double[] samples = new double[fields.Length];
            int offset = 1;
            if (fields[0].Contains("SPIKE"))
            {
                offset = 2;
                samples[0] = int.Parse(fields[1]);
            }
            else
                samples[0] = int.Parse(fields[0]);
            double sum = 0.0;
            int p = 1;
            int nGenes = 0;
            if (int.TryParse(fields[1], out nGenes))
                offset = 2;
            for (int i = offset; i < fields.Length; i++)
            {
                samples[p] = (fields[i] == "NaN") ? 0.0 : double.Parse(fields[i]);
                sum += samples[p++];
            }
            if (sum > 0.0)
                profiles.Add(samples);
        }

    }
}
