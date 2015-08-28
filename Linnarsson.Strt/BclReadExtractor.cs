using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using Linnarsson.Dna;
using Linnarsson.Strt;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    /// <summary>
    /// An experimental for quicker parallell fq copying & read extraction class - not yet in use
    /// </summary>
    public class BclReadExtractor
    {
        private List<LaneReadWriter> laneReadWriters;
        private List<SampleReadWriter> sampleReadWriters;
        private int[] nCyclesByReadIdx = new int[3];

        public int[] GetNCyclesByReadIdx()
        {
            return nCyclesByReadIdx;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="laneReadWriters">One for each read that should be extracted (e.g. up to 3)</param>
        /// <param name="sampleReadWriters">One for each sample that should be extracted from the lane</param>
        public BclReadExtractor(List<LaneReadWriter> laneReadWriters, List<SampleReadWriter> sampleReadWriters)
        {
            throw new NotImplementedException();
            this.laneReadWriters = laneReadWriters;
            this.sampleReadWriters = sampleReadWriters;
        }

        public void Process(string runFolder, int lane)
        {
            if (lane < 1 || lane > 8) throw new InvalidProgramException("Lane must be a number from 1 to 8");
            string[] items = Path.GetFileNameWithoutExtension(runFolder).Split('_');
            if (items.Length < 3) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            int r = 0;
            if (!int.TryParse(items[2], out r)) throw new InvalidDataException("Run folder does not seem to have a valid run ID");
            string runId = items[2];
            string flowcellId = items[3];
            string bcFolder = Path.Combine(runFolder, "Data");
            bcFolder = Path.Combine(bcFolder, "Intensities");
            bcFolder = Path.Combine(bcFolder, "BaseCalls");
            string laneFolder = Path.Combine(bcFolder, "L00" + lane.ToString());
            var config = XDocument.Load(Path.Combine(bcFolder, "config.xml"));
            var readConfig = from c in config.Descendants("Reads")
                             select new
                             {
                                 Index = int.Parse(c.Attribute("Index").Value),
                                 FirstCycle = int.Parse(c.Descendants("FirstCycle").First().Value),
                                 LastCycle = int.Parse(c.Descendants("LastCycle").First().Value)
                             };
            int firstCycle = readConfig.Min(v => v.FirstCycle);
            int lastCycle = readConfig.Max(v => v.LastCycle);
            //Console.WriteLine("lastCycle=" + lastCycle);
            Dictionary<string, byte[]> filters = new Dictionary<string, byte[]>();
            var filterFiles = Directory.GetFiles(laneFolder, "s_" + lane + "_*.filter");
            foreach (var f in filterFiles)
            {
                filters[Path.GetFileNameWithoutExtension(f)] = File.ReadAllBytes(f);
            }
            foreach (var tile in filters.Keys)
            {
                List<byte[]> bclData = new List<byte[]>();
                for (int cycle = firstCycle; cycle <= lastCycle; cycle++)
                { // Find the cycle folder. Find the "C<cycle>.<y>" folder with the highest <y> (due to rerun cycles)                   
                    int rerunIndex = 1;
                    string cycleFolder = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                    while (true)
                    {
                        rerunIndex++;
                        string temp = Path.Combine(laneFolder, "C" + cycle + "." + rerunIndex);
                        if (Directory.Exists(temp)) cycleFolder = temp;
                        else break;
                    }
                    if (!Directory.Exists(cycleFolder))
                    {
                        Console.Error.WriteLine("Could not find cycle folder: " + cycleFolder);
                        continue;
                    }
                    string bclFile = Path.Combine(cycleFolder, tile + ".bcl");
                    if (!File.Exists(bclFile))
                    {
                        Console.Error.WriteLine("BCL file not found: " + bclFile);
                        continue;
                    }
                    bclData.Add(File.ReadAllBytes(bclFile));
                }
                char [][] readSeqs = new char[3][];
                char [][] readQuals = new char[3][];
                foreach (var rc in readConfig)
                {
                    int readIdx = rc.Index - 1;
                    int nCycles = 1 + rc.LastCycle - rc.FirstCycle;
                    nCyclesByReadIdx[readIdx] = nCycles;
                    readSeqs[readIdx] = new char[nCycles];
                    readQuals[readIdx] = new char[nCycles];
                }
                foreach (SampleReadWriter srw in sampleReadWriters)
                    srw.Setup(readSeqs[0].Length, (readSeqs[1] != null) ? readSeqs[1].Length : 0, (readSeqs[2] != null) ? readSeqs[2].Length : 0);
                string hdrStart = string.Format("Run{0}_{1}_L{2}_R", runId, flowcellId, lane);
                for (int ix = 0; ix < bclData[0].Length; ix++) // ix is an index into the clusters (i.e. it is a read)
                {
                    string hdrEnd = string.Format("_T{0}_C{1}", tile.Split('_')[2], ix);
                    bool passedFilter = filters[tile][ix + 8] == 1;
                    foreach (var rc in readConfig)
                    {
                        int readIdx = rc.Index - 1;
                        int seqIdx = 0;
                        for (int cycleIdx = rc.FirstCycle - 1; cycleIdx < rc.LastCycle; cycleIdx++)
                        {
                            int nt = (bclData[cycleIdx][ix] & 3);
                            readSeqs[readIdx][seqIdx] = "ACGT"[nt];
                            readQuals[readIdx][seqIdx] = (char)(((bclData[cycleIdx][ix] & 252) >> 2) + Props.props.QualityScoreBase);
                            seqIdx++;
                        }
                        //Console.WriteLine(readIdx + " : " + new string(readSeqs[readIdx]) + " - " + new string(readQuals[readIdx]));
                        laneReadWriters[readIdx].Write(hdrStart, hdrEnd, readSeqs[readIdx], readQuals[readIdx], passedFilter);
                    }
                    foreach (SampleReadWriter sre in sampleReadWriters)
                        sre.Process(hdrStart, hdrEnd, readSeqs, readQuals, passedFilter);
                }
            }
        }
    }
}
