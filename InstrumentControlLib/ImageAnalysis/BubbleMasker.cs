using System;
using System.Collections.Generic;
using System.Text;
using Linnarsson.ImageAnalysis;
using Linnarsson.Mathematics;

namespace Linnarsson.ImageAnalysis
{
    public class Bubble
    {
        public int xCenter;
        public int yCenter;
        public int xSpan;
        public int ySpan;
        public bool Contains(int x, int y)
        {
            if ((x >= xCenter - xSpan && x <= xCenter + xSpan) &&
                (y >= yCenter - ySpan && y <= yCenter + ySpan))
                return true;
            return false;
        }
        public Bubble(int x, int y, int w, int h)
        {
            xCenter = x;
            yCenter = y;
            xSpan = w;
            ySpan = h;
        }
    }

    public class BubbleMasker
    {
        private int m_MinDiameter = 25;
        public int MinDiameter
        {
            get { return m_MinDiameter; }
            set { m_MinDiameter = value; }
        }
	
        private int m_SearchLineDistance = 10;
        public int SearchLineDistance
        {
            get { return m_SearchLineDistance; }
            set { m_SearchLineDistance = value; }
        }

        private int GetThresholdIntensity(MatrixUshort usm)
        {
            DescriptiveStatistics ds = new DescriptiveStatistics();
            for (int x = m_SearchLineDistance; x < usm.Rows - m_SearchLineDistance; x += m_SearchLineDistance)
            {
                for (int y = 0; y < usm.Columns; y++)
                    ds.Add((double)usm[y, x]);
            }
            ushort threshold = (ushort)(ds.Mean() + 3.0 * ds.StandardDeviation());
            return threshold;
        }

		private Pair<int, int> GetYLimits(MatrixUshort usm, int x, int ySeed, int threshold)
        {
            int yMin = ySeed;
            while (yMin > 0 && usm[yMin, x] > threshold) yMin--;
            int yMax = ySeed;
            while (yMax < usm.Columns && usm[yMax, x] > threshold) yMax++;
            return new Pair<int, int>(yMin, yMax);
        }

		private Pair<int, int> GetXLimits(MatrixUshort usm, int xSeed, int y, int threshold)
        {
            int xMin = xSeed;
            while (xMin > 0 && usm[y, xMin] > threshold) xMin--;
            int xMax = xSeed;
            while (xMax < usm.Columns && usm[y, xMax] > threshold) xMax++;
            return new Pair<int, int>(xMin, xMax);
        }

		private Bubble ExpandBubble(MatrixUshort usm, int xSeed, int ySeed, int threshold)
        {
            int xCenter = xSeed, yCenter = ySeed;
            int ySpan = 0, xSpan = 0;
            Pair<int, int> xLimits = GetXLimits(usm, xSeed, ySeed, threshold);
            Pair<int, int> yLimits = new Pair<int, int>(ySeed, ySeed);
            while (true)
            {
                bool change = false;
                Pair<int, int> bestYLimits = yLimits;
                for (int x = xLimits.First; x < xLimits.Second; x++)
                {
                    Pair<int, int> newYLimits = GetYLimits(usm, x, yCenter, threshold);
                    if (newYLimits.Second - newYLimits.First > ySpan)
                    {
                        bestYLimits = newYLimits;
                        xCenter = x;
                        ySpan = bestYLimits.Second - bestYLimits.First;
                        change = true;
                    }
                }
                yLimits = bestYLimits;
                if (change)
                {
                    Pair<int, int> bestXLimits = xLimits;
                    for (int y = yLimits.First; y < yLimits.Second; y++)
                    {
                        Pair<int, int> newXLimits = GetXLimits(usm, xCenter, y, threshold);
                        if (newXLimits.Second - newXLimits.First > xSpan)
                        {
                            bestXLimits = newXLimits;
                            yCenter = y;
                            xSpan = bestXLimits.Second - bestXLimits.First;
                            change = true;
                        }
                    }
                    xLimits = bestXLimits;
                }
                if (!change) break;
            }
            int xx = (xLimits.Second + xLimits.First) / 2;
            int yy = (yLimits.Second + yLimits.First) / 2;
            int xS = xLimits.Second - xLimits.First;
            int yS = yLimits.Second - yLimits.First;
            return new Bubble(xx, yy, xS, yS);
        }

		public List<Bubble> FindBubbles(MatrixUshort usm)
        {
            List<Bubble> bubbles = new List<Bubble>();
            int threshold = GetThresholdIntensity(usm);
            for (int x = m_SearchLineDistance; x < usm.Rows - m_SearchLineDistance; x += m_SearchLineDistance)
            {
                int height = 0;
                for (int y = 0; y < usm.Columns; y++)
                {
                    if (usm[y, x] > threshold)
                        height++;
                    else
                    {
                        if (height >= m_MinDiameter)
                        {
                            bool skip = false;
                            foreach (Bubble b in bubbles)
                            {
                                if (b.Contains(x, y - 1)) skip = true;
                            }
                            if (!skip)
                            {
                                Bubble bubble = ExpandBubble(usm, x, y - 1, threshold);
                                if (bubble.xSpan >= m_MinDiameter && bubble.ySpan >= m_MinDiameter)
                                    bubbles.Add(bubble);
                            }
                        }
                        height = 0;
                    }
                }
            }
            return bubbles;
        }
    }
}
