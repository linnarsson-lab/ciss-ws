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

        /// <summary>
        /// Average efficiency of spike detection (UMI-corrected observed / true added) in each barcode
        /// </summary>
        private double[] m_LabelingEfficiencyByBc;
        public double[] LabelingEfficiencyByBc { get { return m_LabelingEfficiencyByBc; } private set { m_LabelingEfficiencyByBc = value; } }

        /// <summary>
        /// Efficiency of detection of each individual spike (UMI-corrected observed / true added) in each barcode
        /// </summary>
        public Dictionary<string, double[]> efficiencyBySpike = new Dictionary<string, double[]>();

        private Dictionary<string, double> fractionOfSpikeMols = new Dictionary<string, double>();

        /// <summary>
        /// Maximum number of UMIs used anywhere in each barcode, after singleton/mutation filtering
        /// </summary>
        public int[] maxOccupiedUMIsByBc;
        private int currentMaxOccupiedUMIs = 0;

        public LabelingEfficiencyEstimator(Barcodes barcodes, string spikeConcFile, int totalAddedSpikeMols)
        {
            this.totalAddedSpikeMols = totalAddedSpikeMols;
            UMICount = barcodes.UMICount;
            LabelingEfficiencyByBc = new double[barcodes.Count];
            maxOccupiedUMIsByBc = new int[barcodes.Count];
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
                    double nSpikeMols = gf.TrHits(bcIdx);
                    efficiencyBySpike[gf.NonVariantName][bcIdx] = nSpikeMols / AddedCount(gf.NonVariantName);
                    nMols += nSpikeMols;
                }
            }
            currentBcLabelingEfficiency = nMols / totalAddedSpikeMols;
            LabelingEfficiencyByBc[bcIdx] = currentBcLabelingEfficiency;
        }

        public void FinishBarcode(int bcIdx)
        {
            maxOccupiedUMIsByBc[bcIdx] = currentMaxOccupiedUMIs;
            currentMaxOccupiedUMIs = 0;
        }

        /// <summary>
        /// Calculate the estimated true number of molecule, taking efficiency and UMI collisions into account. Exhaustion of library assumed.
        /// Also keep track of the maximal number of UMIs occupied.
        /// </summary>
        /// <param name="observedMolCount">Number of observed UMIs (after singleton/mutation filter)</param>
        /// <returns></returns>
        public int EstimateTrueCount(int observedMolCount)
        {
            if (observedMolCount > currentMaxOccupiedUMIs)
                currentMaxOccupiedUMIs = observedMolCount;
            return (int)Math.Round(Math.Log(1.0 - observedMolCount / UMICount) / Math.Log(1.0 - currentBcLabelingEfficiency / UMICount));
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
