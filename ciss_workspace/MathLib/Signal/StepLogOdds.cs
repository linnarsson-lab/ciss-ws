using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public class StepLogOdds : LogOdds
    {
        private List<double> posData;
        private List<double> negData;
        public double normFactor;
        public double intensityAtMaxLogOdds;
        public double intensityAtMinLogOdds = 1.0e10;
        public double[] preCalcHistogram;
        public double step;
        int nPreCalcBins = 1000;

        public StepLogOdds(double minProb)
        {
            MinProbability = minProb;
            posData = new List<double>();
            negData = new List<double>();
        }

        public override void AddPos(float intensity)
        {
            AddPos((double) intensity);
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
            if (negData.Count == 0) return 0.0;
            if (intensity <= intensityAtMinLogOdds) return preCalcHistogram[0];
            if (intensity >= intensityAtMaxLogOdds) return preCalcHistogram[nPreCalcBins - 1];
            int bin = (int)Math.Round((intensity - intensityAtMinLogOdds) / step);
            bin = Math.Min(nPreCalcBins - 1, bin);
            return preCalcHistogram[bin];
        }

        public void MakeHistgram(List<double> data, out double[] xValues, out double[] yValues)
        {
            int nBins = 50;
            Histogram histo = Histogram.CreateAutoBinnedInPlace(data, nBins, false);
            xValues = new double[nBins];
            yValues = new double[nBins];
            for (int i = 0; i < nBins; i++)
            {
                xValues[i] = histo.Bins[i].Middle;
                yValues[i] = histo.Bins[i].Density;
            }
        }

        private double Interpolate(double x, double[] xValues, double[] yValues)
        {
            if (x <= xValues[0]) return yValues[0];
            if (x >= xValues[xValues.Length-1]) return yValues[yValues.Length - 1];
            int pos = Array.BinarySearch<double>(xValues, x);
            if (pos < 0)
                pos = ~pos;
            if (pos == 0) pos++;
            double d = xValues[pos] - xValues[pos - 1];
            double y = yValues[pos - 1] * (xValues[pos] - x) / d + yValues[pos] * (x - xValues[pos - 1]) / d;
            return y;
        }

        public override void Calculate()
        {
            if (negData.Count == 0) return;
            double[] xPosValues, yPosValues, xNegValues, yNegValues;
            MakeHistgram(posData, out xPosValues, out yPosValues);
            MakeHistgram(negData, out xNegValues, out yNegValues);
            double posNormFactor = GetNormFactor(xPosValues, yPosValues, intensityAtMinLogOdds, intensityAtMaxLogOdds);
            double negNormFactor = GetNormFactor(xNegValues, yNegValues, intensityAtMinLogOdds, intensityAtMaxLogOdds);
            normFactor = posNormFactor / negNormFactor;
            ScoreTracker<double, int> tracker = new ScoreTracker<double, int>();
            preCalcHistogram = new double[nPreCalcBins];
            step = (intensityAtMaxLogOdds - intensityAtMinLogOdds) / nPreCalcBins;
            /*System.IO.StreamWriter sw = new System.IO.StreamWriter("c:\\Splines\\PNCurves.txt", true);
            for (int bin = 0; bin < nPreCalcBins; bin+=10)
                sw.Write("{0}\t", normFactor * Interpolate(intensityAtMinLogOdds + bin * step, xPosValues, yPosValues));
            sw.WriteLine();
            for (int bin = 0; bin < nPreCalcBins; bin+=10)
                sw.Write("{0}\t", Interpolate(intensityAtMinLogOdds + bin * step, xNegValues, yNegValues));
            sw.WriteLine();
            sw.Close();*/
            for (int bin = 0; bin < nPreCalcBins; bin++)
            {
                double x = intensityAtMinLogOdds + bin * step;
                double y = Math.Log10(normFactor * 
                                       Interpolate(x, xPosValues, yPosValues) / Interpolate(x, xNegValues, yNegValues));
                preCalcHistogram[bin] = y;
                tracker.Examine(y, bin);

            }
            for (int bin = tracker.MaxItem; bin < nPreCalcBins; bin++)
                preCalcHistogram[bin] = tracker.MaxScore;
            for (int bin = 0; bin < tracker.MinItem; bin++)
                preCalcHistogram[bin] = tracker.MinScore;
        }

        private double GetNormFactor(double[] xValues, double[] yValues, double start, double end)
        {
            double sum = 0.0;
            for (int i = 1; i < xValues.Length; i++)
            {
                sum += xValues[i] * yValues[i];
            }
            if (start < xValues[0]) sum += (xValues[0] - start) * yValues[0];
            if (end > xValues[xValues.Length- 1]) sum += (end - xValues[xValues.Length - 1]) * yValues[yValues.Length - 1];
            double normFactor = 1.0 / sum;
            return normFactor;
        }

        public override double GetOverlap()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
