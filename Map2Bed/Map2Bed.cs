using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;
using Linnarsson.Dna;

namespace Map2Bed
{
    class PositionCounter
    {
        private int detectedReads;
        private BitArray detectedUMIs;

        public PositionCounter(int nUMIs)
        {
            //Console.WriteLine("new: nUMIs={0}", nUMIs);
            if (nUMIs > 0)
                detectedUMIs = new BitArray(nUMIs);
        }
        public void Add(int UMIIdx)
        {
            //Console.WriteLine("Add: UMIIdx={0}", UMIIdx);
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

    class Map2Bed
    {
        private Dictionary<string, Dictionary<int, PositionCounter>> counters = new Dictionary<string, Dictionary<int, PositionCounter>>();
        private Map2BedSettings settings;

        private int nTooMultiMappingReads;
        private int nReads, nMols, nMappedPositions;

        private int nMaxMappings;
        private Random rnd;

        public Map2Bed(Map2BedSettings settings)
        {
            this.settings = settings;
            nMaxMappings = settings.maxMultiReadMappings;
            rnd = new Random(System.DateTime.Now.Millisecond);
        }

        public void Convert()
        {
            if (!Directory.Exists(settings.outputFolder))
                Directory.CreateDirectory(settings.outputFolder);
            foreach (string mapFile in settings.inputFiles)
            {
                Console.WriteLine("Processing {0}...", mapFile);
                ReadMapFile(mapFile);
            }
            if (settings.CountMols)
                nMols = WriteOutput("mols.bed.gz", true);
            if (settings.countReads)
                nReads = WriteOutput("reads.bed.gz", false);
            string molTxt = settings.CountMols ? string.Format(" and {0} molecules", nMols) : "";
            Console.WriteLine("Totally {0} reads{1} at {2} mapped positions. {3} multireads were skipped.",
                              nReads, molTxt, nMappedPositions, nTooMultiMappingReads);
        }

        private void ReadMapFile(string mapFile)
        {
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
                if (!counters[chr].TryGetValue(m.Position, out counter))
                {
                    counter = new PositionCounter(settings.nUMIs);
                    counters[chr][m.Position] = counter;
                    nMappedPositions++;
                }
                counter.Add(mrm.UMIIdx);
            }
        }

        int WriteOutput(string filename, bool mols)
        {
            int nTotal = 0;
            string outfilePath = Path.Combine(settings.outputFolder, filename);
            using (StreamWriter writer = outfilePath.OpenWrite())
            {
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
