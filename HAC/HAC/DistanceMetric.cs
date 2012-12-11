using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class DistanceMetric
    {
        public DistanceDataFilter DistanceDataFilter { get; private set; }

        public DistanceMetric()
        {
            DistanceDataFilter = new NullDistanceDataFilter();
        }
        public DistanceMetric(DistanceDataFilter filter)
        {
            DistanceDataFilter = filter;
        }

        public abstract double GetDistance(object[] set1, object[] set2);

        public static DistanceMetric GetDistanceMetric(string metricName, DistanceDataFilter distanceDataFilter)
        {
            switch (metricName.ToLower())
            {
                case "chisq":
                    return new ChiSquareDistance(distanceDataFilter);
                case "braycurtis":
                    return new BrayCurtisDistance(distanceDataFilter);
                case "euclidian":
                    return new EuclidianDistance(distanceDataFilter);
                case "sqeuclidian":
                    return new SquaredEuclidianDistance(distanceDataFilter);
                case "jaccard":
                    return new JaccardDistance(distanceDataFilter);
                case "manhattan":
                    return new ManhattanDistance(distanceDataFilter);
                case "ess":
                    return new EssDistance(distanceDataFilter);
                case "canberra":
                    return new CanberraDistance(distanceDataFilter);
                case "chebyshev":
                    return new ChebyshevDistance(distanceDataFilter);
                default:
                    throw new ArgumentException("Unknown distance metric: " + metricName);
            }
        }
    }
}
