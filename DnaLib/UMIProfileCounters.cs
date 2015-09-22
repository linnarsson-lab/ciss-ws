using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public class UMIHammingDistCalculator
    {
        private int nUMIBits;
        private Dictionary<int, int> dists = new Dictionary<int, int>();

        public UMIHammingDistCalculator(int UMILen)
        {
            nUMIBits = UMILen * 2;
            int nUMIs = (int)Math.Pow(4, UMILen);
            for (int i1 = 0; i1 < nUMIs; i1++)
            {
                for (int i2 = 0; i2 < nUMIs; i2++)
                {
                    int dist = 0;
                    for (int p = 0; p < nUMIBits; p += 2)
                        if (((i1 >> p) & 3) != ((i2 >> p) & 3)) dist++;
                    dists[(i1 << nUMIBits) | i2] = dist;
                }
            }
        }
        public int Dist(int UMI1, int UMI2)
        {
            return dists[(UMI1 << nUMIBits) | UMI2];
        }
    }

    public enum UMICountType { Reads, AllMolecules, NonSingletonMolecules, NonMutatedSingletonMolecules };

    public interface IUMIProfile
    {
        /// <summary>
        /// Return true if using UMI and the current read is a singleton
        /// </summary>
        /// <param name="UMIIdx"></param>
        /// <returns></returns>
        bool Add(int UMIIdx);
        bool UMIOccupied(int UMIIdx);
        int nMols();
        int nNonSingletonMols();
        int nNonMutationSingletonMols();
        int count(UMICountType ct);
        IEnumerable<int> OccupiedUMIs();
    }

    public class UMIReadCountProfile : IUMIProfile
    {
        private static UMIHammingDistCalculator hCalc;
        private static int hDistThres;
        public static void SetUMIHammingDistCalculator(UMIHammingDistCalculator hCalculator, int distThreshold)
        {
            hCalc = hCalculator;
            hDistThres = distThreshold;
        }

        private ushort[] detectedUMIs;

        public UMIReadCountProfile(int nUMIs)
        {
            if (nUMIs > 0)
            {
                detectedUMIs = new ushort[nUMIs];
            }
        }
        public bool Add(int UMIIdx)
        {
            if (detectedUMIs == null) return false;
            detectedUMIs[UMIIdx]++;
            return detectedUMIs[UMIIdx] == 1;
        }
        public bool UMIOccupied(int UMIIdx)
        {
            return detectedUMIs[UMIIdx] > 0;
        }
        public IEnumerable<int> OccupiedUMIs()
        {
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i] > 0) yield return i;
        }
        public int nMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i] > 0) n++;
            return n;
        }
        public int nNonSingletonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i] > 1) n++;
            return n;
        }
        public int nNonMutationSingletonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
            {
                if (detectedUMIs[i] > 1) n++;
                else if (detectedUMIs[i] == 1)
                {
                    bool tooClose = false;
                    for (int j = 0; j < detectedUMIs.Length; j++)
                    {
                        if (j == i) continue;
                        bool close = (hCalc.Dist(i, j) < hDistThres);
                        if (close && (detectedUMIs[j] > 1 || (detectedUMIs[j] == 1 && i > j)))
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) n++;
                }
            }
            return n;
        }
        public int count(UMICountType ct)
        {
            return (ct == UMICountType.Reads) ? detectedUMIs.Sum(v => v)
                : (ct == UMICountType.AllMolecules) ? nMols()
                : (ct == UMICountType.NonSingletonMolecules) ? nNonSingletonMols() : nNonMutationSingletonMols();
        }
        public IEnumerable<ushort> IterReadsPerMol()
        {
            foreach (ushort nReads in detectedUMIs)
                if (nReads > 0) yield return nReads;
        }
    }

    public class UMIZeroOneMoreProfile : IUMIProfile
    {
        private static UMIHammingDistCalculator hCalc;
        private static int hDistThres;
        public static void SetUMIHammingDistCalculator(UMIHammingDistCalculator hCalculator, int distThreshold)
        {
            hCalc = hCalculator;
            hDistThres = distThreshold;
        }

        private int detectedReads;
        private BitArray detectedUMIs;
        private BitArray multitonUMIs;

        public UMIZeroOneMoreProfile(int nUMIs)
        {
            if (nUMIs > 0)
            {
                detectedUMIs = new BitArray(nUMIs);
                multitonUMIs = new BitArray(nUMIs);
            }
        }
        public bool Add(int UMIIdx)
        {
            detectedReads++;
            if (detectedUMIs == null) return false;
            if (detectedUMIs[UMIIdx])
                multitonUMIs[UMIIdx] = true;
            detectedUMIs[UMIIdx] = true;
            return multitonUMIs[UMIIdx] == false;
        }
        public bool UMIOccupied(int UMIIdx)
        {
            return detectedUMIs[UMIIdx] || multitonUMIs[UMIIdx];
        }
        public IEnumerable<int> OccupiedUMIs()
        {
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i]) yield return i;
        }
        public int nMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i]) n++;
            return n;
        }
        public int nNonSingletonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (multitonUMIs[i]) n++;
            return n;
        }
        public int nNonMutationSingletonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
            {
                if (multitonUMIs[i]) n++;
                else if (detectedUMIs[i])
                {
                    bool tooClose = false;
                    for (int j = 0; j < detectedUMIs.Length; j++)
                    {
                        if (j == i) continue;
                        bool close = (hCalc.Dist(i, j) < hDistThres);
                        if (close && (multitonUMIs[j] || (detectedUMIs[j] && i > j)))
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (!tooClose) n++;
                }
            }
            return n;
        }
        public int count(UMICountType ct)
        {
            return (ct == UMICountType.Reads) ? detectedReads
                : (ct == UMICountType.AllMolecules) ? nMols()
                : (ct == UMICountType.NonSingletonMolecules) ? nNonSingletonMols() : nNonMutationSingletonMols();
        }
    }
}
