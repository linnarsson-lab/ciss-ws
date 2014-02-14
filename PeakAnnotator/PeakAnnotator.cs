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
    class TSSData
    {
        public string chr;
        public char trStrand;
        public int start, end;
        public int Length { get { return end - start; } }

        public override string ToString()
        {
            return chr + "\t" + trStrand + "\t" + start + "\t" + end + "\t" + Length;
        }

        public TSSData(string chr, char trStrand, int start, int end)
        {
            this.chr = chr;
            this.trStrand = trStrand;
            this.start = start;
            this.end = end;
        }
    }

    class RepeatData
    {
        public int Length = 0;

        public override string ToString()
        {
            return "\t\t\t\t" + Length;
        }

        public void AddLength(int len)
        {
            Length += len;
        }
    }

    class PeakAnnotator
    {
        private PeakAnnotatorSettings settings;

        private Dictionary<string, IntervalMap<int>> TSSFwIntervals;
        private Dictionary<string, IntervalMap<int>> TSSRevIntervals;
        private Dictionary<string, int> TSSNameToTSSIdx = new Dictionary<string, int>();
        private List<TSSData> tssDatas = new List<TSSData>();
        private Dictionary<string, int[]> TSSExpressionPerFile;

        private Dictionary<string, IntervalMap<int>> RepeatIntervals;
        private Dictionary<string, int> RepeatNameToRepeatIdx = new Dictionary<string, int>();
        private List<RepeatData> repeatDatas = new List<RepeatData>();
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
                        int start = int.Parse(m.Groups[2].Value) - 1; // Allow margin of one to handle 0/1 offset question
                        int end = int.Parse(m.Groups[3].Value) + 1;
                        char trStrand = m.Groups[4].Value[0];
                        start -= (trStrand == '+') ? settings.ext5Prime : settings.ext3Prime;
                        end += (trStrand == '+') ? settings.ext3Prime : settings.ext5Prime;
                        string name = m.Groups[5].Value;
                        name = MakeTSSNameSortableOnGeneName(name);
                        name = MakeTSSNameUnique(name);
                        Dictionary<string, IntervalMap<int>> ivls = (trStrand == '+') ? TSSFwIntervals : TSSRevIntervals;
                        if (!ivls.ContainsKey(chr))
                            ivls[chr] = new IntervalMap<int>(30000);
                        int idx = TSSNameToTSSIdx.Count + 1;
                        TSSNameToTSSIdx[name] = idx;
                        tssDatas.Add(new TSSData(chr, trStrand, start, end));
                        ivls[chr].Add(start, end, idx);
                        n++;
                    }
                }
            }
            return n;
        }

        private string MakeTSSNameUnique(string name)
        {
            if (TSSNameToTSSIdx.ContainsKey(name))
            {
                name += "-AltTSS";
                int altTSS = 1;
                while (TSSNameToTSSIdx.ContainsKey(name + (++altTSS))) ;
                name = name + altTSS;
            }
            return name;
        }

        private static string MakeTSSNameSortableOnGeneName(string name)
        {
            if (name.StartsWith("p@chr"))
                name = name.Substring(2);
            else
            {
                List<string> parts = new List<string>();
                foreach (string part in name.Split(','))
                {
                    if (!part.Contains('@'))
                        parts.Add(part);
                    else
                    {
                        int p = part.IndexOf('@');
                        parts.Add(part.Substring(p + 1) + ":" + part.Substring(0, p));
                    }
                }
                name = string.Join(",", parts.ToArray());
            }
            return name;
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
                    if (!RepeatNameToRepeatIdx.TryGetValue(name, out idx))
                    {
                        idx = RepeatNameToRepeatIdx.Count + 1;
                        RepeatNameToRepeatIdx[name] = idx;
                        repeatDatas.Add(new RepeatData());
                    }
                    RepeatIntervals[chr].Add(start, end, idx);
                    repeatDatas[idx - 1].AddLength(end - start);
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
                writer.WriteLine("TSS/Repeat\tChr\tStrand\tStart\tEnd\tPromoterLen\t" + string.Join("\t", infileNames));
                foreach (string name in TSSNameToTSSIdx.Keys)
                {
                    writer.Write(name + "\t");
                    int valueIdx = TSSNameToTSSIdx[name];
                    writer.Write(tssDatas[valueIdx - 1].ToString());
                    foreach (string infileName in infileNames)
                        writer.Write("\t" + TSSExpressionPerFile[infileName][valueIdx]);
                    writer.WriteLine();
                }
                foreach (string name in RepeatNameToRepeatIdx.Keys)
                {
                    writer.Write(name + "\t");
                    int repTypeIdx = RepeatNameToRepeatIdx[name];
                    writer.Write(repeatDatas[repTypeIdx - 1].ToString());
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
                    Console.Write("Processing {0}...", infile);
                    int totCount = Process(infile);
                    Console.WriteLine("{0} counts.", totCount);
                }
            }
        }

        public int Process(string infile)
        {
            int totCount = 0;
            int[] geneExpression = new int[TSSNameToTSSIdx.Count + 1];
            int[] repeatExpression = new int[RepeatNameToRepeatIdx.Count + 1];
            using (StreamReader reader = infile.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#"))
                        continue;
                    string[] fields = line.Split('\t');
                    string chr = fields[0];
                    bool fw = (fields[1][0] == '+');
                    int posOf5Prime = int.Parse(fields[2]);
                    int count = int.Parse(fields[3]);
                    totCount += count;
                    bool anyTSSHit = false;
                    try
                    {
                        Dictionary<string, IntervalMap<int>> ivls = fw? TSSFwIntervals : TSSRevIntervals;
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
            return totCount;
        }
    }
}
