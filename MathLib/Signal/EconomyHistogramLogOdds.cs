using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Linnarsson.Mathematics
{
    // This is a memory saving version of HistgramLogOdds - should give approx. the same result.
    public class EconomyHistogramLogOdds : LogOdds
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

        private static int startSlotCount = 1000;
        private static double upperIntensityLimit = 4.0;
        private static double lowerIntensityLimit = 0.0;
        private double slotStep;
        public int[] posHistogram;
        public int[] negHistogram;
        private int nValues = 0;

        private float binWidth;
        private float minLimit;

        public EconomyHistogramLogOdds(double minProb)
        {
            slotStep = (startSlotCount - 1) / (upperIntensityLimit - lowerIntensityLimit);
            MinProbability = minProb;
            posHistogram = new int[startSlotCount];
            negHistogram = new int[startSlotCount];
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
            return sb.ToString();
        }

        public override void AddNeg(double value)
        {
            AddNeg((float)value);
        }
        public override void AddNeg(float value)
        {
            value = Math.Max((float)lowerIntensityLimit, value);
            value = Math.Min((float)upperIntensityLimit, value);
            int slot = (int)Math.Floor((value - lowerIntensityLimit) * slotStep);
            negHistogram[slot]++;
            nValues++;
        }
        public override void AddPos(double value)
        {
            AddPos((float)value);
        }
        public override void AddPos(float value)
        {
            value = Math.Max((float)lowerIntensityLimit, value);
            value = Math.Min((float)upperIntensityLimit, value);
            int slot = (int)Math.Floor((value - lowerIntensityLimit) * slotStep);
            posHistogram[slot]++;
            nValues++;
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
            if (nValues == 0) return;

            int upperPos = (int)Math.Round(nValues * (1.0 - m_NBinLimitsPercentileFromExtremes));
            int lowerPos = (int)Math.Round(nValues * m_NBinLimitsPercentileFromExtremes);
            int negCount = 0, posCount = 0;
            int hMax = posHistogram.Length - 1;
            int minSlot = 0, maxSlot = hMax;
            for (int slot = 0; slot < posHistogram.Length; slot++)
            {
                negCount += posHistogram[slot] + negHistogram[slot];
                posCount += posHistogram[hMax - slot] + negHistogram[hMax - slot];
                if (negCount < lowerPos) minSlot++;
                if (posCount < nValues - upperPos) maxSlot--;
            }
            minLimit = (float)(minSlot / slotStep + lowerIntensityLimit);
            float maxLimit = (float)(maxSlot /slotStep + lowerIntensityLimit);
            binWidth = (maxLimit - minLimit) / (float)m_NBins;
            int[] newNegHistogram = new int[m_NBins];
            int[] newPosHistogram = new int[m_NBins];
            MakeHistogram(posHistogram, minSlot, maxSlot, ref newPosHistogram);
            MakeHistogram(negHistogram, minSlot, maxSlot, ref newNegHistogram);

            double[] normPosHistogram = Normalize(newPosHistogram);
            double[] normNegHistogram = Normalize(newNegHistogram);

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

        private void MakeHistogram(int[]oldHistogram, int minSlot, int maxSlot, ref int[] newHistogram)
        {
            double oldSlotStep = (maxSlot - minSlot) / (double)newHistogram.Length;
            for (int newSlot = 0; newSlot < newHistogram.Length; newSlot++)
            {
                double leftSlot = minSlot + newSlot * oldSlotStep;
                int leftSlotFloor = (int)Math.Floor(leftSlot);
                double leftPart = 0.0;
                if (newSlot == 0)
                {
                    for (int l = 0; l <= leftSlotFloor; l++)
                        leftPart += oldHistogram[l];
                }
                else
                    leftPart = oldHistogram[leftSlotFloor] * (leftSlot - (double)leftSlotFloor);
                double rightSlot = leftSlot + oldSlotStep;
                int rightSlotFloor = (int)Math.Floor(rightSlot);
                double rightPart = 0.0;
                if (newSlot == newHistogram.Length - 1)
                {
                    for (int r = rightSlotFloor; r < oldHistogram.Length; r++)
                        rightPart += oldHistogram[r];
                }                
                else
                    rightPart = oldHistogram[rightSlotFloor] * ((double)rightSlotFloor + 1.0 - rightSlot);
                double newValue = leftPart + rightPart;
                for (int j = leftSlotFloor + 1; j < rightSlotFloor; j++)
                    newValue += oldHistogram[j];
                newHistogram[newSlot] = (int)newValue;
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
