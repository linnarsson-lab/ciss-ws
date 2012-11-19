using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    /// <summary>
    /// An element consists of an array of data points. To use single values (e.g. to cluster numbers) add that value 
    /// as the sole data point to an element object.
    /// </summary>
    public class Element
    {
        public static int defaultId = 1;

        List<object> dataPoints = new List<object>();
        public string Id { get; private set; }

        /// <summary>
        /// Defines the elements that neighbor this one at start of clustering
        /// </summary>
        HashSet<Element> neighbors = new HashSet<Element>();
        /// <summary>
        /// Pointer to the single-element cluster this Element is intitially assigned to
        /// </summary>
        public Cluster InitialCluster { get; set; }


        public Element(object[] dataPoints)
        {
            this.AddDataPoints(dataPoints);
            Id = defaultId.ToString();
            defaultId++;
        }

        public Element(string id)
        {
            Id = id;
        }

        public Element(string id, object[] dataPoints)
        {
            Id = id;
            this.AddDataPoints(dataPoints);
        }

        public Element(string id, object[] dataPoints, Element[] neighbors)
        {
            Id = id;
            this.AddDataPoints(dataPoints);
            this.AddNeighbors(neighbors);
        }

        public void AddNeighbors(Element[] neighbors)
        {
            foreach (Element neighbor in neighbors)
                this.neighbors.Add(neighbor);
        }

        public void AddNeighbor(Element neighbor)
        {
            this.neighbors.Add(neighbor);
        }

        public HashSet<Element> GetNeighbors()
        {
            return neighbors;
        }

        public void AddDataPoint(object dataPoint)
        {
            dataPoints.Add(dataPoint);
        }

        public void AddDataPoints(object[] dataPoints)
        {
            foreach(object point in dataPoints)
                this.dataPoints.Add(point);
        }

        public object this[int i]
        {
            get { return this.dataPoints[i]; }
            set { this.dataPoints[i] = value; }
        }

        public object[] GetDataPoints()
        {
            return dataPoints.ToArray<object>();
        }

        public int DataPointCount { get { return dataPoints.Count; } }

    }
}
