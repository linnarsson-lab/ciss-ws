using System;
using System.Collections.Generic;
using System.Linq;


/// <summary>
/// Linear Interpolation using the least squares method
/// <remarks>http://mathworld.wolfram.com/LeastSquaresFitting.html</remarks> 
/// </summary>
public class LinearLeastSquares
{
    /// <summary>
    /// point list constructor
    /// </summary>
    /// <param name="points">points list</param>
    public LinearLeastSquares(IEnumerable<LLPoint> points)
    {
        Points = points;
    }
    /// <summary>
    /// abscissae/ordinates constructor
    /// </summary>
    /// <param name="x">abscissae</param>
    /// <param name="y">ordinates</param>
    public LinearLeastSquares(IEnumerable<float> x, IEnumerable<float> y)
    {
        if (x.Empty() || y.Empty())
            throw new ArgumentNullException("null-x");
        if (y.Empty())
            throw new ArgumentNullException("null-y");
        if (x.Count() != y.Count())
            throw new ArgumentException("diff-count");
        Points = GetPoints(x, y);
    }

    private IEnumerable<LLPoint> GetPoints(IEnumerable<float> x, IEnumerable<float> y)
    {
        using (IEnumerator<float> xn = x.GetEnumerator())
        using (IEnumerator<float> yn = y.GetEnumerator())
        {
            while (xn.MoveNext() && yn.MoveNext())
                yield return new LLPoint { x = xn.Current, y = yn.Current };
        }
    }

    private IEnumerable<LLPoint> Points;
    /// <summary>
    /// original points count
    /// </summary>
    public int Count { get { return Points.Count(); } }

    /// <summary>
    /// group points with equal x value, average group y value
    /// </summary>
    public IEnumerable<LLPoint> UniquePoints
    {
        get
        {
            var grp = Points.GroupBy((p) => { return p.x; });
            foreach (IGrouping<float, LLPoint> g in grp)
            {
                float currentX = g.Key;
                float averageYforX = g.Select(p => p.y).Average();
                yield return new LLPoint() { x = currentX, y = averageYforX };
            }
        }
    }
    /// <summary>
    /// count of point set used for interpolation
    /// </summary>
    public int CountUnique { get { return UniquePoints.Count(); } }
    /// <summary>
    /// abscissae
    /// </summary>
    public IEnumerable<float> X { get { return UniquePoints.Select(p => p.x); } }
    /// <summary>
    /// ordinates
    /// </summary>
    public IEnumerable<float> Y { get { return UniquePoints.Select(p => p.y); } }
    /// <summary>
    /// x mean
    /// </summary>
    public float AverageX { get { return X.Average(); } }
    /// <summary>
    /// y mean
    /// </summary>
    public float AverageY { get { return Y.Average(); } }

    /// <summary>
    /// the computed slope, aka regression coefficient
    /// </summary>
    public float Slope { get { return ssxy / ssxx; } }

    // dotvector(x,y)-n*avgx*avgy
    float ssxy { get { return X.DotProduct(Y) - CountUnique * AverageX * AverageY; } }
    //sum squares x - n * square avgx
    float ssxx { get { return X.DotProduct(X) - CountUnique * AverageX * AverageX; } }

    /// <summary>
    /// computed  intercept
    /// </summary>
    public float Intercept { get { return AverageY - Slope * AverageX; } }

    public override string ToString()
    {
        return string.Format("slope:{0:F02} intercept:{1:F02}", Slope, Intercept);
    }
}

/// <summary>
/// any given point
/// </summary>
public class LLPoint
{
    public float x { get; set; }
    public float y { get; set; }
}

/// <summary>
/// Linq extensions
/// </summary>
public static class Extensions
{
    /// <summary>
    /// dot vector product
    /// </summary>
    /// <param name="a">input</param>
    /// <param name="b">input</param>
    /// <returns>dot product of 2 inputs</returns>
    public static float DotProduct(this IEnumerable<float> a, IEnumerable<float> b)
    {
        float result = 0.0f;
        using (IEnumerator<float> an = a.GetEnumerator())
        using (IEnumerator<float> bn = b.GetEnumerator()) {
            while (an.MoveNext() && bn.MoveNext())
                result += an.Current * bn.Current;
        }
        return result;
    }
    /// <summary>
    /// is empty enumerable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a"></param>
    /// <returns></returns>
    public static bool Empty<T>(this IEnumerable<T> a)
    {
        return a == null || a.Count() == 0;
    }
}