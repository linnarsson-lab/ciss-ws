﻿using System;
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
        public static readonly int nMappedReadsPerFileAtSample = 500000;

        private Dictionary<string, int[]> nUniqueMolsPerLaneAndBc = new Dictionary<string, int[]>();
        private Dictionary<string, int[]> nMappedReadsPerLaneAndBc = new Dictionary<string, int[]>();
        private Dictionary<string, int[]> nDistinctMappingsPerLaneAndBc = new Dictionary<string, int[]>();
        private Barcodes barcodes;
        private int currentBcIdx;
        private int lastNMappedReads;
        private int lastNUniqMols;
        private int lastNDistinctMappings;

        public PerLaneStats(Barcodes barcodes)
        {
            this.barcodes = barcodes;
        }

        public void BeforeFile(int bcIdx, int beforeNBcMappedReads, int beforeNBcUniqMols, int beforeNBcDistinctMappings)
        {
            lastNUniqMols = beforeNBcUniqMols;
            lastNMappedReads = beforeNBcMappedReads;
            lastNDistinctMappings = beforeNBcDistinctMappings;
            currentBcIdx = bcIdx;
        }

        public void AfterFile(string mapFilePath, int currentNBcMappedReads, int currentNBcUniqMols, int currentNDistinctMappings)
        {
            if (Regex.Match(mapFilePath, "chr[sa]").Success)
                return; // Do not analyze mappings to splices
            string runLane = SetupRunLane(mapFilePath);
            int laneNMappedReads = currentNBcMappedReads - lastNMappedReads;
            nMappedReadsPerLaneAndBc[runLane][currentBcIdx] += laneNMappedReads;
            int laneNUniqMols = currentNBcUniqMols - lastNUniqMols;
            nUniqueMolsPerLaneAndBc[runLane][currentBcIdx] += laneNUniqMols;
            int laneNDistinctMappings = currentNDistinctMappings - lastNDistinctMappings;
            nDistinctMappingsPerLaneAndBc[runLane][currentBcIdx] += laneNDistinctMappings;
        }

        private string SetupRunLane(string mapFilePath)
        {
            Match m = Regex.Match(mapFilePath, "(Run[0-9]+_L[0-9])_");
            string runLane = m.Groups[1].Value;
            if (!nMappedReadsPerLaneAndBc.ContainsKey(runLane))
            {
                nMappedReadsPerLaneAndBc[runLane] = new int[barcodes.Count];
                nUniqueMolsPerLaneAndBc[runLane] = new int[barcodes.Count];
                nDistinctMappingsPerLaneAndBc[runLane] = new int[barcodes.Count];
            }
            return runLane;
        }

        public List<Pair<string, double>> GetUniqueMolsPerMappedReads(int bcIdx)
        {
            return GetBcFractions(bcIdx, nMappedReadsPerLaneAndBc);
        }
        public List<Pair<string, double>> GetDistinctMappingsPerMappedReads(int bcIdx)
        {
            return GetBcFractions(bcIdx, nDistinctMappingsPerLaneAndBc);
        }
        private List<Pair<string, double>> GetBcFractions(int bcIdx, Dictionary<string, int[]> dataSet)
        {
            List<Pair<string, double>> result = new List<Pair<string, double>>();
            foreach (string runLane in dataSet.Keys)
            {
                double f = dataSet[runLane][bcIdx] / (double)nMappedReadsPerLaneAndBc[runLane][bcIdx];
                if (double.IsNaN(f)) f = 0.0;
                result.Add(new Pair<string, double>(runLane, f));
            }
            return result;
        }

        public double GetMeanOfHighestLaneFracs()
        {
            string[] runLanes = nUniqueMolsPerLaneAndBc.Keys.ToArray();
            DescriptiveStatistics ds = new DescriptiveStatistics();
            for (int bcIdx = 0; bcIdx < barcodes.Count; bcIdx++)
            {
                double max = 0.0;
                foreach (string runLane in runLanes)
                {
                    double v = (barcodes.HasRandomBarcodes) ?
                        nUniqueMolsPerLaneAndBc[runLane][bcIdx] / (double)nMappedReadsPerLaneAndBc[runLane][bcIdx] :
                        nDistinctMappingsPerLaneAndBc[runLane][bcIdx] / (double)nMappedReadsPerLaneAndBc[runLane][bcIdx];
                    if (!double.IsNaN(v)) max = Math.Max(max, v);
                }
                ds.Add(max);
            }
            return ds.Mean();
        }
    }
}
