﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class Cluster : IEnumerable
    {
        HashSet<Element> elements = new HashSet<Element>();
        HashSet<Cluster> neighbors = new HashSet<Cluster>();
        Fusion fusion;

        public Cluster(Fusion fusion)
        {
            this.fusion = fusion;
        }

        public Cluster(Fusion fusion, Element initialElement)
        {
            this.fusion = fusion;
            this.elements.Add(initialElement);
            DataPointCount = initialElement.DataPointCount;
        }

        public void InitNeighborsFromElements()
        {
            foreach (Element element in elements)
                foreach (Element neighbor in element.GetNeighbors())
                    neighbors.Add(neighbor.InitialCluster);
        }

        public int ElementCount { get { return elements.Count; } }

        public int DataPointCount { get; private set; }

        internal void AddElements(Element[] elements)
        {
            foreach (Element e in elements)
                this.elements.Add(e);
        }

        internal Element[] GetElements()
        {
            return elements.ToArray<Element>();
        }

        internal void AddCluster(Cluster cluster)
        {
            AddElements(cluster.GetElements());
            AddNeighbors(cluster.neighbors);
        }

        private void AddNeighbors(HashSet<Cluster> otherNeighbors)
        {
            foreach (Cluster otherNeighbor in otherNeighbors)
                this.neighbors.Add(otherNeighbor);
        }

        internal void UpdateNeighbors(Cluster newCluster, Cluster oldCluster1, Cluster oldCluster2)
        {
            if (neighbors.Remove(oldCluster1) || neighbors.Remove(oldCluster2))
                neighbors.Add(newCluster);
        }

        internal bool HasNeighbor(Cluster cluster)
        {
            return neighbors.Contains(cluster);
        }

        internal double CalculateDistance(Cluster otherCluster)
        {
            return fusion.CalculateDistance(this, otherCluster);
        }

        #region IEnumerable Member

        public IEnumerator GetEnumerator()
        {
            return elements.GetEnumerator();
        }

        #endregion

    }
}
