using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Linnarsson.Mathematics;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    class PerLaneStats
    {
        private Dictionary<string, int[]> nUniqueMolsPerLaneAndBc = new Dictionary<string, int[]>();
        private Dictionary<string, int[]> nMappedReadsPerLaneAndBc = new Dictionary<string, int[]>();
        private Barcodes barcodes;
        private int currentBcIdx;
        private int lastNMappedReads;
        private int lastNUniqMols;

        public PerLaneStats(Barcodes barcodes)
        {
            this.barcodes = barcodes;
        }

        public void SetupForNextBc(int bcIdx)
        {
            lastNUniqMols = lastNMappedReads = 0;
            currentBcIdx = bcIdx;
        }

        public void AddMapFileData(string mapFilePath, int currentNBcMappedReads, int currentNBcUniqMols)
        {
            string runLane = SetupRunLane(mapFilePath);
            int laneNMappedReads = currentNBcMappedReads - lastNMappedReads;
            nMappedReadsPerLaneAndBc[runLane][currentBcIdx] += laneNMappedReads;
            lastNMappedReads = currentNBcMappedReads;
            int laneNUniqMols = currentNBcUniqMols - lastNUniqMols;
            nUniqueMolsPerLaneAndBc[runLane][currentBcIdx] += laneNUniqMols;
            lastNUniqMols = currentNBcUniqMols;
        }

        private string SetupRunLane(string mapFilePath)
        {
            Match m = Regex.Match(mapFilePath, "(Run[0-9]+_L[0-9])_");
            string runLane = m.Groups[1].Value;
            if (!nMappedReadsPerLaneAndBc.ContainsKey(runLane))
            {
                nMappedReadsPerLaneAndBc[runLane] = new int[barcodes.Count];
                nUniqueMolsPerLaneAndBc[runLane] = new int[barcodes.Count];
            }
            return runLane;
        }

        public List<Pair<string, double>> GetUniqueMolsPerMappedReads(int bcIdx)
        {
            List<Pair<string, double>> result = new List<Pair<string, double>>();
            foreach (string runLane in nUniqueMolsPerLaneAndBc.Keys)
            {
                double f = nUniqueMolsPerLaneAndBc[runLane][bcIdx] / (double)nMappedReadsPerLaneAndBc[runLane][bcIdx];
                result.Add(new Pair<string, double>(runLane, f));
            }
            return result;
        }

        public double GetMeanFrac0()
        {
            string runLane0 = nUniqueMolsPerLaneAndBc.Keys.ToArray()[0];
            DescriptiveStatistics ds = new DescriptiveStatistics();
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                double v = nUniqueMolsPerLaneAndBc[runLane0][bcIdx] / (double)nMappedReadsPerLaneAndBc[runLane0][bcIdx];
                ds.Add(v);
            }
            return ds.Mean();
        }
    }
}
