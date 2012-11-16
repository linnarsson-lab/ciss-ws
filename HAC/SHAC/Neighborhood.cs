using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HAC;

namespace SHAC
{
    /// <summary>
    /// Base class for setup of neighboring elements from given X-Y coordinates
    /// </summary>
    public abstract class Neighborhood
    {
        public abstract void Setup(Element[] elements, int[] xPositions, int[] yPositions);

        public static Neighborhood GetNeighborhood(string neighborhoodName)
        {
            switch (neighborhoodName.ToLower())
            {
                case "queen":
                    return new QueenNeighborhood();
                case "rook":
                    return new RookNeighborhood();
                default:
                    throw new ArgumentException("Unknown neighborhood type: " + neighborhoodName);
            }
        }
    }

    public class QueenNeighborhood : Neighborhood
    {
        public override void Setup(Element[] elements, int[] xPositions, int[] yPositions)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                for (int j = i + 1; j < elements.Length; j++)
                {
                    if (Math.Abs(xPositions[j] - xPositions[i]) <= 1 && Math.Abs(yPositions[j] - yPositions[i]) <= 1)
                    {
                        elements[i].AddNeighbor(elements[j]);
                        elements[j].AddNeighbor(elements[i]);
                    }
                }
            }
        }
    }

    public class RookNeighborhood : Neighborhood
    {
        public override void Setup(Element[] elements, int[] xPositions, int[] yPositions)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                for (int j = i + 1; j < elements.Length; j++)
                {
                    if (Math.Abs(xPositions[i] - xPositions[j]) <= 1 && yPositions[i] == yPositions[j] ||
                        Math.Abs(yPositions[i] - yPositions[j]) <= 1 && xPositions[i] == xPositions[j])
                    {
                        elements[i].AddNeighbor(elements[j]);
                        elements[j].AddNeighbor(elements[i]);
                    }
                }
            }
        }
    }

}
