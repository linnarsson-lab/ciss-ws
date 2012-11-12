using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class JaccardDistance : IDistanceMetric
    {
        public double GetDistance(object[] set1, object[] set2)
        {
            var interSect = set1.Intersect<object>(set2);
            if (interSect.Count() == 0)
                return 1.0 / double.Epsilon;
            var unionSect = set1.Union<object>(set2);
            return 1.0 / (((double)interSect.Count() / unionSect.Count()) + double.Epsilon);
        }
    }
}
