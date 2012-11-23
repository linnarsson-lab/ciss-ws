﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class SquaredEuclidianDistance : DistanceMetric
    {
        public override double GetDistance(object[] set1, object[] set2)
        {
            double s = 0.0;
            for (int i = 0; i < set1.Length; i++)
            {
                double d = (double)set1[i] - (double)set2[i];
                s += d * d;
            }
            return s;
        }
    }
}