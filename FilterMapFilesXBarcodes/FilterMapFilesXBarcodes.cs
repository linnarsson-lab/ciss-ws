using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace FilterMapFilesXBarcodes
{
    class PositionCounter
    {
        private static int nBcs, nUMIs;
        public static double ratioThresholdForFilter = 0.1;
        private static int[] maxReadCountsPerUMI, nBcsWithReadsPerUMI;
        private static int[] bcOfMaxReadCountPerUMI;
        public static Dictionary<int, int> maxBcTo2ndBcFreqs = new Dictionary<int, int>();

        public static readonly int distroMaxNReads = 1000;
        public static int[,] distroOfNReadsIn2ndPeakByMaxPeakNReads = new int[distroMaxNReads, distroMaxNReads];

        public static void SetNBcAndNUMIs(int nBcs, int nUMIs)
        {
            PositionCounter.nBcs = nBcs;
            PositionCounter.nUMIs = nUMIs;
            maxReadCountsPerUMI = new int[nUMIs];
            nBcsWithReadsPerUMI = new int[nUMIs];
            bcOfMaxReadCountPerUMI = new int[nUMIs];
        }

        private Dictionary<int, short> detectedUMIsByBc;

        public PositionCounter()
        {
            detectedUMIsByBc = new Dictionary<int, short>();
        }

        public void Add(int bcIdx, int UMIIdx)
        {
            int codedKey = (UMIIdx << 9) | bcIdx;
            if (detectedUMIsByBc.ContainsKey(codedKey))
                detectedUMIsByBc[codedKey] += 1;
            else
                detectedUMIsByBc[codedKey] = 1;
        }

        public void Precalc()
        {
            Array.Clear(maxReadCountsPerUMI, 0, nUMIs);
            Array.Clear(nBcsWithReadsPerUMI, 0, nUMIs);
            foreach (KeyValuePair<int, short> p in detectedUMIsByBc)
            {
                int UMIIdx = (p.Key >> 9) & (nUMIs-1);
                if (p.Value > maxReadCountsPerUMI[UMIIdx])
                {
                    maxReadCountsPerUMI[UMIIdx] = p.Value;
                    int bcIdx = p.Key & 511;
                    bcOfMaxReadCountPerUMI[UMIIdx] = bcIdx;
                }
            }
            foreach (int codedKey in detectedUMIsByBc.Keys.ToArray())
            {
                int bcIdx = codedKey & 511;
                int UMIIdx = (codedKey >> 9) & (nUMIs-1);
                nBcsWithReadsPerUMI[UMIIdx]++;
                int maxReadCountInUMI = maxReadCountsPerUMI[UMIIdx];
                int readCount = detectedUMIsByBc[codedKey];
                if (bcIdx != bcOfMaxReadCountPerUMI[UMIIdx]) // Secondary peak
                {
                    if (maxReadCountInUMI < distroMaxNReads)
                        distroOfNReadsIn2ndPeakByMaxPeakNReads[maxReadCountInUMI, readCount]++;
                    int maxBcFilterBcCombo = (bcOfMaxReadCountPerUMI[UMIIdx] << 9) | bcIdx;
                    if (maxBcTo2ndBcFreqs.ContainsKey(maxBcFilterBcCombo))
                        maxBcTo2ndBcFreqs[maxBcFilterBcCombo] += 1;
                    else
                        maxBcTo2ndBcFreqs[maxBcFilterBcCombo] = 1;
                }
                if (readCount < maxReadCountInUMI * ratioThresholdForFilter)
                {
                    detectedUMIsByBc[codedKey] = (short)-readCount;
                }
            }
        }

        public void AddToCoOccuringBcHisto(int[] histoOfMaxReadCount, int[] histoOfCasesOfReadsInAnotherBc)
        {
            for (int UMIIdx = 0; UMIIdx < nUMIs; UMIIdx++)
            {
                int maxReadCountInUMI = maxReadCountsPerUMI[UMIIdx];
                if (maxReadCountInUMI > 0)
                {
                    histoOfMaxReadCount[maxReadCountInUMI]++;
                    if (nBcsWithReadsPerUMI[UMIIdx] > 1)
                        histoOfCasesOfReadsInAnotherBc[maxReadCountInUMI]++;
                }
            }
        }

        public int ShouldKeepAfterPrecalc(int bcIdx, int UMIIdx)
        {
            int codedKey = (UMIIdx << 9) + bcIdx;
            return detectedUMIsByBc[codedKey];
        }
    }


    class FilterMapFilesXBarcodes
    {
        private static readonly int maxReadCountPerMol = 10000;

        private FilterMapFilesXBarcodesSettings settings;
        private Dictionary<string, Dictionary<int, PositionCounter>> counters;
        private int[] keptReadCountsHisto, removedReadCountsHisto, histoOfMaxReadCount, histoOfCasesOfReadsInAnotherBc;

        public FilterMapFilesXBarcodes(FilterMapFilesXBarcodesSettings settings)
        {
            this.settings = settings;
            PositionCounter.SetNBcAndNUMIs(settings.nBcs, settings.nUMIs);
            PositionCounter.ratioThresholdForFilter = settings.ratioThresholdForFilter;
            counters = new Dictionary<string, Dictionary<int, PositionCounter>>();
            keptReadCountsHisto = new int[maxReadCountPerMol];
            removedReadCountsHisto = new int[maxReadCountPerMol];
            histoOfMaxReadCount = new int[maxReadCountPerMol];
            histoOfCasesOfReadsInAnotherBc = new int[maxReadCountPerMol];
        }

        public void Process()
        {
            AnalyzeFiles();
            PrecalcThresholds();
            FilterFiles();
            WriteStats();
        }

        private void AnalyzeFiles()
        {
            Console.Write("Analyzing...");
            NoBarcodes bcs =  new NoBarcodes();
            foreach (string inputFile in settings.inputFiles)
            {
                string inputFilename = Path.GetFileName(inputFile);
                int bcIdx = int.Parse(inputFilename.Substring(0, inputFilename.IndexOf('_')));
                Console.Write(".");
                foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(inputFile))
                {
                    MultiReadMapping selectedMapping = mrm[0];
                    int pos = int.MaxValue;
                    foreach (MultiReadMapping m in mrm.IterMappings())
                    {
                        if (m.Position < pos)
                        {
                            pos = m.Position;
                            selectedMapping = m;
                        }
                    }
                    string chr = selectedMapping.Chr + selectedMapping.Strand;
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
                    if (!counters[chr].TryGetValue(selectedMapping.Position, out counter))
                    {
                        counter = new PositionCounter();
                        counters[chr][selectedMapping.Position] = counter;
                    }
                    counter.Add(bcIdx, mrm.UMIIdx);
                }
            }
            Console.WriteLine();
        }

        private void PrecalcThresholds()
        {
            Console.WriteLine("Calculation thresholds...");
            foreach (Dictionary<int, PositionCounter> countersByPos in counters.Values)
            {
                foreach (PositionCounter counter in countersByPos.Values)
                {
                    counter.Precalc();
                    counter.AddToCoOccuringBcHisto(histoOfMaxReadCount, histoOfCasesOfReadsInAnotherBc);
                }
            }
        }

        private void FilterFiles()
        {
            Console.WriteLine("Filtering and writing output files...");
            NoBarcodes bcs =  new NoBarcodes();
            foreach (string inputFile in settings.inputFiles)
            {
                string inputFilename = Path.GetFileName(inputFile); 
                int bcIdx = int.Parse(inputFilename.Substring(0, inputFilename.IndexOf('_')));
                Console.Write("{0}...", inputFile);
                string outputFile = inputFile + ".filtered";
                StreamWriter writer = new StreamWriter(outputFile);
                int nReads  = 0, nSavedReads = 0;
                foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(inputFile))
                {
                    nReads++;
                    MultiReadMapping selectedMapping = mrm[0];
                    int pos = int.MaxValue;
                    foreach (MultiReadMapping m in mrm.IterMappings())
                    {
                        if (m.Position < pos)
                        {
                            pos = m.Position;
                            selectedMapping = m;
                        }
                    }
                    string chr = selectedMapping.Chr + selectedMapping.Strand;
                    int readCount = counters[chr][selectedMapping.Position].ShouldKeepAfterPrecalc(bcIdx, mrm.UMIIdx);
                    bool shouldKeep = readCount > 0;
                    if (shouldKeep)
                    {
                        writer.Write(mrm.ToMapfileLines());
                        keptReadCountsHisto[readCount]++;
                        nSavedReads++;
                    }
                    else
                    {
                        removedReadCountsHisto[-readCount]++;
                    }
                }
                Console.WriteLine("{0} / {1} reads saved.", nSavedReads, nReads);
            }
        }

        private void WriteStats()
        {
            Console.WriteLine("Cases of max read counts per mol within UMI, cases of reads in another bc in same UMI, # removed, and # kept reads within each bin of reads per molecule:");
            Console.WriteLine("Reads/Mol\tCasesOfThisMax\tCasesOfReadsInAnotherBc\tReadsFiltered\tReadsKept");
            int maxCount = maxReadCountPerMol - 1;
            for (; maxCount > 0; maxCount--)
                if (keptReadCountsHisto[maxCount] > 0 || removedReadCountsHisto[maxCount] > 0 ||
                    histoOfMaxReadCount[maxCount] > 0 || histoOfCasesOfReadsInAnotherBc[maxCount] > 0)
                    break;
            for (int c = 1; c <= maxCount; c++)
                Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", 
                    c, histoOfMaxReadCount[c], histoOfCasesOfReadsInAnotherBc[c], removedReadCountsHisto[c], keptReadCountsHisto[c]);

            Console.WriteLine("\nCases of potential 'flow' from each maxRead peak to secondary peak.");
            Console.WriteLine("MaxBc\tSecondaryBc\tNumber of cases");
            int[] bcCombos = PositionCounter.maxBcTo2ndBcFreqs.Keys.ToArray();
            Array.Sort(bcCombos);
            foreach (int bcCombo in bcCombos)
            {
                int maxBcIdx = (bcCombo >> 9) & 511;
                int filteredBcIdx = bcCombo & 511;
                if (settings.bcIdx2Bc != null)
                    Console.WriteLine("{0}\t{1}\t{2}", settings.bcIdx2Bc[maxBcIdx], settings.bcIdx2Bc[filteredBcIdx], PositionCounter.maxBcTo2ndBcFreqs[bcCombo]);
                else
                    Console.WriteLine("{0}\t{1}\t{2}", maxBcIdx, filteredBcIdx, PositionCounter.maxBcTo2ndBcFreqs[bcCombo]);
            }

            Console.WriteLine("\nHistograms showing distro of # reads in secondary peak for each # reads in maxRead peak.");
            Console.Write("MaxPeakReads\t2ndPeakReads=1");
            for (int n = 2; n < PositionCounter.distroMaxNReads; n++)
                Console.Write("\t" + n);
            Console.WriteLine();
            for (int maxReads = 1; maxReads < PositionCounter.distroMaxNReads; maxReads++)
            {
                Console.Write(maxReads);
                for (int n = 1; n < PositionCounter.distroMaxNReads; n++)
                    Console.Write("\t" + PositionCounter.distroOfNReadsIn2ndPeakByMaxPeakNReads[maxReads, n]);
                Console.WriteLine();
            }
        }

    }
}
