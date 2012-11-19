using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class DistanceMetric
    {
        public abstract double GetDistance(object[] set1, object[] set2);

        public static DistanceMetric GetDistanceMetric(string metricName)
        {
            switch (metricName.ToLower())
            {
                case "chisq":
                    return new ChiSquareDistance();
                case "braycurtis":
                    return new BrayCurtisDistance();
                case "euclidian":
                    return new EuclidianDistance();
                case "sqeuclidian":
                    return new SquaredEuclidianDistance();
                case "jaccard":
                    return new JaccardDistance();
                case "manhattan":
                    return new ManhattanDistance();
                case "ess":
                    return new EssDistance();
                case "canberra":
                    return new CanberraDistance();
                case "chebyshev":
                    return new ChebyshevDistance();
                default:
                    throw new ArgumentException("Unknown distance metric: " + metricName);
            }
        }
    }
}
