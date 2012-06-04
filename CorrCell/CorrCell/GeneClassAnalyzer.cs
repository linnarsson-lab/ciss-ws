using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;

namespace CorrCell
{
    public class GeneClassAnalyzer
    {
        private Expression expr;
        private GeneCorrelator gc;
        private Random rnd;
        private int nHistoBins = 40;

        public GeneClassAnalyzer(Expression expr, GeneCorrelator gc)
        {
            this.expr = expr;
            this.gc = gc;
            rnd = new Random(DateTime.Now.Millisecond);
        }

        public void AnalyzePairedGenes(string pairFile)
        {
            List<Pair<int, int>> geneIdxPairs = ReadGenePairs(pairFile);
            List<int[]> histograms = new List<int[]>();
            List<string> titles = new List<string>();
            histograms.Add(CalcPairHistogram(nHistoBins, geneIdxPairs));
            titles.Add("PairCount");
            histograms.Add(CalcBkgHistogram(nHistoBins, geneIdxPairs, geneIdxPairs.Count));
            titles.Add("NonPairCount");
            string outFile = pairFile + ".correlation";
            WriteHistograms(outFile, histograms, titles);
        }

        public void AnalyzeGeneClasses(string classFile)
        {
            Dictionary<string, List<int>> geneIdxClasses = ReadGeneClasses(classFile);
            List<int[]> histograms = new List<int[]>();
            List<string> titles = new List<string>();
            List<Pair<int, int>> allGenePairs = new List<Pair<int, int>>();
            foreach (string className in geneIdxClasses.Keys)
            {
                Console.Write("Analyzing " + className + " - " + geneIdxClasses[className].Count + " genes...");
                titles.Add(className + ".Pairs.Count");
                List<Pair<int, int>> classGenePairs = MakeGenePairs(geneIdxClasses[className]);
                Console.WriteLine(classGenePairs.Count + " gene pairs...");
                allGenePairs.AddRange(classGenePairs);
                histograms.Add(CalcPairHistogram(nHistoBins, classGenePairs));
            }
            Console.WriteLine("Analyzing background...");
            histograms.Add(CalcBkgHistogram(nHistoBins, allGenePairs, allGenePairs.Count));
            titles.Add("NonPaired.Count");
            string outFile = classFile + ".correlation";
            WriteHistograms(outFile, histograms, titles);
        }

        private List<Pair<int, int>> MakeGenePairs(List<int> classGenes)
        {
            List<Pair<int, int>> classGenePairs = new List<Pair<int, int>>();
            for (int i = 0; i < classGenes.Count - 1; i++)
            {
                for (int j = i + 1; j < classGenes.Count; j++)
                {
                    if (classGenes[i] > classGenes[j])
                        classGenePairs.Add(new Pair<int, int>(classGenes[j], classGenes[i]));
                    else
                        classGenePairs.Add(new Pair<int, int>(classGenes[i], classGenes[j]));
                }
            }
            return classGenePairs;
        }

        private Dictionary<string, List<int>> ReadGeneClasses(string classFile)
        {
            Dictionary<string, List<int>> geneIdxClasses = new Dictionary<string, List<int>>();
            using (StreamReader reader = new StreamReader(classFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Trim().Split('\t');
                    int geneIdx = expr.GetGeneIdx(fields[0].Trim());
                    string className = fields[1].Trim();
                    if (geneIdx >= 0)
                    {
                        if (!geneIdxClasses.ContainsKey(className))
                            geneIdxClasses[className] = new List<int>();
                        geneIdxClasses[className].Add(geneIdx);
                    }
                }
            }
            return geneIdxClasses;
        }

        private void WriteHistograms(string outFile, List<int[]> histograms, List<string> titles)
        {
            StreamWriter writer = new StreamWriter(outFile);
            writer.Write("BinStart");
            foreach (string title in titles)
                writer.Write("\t" + title);
            writer.WriteLine();
            for (int i = 0; i < histograms[0].Length; i++)
            {
                writer.Write(i / (double)nHistoBins);
                foreach (int[] histo in histograms)
                    writer.Write("\t" + histo[i]);
                writer.WriteLine();
            }
            writer.Close();
        }

        private List<Pair<int, int>> ReadGenePairs(string pairFile)
        {
            List<Pair<int, int>> geneIdxPairs = new List<Pair<int, int>>();
            using (StreamReader reader = new StreamReader(pairFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] fields = line.Trim().Split('\t');
                    int geneIdxA = expr.GetGeneIdx(fields[0]);
                    int geneIdxB = expr.GetGeneIdx(fields[1]);
                    if (geneIdxA >= 0 && geneIdxB >= 0)
                    {
                        if (geneIdxA > geneIdxB)
                            geneIdxPairs.Add(new Pair<int, int>(geneIdxB, geneIdxA));
                        else
                            geneIdxPairs.Add(new Pair<int, int>(geneIdxA, geneIdxB));
                    }
                }
            }
            return geneIdxPairs;
        }

        private int[] CalcBkgHistogram(int nHistoBins, List<Pair<int, int>> candidateCorrGeneIdxPairs, int nNonPairSamples)
        {
            int[] nonPairHisto = new int[nHistoBins];
            for (int i = 0; i < nNonPairSamples; i++)
            {
                int geneIdxA, geneIdxB;
                do
                {
                    geneIdxA = rnd.Next(expr.GeneCount - 1);
                    geneIdxB = rnd.Next(geneIdxA, expr.GeneCount);
                } while (candidateCorrGeneIdxPairs.Any(p => (p.First == geneIdxA && p.Second == geneIdxB)));
                double corr = gc.GetCorrelation(expr, geneIdxA, geneIdxB).corrMean;
                nonPairHisto[(int)Math.Floor(Math.Abs(corr) * nHistoBins)]++;
            }
            return nonPairHisto;
        }

        private int[] CalcPairHistogram(int nHistoBins, List<Pair<int, int>> geneIdxPairs)
        {
            int[] pairHisto = new int[nHistoBins];
            foreach (Pair<int, int> p in geneIdxPairs)
            {
                double corr = gc.GetCorrelation(expr, p.First, p.Second).corrMean;
                pairHisto[(int)Math.Floor(Math.Abs(corr) * nHistoBins)]++;
            }
            return pairHisto;
        }

    }

}
