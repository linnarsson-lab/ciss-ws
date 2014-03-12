using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Dna;

namespace Map2Pclu
{
    enum CountType { Reads, AllMolecules, NonSingeltonMolecules };

    class PositionCounter
    {
        private int detectedReads;
        private BitArray detectedUMIs;
        private BitArray multitonUMIs;

        public PositionCounter(int nUMIs)
        {
            if (nUMIs > 0)
            {
                detectedUMIs = new BitArray(nUMIs);
                multitonUMIs = new BitArray(nUMIs);
            }
        }
        public void Add(int UMIIdx)
        {
            detectedReads++;
            if (detectedUMIs != null)
            {
                if (detectedUMIs[UMIIdx])
                    multitonUMIs[UMIIdx] = true;
                detectedUMIs[UMIIdx] = true;
            }
        }
        public int nMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i]) n++;
            return n;
        }
        public int nNonSingeltonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (multitonUMIs[i]) n++;
            return n;
        }
        public int nReads()
        {
            return detectedReads;
        }
        public int count(CountType ct)
        {
            return (ct == CountType.Reads) ? detectedReads : (ct == CountType.AllMolecules) ? nMols() : nNonSingeltonMols();
        }
    }

    class Map2Pclu
    {
        private Dictionary<string, Dictionary<int, PositionCounter>> counters;
        private Map2PcluSettings settings;

        private int nTotReads = 0, nTotMols = 0;
        private int nTooMultiMappingReads, nMappedPositions;

        private int nMaxMappings;
        private Random rnd;

        public Map2Pclu(Map2PcluSettings settings)
        {
            this.settings = settings;
            nMaxMappings = settings.maxMultiReadMappings;
            rnd = new Random(System.DateTime.Now.Millisecond);
        }

        public void Convert()
        {
            if (!Directory.Exists(settings.outputFolderOrFilename))
                Directory.CreateDirectory(settings.outputFolderOrFilename);
            string bcPrefix = settings.iterateBarcodes ? "*_" : "";
            string fType = (settings.countType == CountType.Reads) ? "reads" : (settings.countType == CountType.AllMolecules) ? "mols" : "nonSingletonMols";
            string outfilePat = settings.outputFolderOrFilename.EndsWith(".gz")? settings.outputFolderOrFilename :
                Path.Combine(settings.outputFolderOrFilename, settings.filenamePrefix + bcPrefix + fType + ".pclu.gz");
            int maxBcIdx = settings.iterateBarcodes ? settings.maxBarcodeIdx : 0;
            for (int bcIdx = 0; bcIdx <= maxBcIdx; bcIdx++)
            {
                List<int> readLens = new List<int>();
                counters = new Dictionary<string, Dictionary<int, PositionCounter>>();
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
                Console.WriteLine("...{0} hits from {1} reads at {2} mapped positions. {3} multireads were skipped.",
                                  nHits, nReads, nMappedPositions, nTooMultiMappingReads);
                nTotMols += nHits;
                nTotReads += nReads;
            }
            string totMolTxt = settings.HasUMIs ? string.Format(" and {0} molecules", nTotMols) : "";
            Console.WriteLine("All in all were {0} reads{1} processed.", nTotReads, totMolTxt);
            Console.WriteLine("Output is found in " + settings.outputFolderOrFilename + "/" + outfilePat);
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
                Dictionary<int, PositionCounter> chrCounters;
                try
                {
                    chrCounters = counters[chr];
                }
                catch (KeyNotFoundException)
                {
                    chrCounters = new Dictionary<int, PositionCounter>();
                    counters[chr] = chrCounters;
                }
                PositionCounter counter;
                if (!counters[chr].TryGetValue(posOf5Prime, out counter))
                {
                    counter = new PositionCounter(settings.nUMIs);
                    counters[chr][posOf5Prime] = counter;
                    nMappedPositions++;
                }
                counter.Add(mrm.UMIIdx);
            }
            return nReads;
        }

        int WriteOutput(string outfilePath, CountType ct)
        {
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
                    Dictionary<int, PositionCounter> chrCounters = counters[chrStrand];
                    int[] positions = chrCounters.Keys.ToArray();
                    Array.Sort(positions);
                    foreach (int pos in positions)
                    {
                        int n = chrCounters[pos].count(ct);
                        if (settings.IsCountingMols && settings.estimateTrueMolCounts) n = EstimateTrueCount(n);
                        nTotal += n;
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", chr, strand, pos, n);
                    }
                }
            }
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
