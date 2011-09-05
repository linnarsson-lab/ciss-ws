using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Linnarsson.Mathematics
{
	/// <summary>
	/// A LodGram is a function giving the log-odds ratio of two histograms. The
	/// histograms are assumed to have bins ranging from 0 to 255.
	/// </summary>
	public class HistogramLogOdds : LogOdds
	{
        private int m_NBins = 100;
        public int NBins
        {
            get { return m_NBins; }
            set { m_NBins = value; }
        }

        private double m_NBinLimitsPercentileFromExtremes = 0.1;
        public double BinLimitsPercentileFromExtremes
        {
            get { return m_NBinLimitsPercentileFromExtremes; }
            set { m_NBinLimitsPercentileFromExtremes = value; }
        }

        private double m_NBinLimitsFractionalExtension = 0.0;
        public double BinLimitsFractionalExtension
        {
            get { return m_NBinLimitsFractionalExtension; }
            set { m_NBinLimitsFractionalExtension = value; }
        }

		private double[] m_LogOdds = new double[] { 0.0 };
		public double[] LogOdds
		{
			get { return m_LogOdds; }
			set { m_LogOdds = value; }
		}

        public int[] posHistogram;
        public int[] negHistogram;

        private List<float> posData;
        private List<float> negData;
        private float binWidth;
        private float minLimit;

        public HistogramLogOdds(double minProb)
        {
            MinProbability = minProb;
            posData = new List<float>();
            negData = new List<float>();
            posHistogram = new int[m_NBins];
            negHistogram = new int[m_NBins];
        }

        public override double GetLogOdds(double intensity)
        {
            int bin = (int)Math.Round((intensity - minLimit) / binWidth);
            bin = Math.Min(NBins - 1, Math.Max(0, bin));
            return m_LogOdds[bin];
        }

		public override string ToString()
		{
			StringBuilder sb = new StringBuilder();
			for (int bin = 0; bin < NBins; bin += 1)
			{
				if (bin >= m_LogOdds.Length) break;
				sb.Append(m_LogOdds[bin]);
				sb.Append('\t');
			}
			for (int bin = 0; bin < NBins; bin += 1)
			{
				if (bin >= posHistogram.Length) break;
				sb.Append(posHistogram[bin]);
				sb.Append('\t');
			}
			for (int bin = 0; bin < NBins; bin += 1)
			{
				if (bin >= negHistogram.Length) break;
				sb.Append(negHistogram[bin]);
				sb.Append('\t');
			}
			return sb.ToString();
		}

        public override void AddNeg(double intensity)
        {
            AddNeg((float)intensity);
        }
        public override void AddNeg(float value)
        {
            negData.Add(value);
        }

        public override void AddPos(double intensity)
        {
            AddPos((float)intensity);
        }
        public override void AddPos(float value)
        {
            posData.Add(value);
        }

        public override double GetMaxX()
        {
            int i;
            for (i = m_NBins - 1; i >= 1; i--)
            {
                if (posHistogram[i] > 0 || negHistogram[i] > 0)
                    break;
            }
            return (double)i;
        }
        public override double GetMinX()
        {
            int i;
            for (i = 0; i < m_NBins; i++)
            {
                if (posHistogram[i] > 0 || negHistogram[i] > 0)
                    break;
            }
            return (double)i;
        }

        public override void Calculate()
		{
			if (posData.Count == 0 && negData.Count == 0) return;

            float[] sortArray = new float[posData.Count + negData.Count];
            posData.ToArray().CopyTo(sortArray, 0);
            negData.ToArray().CopyTo(sortArray, posData.Count);
            Array.Sort(sortArray);
            int upperPos = (int)Math.Round(sortArray.Length * (1.0 - m_NBinLimitsPercentileFromExtremes));
            float maxLimit = (float)(sortArray[upperPos] * (1.0 + m_NBinLimitsFractionalExtension));
            int lowerPos = (int)Math.Round(sortArray.Length * m_NBinLimitsPercentileFromExtremes);
            minLimit = (float)(sortArray[lowerPos] * (1.0 - m_NBinLimitsFractionalExtension));
            binWidth = (maxLimit - minLimit) / m_NBins;
            MakeHistogram(posData, ref posHistogram);
            MakeHistogram(negData, ref negHistogram);
            posData = null;
            negData = null;
            double[] normPosHistogram = Normalize(posHistogram);
			double[] normNegHistogram = Normalize(negHistogram);

			LogOdds = new double[m_NBins];
			for (int ix = 0; ix < m_NBins; ix++)
			{
				LogOdds[ix] = Math.Log10(normPosHistogram[ix] / normNegHistogram[ix]);
			}

            // Find max and min
		    ScoreTracker<double, int> tracker = new ScoreTracker<double, int>();
            for (int ix = 5; ix <= m_NBins - 5; ix++)
            {
				tracker.Examine(LogOdds[ix], ix);
			}

			// Clamp values below min and above max
			for (int ix = 0; ix < m_NBins; ix++)
			{
				if (ix > tracker.MaxItem) LogOdds[ix] = tracker.MaxScore;
				if (ix < tracker.MinItem) LogOdds[ix] = tracker.MinScore;
			}

        }


        private void MakeHistogram(List<float> data, ref int[] histogram)
        {
            foreach (float relIntensity in data)
            {
                int binNumber = (int)Math.Round((relIntensity - minLimit) / binWidth);
                if (binNumber < 1) binNumber = 1;
                else if (binNumber >= m_NBins) binNumber = m_NBins - 1;
                histogram[binNumber]++;
            }
        }
        
        private double[] Normalize(int[] histo)
		{
			double[] histo_norm = new double[m_NBins];
			double sum = 0.0;
			for (int ix = 0; ix < m_NBins; ix++)
			{
				sum += histo[ix];
			}
            for (int ix = 0; ix < m_NBins; ix++)
			{
				histo_norm[ix] = Math.Max(MinProbability, histo[ix] / sum);
			}
			return histo_norm;
		}

        public override double GetOverlap()
        {
            double posSum = GetCount(posHistogram);
            double negSum = GetCount(negHistogram);
            double minCommon = 0.0;
            for (int j = 0; j < posHistogram.Length; j++)
            {
                double normPos = posHistogram[j] / posSum;
                double normNeg = negHistogram[j] / negSum;
                minCommon += Math.Min(normPos, normNeg);
            }
            double overlap = minCommon / (2.0 - minCommon);
            return overlap;
        }
        private int GetCount(int[] histo)
        {
            int count = 0;
            foreach (int n in histo) count += n;
            return count;
        }
    }
}
