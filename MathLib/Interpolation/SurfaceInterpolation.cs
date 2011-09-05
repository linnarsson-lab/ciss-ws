using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace Linnarsson.Mathematics
{
	/// <summary>
	/// Interpolates a surface using (potentially) different interpolations in the
	/// X and Y directions. 
	/// </summary>
	/// <typeparam name="X"></typeparam>
	/// <typeparam name="Y"></typeparam>
	public class SurfaceInterpolation<X,Y> : IInterpolation2D
		where X: IInterpolation, new() 
		where Y: IInterpolation, new()
	{
		private X[] rowInterp;
		private double[] m_XPoints;
		public double[] XPoints
		{
			get { return m_XPoints; }
			set { m_XPoints = value; }
		}

		private double[] m_YPoints;
		public double[] YPoints
		{
			get { return m_YPoints; }
			set { m_YPoints = value; }
		}

		private Dictionary<double, Y> cache = new Dictionary<double, Y>();
		public double this[double x, double y]
		{
			get
			{
				Y colInterp;
				if(cache.ContainsKey(x)) colInterp = cache[x];
				else
				{
					double[] columnData = new double[YPoints.Length];
					for(int ix = 0; ix < columnData.Length; ix++)
					{
						columnData[ix] = rowInterp[ix][x];
					}
					colInterp = new Y();
					colInterp.Construct(YPoints, columnData);
					if(cache.Count < 5000) cache[x] = colInterp;
				}
				return colInterp[y];
			}
		}

		public SurfaceInterpolation(double[] xPoints, double[] yPoints, double[,] zValues)
		{
			m_XPoints = xPoints;
			m_YPoints = yPoints;

			rowInterp = new X[yPoints.Length];
			for (int yIndex = 0; yIndex < yPoints.Length; yIndex++)
			{
				double[] xValues = new double[xPoints.Length];
				for (int xIndex = 0; xIndex < xValues.Length; xIndex++)
				{
					xValues[xIndex] = zValues[xIndex, yIndex];
				}
				rowInterp[yIndex] = new X();
				rowInterp[yIndex].Construct(xPoints, xValues);
			}
		}
	}

	public class BicubicSpline : SurfaceInterpolation<CubicSpline, CubicSpline>
	{
		public BicubicSpline(double[] xPoints, double[] yPoints, double[,] zValues) : base(xPoints, yPoints, zValues) { }
	}

	public class BipolynomialInterpolation : SurfaceInterpolation<PolynomialInterpolation, PolynomialInterpolation>
	{
		public BipolynomialInterpolation(double[] xPoints, double[] yPoints, double[,] zValues) : base(xPoints, yPoints, zValues) { }
	}
}
