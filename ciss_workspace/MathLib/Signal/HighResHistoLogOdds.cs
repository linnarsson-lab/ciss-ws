using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public class HighResHistoLogOdds : LogOdds
    {
        private List<double> posData;
        private List<double> negData;
        public double intensityAtMaxLogOdds;
        public double intensityAtMinLogOdds = 1.0e10;
        public double[] preCalcHistogram;
        public double step;
        int nPreCalcBins = 256;

        public HighResHistoLogOdds(double minProb)
        {
            MinProbability = minProb;
            posData = new List<double>();
            negData = new List<double>();
        }

        public override void AddPos(float intensity)
        {
            AddPos((double)intensity);
        }
        public override void AddPos(double intensity)
        {
            posData.Add(intensity);
            intensityAtMaxLogOdds = Math.Max(intensityAtMaxLogOdds, intensity);
            intensityAtMinLogOdds = Math.Min(intensityAtMinLogOdds, intensity);
        }

        public override void AddNeg(float intensity)
        {
            AddNeg((double)intensity);
        }
        public override void AddNeg(double intensity)
        {
            negData.Add(intensity);
            intensityAtMaxLogOdds = Math.Max(intensityAtMaxLogOdds, intensity);
            intensityAtMinLogOdds = Math.Min(intensityAtMinLogOdds, intensity);
        }

        public override double GetMaxX()
        {
            return intensityAtMaxLogOdds;
        }
        public override double GetMinX()
        {
            return intensityAtMinLogOdds;
        }

        public override double GetLogOdds(double intensity)
        {
            //if (negData.Count == 0) return 0.0;
            if (intensity <= intensityAtMinLogOdds) return preCalcHistogram[0];
            //if (intensity >= intensityAtMaxLogOdds) return preCalcHistogram[nPreCalcBins - 1];
            int bin = (int)Math.Round((intensity - intensityAtMinLogOdds) / step);
            bin = Math.Min(nPreCalcBins - 1, bin);
            return preCalcHistogram[bin];
        }

        public double[] MakeHistogram(List<double> data)
        {
            Histogram histo = Histogram.Create(data, intensityAtMinLogOdds, intensityAtMaxLogOdds, nPreCalcBins, true);
            double[] yValues = new double[nPreCalcBins];
            for (int i = 0; i < nPreCalcBins; i++)
            {
                yValues[i] = histo.Bins[i].Count;
            }
            return yValues;
        }

        private double[] Smooth(double[] histo)
        {
            double[] smoothHisto = new double[histo.Length];
            int width = (int)Math.Round(nPreCalcBins / 40.0);
            for (int i = 0; i < histo.Length; i++)
            {
                double sum = 0.0;
                for (int j = Math.Max(0, i - width); j <= Math.Min(histo.Length - 1, i + width); j++)
                    sum += histo[j];
                double smoothValue = sum /(double)(width * 2 + 1);
                smoothHisto[i] = smoothValue;
            }
            return smoothHisto;
        }

        private double[] Normalize(double[] histo)
        {
            double dynamicMinProb = 5.0 / (double)posData.Count;
            double[] histo_norm = new double[histo.Length];
            double sum = 0.0;
            foreach (double v in histo)
                sum += v;
            for (int ix = 0; ix < histo.Length; ix++)
            {
                histo_norm[ix] = Math.Max(dynamicMinProb, histo[ix] / sum);
            }
            return histo_norm;
        }

        public override void Calculate()
        {
            if (negData.Count == 0) return;
            double[] posHisto = MakeHistogram(posData);
            double[] negHisto = MakeHistogram(negData);
            double[] smoothPosHisto = Smooth(posHisto);
            double[] smoothNegHisto = Smooth(negHisto);
            double[] normPosHisto = Normalize(smoothPosHisto);
            double[] normNegHisto = Normalize(smoothNegHisto);
            preCalcHistogram = new double[smoothPosHisto.Length];
            for (int ix = 0; ix < preCalcHistogram.Length; ix++)
            {
                preCalcHistogram[ix] = Math.Log10(normPosHisto[ix] / normNegHisto[ix]);
            }
            ScoreTracker<double, int> tracker = new ScoreTracker<double, int>();
            for (int ix = 0; ix < preCalcHistogram.Length; ix++)
            {
                tracker.Examine(preCalcHistogram[ix], ix);
            }
            for (int ix = 0; ix < preCalcHistogram.Length; ix++)
            {
                if (ix > tracker.MaxItem) preCalcHistogram[ix] = tracker.MaxScore;
                if (ix < tracker.MinItem) preCalcHistogram[ix] = tracker.MinScore;
            }
            step = (intensityAtMaxLogOdds - intensityAtMinLogOdds) / nPreCalcBins;
            /*System.IO.StreamWriter sw = new System.IO.StreamWriter("c:\\Splines\\HRHPNCurves.txt", true);
            sw.Write("Pos orig");
            for (int bin = 0; bin < nPreCalcBins; bin++)
                sw.Write("\t{0}", posHisto[bin]);
            sw.Write("\nNeg orig");
            for (int bin = 0; bin < nPreCalcBins; bin++)
                sw.Write("\t{0}", negHisto[bin]);
            sw.Write("\nPos SmoothNorm");
            for (int bin = 0; bin < nPreCalcBins; bin++)
                sw.Write("\t{0}", normPosHisto[bin]);
            sw.Write("\nNeg SmoothNorm");
            for (int bin = 0; bin < nPreCalcBins; bin++)
                sw.Write("\t{0}", normNegHisto[bin]);
            sw.Write("\nLogOdds");
            for (int bin = 0; bin < nPreCalcBins; bin++)
                sw.Write("\t{0}", preCalcHistogram[bin]);
            sw.WriteLine();
            sw.Close();*/
        }


        public override double GetOverlap()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
