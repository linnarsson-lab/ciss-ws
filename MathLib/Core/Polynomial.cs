using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public class Polynomial
    {
        private double[] m_Coefficients;
        public double[] Coefficients
        {
            get { return m_Coefficients; }
            set { m_Coefficients = value; }
        }

        public Polynomial()
        {
        }

        public Polynomial(int degree)
        {
            m_Coefficients = new double[degree + 1];
            for (int ix = 0; ix < Coefficients.Length; ix++)
            {
                Coefficients[ix] = 1d;
            }
        }

        public Polynomial(double[] coefficients)
        {
            m_Coefficients = coefficients;
        }

        public double this[double x]
        {
            get
            {
                double r = Coefficients[Coefficients.Length - 1];
                for (int ix = Coefficients.Length - 2; ix >= 0; ix--)
                {
                    r = r * x + Coefficients[ix];
                }
                return r;
            }
        }

        public double Derivative(double x)
        {
            double r = Coefficients[Coefficients.Length - 1];
            double d = 0d;
            for (int ix = Coefficients.Length - 2; ix >= 0; ix--)
            {
                d = d * x + r;
                r = r * x + Coefficients[ix];
            }
            return d;
        }
    }
}
