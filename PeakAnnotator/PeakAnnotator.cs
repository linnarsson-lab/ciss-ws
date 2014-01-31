﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.Utilities;

namespace PeakAnnotator
{
    class PeakAnnotator
    {
        private PeakAnnotatorSettings settings;

        private Dictionary<string, IntervalMap<int>> RepeatIntervals;
        private Dictionary<string, int> RepeatNameToIdx = new Dictionary<string, int>();
        private Dictionary<string, int[]> RepeatExpressionPerFile;

        public PeakAnnotator(PeakAnnotatorSettings settings)
        {
            this.settings = settings;
            SetupRepeats();
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
            string[] infileNames = RepeatExpressionPerFile.Keys.ToArray();
            using (StreamWriter writer = settings.outfile.OpenWrite())
            {
                writer.WriteLine("\t" + string.Join("\t", infileNames));
                foreach (string repeatName in RepeatNameToIdx.Keys)
                {
                    writer.Write(repeatName);
                    int repTypeIdx = RepeatNameToIdx[repeatName];
                    foreach (string infileName in infileNames)
                        writer.Write("\t" + RepeatExpressionPerFile[infileName][repTypeIdx]);
                    writer.WriteLine();
                }
            }
        }

        private void AnalyzePeaks()
        {
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
            int[] repeatExpression = new int[RepeatNameToIdx.Count + 1];
            using (StreamReader reader = infile.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Split('\t');
                    string chr = fields[0];
                    int direction = (fields[1][0] == '+') ? 1 : -1;
                    int pos = int.Parse(fields[2]);
                    int count = int.Parse(fields[3]);
                    try
                    {
                        foreach (SmallInterval<int> repIvl in RepeatIntervals[chr].IterItems(pos))
                            repeatExpression[repIvl.Item] += count;
                    }
                    catch (Exception)
                    { }
                }
            }
            string infileName = Path.GetFileName(infile);
            RepeatExpressionPerFile[infileName] = repeatExpression;
        }
    }
}
