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
        /// Register an investigated read stating at given position. Call AddSNP() for every SNP in the read.
        /// </summary>
        /// <param name="hitStartPos"></param>
        /// <param name="hitLen"></param>
        public void AddRead(int hitStartPos, int hitLen)
        {
            wiggle.AddReads(hitStartPos, 1);
            totalsSet = false;
        }
        /// <summary>
        /// Register a SNP at a specific position.
        /// </summary>
        /// <param name="snpPos"></param>
        /// <param name="snpNt"></param>
        public void AddSNP(int snpPos, char snpNt)
        {
            if (!snpItems.ContainsKey(snpPos))
                snpItems[snpPos] = new SNPCounter();
            snpItems[snpPos].Add(snpNt);
            totalsSet = false;
        }
        /// <summary>
        ///  Iterate over all SNPs on the chromosome.
        ///  Will set the total read counts from the internal wiggle plot.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<LocatedSNPCounter> SNPItems(int averageReadLength, int marginForWiggle)
        {
            LocatedSNPCounter locCounter = new LocatedSNPCounter();
            locCounter.chr = chr;
            foreach (KeyValuePair<int, SNPCounter> pair in snpItems)
            {
                int chrPos = pair.Key;
                SNPCounter counter = pair.Value;
                if (!totalsSet)
                    counter.nTotal = wiggle.GetReadCount(chrPos, averageReadLength, marginForWiggle);
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

        public void ProcessMapFiles(List<string> mapFilePaths)
        {
            int numReadInFile = 0;
            int nValidReads = 0;
            long totLen = 0;
            foreach (string mapFilePath in mapFilePaths)
            {
                MapFile mapFileReader = MapFile.GetMapFile(mapFilePath, 10, barcodes);
                if (mapFileReader == null)
                    Console.WriteLine("Skipping " + mapFilePath + "- unknown read map file type.");
                foreach (MultiReadMappings mrm in mapFileReader.MultiMappings(mapFilePath))
                {
                    numReadInFile++;
                    if (mrm.HasAltMappings || mrm.NMappings > 1)
                        continue;
                    string chr = mrm[0].Chr;
                    if (!dataByChr.ContainsKey(chr))
                        dataByChr[chr] = new ChrSNPCounter(chr);
                    int hitStartPos = mrm[0].Position;
                    int strandBit = (mrm[0].Strand == '+')? 1 : 0;
                    ChrSNPCounter chrSNPData = dataByChr[mrm[0].Chr];
                    chrSNPData.AddRead(hitStartPos, mrm.SeqLen);
                    totLen += mrm.SeqLen;
                    nValidReads++;
                    if (mrm[0].Mismatches == "")
                        continue;
                    string[] mms = mrm[0].Mismatches.Split(',');
                    foreach (string snp in mms)
                    {
                        int p = snp.IndexOf(':');
                        if (p == -1)
                        {
                            Console.WriteLine("Strange SNP: " + snp + " in " + mrm[0].Mismatches);
                            continue;
                        }
                        int posInRead = int.Parse(snp.Substring(0, p));
                        if (posInRead < marginForWiggle || posInRead > mrm.SeqLen - marginForWiggle) continue;
                        int relPos = (mrm[0].Strand == '+') ? posInRead : mrm.SeqLen - 1 - posInRead;
                        int chrSnpPos = hitStartPos + relPos;
                        char altNtChar = snp[p + 3];
                        chrSNPData.AddSNP(chrSnpPos, altNtChar);
                    }
                }
            }
            averageReadLength = (int)Math.Round(totLen / (double)nValidReads);
            Console.WriteLine("Average read length:" + averageReadLength);
            Console.WriteLine("Counted " + nValidReads + " singleReads on valid chr out of totally " + numReadInFile + " reads in file.");
        }

        public int GetAverageReadLength()
        {
            return averageReadLength;
        }

        public IEnumerable<LocatedSNPCounter> IterSNPLocations()
        {
            foreach (ChrSNPCounter chrSNPCounter in dataByChr.Values)
            {
                foreach (LocatedSNPCounter item in chrSNPCounter.SNPItems(averageReadLength, marginForWiggle))
                    if (item.counter.nSnps > 9)
                        yield return item;
            }
        }

        public void WriteToFile(string file)
        {
            StreamWriter writer = new StreamWriter(file);
            writer.WriteLine("Showing positions with at least 4% SNP:ed reads.");
            writer.WriteLine(LocatedSNPCounter.Header);
            foreach (ChrSNPCounter chrSNPCounter in dataByChr.Values)
            {
                foreach (LocatedSNPCounter item in chrSNPCounter.SNPItems(averageReadLength, marginForWiggle))
                    if (item.counter.nSnps * 25 / item.counter.nTotal >= 1)
                        writer.WriteLine(item.ToString());
            }
            writer.Close();
        }

    }
}
