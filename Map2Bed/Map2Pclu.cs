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
    class PositionCounter
    {
        private int detectedReads;
        private BitArray detectedUMIs;

        public PositionCounter(int nUMIs)
        {
            if (nUMIs > 0)
                detectedUMIs = new BitArray(nUMIs);
        }
        public void Add(int UMIIdx)
        {
            detectedReads++;
            if (detectedUMIs != null)
                detectedUMIs[UMIIdx] = true;
        }
        public int nMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i]) n++;
            return n;
        }
        public int nReads()
        {
            return detectedReads;
        }
        public int count(bool mols)
        {
            return mols ? nMols() : detectedReads;
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
            if (!Directory.Exists(settings.outputFolder))
                Directory.CreateDirectory(settings.outputFolder);
            int maxBcIdx = settings.iterateBarcodes ? settings.maxBarcodeIdx : 0;
            for (int bcIdx = 0; bcIdx <= maxBcIdx; bcIdx++)
            {
                List<int> readLens = new List<int>();
                counters = new Dictionary<string, Dictionary<int, PositionCounter>>();
                int nReads = 0, nMols = 0;
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
                    int readLen = ReadMapFile(file);
                    readLens.Add(readLen);
                }
                if (counters.Count == 0)
                    continue;
                int averageLen = (int)Math.Round(readLens.Sum() / (double)readLens.Count);
                string bcPrefix = settings.iterateBarcodes ? bcIdx + "_" : "";
                if (settings.CountMols)
                    nMols = WriteOutput(bcPrefix + "mols.pclu.gz", true, averageLen);
                if (settings.countReads)
                    nReads = WriteOutput(bcPrefix + "reads.pclu.gz", false, averageLen);
                string molTxt = settings.CountMols ? string.Format(" and {0} molecules", nMols) : "";
                Console.WriteLine("{0} reads{1} at {2} mapped positions. {3} multireads were skipped.",
                                  nReads, molTxt, nMappedPositions, nTooMultiMappingReads);
                nTotMols += nMols;
                nTotReads += nReads;
            }
            string totMolTxt = settings.CountMols ? string.Format(" and {0} molecules", nTotMols) : "";
            Console.WriteLine("All in all were {0} reads{1} processed.", nTotReads, totMolTxt);
            Console.WriteLine("Output is found in " + settings.outputFolder);
        }

        private int ReadMapFile(string mapFile)
        {
            int readLen = 0;
            NoBarcodes bcs = settings.CountMols ? new NoBarcodes() : new NoUMIsNoBarcodes();
            foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(mapFile))
            {
                if (mrm.NMappings > nMaxMappings)
                {
                    nTooMultiMappingReads++;
                    continue;
                }
                int selectedMapping = rnd.Next(mrm.NMappings);
                MultiReadMapping m = mrm[selectedMapping];
                string chr = settings.AllAsPlusStrand ? m.Chr : m.Chr + m.Strand;
                int pos = (settings.AllAsPlusStrand || m.Strand == '+') ? m.Position : m.Position + mrm.SeqLen - 1;
                readLen = mrm.SeqLen;
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
                if (!counters[chr].TryGetValue(pos, out counter))
                {
                    counter = new PositionCounter(settings.nUMIs);
                    counters[chr][pos] = counter;
                    nMappedPositions++;
                }
                counter.Add(mrm.UMIIdx);
            }
            return readLen;
        }

        int WriteOutput(string filename, bool mols, int readLen)
        {
            int nTotal = 0;
            string outfilePath = Path.Combine(settings.outputFolder, filename);
            using (StreamWriter writer = outfilePath.OpenWrite())
            {
                writer.WriteLine("#ReadLen=" + readLen);
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
                        int n = chrCounters[pos].count(mols);
                        nTotal += n;
                        writer.WriteLine("{0}\t{1}\t{2}\t{3}", chr, strand, pos, n);
                    }
                }
            }
            return nTotal;
        }
    }
}
