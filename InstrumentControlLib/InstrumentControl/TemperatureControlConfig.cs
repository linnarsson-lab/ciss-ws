using System;
using System.Collections.Generic;
using System.Text;
using Linnarsson.Mathematics;

namespace Linnarsson.InstrumentControl
{
    public class TemperatureControlConfig
    {
        private Polynomial m_HeatingILimit;
        public Polynomial HeatingILimit
        {
            get { return m_HeatingILimit; }
            set { m_HeatingILimit = value; }
        }

        private double m_HeatingILimitFactor;
        public double HeatingILimitFactor
        {
            get { return m_HeatingILimitFactor; }
            set { m_HeatingILimitFactor = value; }
        }
        private double m_HeatingMinILimit;
        public double HeatingMinILimit
        {
            get { return m_HeatingMinILimit; }
            set { m_HeatingMinILimit = value; }
        }


        private Polynomial m_CoolingILimit;
        public Polynomial CoolingILimit
        {
            get { return m_CoolingILimit; }
            set { m_CoolingILimit = value; }
        }

        private double m_CoolingILimitFactor;
        public double CoolingILimitFactor
        {
            get { return m_CoolingILimitFactor; }
            set { m_CoolingILimitFactor = value; }
        }

        private double m_CoolingMinILimit;
        public double CoolingMinILimit
        {
            get { return m_CoolingMinILimit; }
            set { m_CoolingMinILimit = value; }
        }
	
	
        private double[] m_SteinhartHartCoefficients;
        public double[] SteinhartHartCoefficients
        {
            get { return m_SteinhartHartCoefficients; }
            set { m_SteinhartHartCoefficients = value; }
        }

        private double m_DeadBand;
        public double DeadBand
        {
            get { return m_DeadBand; }
            set { m_DeadBand = value; }
        }

        private double m_MaxCurrent;
        public double MaxCurrent
        {
            get { return m_MaxCurrent; }
            set { m_MaxCurrent = value; }
        }
	
        public TemperatureControlConfig()
        {
            HeatingILimit = new Polynomial(new double[] { -23.182, 2.184, -0.045685, 0.00039057 });
            HeatingILimitFactor = 1.4;
            CoolingILimit = new Polynomial(new double[] { 69.883, -5.98, 0.14988, -0.0002958 });
            CoolingILimitFactor = 1.4;
            SteinhartHartCoefficients = new double[] { 1.129241e-3, 2.341077e-4, 8.775468e-8 };
        }

    }
}
