using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HAC
{
    public class Cluster : IEnumerable
    {
        private static int defaultId = 1;

        HashSet<Element> elements = new HashSet<Element>();
        public List<Cluster> Children = new List<Cluster>();
        public double ChildrenDistance { get; set; }

        public bool IsLeaf { get { return Children.Count == 0; } }
        public bool HasSpatialRestriction { get { return neighbors.Count > 0; } }

        HashSet<Cluster> neighbors = new HashSet<Cluster>();
        Fusion fusion;

        public string Id { get; private set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format("{0}. {1} elements:", Id, elements.Count));
            List<Element> es = elements.ToList();
            es.Sort((x, y) => x.Id.CompareTo(y.Id));
            foreach (Element e in es)
                sb.Append(" " + e.Id);
            sb.Append('\n');
            if (IsLeaf)
                sb.Append("   - no children.");
            else
            {
                sb.Append("   Children:");
                foreach (Cluster c in Children)
                    sb.Append(" " + c.Id);
                sb.Append('\n');
            }
            if (HasSpatialRestriction)
            {
                sb.Append(string.Format("   {0} Neighbors:", neighbors.Count));
                List<Cluster> ns = neighbors.ToList();
                ns.Sort((x, y) => x.Id.CompareTo(y.Id));
                foreach (Cluster n in ns)
                    sb.Append(" " + n.Id);
                sb.Append('\n');
            }
            return sb.ToString();
        }

        public Cluster(Fusion fusion)
        {
            this.fusion = fusion;
            this.Id = "Cluster" + defaultId;
            defaultId++;
        }

        public Cluster(Fusion fusion, Element initialElement)
        {
            this.fusion = fusion;
            this.elements.Add(initialElement);
            DataPointCount = initialElement.DataPointCount;
            this.Id = initialElement.Id;
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

        public Element[] GetElements()
        {
            return elements.ToArray<Element>();
        }

        internal void FromPair(ClusterPair pair)
        {
            AddCluster(pair.Cluster1);
            AddCluster(pair.Cluster2);
            ChildrenDistance = pair.Distance;
            neighbors.Remove(pair.Cluster1);
            neighbors.Remove(pair.Cluster2);
        }

        internal void AddCluster(Cluster cluster)
        {
            Children.Add(cluster);
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
            bool eitherINneighbors = neighbors.Remove(oldCluster1);
            eitherINneighbors |= neighbors.Remove(oldCluster2);
            if (eitherINneighbors)
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
