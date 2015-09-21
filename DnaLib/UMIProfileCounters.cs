using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Dna
{
    public enum UMICountType { Reads, AllMolecules, NonSingeltonMolecules };

    public interface IUMIProfile
    {
        /// <summary>
        /// Return true if using UMI and the current read is a singleton
        /// </summary>
        /// <param name="UMIIdx"></param>
        /// <returns></returns>
        bool Add(int UMIIdx);
        int nMols();
        int nNonSingeltonMols();
        int count(UMICountType ct);
        IEnumerable<int> OccupiedUMIs();
    }

    public class UMIReadCountProfile : IUMIProfile
    {
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
        public int nNonSingeltonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (detectedUMIs[i] > 1) n++;
            return n;
        }
        public int count(UMICountType ct)
        {
            return (ct == UMICountType.Reads) ? detectedUMIs.Sum(v => v) : (ct == UMICountType.AllMolecules) ? nMols() : nNonSingeltonMols();
        }
        public IEnumerable<ushort> IterReadsPerMol()
        {
            foreach (ushort nReads in detectedUMIs)
                if (nReads > 0) yield return nReads;
        }
    }

    public class UMIZeroOneMoreProfile : IUMIProfile
    {
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
        public int nNonSingeltonMols()
        {
            int n = 0;
            for (int i = 0; i < detectedUMIs.Length; i++)
                if (multitonUMIs[i]) n++;
            return n;
        }
        public int count(UMICountType ct)
        {
            return (ct == UMICountType.Reads) ? detectedReads : (ct == UMICountType.AllMolecules) ? nMols() : nNonSingeltonMols();
        }
    }
}
