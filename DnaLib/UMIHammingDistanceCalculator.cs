using System;
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

}
