using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public abstract class Transformation
    {
        public abstract void Transform(Element[] elements);

        public static Transformation GetTransformation(string transformation)
        {
            switch (transformation.ToLower())
            {
                case "zscore":
                    return new ZScoreTransform();
                case "chisq":
                    return new ChiSqTransform();
                case "":
                case null:
                    return new NullTransformation();
                default:
                    throw new ArgumentException("Unknown transformation: " + transformation);
            }
        }
    }
}
