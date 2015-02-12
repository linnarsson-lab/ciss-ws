﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Dna;

namespace Map2Pclu
{
    class UMIProfileFactory
    {
        Map2PcluSettings settings;

        public UMIProfileFactory(Map2PcluSettings settings)
        {
            this.settings = settings;
        }
        public IUMIProfile GetCounter()
        {
            if (settings.analyzeBcLeakage || settings.AnalyzeReadsPerMol)
                return new UMIReadCountProfile(settings.nUMIs);
            else
                return new UMIZeroOneMoreProfile(settings.nUMIs);
        }
    }

    class Map2Pclu
    {
        private Dictionary<string, Dictionary<int, IUMIProfile>> counters;
        private Map2PcluSettings settings;
        private UMIProfileFactory counterFactory;

        private int nTotReads = 0, nTotMols = 0;
        private int nTooMultiMappingReads, nMappedPositions;

        private int nMaxMappings;
        private Random rnd;

        public Map2Pclu(Map2PcluSettings settings)
        {
            this.settings = settings;
            nMaxMappings = settings.maxMultiReadMappings;
            rnd = new Random(System.DateTime.Now.Millisecond);
            counterFactory = new UMIProfileFactory(settings);
        }

        public void Convert()
        {
            string bcPrefix = settings.iterateBarcodes ? "*_" : "";
            string fType = (settings.countType == UMICountType.Reads) ? "reads" : (settings.countType == UMICountType.AllMolecules) ? "mols" : "nonSingletonMols";
            string outfilePat = settings.outputFolderOrFilename;
            if (!outfilePat.EndsWith(".gz"))
            {
                if (!Directory.Exists(settings.outputFolderOrFilename))
                    Directory.CreateDirectory(settings.outputFolderOrFilename);
                outfilePat = Path.Combine(settings.outputFolderOrFilename, settings.filenamePrefix + bcPrefix + fType + ".pclu.gz");
            }
            int maxBcIdx = settings.iterateBarcodes ? settings.maxBarcodeIdx : 0;
            for (int bcIdx = 0; bcIdx <= maxBcIdx; bcIdx++)
            {
                counters = new Dictionary<string, Dictionary<int, IUMIProfile>>();
                int nReads = 0, nHits = 0;
                nTooMultiMappingReads = nMappedPositions = 0;
                foreach (string mapFile in settings.inputFiles)
                {
                    string file = mapFile;
                    if (settings.iterateBarcodes)
                    {
                        string dir = Path.GetDirectoryName(mapFile);
                        file = settings.ReplaceBarcode(Path.GetFileName(mapFile), bcIdx);
                        file = Path.Combine(dir, file);
                        if (file == null || !File.Exists(file)) continue;
                    }
                    Console.WriteLine("{0}...", file);
                    nReads += ReadMapFile(file);
                }
                if (counters.Count == 0)
                    continue;
                string outfilePath = outfilePat.Replace("*", bcIdx.ToString());
                nHits = WriteOutput(outfilePath, settings.countType);
                Console.WriteLine("...{0} hits from {1} reads at {2} mapped positions. {3} multireads skipped.",
                                  nHits, nReads, nMappedPositions, nTooMultiMappingReads);
                nTotMols += nHits;
                nTotReads += nReads;
            }
            string totMolTxt = settings.HasUMIs ? string.Format(" and {0} molecules", nTotMols) : "";
            if (settings.iterateBarcodes)
                Console.WriteLine("Totally in {0} barcodes were {1} reads{2} processed.", maxBcIdx + 1, nTotReads, totMolTxt);
            Console.WriteLine("...output is found in " + outfilePat);
        }

        private int ReadMapFile(string mapFile)
        {
            int nReads = 0;
            NoBarcodes bcs = settings.HasUMIs ? new NoBarcodes() : new NoUMIsNoBarcodes();
            foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(mapFile))
            {
                nReads++;
                if (mrm.NMappings > nMaxMappings)
                {
                    nTooMultiMappingReads++;
                    continue;
                }
                int selectedMapping = rnd.Next(mrm.NMappings);
                MultiReadMapping m = mrm[selectedMapping];
                string chr = settings.AllAsPlusStrand ? m.Chr : m.Chr + m.Strand;
                int posOf5Prime = (settings.AllAsPlusStrand || m.Strand == '+') ? m.Position : m.Position + mrm.SeqLen - 1;
                Dictionary<int, IUMIProfile> chrCounters;
                try
                {
                    chrCounters = counters[chr];
                }
                catch (KeyNotFoundException)
                {
                    chrCounters = new Dictionary<int, IUMIProfile>();
                    counters[chr] = chrCounters;
                }
                IUMIProfile counter;
                if (!counters[chr].TryGetValue(posOf5Prime, out counter))
                {
                    counter = counterFactory.GetCounter();
                    counters[chr][posOf5Prime] = counter;
                    nMappedPositions++;
                }
                counter.Add(mrm.UMIIdx);
            }
            return nReads;
        }

        int WriteOutput(string outfilePath, UMICountType ct)
        {
            int[] readsPerMolDistro = new int[10000];
            int nTotal = 0;
            using (StreamWriter writer = outfilePath.OpenWrite())
            {
                writer.WriteLine("#Chr\tStrand\tPosOf5Prime\tCount");
                string[] chrStrands = counters.Keys.ToArray();
                Array.Sort(chrStrands);
                foreach (string chrStrand in chrStrands)
                {
                    string chr = chrStrand;
                    char strand = '+';
                    if (!settings.AllAsPlusStrand)
                    {
                        strand = chr[chr.Length - 1];
                        chr = chr.Substring(0, chr.Length - 1);
                    }
                    Dictionary<int, IUMIProfile> chrCounters = counters[chrStrand];
                    int[] positions = chrCounters.Keys.ToArray();
                    Array.Sort(positions);
                    foreach (int pos in positions)
                    {
                        int n = chrCounters[pos].count(ct);
                        if (settings.IsCountingMols && settings.estimateTrueMolCounts) n = EstimateTrueCount(n);
                        if (n == 0) continue;
                        nTotal += n;
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", chr, strand, pos, n);
                        if (settings.AnalyzeReadsPerMol)
                        {
                            foreach (int nReads in ((UMIReadCountProfile)chrCounters[pos]).IterReadsPerMol())
                                readsPerMolDistro[nReads]++;
                        }
                    }
                }
            }
            if (settings.AnalyzeReadsPerMol)
                WriteReadsPerMolDistroLine(outfilePath, readsPerMolDistro);
            return nTotal;
        }

        private void WriteReadsPerMolDistroLine(string outfilePath, int[] readsPerMolDistro)
        {
            using (StreamWriter distroWriter = new StreamWriter(settings.readsPerMolFile, true))
            {
                distroWriter.Write(outfilePath);
                foreach (int nCases in readsPerMolDistro)
                    distroWriter.Write("\t" + nCases);
                distroWriter.WriteLine();
                distroWriter.Flush();
            }
        }

        public int EstimateTrueCount(int nMols)
        {
            if (nMols > settings.nUMIs)
                return nMols;
            return (int)Math.Round(Math.Log(1.0 - (double)nMols / settings.nUMIs) / Math.Log(1.0 - 1.0 / settings.nUMIs));
        }

    }
}
