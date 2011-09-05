using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace Linnarsson.Mathematics
{
	public class ClusteringProgressChangedEventArgs : EventArgs
	{
		public string StageName { get; set; }
		public int StageNumber { get; set; }
		public int TotalNumberOfStages { get; set; }
		public int PercentCompletedThisStage { get; set; }
		public bool Cancel { get; set; }
	}

	public enum ClusteringSimilarityMetric { Correlation }

	/// <summary>
	/// Represents a vector on the torus
	/// </summary>
	public struct VectorT
	{
		private double m_dX;
		public double dX
		{
			get { return m_dX; }
		}
		private double m_dY;
		public double dY
		{
			get { return m_dY; }
		}

		public double Length
		{
			get { return Math.Sqrt(dX * dX + dY * dY); }
		}
		public VectorT(double dx, double dy)
		{
			m_dX = dx;
			m_dY = dy;
		}

		public VectorT Add(VectorT other)
		{
			return new VectorT(dX + other.dX, dY + other.dY);
		}

		public VectorT Rescale(double newLength)
		{
			double L = Length;
			if(L == 0) return new VectorT(0,0);
			return new VectorT(dX * newLength / L, dY * newLength / L);
		}
	}

	/// <summary>
	/// Represents a point on the unit square with toroidal boundary conditions
	/// </summary>
	public struct PointT
	{
		private double m_X;
		public double X
		{
			get { return m_X; }
		}

		private double m_Y;
		public double Y
		{
			get { return m_Y; }
		}

		public PointT(double x, double y)
		{
			m_X = x;
			m_Y = y;
		}

		/// <summary>
		/// Returns the (shortest) distance between the two points
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public double Distance(PointT other)
		{
			return Direction(other).Length;
			//return Math.Sqrt(
			//    Math.Pow(Math.Min(Math.Abs(X - other.X), 1 - Math.Abs(X - other.X)), 2) + 
			//    Math.Pow(Math.Min(Math.Abs(Y - other.Y), 1 - Math.Abs(Y - other.Y)), 2));
		}

		public VectorT Direction(PointT other)
		{
			double dx = other.X - X;
			if(dx < -0.5) dx += 1;
			else if(dx > 0.5) dx -= 1;

			double dy = other.Y - Y;
			if(dy < -0.5) dy += 1;
			else if(dy > 0.5) dy -= 1; 
			
			return new VectorT(dx, dy);
		}

		public PointT Add(VectorT delta)
		{
			return new PointT(
				Math.Abs((X + delta.dX) % 1),
				Math.Abs((Y + delta.dY) % 1));
		}
	}

	public class Cluster
	{
		private Dataset m_Dataset;
		public Dataset Dataset
		{
			get { return m_Dataset; }
		}
		private List<int> m_Members;
		/// <summary>
		/// List of the column indexes for the members of this cluster
		/// </summary>
		public List<int> Members
		{
			get { return m_Members; }
		}

		private PointT[] m_MemberPositions;
		/// <summary>
		/// The location of each member in the unit square
		/// </summary>
		public PointT[] MemberPositions
		{
			get { return m_MemberPositions; }
		}

		/// <summary>
		/// The location of the cluster in the unit square
		/// </summary>
		public PointT ClusterPosition { get; set; }

		public int Count
		{
			get { return m_Members.Count; }
		}

		private double[] m_Centroid;
		/// <summary>
		/// The centroid of the cluster member data (i.e. average by row)
		/// </summary>
		public double[] Centroid
		{
			get { return m_Centroid; }
		}

		public Cluster(Dataset data, List<int> members)
		{
			m_Members = members;
			m_Dataset = data;
			double[] cntr = new double[data.Columns[0].Length];
			for(int i = 0; i < cntr.Length; i++)
			{
				double sum = 0;
				for(int j = 0; j < members.Count; j++)
				{
					sum += data.Columns[members[j]][i];
				}
				cntr[i] = sum / members.Count;
			}
			m_Centroid = cntr;
			ClusterPosition = new PointT(0.5,0.5);
			m_MemberPositions = new PointT[members.Count];
			for(int i = 0; i < members.Count; i++)
			{
				m_MemberPositions[i] = new PointT(0.5,0.5);
			}
		}
	}

	public class TopographicClustering
	{
		public event EventHandler<ClusteringProgressChangedEventArgs> ClusteringProgressChanged = delegate { };

		public ClusteringSimilarityMetric SimilarityMetric { get; set; }
		private double[,] Similarities { get; set; }
		public readonly Dataset Dataset;
		public double MinimumClusterSimilarity { get; set; }
		public double GridResolution { get; set; }
		public double MapResolution { get; set; }
		public double ClusterDiameter { get; set; }
		private double[,] m_Elevations;
		public double[,] Elevations
		{
			get { return m_Elevations; }
		}

		private List<Cluster> m_Clusters;
		public List<Cluster> Clusters
		{
			get { return m_Clusters; }
		}

		public TopographicClustering(Dataset data)
		{
			Dataset = data;
			SimilarityMetric = ClusteringSimilarityMetric.Correlation;
			MinimumClusterSimilarity = 0.5;	// The minimum cluster similarity, which defines the number of clusters
			GridResolution = 0.01;			// The resolution at which clusters and objects are placed
											// (although objects end up at an arbitrarily fine resolution)
			MapResolution = 0.02;			// The resolution at which map contours are computed
			ClusterDiameter = 0.2;			// Approximate size of the clusters
		}

		public void Cluster()
		{
			QtCluster(); // Partition the objects using QT clustering
			PlaceClusters(40); // Place the clusters using mutual attraction/repulsion
			PlaceObjects(20); // Place objects around clusters using elastic pull
			printDebug();
			CalculateElevations(); // Create the elevation map
		}

		private void printDebug()
		{
			SaveFileDialog sfd = new SaveFileDialog();
			if(sfd.ShowDialog() == DialogResult.OK)
			{
				StreamWriter sw = new StreamWriter(sfd.FileName);
				foreach(Cluster c in Clusters)
				{
					sw.WriteLine("C" + c.Count.ToString() + "\t" + c.ClusterPosition.X.ToString() + "\t" + c.ClusterPosition.Y.ToString());
				}
				foreach(Cluster c in Clusters)
				{
					for(int i = 0; i < c.MemberPositions.Length; i++)
					{
						sw.WriteLine(Dataset.ColumnNames[c.Members[i]] + "\t" + c.MemberPositions[i].X.ToString() + "\t" + c.MemberPositions[i].Y.ToString());
					}
				}
				sw.Close();
			}
		}

		/// <summary>
		/// Calculates elevation data for the map underlying the clusters
		/// For grid cells that contain objects, the elevation is two minus the average distance 
		/// from the object to its cluster centroid (i.e. data distance, not geometric
		/// distance; range 0 - 2). For grid cells without objects, the elevation is the 
		/// the negative of the geometric distance to the nearest cluster (range 0 to -0.5).
		/// The overall range is thus -0.5 to 2.0 and positive elevations contain objects whereas
		/// negative elevations do not. This may often neatly separate clusters into islands of 
		/// positive elevation (green, brown, gray, white) surrounded by seas of negative elevation (blue).
		/// </summary>
		private void CalculateElevations()
		{
			int width = (int)(1 / MapResolution);
			double[,] grid = new double[width, width];
			int[,] gridCounts = new int[width, width];

			// TODO: share this with the other method
			double[,] objectClusterDistances = ComputeObjectClusterSimilarities();

			for (int c = 0; c < Clusters.Count; c++)
			{
				int m = Clusters[c].Members.Count;
				for(int i = 0; i < m; i++)
				{
					int x = (int)(width * Clusters[c].MemberPositions[i].X);
					int y = (int)(width * Clusters[c].MemberPositions[i].Y);
					gridCounts[x, y]++;
					grid[x, y] += objectClusterDistances[c, i];
				}
			}
			// Now deal with the sea & convert land elevations to averages
			for(int i = 0; i < width; i++)
			{
				for(int j = 0; j < width; j++)
				{
					if(gridCounts[i, j] == 0) // we're at sea
					{
						// This may end up looking a bit strange, since coastlines will have no
						// relation with adjacent sea. Perhaps better to dilate from the coast?
						double d = double.MaxValue;
						PointT pt = new PointT(i * MapResolution, j * MapResolution);
						foreach(Cluster c in Clusters)
						{
							d = Math.Min(d, pt.Distance(c.ClusterPosition));
						}
						grid[i, j] = -d;
					}
					else grid[i, j] = grid[i, j] / gridCounts[i, j];
				}
			}
			m_Elevations = grid;
		}

		private void PlaceObjects(int iterations)
		{
			double[,] objectClusterSimilarities = ComputeObjectClusterSimilarities();
			// Set the initial positions equal to the cluster center
			foreach(Cluster c in Clusters)
			{
				for(int i = 0; i < c.MemberPositions.Length; i++)
				{
					c.MemberPositions[i] = c.ClusterPosition;
				}
			}
			foreach(Cluster c in Clusters)
			{
				for(int i = 0; i < c.Members.Count; i++)
				{
					VectorT resultVector = new VectorT(0, 0);
					for(int j = 0; j < Clusters.Count; j++)
					{
						Cluster other = Clusters[j];
						if(c == other) continue;
						// Calculate vector of elastic potential
						double geometricDistance = c.ClusterPosition.Distance(other.ClusterPosition);
						double pullDistance = ClusterDiameter * Math.Sqrt(objectClusterSimilarities[j, i]*(0.71-geometricDistance));
						
						// Figure out the direction of pull
						VectorT direction = c.ClusterPosition.Direction(other.ClusterPosition);
						resultVector = resultVector.Add(direction.Rescale(pullDistance));
					}
					c.MemberPositions[i] = c.MemberPositions[i].Add(resultVector);
				}
			}
		}

		/// <summary>
		/// Place objects at their maximum attraction
		/// </summary>
		/// <param name="iterations"></param>
		private void PlaceObjectsOld(int iterations)
		{
			double[,] objectClusterSimilarities = ComputeObjectClusterSimilarities();
			// Set the initial positions equal to the cluster center
			foreach(Cluster c in Clusters)
			{
				for(int i = 0; i < c.MemberPositions.Length; i++)
				{
					c.MemberPositions[i] = c.ClusterPosition;
				}
			}
			// Precalculate the normal distribution
			double[] attractionShape = new double[1000];
			for(int i = 0; i < attractionShape.Length; i++)
			{
				attractionShape[i] = new NormalDistribution(0, ClusterDiameter).PDF(i / 1000d);
			}
			for(int iter = 0; iter < iterations; iter++)
			{
				// Set the temperature for this iteration (the sampling variance)
				// Range is 0.25 down to GridResolution, with a linear decay
				double temp = (0.25 - GridResolution) * (1 - iter / (double)iterations) + GridResolution;

				// Compute a set of offsets to sample (no need to do this for each object)
				int samples = (int)((1 / GridResolution) * (1 / GridResolution))/10;
				double[] dx = new double[samples];
				double[] dy = new double[samples];
				for(int i = 0; i < samples; i++)
				{
					dx[i] = new NormalDistribution(0, temp).Sample();
					dy[i] = new NormalDistribution(0, temp).Sample();
				}

				foreach(Cluster c in Clusters)
				{
					for(int i = 0; i < c.Members.Count; i++)
					{
						ScoreTracker<double, PointT> st = new ScoreTracker<double, PointT>();
						
						PointT pt = c.MemberPositions[i];
						for(int s = 0; s < samples; s++)
						{
							double A = 0;
							for(int j = 0; j < Clusters.Count; j++)
							{
								Cluster other = Clusters[j];
								if(c == other) continue;
								double geometricDistance = pt.Distance(other.ClusterPosition);
								// Calculate attraction as normal distribution
								A += objectClusterSimilarities[j, i] * attractionShape[(int)(geometricDistance * 1000)];
							}

							A = A * new NormalDistribution(0, ClusterDiameter).PDF(pt.Distance(c.ClusterPosition));
							st.Examine(A, pt);
							pt = new PointT(Math.Abs((c.MemberPositions[i].X + dx[s]) % 1), Math.Abs((c.MemberPositions[i].Y + dy[s]) % 1));
						}
						c.MemberPositions[i] = st.MaxItem;
					}
				}
			}
		}

		/// <summary>
		/// Place the clusters at their minimum repulsion
		/// </summary>
		private void PlaceClusters(int iterations)
		{
			double[,] centroidSimilarities = ComputeClusterSimilarities();

			//// Precalculate the force field
			//double[] repulsionShape = new double[1000];
			//for(int i = 0; i < repulsionShape.Length; i++)
			//{
			//    //repulsionShape[i] = new NormalDistribution(0, ClusterCohesion).PDF(i/1000d);
			//    repulsionShape[i] = Math.Pow(1 - i / 1000d, 1/ClusterRepulsion);
			//}
			foreach(Cluster c in Clusters)
			{
				c.ClusterPosition = new PointT(MersenneTwister.Instance.NextDouble(), MersenneTwister.Instance.NextDouble());
			}
			for(int iter = 0; iter < iterations; iter++)
			{
				for(int i = 0; i < Clusters.Count; i++)
				{
					ScoreTracker<double, PointT> st = new ScoreTracker<double, PointT>();
					for(double x = 0; x < 1; x += GridResolution)
					{
						for(double y = 0; y < 1; y += GridResolution)
						{
							PointT pt = new PointT(x, y);
							double R = 0;
							for(int j = 0; j < Clusters.Count; j++)
							{
								Cluster other = Clusters[j];
								if(i == j) continue;
								double geometricDistance = pt.Distance(other.ClusterPosition);

								// Calculate energy potential
								//R += centroidDistances[i, j] * new NormalDistribution(0, ClusterRepulsion).PDF(geometricDistance);
								//R += (2 - centroidSimilarities[i, j]) * repulsionShape[(int)(geometricDistance*1000)];
								R += 1 / geometricDistance - new NormalDistribution(0.1, 0.1).PDF(geometricDistance) * centroidSimilarities[i, j] * 10;
							}
							st.Examine(R, pt);
						}
					}
					Clusters[i].ClusterPosition = st.MinItem;
				}
			}
		}


		/// <summary>
		/// Cluster the given dataset by columns (i.e. items are in columns, measures are in rows),
		/// using the QT algorithm of Heyer et al (Genome Research 1999, 9:1106-1115)
		/// </summary>
		/// <param name="data"></param>
		/// <param name="maxD">Maximum cluster diameter</param>
		private void QtCluster()
		{
			int numcols = Dataset.Columns.Count;

			// 1. Compute distance matrix
			if(Similarities == null) Similarities = ComputeObjectSimilarities(Dataset);
			
			// 2. Compute clusters
			int unclustered = numcols;
			int nextCluster = 1;
			int[] clusterIndexes = new int[numcols]; // zero means currently unclustered, otherwise number means cluster #
			List<Cluster> result = new List<Cluster>(); // The clusters that we will return
			while(unclustered > 0)
			{
				// Find the best cluster for each column
				List<int>[] clusters = new List<int>[numcols];
				for(int i = 0; i < numcols; i++)
				{
					clusters[i] = findQtClusterForColumn(Dataset, i, clusterIndexes);
				}

				// See which cluster is largest
				ScoreTracker<int, int> largestCluster = new ScoreTracker<int, int>();
				for(int i = 0; i < numcols; i++)
				{
					largestCluster.Examine(clusters[i].Count, i);
				}

				// Update the cluster indexes
				unclustered -= largestCluster.MaxScore;
				result.Add(new Cluster(Dataset, clusters[largestCluster.MaxItem]));
				foreach(int c in clusters[largestCluster.MaxItem])
				{
					clusterIndexes[c] = nextCluster;
				}
				nextCluster++;
			}
			m_Clusters = result;
		}

		private List<int> findQtClusterForColumn(Dataset data, int col, int[] clusterIndexes)
		{
			int numcols = data.Columns.Count;
			List<int> members = new List<int>();

			// The seed is a member of the cluster
			members.Add(col);

			while(true)
			{
				// Find the column that minimally extends the cluster diameter
				ScoreTracker<double, int> bestCandidate = new ScoreTracker<double, int>();
				for(int i = 0; i < numcols; i++)
				{
					if(clusterIndexes[i] != 0) continue; // already clustered
					if(members.Contains(i)) continue;
					
					// Compute the new cluster diameter
					ScoreTracker<double> st = new ScoreTracker<double>();
					foreach(int m in members)
					{
						st.Examine(Similarities[i, m]);
					}
					double d = st.MinScore;
					bestCandidate.Examine(d, i);
				}
				if(bestCandidate.HasBestScore == false || bestCandidate.MaxScore < MinimumClusterSimilarity) return members;
				members.Add(bestCandidate.MaxItem);
			}
		}

		private double[,] ComputeObjectSimilarities(Dataset data)
		{
			int cols = data.Columns.Count;
			double[,] result = new double[cols, cols];
			for(int i = 0; i < cols; i++)
			{
				for(int j = 0; j < cols; j++)
				{
					if(j > i) break;
					switch(SimilarityMetric)
					{
						case ClusteringSimilarityMetric.Correlation:
							result[i, j] = (1 + DescriptiveStatistics.Correlation(data.Columns[i], data.Columns[j]))/2;
							break;
					}
				}
			}
			for(int i = 0; i < cols; i++)
			{
				for(int j = 0; j < cols; j++)
				{
					if(j < i) continue;
					result[i, j] = result[j, i];
				}
			}
			return result;
		}

		private double[,] ComputeClusterSimilarities()
		{
			int cols = Clusters.Count;
			double[,] result = new double[cols, cols];
			for(int i = 0; i < cols; i++)
			{
				for(int j = 0; j < cols; j++)
				{
					if(j > i) break;
					switch(SimilarityMetric)
					{
						case ClusteringSimilarityMetric.Correlation:
							result[i, j] = (1 + DescriptiveStatistics.Correlation(Clusters[i].Centroid, Clusters[j].Centroid))/2;
							break;
					}
				}
			}
			for(int i = 0; i < cols; i++)
			{
				for(int j = 0; j < cols; j++)
				{
					if(j < i) continue;
					result[i, j] = result[j, i];
				}
			}
			return result;
		}

		private double[,] ComputeObjectClusterSimilarities()
		{
			int cols = Clusters.Count;
			int rows = Dataset.Columns.Count;
			double[,] result = new double[cols, rows];
			for(int i = 0; i < cols; i++)
			{
				for(int j = 0; j < rows; j++)
				{
					switch(SimilarityMetric)
					{
						case ClusteringSimilarityMetric.Correlation:
							result[i, j] = (1 + DescriptiveStatistics.Correlation(Clusters[i].Centroid, Dataset.Columns[j]))/2;
							break;
					}
				}
			}
			return result;
		}
	}
}
