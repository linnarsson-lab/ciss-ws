using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Linnarsson.Mathematics
{
	public class ImageAnalysis
	{
		public static List<Point> FindLocalMaxima(MatrixUshort m, int distance)
		{
			List<Point> result = new List<Point>();
			for(int r = distance; r < m.Rows - distance; r++)
			{
				for(int c = distance; c < m.Columns - distance; c++)
				{
					ushort max = ushort.MinValue;
					for(int rx = -distance; rx < distance; rx++)
					{
						for(int cx = -distance; cx < distance; cx++)
						{
							if(rx == 0 && cx == 0) continue;
							if(m[r + rx, c + cx] > max) max = m[r + rx, c + cx];
						}						
					}
					if(m[r, c] > max) result.Add(new Point(c, r));
				}
			}
			return result;
		}

		/// <summary>
		/// Return the best alignment of two images, based on the maximum product at
		/// positions defined by the given list of points (which may be e.g. features).
		/// </summary>
		/// <returns></returns>
		public static Point AlignXY(MatrixUshort a, MatrixUshort b, List<Point> features, int maxOffset)
		{
			if(a == b) return new Point(0, 0);

			Point bestOffsets = new Point(0, 0);
			ScoreTracker<double, Point> bestAlignment = new ScoreTracker<double, Point>();
			for(int offsetX = -maxOffset; offsetX < maxOffset; offsetX++)
			{
				for(int offsetY = -maxOffset; offsetY < maxOffset; offsetY++)
				{
					Rectangle unipR = new Rectangle(0, 0, a.Columns, a.Rows);
					Rectangle clipR = new Rectangle(offsetX, offsetY, b.Columns, b.Rows);
					unipR.Intersect(clipR);
					double score = 0;
					int scoreCount = 0;
					for(int ix = 0; ix < features.Count; ix++)
					{
						if(features[ix].X - offsetX > 0 && features[ix].X - offsetX < b.Columns && features[ix].Y - offsetY > 0 && features[ix].Y - offsetY < b.Rows)
						{
							score += b[features[ix].Y - offsetY, features[ix].X - offsetX] * a[features[ix].Y, features[ix].X];
							scoreCount++;
						}
					}
					bestAlignment.Examine(score / scoreCount, new Point(offsetX, offsetY));
				}
			}
			return bestAlignment.MaxItem;
		}
	}
}
