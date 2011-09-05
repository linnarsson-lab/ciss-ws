using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class HistogramBin
	{
		private double m_LowerBound;
		public double LowerBound
		{
			get { return m_LowerBound; }
			set { m_LowerBound = value; }
		}

		private double m_UpperBound;
		public double UpperBound
		{
			get { return m_UpperBound; }
			set { m_UpperBound = value; }
		}

		public double Middle
		{
			get { return LowerBound + (UpperBound - LowerBound)/2.0; }
		}

		public double Density
		{
			get { return Count/(UpperBound - LowerBound); }
		}

		private double m_Count;
		public double Count
		{
			get { return m_Count; }
			set { m_Count = value; }
		}

		public HistogramBin(double lower, double upper, double count)
		{
			m_LowerBound = lower;
			m_UpperBound = upper;
			m_Count = count;
		}

		public HistogramBin(double lower, double upper)
		{
			m_LowerBound = lower;
			m_UpperBound = upper;
			m_Count = 0;
		}

	}

	public class Histogram
	{
		private List<HistogramBin> m_Bins = new List<HistogramBin>();
		public List<HistogramBin> Bins
		{
			get { return m_Bins; }
			set { m_Bins = value; }
		}


		/// <summary>
		/// Create a regular histogram of the data.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="lower"></param>
		/// <param name="upper"></param>
		/// <param name="numBins"></param>
		/// <param name="inPlace">If true, the data will be sorted in place; if false, a copy of the data will be made.</param>
		/// <returns></returns>
		public static Histogram Create(IList<double> data, double lower, double upper, int numBins, bool inPlace)
		{
			if(!inPlace)
			{
				double[] copy = new double[data.Count];
				data.CopyTo(copy, 0);
				data = copy;
			}

			Histogram result = new Histogram();
			Sort.HeapSort<double>(data);
			int ix = 0;
			double binSize = (upper - lower) / numBins;

			// Skip NaN and items below lower
			while(ix < data.Count && (data[ix] < lower || double.IsNaN(data[ix]))) ix++;

			result.Bins.Add(new HistogramBin(lower, lower + binSize));
			HistogramBin current = result.Bins[0];
			while(ix < data.Count && data[ix] < upper)
			{
				while(data[ix] >= current.UpperBound)
				{
					current = new HistogramBin(current.UpperBound, current.UpperBound + binSize);
					result.Bins.Add(current);
				}
				current.Count++;
				ix++;
			}
			while(result.Bins.Count < numBins)
			{
				current = new HistogramBin(current.UpperBound, current.UpperBound + binSize);
				result.Bins.Add(current);
			}
			return result;
		}

		/// <summary>
		/// Create a logarithmically binned histogram
		/// </summary>
		/// <param name="data"></param>
		/// <param name="lower">The lower bound of the histogram</param>
		/// <param name="binFactor">The multiplicative factor between each bin and the next</param>
		/// <param name="inPlace">If true, the data will be sorted in place; if false, a copy of the data will be made.</param>
		/// <returns></returns>
		public static Histogram CreateLogarithmic(IList<double> data, double lower, double binFactor, bool inPlace)
		{
			if(lower <= 0 || binFactor <= 1.0) throw new ArgumentOutOfRangeException("Invalid arguments for logarithmic histogram!");

			if(!inPlace)
			{
				double[] copy = new double[data.Count];
				data.CopyTo(copy, 0);
				data = copy;
			}
			Sort.HeapSort<double>(data);

			Histogram histo = new Histogram();
			int ix = 0;
			int count = 0;
			double upper = lower * binFactor;

			// Skip NaN and items below lower
			while(ix < data.Count && (data[ix] < lower || double.IsNaN(data[ix]))) ix++;

			// Move along until the first data point is inside the bin
			while(data[ix] > upper)
			{
				lower = upper;
				upper *= binFactor;
			}

			// Create bins until we run out of data
			while(ix < data.Count)
			{
				count++;
				if(data[ix] > upper)
				{
					histo.Bins.Add(new HistogramBin(lower, upper, count));
					lower = upper;
					upper *= binFactor;
					count = 0;
				}
				ix++;
			}
			return histo;
		}


		/// <summary>
		/// Creates a histogram with bins adjusted so that each bin contains an equal number of items. As 
		/// a side effect, the input array will be sorted.
		/// </summary>
		/// <param name="data"></param>
		/// <param name="numBins"></param>
		/// <param name="inPlace">If true, the data will be sorted in place; if false, a copy of the data will be made.</param>
		/// <returns></returns>
		public static Histogram CreateAutoBinnedInPlace(IList<double> data, int numBins, bool inPlace)
		{
			if(!inPlace)
			{
				double[] copy = new double[data.Count];
				data.CopyTo(copy, 0);
				data = copy;
			}
			int binCount = data.Count / numBins;
			Histogram histo = new Histogram();

			Sort.HeapSort<double>(data);
			double lower = data[0];
			for(int ix = 0; ix < numBins - 1; ix++)
			{
				histo.Bins.Add(new HistogramBin(data[ix * binCount], data[(ix + 1) * binCount], binCount));
			}
			if(data.Count - (numBins - 1) * binCount != 0) histo.Bins.Add(new HistogramBin(data[(numBins - 1) * binCount], data[data.Count - 1], data.Count - (numBins - 1) * binCount));
			return histo;
		}
	}
}
