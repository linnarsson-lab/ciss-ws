using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics.Interpolation
{
    public class Splint
    {
        public static double splint(double[] t, int n, double[] c,
                             int k, double a, double b, out double[] wrk)
        {
            /* function splint calculates the integral of a spline function s(x)
               of degree k, which is given in its normalized b-spline representation
               calling sequence:
                  aint = splint(t,n,c,k,a,b,wrk)
               input parameters:
                 t    : array, length n, which contains the position of the knots
                        of s(x).
                 n    : integer, giving the total number of knots of s(x).
                 c    : array, length n, containing the b-spline coefficients.
                 k    : integer, giving the degree of s(x).
                 a,b  : double values, containing the end points of the integration
                        interval. s(x) is considered to be identically zero outside
                        the interval (t(k+1),t(n-k)).
               output parameter:
                 aint : double, containing the integral of s(x) between a and b.
                 wrk  : double array, length n. Used as working space
                        on output, wrk will contain the integrals of the normalized
                        b-splines defined on the set of knots.
               references :
                 gaffney p.w. : the calculation of indefinite integrals of b-splines
                                j. inst. maths applics 17 (1976) 37-41.
                 dierckx p. : curve and surface fitting with splines, monographs on
                              numerical analysis, oxford university press, 1993.
               author :
                 p.dierckx
                 dept. computer science, k.u.leuven
                 celestijnenlaan 200a, b-3001 heverlee, belgium.
                 e-mail : Paul.Dierckx@cs.kuleuven.ac.be
         
               latest update : march 1987
            */
            int nk1 = n - k - 1;
            /*  calculate the integrals wrk[i] of the normalized b-splines
                ni,k+1(x), i=1,2,...nk1.*/
            fpintb(t, n, out wrk, nk1, a, b);
            //  calculate the integral of s(x).
            double splint = 0.0;
            for (int i = 1; i <= nk1; i++)
                splint = splint + c[i] * wrk[i];
            return splint;
        }

        public static void fpintb(double[] t, int n, out double[] bint, int nk1, double x, double y)
        {
            /*  subroutine fpintb calculates integrals of the normalized b-splines
              nj,k+1(x) of degree k, defined on the set of knots t[j],j=1,2,...n.
              it makes use of the formulae of gaffney for the calculation of
              indefinite integrals of b-splines.
              calling sequence:
                 fpintb(t,n,bint,nk1,x,y)
              input parameters:
                t    : double array,length n, containing the position of the knots.
                n    : integer value, giving the number of knots.
                nk1  : integer value, giving the number of b-splines of degree k,
                       defined on the set of knots ,i.e. nk1 = n-k-1.
                x,y  : double values, containing the end points of the integration
                       interval.
              output parameter:
                bint : array,length nk1, containing the integrals of the b-splines.
             */
            int ib, j1, l, li, lj, lk;
            int ia = 0;
            double arg, f;
            double[] aint = new double[7];
            double[] h = new double[7];
            double[] h1 = new double[7];
            double one = 0.1e+01;
            int k1 = n - nk1;
            double ak = k1;
            int k = k1 - 1;
            bint = new double[nk1 + 1];
            for (int i = 1; i <= nk1; i++)
                bint[i] = 0;
            //  the integration limits are arranged in increasing order.
            double a = x;
            double b = y;
            bool reversedLimits = false;
            if (a == b) return;
            if (a - b > 0)
            {
                a = y;
                b = x;
                reversedLimits = true;
            }
            if (a < t[k1]) a = t[k1];
            if (b > t[nk1 + 1]) b = t[nk1 + 1];
            /*  using the expression of gaffney for the indefinite integral of a
              b-spline we find that
              bint(j) = (t(j+k+1)-t(j))*(res(j,b)-res(j,a))/(k+1)
                where for t(l) <= x < t(l+1)
                res(j,x) = 0, j=1,2,...,l-k-1
                         = 1, j=l+1,l+2,...,nk1
                         = aint(j+k-l+1), j=l-k,l-k+1,...,l
                           = sumi((x-t(j+i))*nj+i,k+1-i(x)/(t(j+k+1)-t(j+i)))
                             i=0,1,...,k
             */
            l = k1;
            //  set arg = a.
            arg = a;
            for (int it = 1; it <= 2; it++)
            {
                //  search for the knot interval t(l) <= arg < t(l+1).
                while (arg >= t[l + 1] && l < nk1)
                    l++;
                //  calculation of aint(j), j=1,2,...,k+1.
                for (int j = 1; j <= k1; j++)
                    aint[j] = 0;
                aint[1] = (arg - t[l]) / (t[l + 1] - t[l]);
                h1[1] = one;
                for (int j = 1; j <= k; j++)
                {
                    //  evaluation of the non-zero b-splines of degree j at arg,i.e.
                    //    h(i+1) = nl-j+i,j(arg), i=0,1,...,j.
                    h[1] = 0;
                    for (int i = 1; i <= j; i++)
                    {
                        li = l + i;
                        lj = li - j;
                        f = h1[i] / (t[li] - t[lj]);
                        h[i] = h[i] + f * (t[li] - arg);
                        h[i + 1] = f * (arg - t[lj]);
                    }
                    //  updating of the integrals aint.
                    j1 = j + 1;
                    for (int i = 1; i <= j1; i++)
                    {
                        li = l + i;
                        lj = li - j1;
                        aint[i] = aint[i] + h[i] * (arg - t[lj]) / (t[li] - t[lj]);
                        h1[i] = h[i];
                    }
                }
                if (it == 2) break;
                //  updating of the integrals bint
                lk = l - k;
                ia = lk;
                for (int i = 1; i <= k1; i++)
                {
                    bint[lk] = -aint[i];
                    lk++;
                }
                //  set arg = b.
                arg = b;
            }
            //  updating of the integrals bint.
            lk = l - k;
            ib = lk - 1;
            for (int i = 1; i <= k1; i++)
            {
                bint[lk] += aint[i];
                lk++;
            }
            if (ib >= ia)
            {
                for (int i = ia; i <= ib; i++)
                    bint[i] += one;
            }
            //  the scaling factors are taken into account.
            f = one / ak;
            for (int i = 1; i <= nk1; i++)
            {
                bint[i] = bint[i] * (t[i+k1] - t[i]) * f;
            }
            //  the order of the integration limits is taken into account.
            if (reversedLimits)
            {
                for (int i = 1; i <= nk1; i++)
                    bint[i] = -bint[i];
            }
        }
    }
}
