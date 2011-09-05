using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public abstract class LogOdds
    {
        protected double m_MinProbability;
        /// <summary>
        /// The minimum probability, which is used to extrapolate the histograms beyond
        /// the range where adequate numbers of events have been recorded.
        /// </summary>
        public double MinProbability
        {
            get { return m_MinProbability; }
            set { m_MinProbability = value; }
        }

        public abstract void AddPos(float value);
        public abstract void AddNeg(float value);
        public abstract void AddPos(double value);
        public abstract void AddNeg(double value);
        public abstract double GetLogOdds(double intensity);
        public abstract void Calculate();
        public abstract double GetOverlap();
        public abstract double GetMinX();
        public abstract double GetMaxX();


    }
}
