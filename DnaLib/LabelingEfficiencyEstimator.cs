using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
    public class LabelingEfficiencyEstimator
    {
        double totalAddedSpikeMols;
        double UMICount;
        double currentBcLabelingEfficiency = 1.0; // init to 1.0 to avoid initial math overflow on first spike set

        private double[] m_LabelingEfficiencyByBc;
        public double[] LabelingEfficiencyByBc { get { return m_LabelingEfficiencyByBc; } private set { m_LabelingEfficiencyByBc = value; } }

        public Dictionary<string, double[]> efficiencyBySpike = new Dictionary<string, double[]>();

        private Dictionary<string, double> fractionOfSpikeMols = new Dictionary<string, double>();

        public LabelingEfficiencyEstimator(Barcodes barcodes, string spikeConcFile, int totalAddedSpikeMols)
        {
            this.totalAddedSpikeMols = totalAddedSpikeMols;
            UMICount = barcodes.UMICount;
            LabelingEfficiencyByBc = new double[barcodes.Count];
            using (StreamReader reader = spikeConcFile.OpenRead())
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    string[] fields = line.Trim().Split('\t');
                    string spikeName = fields[0];
                    double spikeFraction = double.Parse(fields[1]);
                    fractionOfSpikeMols[spikeName] = spikeFraction;
                    efficiencyBySpike[spikeName] = new double[barcodes.Count];
                }
            }
        }

        public double AddedCount(string spikeName)
        {
            return fractionOfSpikeMols[spikeName] * totalAddedSpikeMols;
        }

        public void CalcEfficiencyFromSpikes(IEnumerable<GeneFeature> geneFeatures, int bcIdx)
        {
            double nMols = 0.0;
            foreach (GeneFeature gf in geneFeatures)
            {
                if (fractionOfSpikeMols.ContainsKey(gf.NonVariantName))
                {
                    double nSpikeMols = gf.TranscriptHitsByBarcode[bcIdx];
                    efficiencyBySpike[gf.NonVariantName][bcIdx] = nSpikeMols / AddedCount(gf.NonVariantName);
                    nMols += nSpikeMols;
                }
            }
            currentBcLabelingEfficiency = nMols / totalAddedSpikeMols;
            LabelingEfficiencyByBc[bcIdx] = currentBcLabelingEfficiency;
        }

        /// <summary>
        /// Calculate the estimated true number of molecule, taking efficiency and UMI collisions into account. Exhaustion of library assumed.
        /// </summary>
        /// <param name="numMolecules">Number of observed UMIs</param>
        /// <returns></returns>
        public int EstimateTrueCount(int numMolecules)
        {
            return (int)Math.Round(Math.Log(1.0 - numMolecules / UMICount) / Math.Log(1.0 - currentBcLabelingEfficiency / UMICount));
        }

        /// <summary>
        /// Estimate the number of molecule taking UMI collisions into account. Exhaustion of library assumed.
        /// </summary>
        /// <param name="numMolecules">Number of observed UMIs</param>
        /// <returns></returns>
        public int UMICollisionCompensate(int numMolecules)
        {
            return (int)Math.Round(Math.Log(1.0 - numMolecules / UMICount) / Math.Log(1.0 - 1.0 / UMICount));
        }

    }
}
