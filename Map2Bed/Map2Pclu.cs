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
            if (settings.countType == UMICountType.NonMutatedSingletonMolecules)
            {
                UMIReadCountProfile.SetUMIHammingDistCalculator(new UMIHammingDistCalculator(settings.UMILen), settings.minHammingDist);
                UMIZeroOneMoreProfile.SetUMIHammingDistCalculator(new UMIHammingDistCalculator(settings.UMILen), settings.minHammingDist);
            }
        }
        public IUMIProfile GetCounter()
        {
            if (settings.analyzeBcLeakage || settings.AnalyzeReadsPerMol)
                return new UMIReadCountProfile(settings.nUMIs);
            else
                return new UMIZeroOneMoreProfile(settings.nUMIs);
        }
    }

    class UMIDistroCounter
    {
        private int[] molsByUMI;
        private int[] lonelyMolsByUMI;
        private int[] molPairsByUMIs;
        private Map2PcluSettings settings;

        public UMIDistroCounter(Map2PcluSettings settings)
        {
            this.settings = settings;
            molsByUMI = new int[settings.nUMIs];
            lonelyMolsByUMI = new int[settings.nUMIs];
            int codedPairSpace = 2 << (settings.nUMIBits * 2);
            molPairsByUMIs = new int[codedPairSpace];
        }
        public void Add(IUMIProfile counter)
        {
            int idx1 = -1, idx2 = -1;
            int nMols = counter.nMols();
            foreach (int umiIdx in counter.OccupiedUMIs())
            {
                molsByUMI[umiIdx]++;
                if (nMols == 1) lonelyMolsByUMI[umiIdx]++;
                if (idx1 == -1) idx1 = umiIdx;
                else if (idx2 == -1) { idx2 = idx1; idx1 = umiIdx; }
            }
            if (nMols == 2)
                molPairsByUMIs[(idx2 << settings.nUMIBits) | idx1]++;
        }
        public int HammingDist(int umiIdx1, int umiIdx2)
        {
            int dist = 0;
            for (int i = 0; i < settings.UMILen; i++)
            {
                if ((umiIdx1 & 3) != (umiIdx2 & 3)) dist++;
                umiIdx1 >>= 2;
                umiIdx2 >>= 2;
            }
            return dist;
        }
        public void OutputResults(string filenameBase)
        {
            using (StreamWriter writer = new StreamWriter(filenameBase + "_SingletonPairStats.tab"))
            {
                writer.Write("UMI:");
                for (int i = 0; i < settings.nUMIs; i++)
                    writer.Write("\t" + i.ToString());
                writer.Write("\nNMolecules:");
                foreach (int c in molsByUMI)
                    writer.Write("\t" + c.ToString());
                writer.Write("\nNLonelyMolecules:");
                foreach (int c in lonelyMolsByUMI)
                    writer.Write("\t" + c.ToString());
                double[] pEachUMIFromAll = new double[settings.nUMIs];
                double[] pEachUMIFromLonely = new double[settings.nUMIs];
                for (int i = 0; i < settings.nUMIs; i++)
                {
                    pEachUMIFromAll[i] = molsByUMI[i] / (double)molsByUMI.Sum();
                    pEachUMIFromLonely[i] = lonelyMolsByUMI[i] / (double)lonelyMolsByUMI.Sum();
                }
                int nAllPairs = molPairsByUMIs.Sum();
                int[] nPairsByHammingDist = new int[settings.UMILen + 1];
                double[] nExpectedPairsByHammingDistFromAll = new double[settings.UMILen + 1];
                double[] nExpectedPairsByHammingDistFromLonely = new double[settings.UMILen + 1];
                for (int idx1 = 0; idx1 < settings.nUMIs; idx1++)
                    for (int idx2 = 0; idx2 < idx1; idx2++)
                    {
                        int codedPair = (idx2 << settings.nUMIBits) | idx1;
                        double nExpectedPairsFromAll = 2 * pEachUMIFromAll[idx1] * pEachUMIFromAll[idx2] * nAllPairs;
                        double nExpectedPairsFromLonely = 2 * pEachUMIFromLonely[idx1] * pEachUMIFromLonely[idx2] * nAllPairs;
                        int nActualPairs = molPairsByUMIs[codedPair];
                        int hammingDist = HammingDist(idx1, idx2);
                        nPairsByHammingDist[hammingDist] += nActualPairs;
                        nExpectedPairsByHammingDistFromAll[hammingDist] += nExpectedPairsFromAll;
                        nExpectedPairsByHammingDistFromLonely[hammingDist] += nExpectedPairsFromLonely;
                    }
                writer.WriteLine("\n\nNAllPairs:\t" + nAllPairs);
                writer.Write("\nHammingDist:");
                for (int i = 1; i <= settings.UMILen; i++)
                    writer.Write("\t" + i.ToString());
                writer.Write("\nNActualPairs:");
                for (int i = 1; i <= settings.UMILen; i++)
                    writer.Write("\t" + nPairsByHammingDist[i]);
                writer.Write("\nNExpectedPairsUsingUMIProbsFromAllMols:");
                for (int i = 1; i <= settings.UMILen; i++)
                    writer.Write("\t" + (int)Math.Round(nExpectedPairsByHammingDistFromAll[i]));
                writer.Write("\nNExpectedPairsUsingUMIProbsFromLonelyMols:");
                for (int i = 1; i <= settings.UMILen; i++)
                    writer.Write("\t" + (int)Math.Round(nExpectedPairsByHammingDistFromLonely[i]));
                writer.WriteLine();
            }
        }
    }

    class ReadsPerMolCounter
    {
        private int[] readsPerMolDistro;

        public ReadsPerMolCounter()
        {
            readsPerMolDistro = new int[10000];
        }

        public void Add(int nReads)
        {
            while (nReads > readsPerMolDistro.Length)
            {
                Array.Resize(ref readsPerMolDistro, readsPerMolDistro.Length * 2);
            }
            readsPerMolDistro[nReads]++;
        }

        public string GetHeader()
        {
            return "Barcode\t#Cases-1read/mol\t#Cases-2reads/mol...";
        }

        public string GetReadsPerMolDistroLine(string categoryName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(categoryName);
            foreach (int nCases in readsPerMolDistro)
                sb.Append("\t" + nCases);
            return sb.ToString();
        }
    }

    class Map2Pclu
    {
        private Dictionary<string, Dictionary<int, IUMIProfile>> counters;
        private Map2PcluSettings settings;
        private UMIProfileFactory counterFactory;
        private ReadsPerMolCounter summaryRpmCounter;
        private UMIDistroCounter umiDistroCounter;

        private int nBcReads = 0, nBcSingletons = 0, nBcNewPosSingletons = 0, nBcNon1MutatedSingletons = 0, nBcNon2MutatedSingletons = 0;
        private int nTotReads = 0, nTotMols = 0;
        private int nTooMultiMappingReads, nMappedPositions;

        private static int nSingletonSampleInterval = 10000;
        private List<int> nSingletonsByDepth;
        private Dictionary<int, List<int>> nSingletonsByDepthByBc = new Dictionary<int, List<int>>();
        private List<int> nNewPosSingletonsByDepth;
        private Dictionary<int, List<int>> nNewPosSingletonsByDepthByBc = new Dictionary<int, List<int>>();
        private List<int> nNon1MutatedSingletonsByDepth;
        private Dictionary<int, List<int>> nNon1MutatedSingletonsByDepthByBc = new Dictionary<int, List<int>>();
        private List<int> nNon2MutatedSingletonsByDepth;
        private Dictionary<int, List<int>> nNon2MutatedSingletonsByDepthByBc = new Dictionary<int, List<int>>();

        private int nMaxMappings;
        private Random rnd;

        public Map2Pclu(Map2PcluSettings settings)
        {
            this.settings = settings;
            nMaxMappings = settings.maxMultiReadMappings;
            nSingletonSampleInterval = settings.singletonSampleInterval;
            rnd = new Random(System.DateTime.Now.Millisecond);
            counterFactory = new UMIProfileFactory(settings);
            if (settings.AnalyzeReadsPerMol)
            {
                summaryRpmCounter = new ReadsPerMolCounter();
                using (StreamWriter distroWriter = new StreamWriter(settings.readsPerMolFile))
                {
                    distroWriter.WriteLine(summaryRpmCounter.GetHeader());
                }
            }
            if (settings.AnalyzeSingletons)
                umiDistroCounter = new UMIDistroCounter(settings);
        }

        public void WriteSingeltonsByDepth()
        {
            StreamWriter writer = new StreamWriter(settings.singletonFilenameBase + "_SingletonsByDepth.tab");
            writer.Write("nReads:");
            for (int n = 1; n <= nSingletonsByDepthByBc.Max(p => p.Value.Count); n++)
                writer.Write("\t" + (n * nSingletonSampleInterval) );
            writer.WriteLine("\nBcIdx\tCounts...");
            writer.WriteLine("AllSingletons:");
            foreach (KeyValuePair<int, List<int>> p in nSingletonsByDepthByBc)
                writer.WriteLine(p.Key + "\t" + string.Join("\t", p.Value.ConvertAll(v => v.ToString()).ToArray()));
            writer.WriteLine("NewPosSingletons:");
            foreach (KeyValuePair<int, List<int>> p in nNewPosSingletonsByDepthByBc)
                writer.WriteLine(p.Key + "\t" + string.Join("\t", p.Value.ConvertAll(v => v.ToString()).ToArray()));
            writer.WriteLine("Non1MutatedSingletons:");
            foreach (KeyValuePair<int, List<int>> p in nNon1MutatedSingletonsByDepthByBc)
                writer.WriteLine(p.Key + "\t" + string.Join("\t", p.Value.ConvertAll(v => v.ToString()).ToArray()));
            writer.WriteLine("Non2MutatedSingletons:");
            foreach (KeyValuePair<int, List<int>> p in nNon2MutatedSingletonsByDepthByBc)
                writer.WriteLine(p.Key + "\t" + string.Join("\t", p.Value.ConvertAll(v => v.ToString()).ToArray()));
            writer.WriteLine();
            writer.Close();
        }

        public void Convert()
        {
            string bcPrefix = settings.BarcodePrefix;
            string fType = (settings.countType == UMICountType.Reads) ? "reads"
                : (settings.countType == UMICountType.AllMolecules) ? "mols"
                : (settings.countType == UMICountType.NonSingletonMolecules) ? "nonSingletonMols" : "nonMutatedSingletonMols";
            string outfilePat = settings.outputFolderOrFilename;
            if (!outfilePat.EndsWith(".gz"))
            {
                if (!Directory.Exists(settings.outputFolderOrFilename))
                    Directory.CreateDirectory(settings.outputFolderOrFilename);
                outfilePat = Path.Combine(settings.outputFolderOrFilename, settings.filenamePrefix + bcPrefix + fType + ".pclu.gz");
            }
            int maxBcIdx = settings.MaxBarcodeIdx;
            for (int bcIdx = 0; bcIdx <= maxBcIdx; bcIdx++)
            {
                counters = new Dictionary<string, Dictionary<int, IUMIProfile>>();
                nTooMultiMappingReads = nMappedPositions = 0;
                int nReads = CountBarcode(bcIdx);
                if (counters.Count == 0)
                    continue;
                string bcOutfilePath = outfilePat.Replace("*", bcIdx.ToString());
                int nHits = WriteBarcodeOutput(bcOutfilePath, settings.countType);
                Console.WriteLine("...{0} hits from {1} reads at {2} mapped positions. {3} multireads skipped.",
                                  nHits, nReads, nMappedPositions, nTooMultiMappingReads);
                nTotMols += nHits;
                nTotReads += nReads;
            }
            string totMolTxt = settings.HasUMIs ? string.Format(" and {0} molecules", nTotMols) : "";
            if (settings.AnalyzeReadsPerMol)
                using (StreamWriter distroWriter = new StreamWriter(settings.readsPerMolFile))
                    distroWriter.WriteLine(summaryRpmCounter.GetReadsPerMolDistroLine("Total"));
            if (settings.iterateBarcodes)
                Console.WriteLine("Totally in {0} barcodes were {1} reads{2} processed.", maxBcIdx + 1, nTotReads, totMolTxt);
            if (settings.AnalyzeSingletons)
            {
                umiDistroCounter.OutputResults(settings.singletonFilenameBase);
                WriteSingeltonsByDepth();
            }
            Console.WriteLine("...output is found in " + outfilePat);
        }

        private int CountBarcode(int bcIdx)
        {
            nBcReads = 0;
            nBcSingletons = 0;
            nBcNewPosSingletons = 0;
            nBcNon1MutatedSingletons = 0;
            nBcNon2MutatedSingletons = 0;
            nSingletonsByDepth = new List<int>();
            nNewPosSingletonsByDepth = new List<int>();
            nNon1MutatedSingletonsByDepth = new List<int>();
            nNon2MutatedSingletonsByDepth = new List<int>();
            foreach (string mapFilePath in settings.inputFiles)
            {
                string filePath = mapFilePath;
                if (settings.iterateBarcodes)
                {
                    string dir = Path.GetDirectoryName(mapFilePath);
                    filePath = settings.ReplaceBarcode(Path.GetFileName(mapFilePath), bcIdx);
                    filePath = Path.Combine(dir, filePath);
                    if (filePath == null || !File.Exists(filePath)) continue;
                }
                else if (settings.sortMapFilesByBarcode && !Path.GetFileName(filePath).StartsWith(bcIdx.ToString() + "_"))
                        continue;
                Console.WriteLine("{0}...", filePath);
                ReadMapFile(filePath);
            }
            nSingletonsByDepthByBc.Add(bcIdx, nSingletonsByDepth);
            nNewPosSingletonsByDepthByBc.Add(bcIdx, nNewPosSingletonsByDepth);
            nNon1MutatedSingletonsByDepthByBc.Add(bcIdx, nNon1MutatedSingletonsByDepth);
            nNon2MutatedSingletonsByDepthByBc.Add(bcIdx, nNon2MutatedSingletonsByDepth);
            return nBcReads;
        }

        private void ReadMapFile(string mapFile)
        {
            foreach (MultiReadMappings mrm in new BowtieMapFile(100).MultiMappings(mapFile))
            {
                if (mrm.MappingsIdx > nMaxMappings)
                {
                    nTooMultiMappingReads++;
                    continue;
                }
                int selectedMapping = rnd.Next(mrm.MappingsIdx);
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
                    nBcNewPosSingletons++;
                    counter = counterFactory.GetCounter();
                    counters[chr][posOf5Prime] = counter;
                    nMappedPositions++;
                }
                nBcReads++;
                bool isSingleton = counter.Add(mrm.UMIIdx);
                if (isSingleton)
                {
                    nBcSingletons++;
                    bool tooCloseMut1 = false, tooCloseMut2 = false;
                    for (int i1 = 0; i1 < settings.nUMIBits; i1 += 2)
                    {
                        for (int xorVal = 1; xorVal <= 3; xorVal++)
                        {
                            int mut1UMI = mrm.UMIIdx ^ (xorVal << i1);
                            if (counter.UMIOccupied(mut1UMI))
                            {
                                tooCloseMut1 = tooCloseMut2 = true;
                                break;
                            }
                            for (int i2 = 0; i2 < settings.nUMIBits; i2 += 2)
                            {
                                if (i1 == i2) continue;
                                if (counter.UMIOccupied(mut1UMI ^ (1 << i2)) ||
                                    counter.UMIOccupied(mut1UMI ^ (2 << i2)) ||
                                    counter.UMIOccupied(mut1UMI ^ (3 << i2))) tooCloseMut2 = true;
                            }
                        }
                    }
                    if (!tooCloseMut1) nBcNon1MutatedSingletons++;
                    if (!tooCloseMut2) nBcNon2MutatedSingletons++;
                }
                if (nBcReads % nSingletonSampleInterval == 0)
                {
                    nSingletonsByDepth.Add(nBcSingletons);
                    nNewPosSingletonsByDepth.Add(nBcNewPosSingletons);
                    nNon1MutatedSingletonsByDepth.Add(nBcNon1MutatedSingletons);
                    nNon2MutatedSingletonsByDepth.Add(nBcNon2MutatedSingletons);
                }
            }
        }

        int WriteBarcodeOutput(string outfilePath, UMICountType ct)
        {
            ReadsPerMolCounter rpmCounter = new ReadsPerMolCounter();
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
                        if (umiDistroCounter != null)
                            umiDistroCounter.Add(chrCounters[pos]);
                        int n = chrCounters[pos].count(ct);
                        if (settings.IsCountingMols && settings.estimateTrueMolCounts) n = EstimateTrueCount(n);
                        if (n == 0) continue;
                        nTotal += n;
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", chr, strand, pos, n);
                        if (settings.AnalyzeReadsPerMol)
                        {
                            foreach (int nReads in ((UMIReadCountProfile)chrCounters[pos]).IterReadsPerMol())
                            {
                                rpmCounter.Add(nReads);
                                summaryRpmCounter.Add(nReads);
                            }
                        }
                    }
                }
            }
            if (settings.AnalyzeReadsPerMol)
                using (StreamWriter distroWriter = new StreamWriter(settings.readsPerMolFile))
                    distroWriter.WriteLine(rpmCounter.GetReadsPerMolDistroLine(outfilePath));
            return nTotal;
        }

        public int EstimateTrueCount(int nMols)
        {
            if (nMols > settings.nUMIs)
                return nMols;
            return (int)Math.Round(Math.Log(1.0 - (double)nMols / settings.nUMIs) / Math.Log(1.0 - 1.0 / settings.nUMIs));
        }

    }
}
