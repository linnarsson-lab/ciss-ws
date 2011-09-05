using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics.Signal
{
    public class SignalData
    {
        private double[] m_Data;
        public double[] Data
        {
            get { return m_Data; }
            set { m_Data = value; }
        }

        private double m_X0;
        public double X0
        {
            get { return m_X0; }
            set { m_X0 = value; }
        }

        private double m_XStep;
        public double XStep
        {
            get { return m_XStep; }
            set { m_XStep = value; }
        }

        public double XMax
        {
            get { return m_X0 + (m_XStep * m_Data.Length); }
        }

        public int Length
        {
            get { return m_Data.Length; }
        }

        public SignalData(double[] data, double x0, double xStep)
        {
            this.m_Data = data;
            this.m_X0 = x0;
            this.m_XStep = xStep;
        }

    }
}
