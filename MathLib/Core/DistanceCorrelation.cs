using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
    public class DistanceCorrelation
    {
        public static void Test()
        {
            double[,] x = new double[,] { { 1, 3, 6 }, { 4, 2, 4 }, { 10, 4, 2 } };
            Console.WriteLine("Matrix x:");
            DisplayMatrix(x);
            Console.WriteLine("Euclidean Distance of x:");
            DisplayMatrix(EuclideanDistance(x));
            Console.WriteLine("Transpose of x:");
            DisplayMatrix(Transpose(x));
            Console.WriteLine("dcor x-x: " + Calc(x, x, 1.0).dCor);
            double[,] y = new double[,] { { 5, 3, 3 }, { 7, 2, 1 }, { 20, 8, 5 } };
            Console.WriteLine("Matrix y:");
            DisplayMatrix(y);
            Console.WriteLine("dcor stats x-y: " + Calc(x, y, 1.0).ToString());
            Console.WriteLine("Expected from R:\n$dCov=4.766911\n$dCor=0.9783073\n$dVarX=3.600235\n$dVarY=6.594661");
        }
        private static void DisplayMatrix(double[,] x)
        {
            for (int i = 0; i < x.GetLength(0); i++)
            {
                for (int j = 0; j < x.GetLength(1); j++)
                    Console.Write("{0,8:0.####}", x[i, j]);
                Console.WriteLine();
            }
        }

        private static readonly double DBL_EPSILON = 1.0e-15; // Substitute for the C/C+ constant missing in C#

        public class DcorResult
        {
            public double dCov;  // sample distance covariance
            public double dCor;  // sample distance correlation
            public double dVarX; // distance variance of x sample
            public double dVarY; // distance variance of y sample
            public DcorResult(double dCov, double dCor, double dVarX, double dVarY)
            {
                this.dCov = dCov;
                this.dCor = dCor;
                this.dVarX = dVarX;
                this.dVarY = dVarY;
            }

            public override string ToString()
            {
                return "dCov=" + dCov + " dCor=" + dCor + " dVarX=" + dVarX + " dVarY=" + dVarY;
            }
        }

        /// <summary>
        /// Calculate the distance correlation for one-dimensional sample series in x and y
        /// </summary>
        /// <param name="x">first sample series</param>
        /// <param name="y">second sample series</param>
        /// <returns>Distance correlation value</returns>
        public static double distancecorrelation(double[] x, double[] y)
        {
            double[,] xmatrix = new double[x.Length, 1];
            double[,] ymatrix = new double[y.Length, 1];
            for (int i = 0; i < x.Length; i++)
            {
                xmatrix[i, 0] = x[i];
                ymatrix[i, 0] = y[i];
            }
            return Calc(xmatrix, ymatrix, 1.0).dCor;
            /* R energy package code:
                    dcor <- 
                    function(x, y, index=1.0) {
                        # distance correlation statistic for independence
                        return(.dcov(x, y, index)[2])
                    } 
            */
        }

        /// <summary>
        /// Calculate the distance correlation metrics from two multi-dimensional sample series in x and y
        /// </summary>
        /// <param name="x">first index are samples, second the sampled value arrays</param>
        /// <param name="y">first index are samples, second the sampled value arrays</param>
        /// <param name="index">exponent on Euclidean distance, in (0,2]</param>
        /// <returns></returns>
        public static DcorResult Calc(double[,] x, double[,] y, double index)
        {
            bool areDistances = true;
            int n = x.GetLength(0);
            if (n != y.GetLength(0))
                throw new ArgumentException("Data matrices do not have the same sample size!");
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < x.GetLength(1); j++)
                    if (double.IsInfinity(x[i, j]) || double.IsNaN(x[i, j]))
                        throw new ArgumentException("Non-numerical or infinity values in sample data matrices!");
                for (int j = 0; j < x.GetLength(1); j++)
                    if (double.IsInfinity(y[i, j]) || double.IsNaN(y[i, j]))
                        throw new ArgumentException("Non-numerical or infinity values in sample data matrices!");
            }
            double[,] xt = Transpose(EuclideanDistance(x));
            double[,] yt = Transpose(EuclideanDistance(y));
            return DCOV(xt, yt, areDistances, index);

            /* R package energy code:
            .dcov <- 
            function(x, y, index=1.0) {
                # distance covariance statistic for independence
                # dcov = [dCov,dCor,dVar(x),dVar(y)]   (vector)
                # this function provides the fast method for computing dCov
                # it is called by the dcov and dcor functions
                if (!(class(x) == "dist")) x <- dist(x)
                if (!(class(y) == "dist")) y <- dist(y)
                x <- as.matrix(x)
                y <- as.matrix(y)
                dst <- TRUE  
                n <- nrow(x)
                m <- nrow(y)
                if (n != m) stop("Sample sizes must agree")
                if (! (all(is.finite(c(x, y))))) 
                    stop("Data contains missing or infinite values")
                dims <- c(n, NCOL(x), NCOL(y), dst)
                idx <- 1:dims[1]
                DCOV <- numeric(4)
                a <- .C("dCOV", 
                        x = as.double(t(x)),
                        y = as.double(t(y)),
                        byrow = as.integer(TRUE),
                        dims = as.integer(dims), 
                        index = as.double(index),
                        idx = as.double(idx),
                        DCOV = as.double(DCOV), 
                        PACKAGE = "energy")
                return(a$DCOV)
            }
            */
        }

        /// <summary>
        /// Calculate dcor for two sample arrays or distance matrices
        /// </summary>
        /// <param name="x">either a sample series or distance matrix</param>
        /// <param name="y">either a sample series or distance matrix</param>
        /// <param name="areDistances">true to indicate that x and y are distance matrices</param>
        /// <param name="index">exponent on Euclidean distance, in (0,2]</param>
        /// <returns></returns>
        private static DcorResult DCOV(double[,] x, double[,] y, bool areDistances, double index)
        {
            /*  computes dCov(x,y), dCor(x,y), dVar(x), dVar(y)
                V-statistic is n*dCov^2 where n*dCov^2 --> Q
                dims[0] = n (sample size)
                dims[1] = p (dimension of X)
                dims[2] = q (dimension of Y)
                //dims[3] = dst (logical, TRUE if x, y are distances)
                index : exponent for distance
                idx   : index vector, a permutation of sample indices
                DCOV  : vector [dCov, dCor, dVar(x), dVar(y)]
             */
            int n = x.GetLength(0);
            double[,] Dx, Dy;
            double[] DCOV = new double[4];
            int p = x.GetLength(1);
            int q = y.GetLength(1);
            if (areDistances)
            {
                Dx = x;
                Dy = y;
            }
            else
            {
                Dx = EuclideanDistance(x);
                Dy = EuclideanDistance(y);
            }
            IndexDistance(ref Dx, index);
            IndexDistance(ref Dy, index);
            double[,] A = new double[n, n];
            double[,] B = new double[n, n];
            A = Akl(ref Dx);
            B = Akl(ref Dy);
            /* compute dCov(x,y), dVar(x), dVar(y) */
            for (int k = 0; k < n; k++)
                for (int j = 0; j < n; j++)
                {
                    DCOV[0] += A[k, j] * B[k, j];
                    DCOV[2] += A[k, j] * A[k, j];
                    DCOV[3] += B[k, j] * B[k, j];
                }
            double n2 = n * n;
            for (int k = 0; k < 4; k++)
            {
                DCOV[k] /= n2;
                if (DCOV[k] > 0.0)
                    DCOV[k] = Math.Sqrt(DCOV[k]);
                else
                    DCOV[k] = 0.0;
            }
            /* compute dCor(x, y) */
            double V = DCOV[2] * DCOV[3];
            if (V > DBL_EPSILON)
                DCOV[1] = DCOV[0] / Math.Sqrt(V);
            else
                DCOV[1] = 0.0;
            return new DcorResult(DCOV[0], DCOV[1], DCOV[2], DCOV[3]);
        }

        private static double[,] Akl(ref double[,] akl)
        {
            /* -computes the A_{kl} or B_{kl} distances from the
                distance matrix (a_{kl}) or (b_{kl}) for dCov, dCor, dVar
                dCov = mean(Akl*Bkl), dVar(X) = mean(Akl^2), etc.
            */
            int n = akl.GetLength(0);
            double[,] A = new double[n, n];
            double[] akbar = new double[n];
            double abar = 0.0;
            for (int k = 0; k < n; k++)
            {
                akbar[k] = 0.0;
                for (int j = 0; j < n; j++)
                {
                    akbar[k] += akl[k, j];
                }
                abar += akbar[k];
                akbar[k] /= (double)n;
            }
            abar /= (double)(n * n);
            for (int k = 0; k < n; k++)
                for (int j = k; j < n; j++)
                {
                    A[k, j] = akl[k, j] - akbar[k] - akbar[j] + abar;
                    A[j, k] = A[k, j];
                }
            return A;
        }

        public static double[,] EuclideanDistance(double[,] x)
        {
            /* x is an n by d matrix, in row order (n vectors in R^d).
               Compute the Euclidean distance matrix Dx */
            int n = x.GetLength(0);
            int d = x.GetLength(1);
            double[,] Dx = new double[n, n];
            for (int i = 1; i < n; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    double dsum = 0.0;
                    for (int k = 0; k < d; k++)
                    {
                        double dif = x[i, k] - x[j, k]; // *(x + p + k) - *(x + q + k);
                        dsum += dif * dif;
                    }
                    Dx[i, j] = Dx[j, i] = Math.Sqrt(dsum);
                }
            }
            return Dx;
        }

        public static void IndexDistance(ref double[,] Dx, double index)
        {
            /* Dx is an n by n Euclidean distance matrix. If index NEQ 1, compute D^index */
            if (Math.Abs(index - 1.0) > DBL_EPSILON)
            {
                int n = Dx.GetLength(0);
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                    {
                        Dx[i, j] = Math.Pow(Dx[i, j], index);
                        Dx[j, i] = Dx[i, j];
                    }
            }
        }

        public static double[,] Transpose(double[,] x)
        {
            int xrows = x.GetLength(0);
            int xcols = x.GetLength(1);
            double[,] xt = new double[xcols, xrows];
            for (int i = 0; i < xrows; i++)
                for (int j = 0; j < xcols; j++)
                    xt[j, i] = x[i, j];
            return xt;
        }

/* From R package energy:
        DCOR <- 
        function(x, y, index=1.0) {
            # distance covariance and correlation statistics
            # alternate method, implemented in R without .C call
            # this method is usually slower than the C version
            if (!(class(x) == "dist")) x <- dist(x)
            if (!(class(y) == "dist")) y <- dist(y)
            x <- as.matrix(x)
            y <- as.matrix(y)
            n <- nrow(x)
            m <- nrow(y)
            if (n != m) stop("Sample sizes must agree")
            if (! (all(is.finite(c(x, y)))))
                stop("Data contains missing or infinite values")
            if (index < 0 || index > 2) {
                warning("index must be in [0,2), using default index=1")
                index=1.0}
    
            stat <- 0
            dims <- c(n, ncol(x), ncol(y))
    
            Akl <- function(x) {
                d <- as.matrix(x)^index
                m <- rowMeans(d)
                M <- mean(d)
                a <- sweep(d, 1, m)
                b <- sweep(a, 2, m)
                return(b + M) 
            }

            A <- Akl(x)
            B <- Akl(y)
            dCov <- sqrt(mean(A * B))
            dVarX <- sqrt(mean(A * A))
            dVarY <- sqrt(mean(B * B))
            V <- sqrt(dVarX * dVarY)
            if (V > 0)
              dCor <- dCov / V else dCor <- 0
            return(list(dCov=dCov, dCor=dCor, dVarX=dVarX, dVarY=dVarY))
        }
*/

    }
}
