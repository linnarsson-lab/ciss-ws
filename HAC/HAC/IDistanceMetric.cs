using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public interface IDistanceMetric
    {
        double GetDistance(object[] set1, object[] set2);
    }
}
