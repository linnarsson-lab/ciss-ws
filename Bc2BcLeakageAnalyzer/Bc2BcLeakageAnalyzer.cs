using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;

namespace Bc2BcLeakageAnalyzer
{
    class Bc2BcLeakageAnalyzer
    {
        private Dictionary<string, Dictionary<int, Dictionary<int, int>>> counters;
        private int maxBcIdx = 0;

        Bc2BcLeakageAnalyzerSettings settings;

        public Bc2BcLeakageAnalyzer(Bc2BcLeakageAnalyzerSettings settings)
        {
            this.settings = settings;
            counters = new Dictionary<string, Dictionary<int, Dictionary<int, int>>>();
        }

        public void ReadMapFiles()
        {
            string[] mapFiles = Directory.GetFiles(settings.mapFolder, "*.map");
            Array.Sort(mapFiles);
            foreach (string mapFile in mapFiles)
            {
                Console.Write("{0}...", mapFile);
                int nReads = ReadMapFile(mapFile);
                Console.WriteLine("{0} reads.", nReads);
            }
        }

        private int ReadMapFile(string mapFile)
        {
            int bcIdx = int.Parse(Path.GetFileName(mapFile).Split('_')[0]);
            maxBcIdx = Math.Max(maxBcIdx, bcIdx);
            NoBarcodes bcs = new NoBarcodes();
            int nReads = 0;
            foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(mapFile))
            {
                nReads++;
                if (mrm.MappingsIdx > 1)
                    continue;
                MultiReadMapping m = mrm[0];
                string chr = m.Chr + m.Strand;
                int posOf5Prime = (m.Strand == '+') ? m.Position : m.Position + mrm.SeqLen - 1;
                Dictionary<int, Dictionary<int, int>> chrCounters;
                int codedBcUMI = (bcIdx << 14) | mrm.UMIIdx;
                try
                {
                    chrCounters = counters[chr];
                }
                catch (KeyNotFoundException)
                {
                    chrCounters = new Dictionary<int, Dictionary<int, int>>();
                    counters[chr] = chrCounters;
                }
                Dictionary<int, int> bcUMICounters;
                if (!counters[chr].TryGetValue(posOf5Prime, out bcUMICounters))
                {
                    bcUMICounters = new Dictionary<int, int>();
                    counters[chr][posOf5Prime] = bcUMICounters;
                    bcUMICounters[codedBcUMI] = 1;
                }
                else if (!bcUMICounters.ContainsKey(codedBcUMI))
                    bcUMICounters[codedBcUMI] = 1;
                else
                    bcUMICounters[codedBcUMI]++;
            }
            return nReads;
        }

        public void Analyze()
        {
            Console.WriteLine("Chr\tPos\tMaxBcIdx\tUMIIdx\tMaxBcReads\tTotReads");
            int[] readSumsByBc = new int[maxBcIdx + 1];
            int[] molSumsByBc = new int[maxBcIdx + 1];
            int[] bcIndexes = new int[maxBcIdx + 1];
            int[] readSumsByUMI = new int[settings.nUMIs + 1];
            foreach (KeyValuePair<string, Dictionary<int, Dictionary<int, int>>> p1 in counters)
            {
                string chr = p1.Key;
                Dictionary<int, Dictionary<int, int>> chrCounters = p1.Value;
                foreach (KeyValuePair<int, Dictionary<int, int>> p2 in chrCounters)
                {
                    int pos = p2.Key;
                    Dictionary<int, int> bcUMICounters = p2.Value;
                    Array.Clear(readSumsByBc, 0, readSumsByBc.Length);
                    Array.Clear(molSumsByBc, 0, molSumsByBc.Length);
                    Array.Clear(readSumsByUMI, 0, readSumsByUMI.Length);
                    for (int bcIdx = 0; bcIdx < maxBcIdx; bcIdx++)
                    {
                        bcIndexes[bcIdx] = bcIdx;
                        int nReads;
                        for (int UMIIdx = 0; UMIIdx < settings.nUMIs; UMIIdx++)
                        {
                            if (bcUMICounters.TryGetValue((bcIdx << 14) | UMIIdx, out nReads))
                            {
                                readSumsByBc[bcIdx] += nReads;
                                readSumsByUMI[UMIIdx] += nReads;
                                if (nReads > 0)
                                    molSumsByBc[bcIdx] += 1;
                            }
                        }
                    }
                    if (readSumsByBc.Max() >= settings.MinReadRatioBc1ToBc2)
                    {
                        Sort.QuickSort(readSumsByBc, bcIndexes, molSumsByBc);
                        int maxReadBcIdx = bcIndexes[bcIndexes.Length - 1];
                        if (readSumsByBc[readSumsByBc.Length - 1] > readSumsByBc[readSumsByBc.Length - 2] * settings.MinReadRatioBc1ToBc2
                            && readSumsByBc[readSumsByBc.Length - 1] > molSumsByBc[molSumsByBc.Length - 1] * settings.MinReadToMolRatioBc1)
                        {
                            int nReads;
                            for (int UMIIdx = 0; UMIIdx < settings.nUMIs; UMIIdx++)
                            {
                                if (bcUMICounters.TryGetValue((maxReadBcIdx << 14) | UMIIdx, out nReads))
                                {
                                    if (nReads > settings.MinReadsPerUMIInMaxBc)
                                    {
                                        int secondaryBcsReadSum = readSumsByUMI[UMIIdx];
                                        Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", chr, pos, maxReadBcIdx, UMIIdx, nReads, secondaryBcsReadSum);
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }
    }
}
