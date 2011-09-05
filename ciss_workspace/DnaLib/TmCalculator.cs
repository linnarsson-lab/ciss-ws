using System;
using System.Collections.Generic;

namespace Linnarsson.Dna
{
    public class TmCalculator
    {
        private Dictionary<string, double> dH;
        private Dictionary<string, double> dS;

        public TmCalculator()
        {
            dH = new Dictionary<string, double>();
            dH.Add("AA", -9.1);
            dH.Add("TT", -9.1);
            dH.Add("AT", -8.6);
            dH.Add("TA", -6.0);
            dH.Add("CA", -5.8);
            dH.Add("TG", -5.8);
            dH.Add("GT", -6.5);
            dH.Add("AC", -6.5);
            dH.Add("CT", -7.8);
            dH.Add("AG", -7.8);
            dH.Add("GA", -5.6);
            dH.Add("TC", -5.6);
            dH.Add("CG", -11.9);
            dH.Add("GC", -11.1);
            dH.Add("GG", -11.0);
            dH.Add("CC", -11.0);
            dS = new Dictionary<string, double>();
            dS.Add("AA", -24.0);
            dS.Add("TT", -24.0);
            dS.Add("AT", -23.9);
            dS.Add("TA", -16.9);
            dS.Add("CA", -12.9);
            dS.Add("TG", -12.9);
            dS.Add("GT", -17.3);
            dS.Add("AC", -17.3);
            dS.Add("CT", -20.8);
            dS.Add("AG", -20.8);
            dS.Add("GA", -13.5);
            dS.Add("TC", -13.5);
            dS.Add("CG", -27.8);
            dS.Add("GC", -26.7);
            dS.Add("GG", -26.6);
            dS.Add("CC", -26.6);
        }

		public double GetTm(DnaSequence s, double conc1, double conc2, double saltConc)
        {
            return s.Count < 50 ? GetNNTm(s, conc1, conc2, saltConc) : GetLongOligoTm(s, saltConc, 0.0);
        }

        /// <summary>
        /// Uses nearest neighbout method - for oligos less than 50 bases
        /// </summary>
        /// <param name="s"></param>
        /// <param name="conc1"></param>
        /// <param name="conc2"></param>
        /// <param name="saltConc"></param>
        /// <returns></returns>
		public double GetNNTm(DnaSequence s, double conc1, double conc2, double saltConc)
        {
            double dSSum = 0.0;
            double dHSum = 0.0;
            for (long i = 0; i < s.Count - 1; i++)
            {
                string duplet = s.SubSequence(i, 2).ToString();
                dHSum += dH[duplet];
                dSSum += dS[duplet];
            }
            double conc;
            if (conc2 == 0.0)
                conc = conc1 / 4;
            else if (conc1 > conc2)
                conc = conc1 - conc2 / 2;
            else
                conc = conc2 - conc1 / 2;
            double Tm = 1000 * dHSum / (-10.8 + dSSum + 1.987 * Math.Log(conc))
                        - 273.15 + 16.6 * Math.Log10(saltConc);
            return Tm;
        }

        /// <summary>
        /// Use for oligos longer than 50 bases
        /// </summary>
        /// <param name="s"></param>
        /// <param name="saltConc"></param>
        /// <param name="formamideConc"></param>
        /// <returns></returns>
		public double GetLongOligoTm(DnaSequence s, double saltConc, double formamideConc)
        {
            double GCFraction = (double)s.CountCases(IupacEncoding.GC) / s.Count;
            double Tm = 81.5 + 16.6 * Math.Log10(saltConc) + 41 * GCFraction 
                        - 500 / (double)s.Count - 0.62 * formamideConc;
            return Tm;
        }
    }
}
