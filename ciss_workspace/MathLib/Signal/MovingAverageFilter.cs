using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics.Signal
{
    public class MovingAverageFilter
    {
        private int m_FilterHalfWidth;
        public int FilterHalfWidth
        {
            get { return m_FilterHalfWidth; }
            set { m_FilterHalfWidth = value; }
        }
	
        public MovingAverageFilter(int filterHalfWidth)
        {
            this.m_FilterHalfWidth = filterHalfWidth;
        }

        public double[] Process(double[] inData)
        {
            double[] outData = new double[inData.Length];
            int filterWidth = this.m_FilterHalfWidth * 2 + 1;
            double windowSum = 0.0;
            for (int i = 0; i < filterWidth; i++)
                windowSum += inData[i];
            double firstAverage = windowSum / filterWidth;
            int midPoint = 0;
            for (; midPoint <= this.m_FilterHalfWidth; midPoint++)
                outData[midPoint] = firstAverage;
            int removePoint = 0;
            for (int addPoint = filterWidth; addPoint < inData.Length; addPoint++)
			{
                windowSum -= inData[removePoint++];
                windowSum += inData[addPoint];
                outData[midPoint++] = windowSum / filterWidth;
			}
            double lastAverage = windowSum / filterWidth;
            for (; midPoint < inData.Length; midPoint++)
                outData[midPoint] = lastAverage;
            return outData;
        }

        public int[] Process(int[] inData)
        {
            int[] outData = new int[inData.Length];
            int filterWidth = this.m_FilterHalfWidth * 2 + 1;
            int windowSum = 0;
            for (int i = 0; i < filterWidth; i++)
                windowSum += inData[i];
            int firstAverage = (int)Math.Round( (float)windowSum / filterWidth );
            int midPoint = 0;
            for (; midPoint <= this.m_FilterHalfWidth; midPoint++)
                outData[midPoint] = firstAverage;
            int removePoint = 0;
            for (int addPoint = filterWidth; addPoint < inData.Length; addPoint++)
            {
                windowSum -= inData[removePoint++];
                windowSum += inData[addPoint];
                outData[midPoint++] = (int)Math.Round( (float)windowSum / filterWidth );
            }
            int lastAverage = (int)Math.Round( (float)windowSum / filterWidth );
            for (; midPoint < inData.Length; midPoint++)
                outData[midPoint] = lastAverage;
            return outData;
        }

    }
}
