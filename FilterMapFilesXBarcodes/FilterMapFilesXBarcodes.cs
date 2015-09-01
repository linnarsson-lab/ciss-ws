using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace FilterMapFilesXBarcodes
{
    /// <summary>
    /// Keeps track of, and builds statistics for, all reads that map at a certain genomic position (and strand).
    /// They are sorted by UMI and barcode, in order to allow filtering across the whole dataset.
    /// </summary>
    class PositionCounter
    {
        private static int nBcs, nUMIs;
        public static double ratioThresholdForFilter = 0.1;
        private static int[] maxReadCountsPerUMI, nBcsWithReadsPerUMI;
        private static int[] bcOfMaxReadCountPerUMI;
        public static Dictionary<int, int> maxBcTo2ndBcFreqs = new Dictionary<int, int>();

        private static int[] maxReadCountsPerBc, nUMIsWithReadsPerBc;
        private static int[] UMIOfMaxReadCountPerBc;
        public static Dictionary<int, int> maxUMIToSingletonUMIFreqs = new Dictionary<int, int>();

        public static readonly int distroMaxNReads = 4000;
        public static int[,] distroOfNReadsIn2ndPeakByMaxPeakNReads = new int[distroMaxNReads, distroMaxNReads];
        public static int[,] distroOfNBcsWithDataByMaxPeakNReads;

        public static void SetNBcAndNUMIs(int nBcs, int nUMIs)
        {
            PositionCounter.nBcs = nBcs;
            PositionCounter.nUMIs = nUMIs;
            maxReadCountsPerUMI = new int[nUMIs];
            nBcsWithReadsPerUMI = new int[nUMIs];
            bcOfMaxReadCountPerUMI = new int[nUMIs];
            maxReadCountsPerBc = new int[nBcs];
            nUMIsWithReadsPerBc = new int[nBcs];
            UMIOfMaxReadCountPerBc = new int[nBcs];
            distroOfNBcsWithDataByMaxPeakNReads = new int[distroMaxNReads, nBcs];
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
            Array.Clear(maxReadCountsPerBc, 0, nBcs);
            Array.Clear(nUMIsWithReadsPerBc, 0, nBcs);
            foreach (KeyValuePair<int, short> p in detectedUMIsByBc)
            {
                int bcIdx = p.Key & 511;
                int UMIIdx = (p.Key >> 9) & (nUMIs - 1);
                if (p.Value > maxReadCountsPerUMI[UMIIdx])
                {
                    maxReadCountsPerUMI[UMIIdx] = p.Value;
                    bcOfMaxReadCountPerUMI[UMIIdx] = bcIdx;
                }
                if (p.Value > maxReadCountsPerBc[bcIdx])
                {
                    maxReadCountsPerBc[bcIdx] = p.Value;
                    UMIOfMaxReadCountPerBc[bcIdx] = UMIIdx;
                }

            }
            foreach (int codedKey in detectedUMIsByBc.Keys.ToArray())
            {
                int bcIdx = codedKey & 511;
                int UMIIdx = (codedKey >> 9) & (nUMIs-1);
                nBcsWithReadsPerUMI[UMIIdx]++;
                nUMIsWithReadsPerBc[bcIdx]++;
                int readCount = detectedUMIsByBc[codedKey];
                int maxReadCountInUMI = maxReadCountsPerUMI[UMIIdx];
                if (bcIdx != bcOfMaxReadCountPerUMI[UMIIdx])
                { // It is a secondary peak within this UMI
                    if (maxReadCountInUMI < distroMaxNReads)
                        distroOfNReadsIn2ndPeakByMaxPeakNReads[maxReadCountInUMI, readCount]++;
                    int maxBcFilterBcCombo = (bcOfMaxReadCountPerUMI[UMIIdx] << 9) | bcIdx;
                    if (maxBcTo2ndBcFreqs.ContainsKey(maxBcFilterBcCombo))
                        maxBcTo2ndBcFreqs[maxBcFilterBcCombo] += 1;
                    else
                        maxBcTo2ndBcFreqs[maxBcFilterBcCombo] = 1;
                }
                if (UMIIdx != UMIOfMaxReadCountPerBc[bcIdx] && readCount == 1 && nUMIsWithReadsPerBc[bcIdx] == 2)
                { // It is a lonely secondary singleton peak within this barcode
                    int maxUMIToSingletonUMICombo = (UMIOfMaxReadCountPerBc[bcIdx] << 12) | UMIIdx;
                    if (maxUMIToSingletonUMIFreqs.ContainsKey(maxUMIToSingletonUMICombo))
                        maxUMIToSingletonUMIFreqs[maxUMIToSingletonUMICombo] += 1;
                    else
                        maxUMIToSingletonUMIFreqs[maxUMIToSingletonUMICombo] = 1;
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
                    int nBcsWithReads = nBcsWithReadsPerUMI[UMIIdx];
                    if (maxReadCountInUMI < distroMaxNReads)
                        distroOfNBcsWithDataByMaxPeakNReads[maxReadCountInUMI, nBcsWithReads]++;
                    if (nBcsWithReads > 1)
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

    /// <summary>
    /// Filters reads in map files of a full plate by analyzing cross-contamination between the different barcodes
    /// within each UMI-position-strand combination.
    /// </summary>
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
            Console.Write("Analyzing...");
            AnalyzeFiles();
            Console.WriteLine("Calculation thresholds...");
            PrecalcThresholds();
            Console.WriteLine("Filtering and writing output files...");
            FilterFiles();
            WriteStats();
        }

        private void AnalyzeFiles()
        {
            foreach (string inputFile in settings.inputFiles)
            {
                string inputFilename = Path.GetFileName(inputFile);
                int bcIdx = int.Parse(inputFilename.Substring(0, inputFilename.IndexOf('_')));
                Console.Write(".");
                foreach (MultiReadMappings mrm in new BowtieMapFile(100).MultiMappings(inputFile))
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
            NoBarcodes bcs =  new NoBarcodes();
            foreach (string inputFile in settings.inputFiles)
            {
                string inputFilename = Path.GetFileName(inputFile); 
                int bcIdx = int.Parse(inputFilename.Substring(0, inputFilename.IndexOf('_')));
                Console.Write("{0}...", inputFile);
                string outputFile = inputFile + ".filtered";
                StreamWriter writer = new StreamWriter(outputFile);
                int nReads  = 0, nSavedReads = 0;
                //foreach (MultiReadMappings mrm in new BowtieMapFile(100, bcs).MultiMappings(inputFile))
                foreach (MultiReadMappings mrm in new BowtieMapFile(100).MultiMappings(inputFile))
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
            WriteSecondaryPeakCountsAndFilteredReads();
            WriteUMILeakage();
            WriteBarcodeLeakage();
            WriteHistosOfSecondaryPeakReadCounts();
            WriteHistosOfNoOfSecondaryPeaks();
        }

        private void WriteSecondaryPeakCountsAndFilteredReads()
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
        }

        private static void WriteHistosOfSecondaryPeakReadCounts()
        {
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

        private void WriteHistosOfNoOfSecondaryPeaks()
        {
            Console.WriteLine("\nHistograms showing distro of # barcodes with a peak for each # reads in maxRead peak.");
            Console.Write("MaxPeakReads\tNBarcodes=1");
            for (int n = 2; n < settings.nBcs; n++)
                Console.Write("\t" + n);
            Console.WriteLine();
            for (int maxReads = 1; maxReads < PositionCounter.distroMaxNReads; maxReads++)
            {
                Console.Write(maxReads);
                for (int n = 1; n < settings.nBcs; n++)
                    Console.Write("\t" + PositionCounter.distroOfNBcsWithDataByMaxPeakNReads[maxReads, n]);
                Console.WriteLine();
            }
        }

        private void WriteBarcodeLeakage()
        {
            Console.WriteLine("\nCases of potential 'flow' from each maxRead peak to secondary peak.");
            Console.WriteLine("From->To\tMaxBc\tSecondaryBc\tNumber of cases");
            int[] bcCombos = PositionCounter.maxBcTo2ndBcFreqs.Keys.ToArray();
            Array.Sort(bcCombos);
            foreach (int bcCombo in bcCombos)
            {
                int maxBcIdx = (bcCombo >> 9) & 511;
                int filteredBcIdx = bcCombo & 511;
                Console.Write("{0}->{1}\t", maxBcIdx, filteredBcIdx);
                if (settings.bcIdx2Bc != null)
                    Console.WriteLine("{0}\t{1}\t{2}", settings.bcIdx2Bc[maxBcIdx], settings.bcIdx2Bc[filteredBcIdx], PositionCounter.maxBcTo2ndBcFreqs[bcCombo]);
                else
                    Console.WriteLine("{0}\t{1}\t{2}", maxBcIdx, filteredBcIdx, PositionCounter.maxBcTo2ndBcFreqs[bcCombo]);
            }
        }

        private void WriteUMILeakage()
        {
            Console.WriteLine("\nCases of potential 'flow' from each maxRead UMI to secondary UMI with singleton, when these are the only peaks in bc, sorted by sequence distance.");
            Console.WriteLine("Hamming distance\tNumber of cases");
            int[] UMICombos = PositionCounter.maxUMIToSingletonUMIFreqs.Keys.ToArray();
            int[] countsByDistance = new int[7];
            foreach (int UMICombo in UMICombos)
            {
                int maxUMIIdx = (UMICombo >> 12) & 4095;
                int singletonUMIIdx = UMICombo & 4095;
                int dist = CalcDistance(maxUMIIdx, singletonUMIIdx);
                countsByDistance[dist]++;
            }
            for (int i = 1; i < countsByDistance.Length; i++)
            {
                Console.WriteLine("{0}\t{1}", i, countsByDistance[i]);
            }

        }

        private int CalcDistance(int bcIdx1, int bcIdx2)
        {
            int dist = 0;
            while (bcIdx1 != 0 || bcIdx2 != 0)
            {
                if ((bcIdx1 & 3) != (bcIdx2 & 3))
                    dist++;
                bcIdx1 >>= 2;
                bcIdx2 >>= 2;
            }
            return dist;
        }
    }
}
