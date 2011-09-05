using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics.Clustering
{
	/// <summary>
	/// Represents a topographic map used as the background image for a 
	/// topographic clustering. The map has a number of contoured levels and line
	/// segments separating them
	/// </summary>
	public class TopographicMap
	{
		private int[,] m_Levels;
		// A matrix of integers giving levels and contour lines. Even numbers are levels,
		// odd numbers are contour lines. Level 0 is contoured with line 1 etc.
		public int[,] Levels
		{
			get { return m_Levels; }
			set { m_Levels = value; }
		}
		public readonly int Width;

		public TopographicMap(double[,] grid, double[] levels, int mapWidth)
		{
			Width = mapWidth;
			m_Levels = new int[mapWidth, mapWidth]; // these are akin to pixels, but give level numbers
			double ratio = mapWidth/(double)grid.GetLength(0);

			// The idea is to create a spline interpolation of the grid and then use that to draw the contours
			double[] xPoints = new double[grid.GetLength(0)];
			double[] yPoints = new double[grid.GetLength(1)];
			BicubicSpline bs = new BicubicSpline(xPoints, yPoints, grid);
			double[,] interpolated = new double[mapWidth, mapWidth];
			for(int x = 0; x < mapWidth; x++)
			{
				for(int y = 0; y < mapWidth; y++)
				{
					interpolated[x, y] = bs[(int)(x/ratio), (int)(y/ratio)];
				}
			}
			for(int x = 1; x < mapWidth-1; x++)
			{
				for(int y = 1; y < mapWidth-1; y++)
				{
					// Find the min and max values of surrounding pixels
					ScoreTracker<double> st = new ScoreTracker<double>();
					st.Examine(interpolated[x - 1, y - 1]);
					st.Examine(interpolated[x, y - 1]);
					st.Examine(interpolated[x + 1, y - 1]);
					st.Examine(interpolated[x - 1, y]);
					st.Examine(interpolated[x + 1, y]);
					st.Examine(interpolated[x - 1, y + 1]);
					st.Examine(interpolated[x, y + 1]);
					st.Examine(interpolated[x + 1, y + 1]);

					// Determine the level of each
					int levelMin = 0, levelMax = 0;
					for(int L = 0; L < levels.Length; L++)
					{
						if(st.MinScore > levels[L]) levelMin = L;
						if(st.MaxScore > levels[L]) levelMax = L;
					}
					if(levelMin == levelMax) m_Levels[x, y] = levelMin * 2;
					else m_Levels[x, y] = levelMax * 2 - 1;
				}
			}
		}
	}
}
