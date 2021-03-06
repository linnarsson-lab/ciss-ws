﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class CentroidLinkage : Fusion
    {
        public override double CalculateDistance(Cluster cluster1, Cluster cluster2)
        {
            object[] averages1 = GetClusterAverage(cluster1);
            object[] averages2 = GetClusterAverage(cluster2);
            return metric.GetDistance(averages1, averages2);
        }

    }
}
