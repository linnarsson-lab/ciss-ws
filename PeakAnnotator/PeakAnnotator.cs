using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace PeakAnnotator
{
    class PeakAnnotator
    {
        private PeakAnnotatorSettings settings;

        private Dictionary<string, IntervalMap<int>> TSSFwIntervals;
        private Dictionary<string, IntervalMap<int>> TSSRevIntervals;
        private Dictionary<string, int> TSSNameToIdx = new Dictionary<string, int>();
        private Dictionary<string, int[]> TSSExpressionPerFile;

        private Dictionary<string, IntervalMap<int>> RepeatIntervals;
        private Dictionary<string, int> RepeatNameToIdx = new Dictionary<string, int>();
        private Dictionary<string, int[]> RepeatExpressionPerFile;

        public PeakAnnotator(PeakAnnotatorSettings settings)
        {
            this.settings = settings;
            SetupTSSPeaks(settings.genome.Build);
            SetupRepeats();
        }

        private void SetupTSSPeaks(string build)
        {
            string TSSPeakFile = string.Format("/data/seq/F5_data/{0}_peaks.tab", build);
            TSSFwIntervals = new Dictionary<string, IntervalMap<int>>();
            TSSRevIntervals = new Dictionary<string, IntervalMap<int>>();
            Console.Write("Reading TSS peaks...");
            int n = LoadTSSPeakFile(TSSPeakFile);
            Console.WriteLine("{0} peaks.", n);
        }

        private int LoadTSSPeakFile(string peakPath)
        {
            string matchPattern = "chr(.+):([0-9]+)\\.\\.([0-9]+),([-+])\t(.+)$";
            int n = 0;
            string line;
            using (StreamReader reader = peakPath.OpenRead())
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#")) continue;
                    Match m = Regex.Match(line, matchPattern);
                    if (m.Success)
                    {
                        string chr = m.Groups[1].Value;
                        int start = int.Parse(m.Groups[2].Value);
                        int end = int.Parse(m.Groups[3].Value);
                        int direction  = (m.Groups[4].Value == "+")? 1: -1;
                        string name = m.Groups[5].Value;
                        if (name.Contains('@'))
                        {
                            int p = name.IndexOf('@');
                            name = name.Substring(p + 1) + ":" + name.Substring(0, p);
                        }
                        Dictionary<string, IntervalMap<int>> ivls = (direction == 1) ? TSSFwIntervals : TSSRevIntervals;
                        if (!ivls.ContainsKey(chr))
                            ivls[chr] = new IntervalMap<int>(30000);
                        int idx;
                        if (!TSSNameToIdx.TryGetValue(name, out idx))
                        {
                            idx = TSSNameToIdx.Count + 1;
                            TSSNameToIdx[name] = idx;
                        }
                        ivls[chr].Add(start, end, idx);
                        n++;
                    }
                }
            }
            return n;
        }

        private void SetupRepeats()
        {
            RepeatIntervals = new Dictionary<string, IntervalMap<int>>();
            string[] rmskFiles = PathHandler.GetRepeatMaskFiles(settings.genome);
            Console.Write("Reading {0} masking files..", rmskFiles.Length);
            int n = 0;
            foreach (string rmskFile in rmskFiles)
                n += LoadRepeatMaskFile(rmskFile);
            Console.WriteLine("{0} repeat regions.", n);
        }

        private int LoadRepeatMaskFile(string rmskPath)
        {
            string[] record;
            int fileTypeOffset = 0;
            if (rmskPath.EndsWith("out"))
                fileTypeOffset = -1;
            int n = 0;
            using (StreamReader reader = rmskPath.OpenRead())
            {
                string line = reader.ReadLine();
                while (line == "" || !char.IsDigit(line.Trim()[0]))
                    line = reader.ReadLine();
                while (line != null)
                {
                    record = line.Split('\t');
                    string chr = record[5 + fileTypeOffset].Substring(3);
                    int start = int.Parse(record[6 + fileTypeOffset]);
                    int end = int.Parse(record[7 + fileTypeOffset]);
                    string name = record[10 + fileTypeOffset];
                    if (!RepeatIntervals.ContainsKey(chr))
                        RepeatIntervals[chr] = new IntervalMap<int>(30000);
                    int idx;
                    if (!RepeatNameToIdx.TryGetValue(name, out idx))
                    {
                        idx = RepeatNameToIdx.Count + 1;
                        RepeatNameToIdx[name] = idx;
                    }
                    RepeatIntervals[chr].Add(start, end, idx);
                    n++;
                    line = reader.ReadLine();
                }
            }
            return n;
        }

        public void Process()
        {
            AnalyzePeaks();
            WriteOutput();
        }

        private void WriteOutput()
        {
            string[] infileNames = TSSExpressionPerFile.Keys.ToArray();
            using (StreamWriter writer = settings.outfile.OpenWrite())
            {
                writer.WriteLine("\t" + string.Join("\t", infileNames));
                foreach (string name in TSSNameToIdx.Keys)
                {
                    writer.Write(name);
                    int valueIdx = TSSNameToIdx[name];
                    foreach (string infileName in infileNames)
                        writer.Write("\t" + TSSExpressionPerFile[infileName][valueIdx]);
                    writer.WriteLine();
                }
                foreach (string name in RepeatNameToIdx.Keys)
                {
                    writer.Write(name);
                    int repTypeIdx = RepeatNameToIdx[name];
                    foreach (string infileName in infileNames)
                        writer.Write("\t" + RepeatExpressionPerFile[infileName][repTypeIdx]);
                    writer.WriteLine();
                }
            }
        }

        private void AnalyzePeaks()
        {
            TSSExpressionPerFile = new Dictionary<string, int[]>();
            RepeatExpressionPerFile = new Dictionary<string, int[]>();
            foreach (string infile in settings.infiles)
            {
                if (!File.Exists(infile))
                    Console.WriteLine("Error: File does not exist: " + infile);
                else
                {
                    Console.WriteLine("Processing {0}...", infile);
                    Process(infile);
                }
            }
        }

        public void Process(string infile)
        {
            int readLen = 38;
            int[] geneExpression = new int[TSSNameToIdx.Count + 1];
            int[] repeatExpression = new int[RepeatNameToIdx.Count + 1];
            using (StreamReader reader = infile.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#ReadLen=")) readLen = int.Parse(line.Substring(9));
                    string[] fields = line.Split('\t');
                    string chr = fields[0];
                    int direction = (fields[1][0] == '+') ? 1 : -1;
                    int pos = int.Parse(fields[2]);
                    int posOf5Prime = (direction == 1)? pos : pos + readLen - 1;
                    int count = int.Parse(fields[3]);
                    bool anyTSSHit = false;
                    try
                    {
                        Dictionary<string, IntervalMap<int>> ivls = (direction == 1) ? TSSFwIntervals : TSSRevIntervals;
                        foreach (SmallInterval<int> tssIvl in ivls[chr].IterItems(posOf5Prime))
                        {
                            geneExpression[tssIvl.Item] += count;
                            anyTSSHit = true;
                        }
                    }
                    catch (KeyNotFoundException)
                    {}
                    if (!anyTSSHit)
                    {
                        try
                        {
                            foreach (SmallInterval<int> repIvl in RepeatIntervals[chr].IterItems(posOf5Prime))
                                repeatExpression[repIvl.Item] += count;
                        }
                        catch (KeyNotFoundException)
                        { }
                    }
                }
            }
            string infileName = Path.GetFileName(infile);
            TSSExpressionPerFile[infileName] = geneExpression;
            RepeatExpressionPerFile[infileName] = repeatExpression;
        }
    }
}
