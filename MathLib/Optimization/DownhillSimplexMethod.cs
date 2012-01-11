using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    public delegate double FunctionToMinimize(double[] inputVector);

    /// <summary>
    /// Adapted from "Numerical Recipes in C" p.411-412
    /// </summary>
    public class DownhillSimplexMethod
    {
        int maxNumberOfFunctionEvaluations;
        const double tiny = 1.0e-10;

        public DownhillSimplexMethod(int maxNumberOfFunctionEvaluations)
        {
            this.maxNumberOfFunctionEvaluations = maxNumberOfFunctionEvaluations;
        }

        public int Amoeba(ref double[,] p, ref double[] y,
                           double fractionalConvergenceTolerance, FunctionToMinimize funk)
        /// <summary>
        /// Multidimensional minimization of funk(x) where x is double[ndim]
        /// </summary>
        /// <param name="p">Initial vertices of the starting simplex (dimension: [ndim+1, ndim])</param>
        /// <param name="y">Function values at the vertices of p (dimension [ndim+1])</param>
        /// <param name="fractionalConvergenceTolerance">Termination criterion</param>
        /// <param name="funk">Function to minimize</param>
        /// <returns>Actual number of function evaluations taken.
        /// p and y are set to new points, all within the specified tolerance of a
        /// minimum value of the function.
        /// </returns>
        {
            int ndim = p.GetLength(1);
            int nFunctionEvaluations = 0;
            int i, ihi, ilo, inhi, j;
            int mpts = ndim + 1;
            double rtol, sum, ysave, ytry;

            double[] psum = new double[ndim];
            for (j = 0; j < ndim; j++)
            {
                sum = 0.0;
                for (i = 0; i < mpts; i++)
                    sum += p[i, j];
                psum[j] = sum;
            }

            while (true)
            {
                ilo = 0;
                if (y[0] > y[1])
                {
                    ihi = 0; inhi = 1;
                }
                else
                {
                    ihi = 1; inhi = 0;
                }
                for (i = 0; i < mpts; i++)
                {
                    if (y[i] <= y[ilo]) 
                        ilo = i;
                    if (y[i] > y[ihi])
                    {
                        inhi = ihi;
                        ihi = i;
                    }
                    else if (y[i] > y[inhi] && i != ihi)
                            inhi = i;
                }
                rtol = 2.0 * Math.Abs(y[ihi] - y[ilo]) /
                       (Math.Abs(y[ihi]) + Math.Abs(y[ilo]) + tiny);
                if (rtol < fractionalConvergenceTolerance)
                {
                    double temp = y[0];
                    y[0] = y[ilo];
                    y[ilo] = temp;
                    for (i = 0; i < ndim; i++)
                    {
                        temp = p[0, i];
                        p[0, i] = p[ilo, i];
                        p[ilo, i] = temp;
                    }
                    break;
                }
                if (nFunctionEvaluations >= maxNumberOfFunctionEvaluations)
                    throw new OverflowException();
                nFunctionEvaluations += 2;
                ytry = amotry(ref p, ref y, ref psum, ndim, funk, ihi, -1.0);
                if (ytry <= y[ilo])
                    ytry = amotry(ref p, ref y, ref psum, ndim, funk, ihi, 2.0);
                else if (ytry >= y[inhi])
                {
                    ysave = y[ihi];
                    ytry = amotry(ref p, ref y, ref psum, ndim, funk, ihi, 0.5);
                    if (ytry >= ysave)
                    {
                        for (i = 0 ; i < mpts; i++)
                        {
                            if (i != ilo)
                            {
                                for (j = 0; j < ndim; j++)
                                    p[i, j] = psum[j] = 0.5 * (p[i, j] + p[ilo, j]);
                                y[i] = funk(psum);
                            }
                        }
                        nFunctionEvaluations += ndim;
                        for (j = 0; j < ndim; j++)
                        {
                            sum = 0.0;
                            for (i = 0; i < mpts; i++)
                                sum += p[i, j];
                            psum[j] = sum;
                        }
                    }
                }
                else
                    nFunctionEvaluations--;
            }
            return nFunctionEvaluations;

        }

        private double amotry(ref double[,] p, ref double[] y, ref double[] psum, int ndim,
                              FunctionToMinimize funk, int ihi, double fac)
        {
            int j;
            double fac1, fac2, ytry;
            double[] ptry = new double[ndim];
            fac1 = (1.0 - fac) / ndim;
            fac2 = fac1 - fac;
            for (j = 0; j < ndim; j++)
                ptry[j] = psum[j] * fac1 - p[ihi, j] * fac2;
            ytry = funk(ptry);
            if (ytry < y[ihi])
            {
                y[ihi] = ytry;
                for (j = 0; j < ndim; j++)
                {
                    psum[j] += ptry[j] - p[ihi, j];
                    p[ihi, j] = ptry[j];
                }
            }
            return ytry;
        }

    }
}
