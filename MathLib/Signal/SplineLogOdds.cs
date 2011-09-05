using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public class SplineLogOdds : LogOdds
    {
        int nBins = 25;
        private List<double> posData;
        private List<double> negData;
        public CubicSpline posCubicSpline;
        public CubicSpline negCubicSpline;
        public double normFactor;
        public double maxLogOdds;
        public double intensityAtMaxLogOdds = 0.0;
        public double minLogOdds;
        public double intensityAtMinLogOdds = 0.0;
        private double maxPosInt, maxNegInt, minPosInt = 1.0e10, minNegInt = 1.0e10;

        public SplineLogOdds(double minProb)
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
            maxPosInt = Math.Max(maxPosInt, intensity);
            minPosInt = Math.Min(minPosInt, intensity);
        }

        public override void AddNeg(float intensity)
        {
            AddNeg((double)intensity);
        }
        public override void AddNeg(double intensity)
        {
            negData.Add(intensity);
            maxNegInt = Math.Max(maxNegInt, intensity);
            minNegInt = Math.Min(minNegInt, intensity);
        }

        public override double GetMaxX()
        {
            return Math.Max(maxPosInt, maxNegInt);
        }
        public override double GetMinX()
        {
            return Math.Min(minPosInt, minNegInt);
        }

        public override double GetLogOdds(double intensity)
        {
            if (negData.Count == 0) return 0.0;
            if (intensity < intensityAtMinLogOdds) return minLogOdds;
            if (intensity > intensityAtMaxLogOdds) return maxLogOdds;
            return normFactor * Math.Max(0.0001, posCubicSpline[intensity]) / Math.Max(0.0001, negCubicSpline[intensity]);
        }
        
        public CubicSpline MakeCubicSpline(List<double> data, double minX, double maxX)
        {
            Histogram autoHistogram = Histogram.CreateAutoBinnedInPlace(data, nBins, false);
            double[] xValues = new double[nBins + 2];
            double[] yValues = new double[nBins + 2];
            xValues[0] = minX; //autoHistogram.Bins[0].LowerBound;
            yValues[0] = autoHistogram.Bins[0].Density;
            for (int i = 0; i < nBins; i++)
            {
                xValues[i + 1] = autoHistogram.Bins[i].Middle;
                yValues[i + 1] = autoHistogram.Bins[i].Density;
            }
            xValues[nBins + 1] = maxX; // autoHistogram.Bins[autoHistogram.Bins.Count - 1].UpperBound;
            yValues[nBins + 1] = autoHistogram.Bins[autoHistogram.Bins.Count - 1].Density;
            return new CubicSpline(xValues, yValues);
        }

        public override void Calculate()
		{
            if (negData.Count == 0) return;
            intensityAtMaxLogOdds = Math.Max(maxPosInt, maxNegInt);
            intensityAtMinLogOdds = Math.Min(minPosInt, minNegInt);
            posCubicSpline = MakeCubicSpline(posData, intensityAtMinLogOdds, intensityAtMaxLogOdds);
            negCubicSpline = MakeCubicSpline(negData, intensityAtMinLogOdds, intensityAtMaxLogOdds);
			double posNormFactor = Normalize(posCubicSpline);
			double negNormFactor = Normalize(negCubicSpline);
            normFactor = posNormFactor / negNormFactor;
            ScoreTracker<double, double> tracker = new ScoreTracker<double, double>();
            double step = (intensityAtMaxLogOdds - intensityAtMinLogOdds) / 200.0;
            for (double x = intensityAtMinLogOdds; x <= intensityAtMaxLogOdds; x += step)
            {
                tracker.Examine(GetLogOdds(x), x);
            }
            intensityAtMaxLogOdds = tracker.MaxItem;
            maxLogOdds = tracker.MaxScore;
            intensityAtMinLogOdds = tracker.MinItem;
            minLogOdds = tracker.MinScore;
		}

		private double Normalize(CubicSpline cs)
		{
            double normFactor = 1.0 / cs.Integral();
            return normFactor;
		}

        public override double GetOverlap()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
