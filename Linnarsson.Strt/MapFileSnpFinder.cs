using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;

namespace Linnarsson.Strt
{
    /// <summary>
    /// Keeps track of all SNPs found on a specific chromosome
    /// </summary>
    public class ChrSNPCounter
    {
        /// <summary>
        /// SNP statistics indexed by position on chromosome
        /// </summary>
        private Dictionary<int, SNPCounter> snpItems = new Dictionary<int, SNPCounter>();
        /// <summary>
        /// Keeps a wiggle plot to measure and set the total number of reads that covers every SNP position
        /// </summary>
        private Wiggle wiggle = new Wiggle();
        bool totalsSet = false;
        private string chr;

        public ChrSNPCounter(string chr)
        {
            this.chr = chr;
        }
        /// <summary>
        /// Register an investigated read starting at given position. Then call AddSNP() for every SNP in the read.
        /// This step is needed to calculate the coverage of every SNP base.
        /// </summary>
        /// <param name="hitStartPos"></param>
        /// <param name="hitLen"></param>
        public void AddRead(int hitStartPos, int hitLen)
        {
            wiggle.AddARead(hitStartPos);
            totalsSet = false;
        }
        /// <summary>
        /// Register a potential SNP (mismatch) at a specific position.
        /// </summary>
        /// <param name="mm">A mismatch parsed from the (bowtie) read mapping</param>
        /// <returns>True if this was a new position</returns>
        public bool AddSNP(Mismatch mm)
        {
            bool result = false;
            SNPCounter snpCounter;
            if (!snpItems.TryGetValue(mm.posInChr, out snpCounter))
            {
                result = true;
                snpCounter = new SNPCounter(mm.posInChr, mm.refNtInChrDir);
                snpItems[mm.posInChr] = snpCounter;
            }
            snpCounter.Add(mm.ntInChrDir);
            totalsSet = false;
            return result;
        }

        /// <summary>
        ///  Iterate over all SNPs on the chromosome.
        ///  Will set the total read counts from the internal wiggle plot.
        /// </summary>
        /// <param name="averageReadLength">Needed for read count estimation across SNP pos</param>
        /// <param name="marginForWiggle">Min distance from read end to SNP pos</param>
        /// <param name="minNSNPs">Minimum number of reads with a SNPed based</param>
        /// <returns></returns>
        public IEnumerable<LocatedSNPCounter> SNPItems(int averageReadLength, int marginForWiggle, int minNSNPs)
        {
            LocatedSNPCounter locCounter = new LocatedSNPCounter();
            locCounter.chr = chr;
            foreach (KeyValuePair<int, SNPCounter> pair in snpItems)
            {
                int chrPos = pair.Key;
                SNPCounter counter = pair.Value;
                if (counter.nSnps < minNSNPs) continue;
                if (!totalsSet)
                    counter.nTotal = (ushort)wiggle.GetReadCount(chrPos, averageReadLength, marginForWiggle);
                locCounter.chrPos = chrPos;
                locCounter.counter = counter;
                yield return locCounter;
            }
            totalsSet = true;
        }
    }

    /// <summary>
    /// Use to analyze SNPs in a set of Bowtie .map files
    /// </summary>
    public class MapFileSnpFinder
    {
        public static int marginForWiggle = 2;
        public static int averageReadLength;

        Barcodes barcodes;
        Dictionary<string, ChrSNPCounter> dataByChr = new Dictionary<string, ChrSNPCounter>();

        public MapFileSnpFinder(Barcodes barcodes)
        {
            this.barcodes = barcodes;
        }

        /// <summary>
        /// Define all positions where potential SNPs are located, using only single-mapping reads
        /// </summary>
        /// <param name="mapFilePaths"></param>
        public void ProcessMapFiles(List<string> mapFilePaths)
        {
            Console.Write("Defining SNP positions by scanning {0} map files..", mapFilePaths.Count);
            int numReadInFile = 0;
            int nValidReads = 0, nReadsWMismatches = 0, nDistinctPositions = 0;
            long totLen = 0;
            foreach (string mapFilePath in mapFilePaths)
            {
                if (!File.Exists(mapFilePath))
                    continue;
                MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, barcodes);
                if (mapFileReader == null)
                    Console.WriteLine("\n  Skipping {0} - unknown read map file type.", mapFilePath);
                Console.Write(".");
                foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
                {
                    numReadInFile++;
                    if (mrm.HasMultipleMappings)
                        continue;
                    nValidReads++;
                    int hitStartPos = mrm[0].Position;
                    string chr = mrm[0].Chr;
                    ChrSNPCounter chrSNPData;
                    if (!dataByChr.TryGetValue(chr, out chrSNPData))
                    {
                        chrSNPData = new ChrSNPCounter(chr);
                        dataByChr[chr] = chrSNPData;
                    }
                    chrSNPData.AddRead(hitStartPos, mrm.SeqLen);
                    totLen += mrm.SeqLen;
                    if (mrm[0].HasMismatches)
                    {
                        nReadsWMismatches++;
                        foreach (Mismatch mm in mrm[0].IterMismatches(0))
                        {
                            if (mm.relPosInChrDir < marginForWiggle || mm.relPosInChrDir >= mrm.SeqLen - marginForWiggle) continue;
                            int chrSnpPos = hitStartPos + mm.relPosInChrDir;
                            if (chrSNPData.AddSNP(mm)) nDistinctPositions++;
                        }
                    }
                }
            }
            averageReadLength = (int)Math.Ceiling(totLen / (double)nValidReads);
            Console.WriteLine("\nTotally {0} reads in {1} files. Average readLen: {2}.",
                              numReadInFile, mapFilePaths.Count, averageReadLength);
            Console.WriteLine("{0} / {1} singleReads had some mismatch. {2} distinct expressed genomic mismatch positions were found.",
                              nReadsWMismatches, nValidReads, nDistinctPositions);
        }

        public int GetAverageReadLength()
        {
            return averageReadLength;
        }

        /// <summary>
        /// Iterate over all SNPs that have at least minNSNPHits hits to the SNP Nt
        /// </summary>
        /// <param name="minNSNPHits">Minimum number of hits to the SNP:ed Nt</param>
        /// <returns>Reused container of SNP data</returns>
        public IEnumerable<LocatedSNPCounter> IterSNPLocations(int minNSNPHits)
        {
            foreach (ChrSNPCounter chrSNPCounter in dataByChr.Values)
            {
                foreach (LocatedSNPCounter item in chrSNPCounter.SNPItems(averageReadLength, marginForWiggle, minNSNPHits))
                    yield return item;
            }
        }

        public void WriteToFile(string file)
        {
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Showing positions with >= 10 reads of which >= 4% and at least 2 are SNP:ed.");
            writer.WriteLine(LocatedSNPCounter.Header);
            foreach (ChrSNPCounter chrSNPCounter in dataByChr.Values)
            {
                foreach (LocatedSNPCounter item in chrSNPCounter.SNPItems(averageReadLength, marginForWiggle, 2))
                {
                    if ((item.counter.nTotal >= 10) && (item.counter.nSnps / (double)item.counter.nTotal >= 0.04))
                        writer.WriteLine(item.ToString());
                }
            }
            writer.Close();
        }

    }
}
