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
        private static int[] precalcBuffer;

        public static void SetNBcAndNUMIs(int nBcs, int nUMIs)
        {
            PositionCounter.nBcs = nBcs;
            PositionCounter.nUMIs = nUMIs;
            precalcBuffer = new int[nUMIs];
        }

        private Dictionary<int, short> detectedUMIsByBc;


        public PositionCounter()
        {
            detectedUMIsByBc = new Dictionary<int, short>();
        }
        public void Add(int bcIdx, int UMIIdx)
        {
            int codedKey = (UMIIdx << 9) + bcIdx;
            if (detectedUMIsByBc.ContainsKey(codedKey))
                detectedUMIsByBc[codedKey] += 1;
            else
                detectedUMIsByBc[codedKey] = 1;
        }

        public void AddToCoOccuringBcHisto(int[] histoOfMaxReadCount, int[] histoOfCasesOfReadsInAnotherBc)
        {
            for (int UMIIdx = 0; UMIIdx < nUMIs; UMIIdx++)
            {
                int codedKeyBase = (UMIIdx << 9);
                int maxReadCountInThisUMI = 0;
                int nBcWithReadsUnThisUMI = 0;
                short temp;
                for (int codedKey = codedKeyBase; codedKey < codedKeyBase + nBcs; codedKey++)
                {
                    if (detectedUMIsByBc.TryGetValue(codedKey, out temp))
                    {
                        nBcWithReadsUnThisUMI++;
                        maxReadCountInThisUMI = Math.Max(maxReadCountInThisUMI, Math.Abs(temp));
                    }
                }
            }
        }

        public void Precalc()
        {
            Array.Clear(precalcBuffer, 0, nUMIs);
            foreach (KeyValuePair<int, short> p in detectedUMIsByBc)
            {
                int UMIIdx = p.Key >> 9;
                precalcBuffer[UMIIdx] = Math.Max(precalcBuffer[UMIIdx], p.Value);
            }
            foreach (int codedKey in detectedUMIsByBc.Keys.ToArray())
            {
                int bcIdx = codedKey & 511;
                int UMIIdx = codedKey >> 9;
                int maxReadCountInUMI = precalcBuffer[UMIIdx];
                int readCount = detectedUMIsByBc[codedKey];
                if (readCount < maxReadCountInUMI / 10)
                    detectedUMIsByBc[codedKey] = (short)-readCount;
            }
        }

        public int ShouldKeepAfterPrecalc(int bcIdx, int UMIIdx)
        {
            int codedKey = (UMIIdx << 9) + bcIdx;
            return detectedUMIsByBc[codedKey];
        }

        /// <summary>
        /// Return +readCount if should be kept, and -readCount otherwise.
        /// </summary>
        /// <param name="bcIdx"></param>
        /// <param name="UMIIdx"></param>
        /// <returns></returns>
        public int ShouldKeep(int bcIdx, int UMIIdx)
        {
            int codedKey = (UMIIdx << 9) + bcIdx;
            int readCount = detectedUMIsByBc[codedKey];
            if (readCount >= GetCountThreshold(UMIIdx)) //(readCount > 2 || readCount >= GetCountThreshold(UMIIdx))
                return readCount;
            else
                return -readCount;
        }
        private int GetCountThreshold(int UMIIdx)
        {
            int codedKeyBase = (UMIIdx << 9);
            int maxReadCountInThisUMI = 0;
            short temp;
            for (int codedKey = codedKeyBase; codedKey < codedKeyBase + nBcs; codedKey++)
            {
                if (detectedUMIsByBc.TryGetValue(codedKey, out temp))
                    maxReadCountInThisUMI = Math.Max(maxReadCountInThisUMI, temp);
            }
            int readCountThresholdInThisUMI = maxReadCountInThisUMI / 10;
            return readCountThresholdInThisUMI;
        }

    }


    class FilterMapFilesXBarcodes
    {
        private static readonly int maxReadCountPerMol = 10000;

        private FilterMapFilesXBarcodesSettings settings;
        private Dictionary<string, Dictionary<int, PositionCounter>> counters;
        private int[] keptReadCountsHisto, removedReadCountsHisto;
        public FilterMapFilesXBarcodes(FilterMapFilesXBarcodesSettings settings)
        {
            this.settings = settings;
            PositionCounter.SetNBcAndNUMIs(settings.nBcs, settings.nUMIs);
            counters = new Dictionary<string, Dictionary<int, PositionCounter>>();
            keptReadCountsHisto = new int[maxReadCountPerMol];
            removedReadCountsHisto = new int[maxReadCountPerMol];
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
            NoBarcodes bcs =  new NoBarcodes();
            foreach (string inputFile in settings.inputFiles)
            {
                string inputFilename = Path.GetFileName(inputFile);
                int bcIdx = int.Parse(inputFilename.Substring(0, inputFilename.IndexOf('_')));
                Console.WriteLine("{0}...", inputFile);
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
        }

        private void PrecalcThresholds()
        {
            foreach (Dictionary<int, PositionCounter> countersByPos in counters.Values)
            {
                foreach (PositionCounter counter in countersByPos.Values)
                {
                    counter.Precalc();
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
            int[] histoOfMaxReadCount = new int[maxReadCountPerMol];
            int[] histoOfCasesOfReadsInAnotherBc = new int[maxReadCountPerMol];
            foreach (Dictionary<int, PositionCounter> countersByPos in counters.Values)
            {
                foreach (PositionCounter counter in countersByPos.Values)
                {
                    counter.AddToCoOccuringBcHisto(histoOfMaxReadCount, histoOfCasesOfReadsInAnotherBc);
                }
            }

            Console.WriteLine("Cases of max read counts per mol within UMI, cases of reads in another bc in same UMI, # removed, and # kept reads within each bin of reads per molecule:");
            Console.WriteLine("Reads/Mol\tCasesOfThisMax\tCasesOfReadsInAnotherBc\tReadsFiltered\tReadsKept");
            int maxCount = maxReadCountPerMol - 1;
            for (; maxCount > 0; maxCount--)
                if (keptReadCountsHisto[maxCount] > 0 || removedReadCountsHisto[maxCount] > 0 ||
                    histoOfMaxReadCount[maxCount] > 0 || histoOfCasesOfReadsInAnotherBc[maxCount] > 0)
                    break;
            for (int c = 0; c <= maxCount; c++)
                Console.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}", 
                    c, histoOfMaxReadCount[c], histoOfCasesOfReadsInAnotherBc[c], removedReadCountsHisto[c], keptReadCountsHisto[c]);

        }
        
    }
}
