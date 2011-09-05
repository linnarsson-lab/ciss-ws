using System;
using System.Collections;
using System.Collections.Generic;

namespace Linnarsson.Mathematics
{
	public enum DistributionTails { LeftTailed, RightTailed, TwoTailed }

	public struct StudentTSummary
	{
		public double TValue;
		public double PValue;
		public double DegreesOfFreedom;

		public StudentTSummary(double t, double p, double df)
		{
			TValue = t;
			PValue = p;
			DegreesOfFreedom = df;
		}
	}

	/// <summary>
	/// A class that provides static methods for common statistical formulas.
	/// </summary>
	public class DescriptiveStatistics
	{
		private bool hasRemoved = false;
		private double min = double.MaxValue;
		private double max = double.MinValue;
		private int sampleNumber; // = 0;
		private double sampleWeight; //  = 0.0;
		private double sum; //  = 0.0;
		private double quadraticSum; //  = 0.0;
		private double downsideQuadraticSum; //  = 0.0;
		private double cubicSum; //  = 0.0;
		private double fourthPowerSum; //  = 0.0;


		/// <summary>
		/// Create a new instance of this class. Instances can be used to accumulate data
		/// and compute statistics on the aggregate. If you only need a one-off calculation
		/// use the static methods instead.
		/// </summary>
		public DescriptiveStatistics()
		{
		}
		public DescriptiveStatistics(double [] values) : base()
		{
			this.AddRange(values);
		}
		#region Static methods

		/// <summary>
		/// Simultaneously find the min and max values in an array. If you also need their positions, use
		/// ScoreTracker.MinMax().
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <param name="min"></param>
		/// <param name="max"></param>
		public static void MinMax<T>(IList<T> values, out T min, out T max) where T: IComparable<T>
		{
			ScoreTracker<T,int> tracker = ScoreTracker<T>.MinMax(values);
			min = tracker.MinScore;
			max = tracker.MaxScore;
		}
		/// <summary>
		/// Get the maximum value in an array. If you also need the minimum, use MinMax(). If
		/// you also need the positions of max and min, use ScoreTracker.MinMax()."/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <returns></returns>
		public static T Max<T>(IList<T> values) where T : IComparable<T>
		{
			T min, max;
			MinMax<T>(values, out min, out max);
			return max;
		}

		/// <summary>
		/// Get the minimum value in an array. If you also need the maximum, use MinMax(). If
		/// you also need the positions of max and min, use ScoreTracker.MinMax()."/>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="values"></param>
		/// <returns></returns>
		public static T Min<T>(IList<T> values) where T : IComparable<T>
		{
			T min, max;
			MinMax<T>(values, out min, out max);
			return min;
		}


		/// <summary>
		/// Obtain the standard deviation of a set of numbers.
		/// </summary>
		/// <param name="numbers">The set of numbers.</param>
		/// <returns>The standard deviation of the numbers.</returns>
		public static double Stdev(double [] numbers)
		{
			return Math.Sqrt(Variance(numbers));
		}
		public static double Stdev(int[] numbers)
		{
			return Math.Sqrt(Variance(numbers));
		}

        /// <summary>
        /// Calculate the Standard error of the mean
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        public static double SEM(double[] numbers)
        {
            if (numbers.Length == 0) return double.NaN;
            return Stdev(numbers) / Math.Sqrt(numbers.Length);
        }

        /// <summary>
        /// Calculate the Coefficient of Variation
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        public static double CV(double[] numbers)
        {
            if (numbers.Length == 0) return double.NaN;
            double mean = Mean(numbers);
            double sd = Math.Sqrt(SSM(numbers, mean) / (numbers.Length - 1));
            return sd / mean;
        }

        private static double SSM(double[] numbers, double mean)
        {
            if (numbers.Length == 0) return double.NaN;
            double ss = 0.0;
            foreach (double v in numbers)
                ss += Math.Pow(v - mean, 2);
            return ss;
        }

		/// <summary>
		/// Obtain the average of a set of numbers.
		/// </summary>
		/// <param name="numbers">The set of numbers.</param>
		/// <returns>The average of the numbers.</returns>
		public static double Mean(double [] numbers)
		{
			double sum = 0F;
			foreach(double number in numbers)
			{
				sum += number;
			}
			return sum / numbers.Length;
		}

		/// <summary>
		/// Obtain the average of a set of numbers.
		/// </summary>
		/// <param name="numbers">The set of numbers.</param>
		/// <returns>The average of the numbers.</returns>
		public static double Mean(int[] numbers)
		{
			double sum = 0F;
			foreach(int number in numbers)
			{
				sum += number;
			}
			return sum / numbers.Length;
		}


		/// <summary>
		/// Compute the median of an array of numbers. NOTE: the array
		/// must be sorted. Use Median() if the array is not sorted.
		/// </summary>
		/// <param name="numbers">A sorted array of numbers.</param>
		/// <returns></returns>
		public static double MedianSorted(IList<double> numbers)
		{
			if (numbers.Count % 2 == 1)
			{ 
				return numbers[(numbers.Count) / 2];
			}
			else
			{
				return (numbers[numbers.Count / 2 - 1] + numbers[numbers.Count / 2]) / 2;
			}
		}

		public static double PercentileSorted(IList<double> numbers, double p)
		{
			return numbers[(int)(numbers.Count*p)];
		}
		public static double Percentile(IList<double> numbers, double p)
		{
			List<double> temp = new List<double>(numbers);
			temp.Sort();
			return PercentileSorted(temp, p);
		}

		/// <summary>
		/// Compute the median of an array of numbers. 
		/// NOTE: If the array is (or can be) sorted, call MedianSorted() instead to avoid the need to
		/// create a sorted copy of the array.
		/// </summary>
		/// <param name="numbers"></param>
		/// <returns></returns>
		public static double Median(IList<double> numbers)
		{
			List<double> temp = new List<double>(numbers);
			temp.Sort();
			return MedianSorted(temp);
		}

		/// <summary>
		/// Compute a robust average using Tukey's one-step biweight method.
		/// NOTE: The input must be sorted.
		/// </summary>
		/// <param name="numbers">A sorted array of numbers.</param>
		/// <param name="c">The control parameter.</param>
		/// <returns></returns>
		public static double RobustMean(double [] numbers, double c)
		{
			double median = DescriptiveStatistics.MedianSorted(numbers);
			double [] absdiff = new double [numbers.Length];

			for(int ix = 0; ix < numbers.Length; ix++)
			{
				absdiff[ix] = Math.Abs(numbers[ix] - median);
			}
			double [] temp = (double [])(absdiff.Clone());
			Array.Sort(temp);
			double MAD = DescriptiveStatistics.MedianSorted(temp);

			double MAD_FACTOR = (c*MAD + 0.0001f);
			for(int ix = 0; ix < numbers.Length; ix++)
			{
				temp[ix] = (Math.Abs(numbers[ix] - median)) / MAD_FACTOR;
			}
			
			double sum1 = 0, sum2 = 0;
			for(int ix = 0; ix < numbers.Length; ix++)
			{
				sum1 += numbers[ix]*TukeyWeight(temp[ix]); 
				sum2 += TukeyWeight(temp[ix]);
			}
			return sum1/sum2;
		}

		private static double TukeyWeight(double u)
		{
			return Math.Abs(u) > 1 ? 0 : Math.Pow(1 - Math.Pow(u, 2),2);
		}
		/// <summary>
		/// Compute a robust average using Tukey's one-step biweight method,
		/// using a default control parameter of c = 5.0. NOTE: the input
		/// must be sorted.
		/// </summary>
		/// <param name="numbers">A sorted array of numbers.</param>
		/// <returns></returns>
		public static double RobustMean(double [] numbers)
		{
			return DescriptiveStatistics.RobustMean(numbers, 5);
		}

		/// <summary>
		/// Obtain the variance of a set of numbers.
		/// </summary>
		/// <param name="numbers">The set of numbers.</param>
		/// <returns>The variance of the numbers.</returns>
		public static double Variance(double [] numbers)
		{
			double sumOfSquares = 0F;
			double average = Mean(numbers);

			foreach(double number in numbers)
			{
				sumOfSquares += Math.Pow(number - average, 2); 
			}
			return sumOfSquares/(numbers.Length - 1);
		}
		public static double Variance(int[] numbers)
		{
			double sumOfSquares = 0F;
			double average = Mean(numbers);

			foreach(int number in numbers)
			{
				sumOfSquares += Math.Pow(number - average, 2);
			}
			return sumOfSquares / (numbers.Length - 1);
		}
		/// <summary>
		/// Calculate the R-hat value used to assess convergence of a Markov chain simulation
		/// R-hat is defined in Gelman "Bayesian Data Analysis".
		/// </summary>
		/// <param name="vector1"></param>
		/// <param name="vector2"></param>
		/// <returns></returns>
		public static float R_hat(float[] vector1, float[] vector2)
		{
            throw new NotImplementedException();
		}

		public static double Correlation(double[] a, double[] b)
		{
			if(a.Length != b.Length) throw new InvalidOperationException("The two arrays must be of same length");
			double meanA = Mean(a), meanB = Mean(b);
			double sdA = Stdev(a), sdB = Stdev(b);
			double result = 0d;

			for(int i = 0; i < a.Length; i++)
			{
				result += ((a[i] - meanA) / sdA) * ((b[i] - meanB) / sdB);
			}
			result = result / (a.Length - 1);
			return result;
		}

		public static double Correlation(int[] a, int[] b)
		{
			if(a.Length != b.Length) throw new InvalidOperationException("The two arrays must be of same length");
			double meanA = Mean(a), meanB = Mean(b);
			double sdA = Stdev(a), sdB = Stdev(b);
			double result = 0d;

			for(int i = 0; i < a.Length; i++)
			{
				result += ((a[i] - meanA) / sdA) * ((b[i] - meanB) / sdB);
			}
			result = result / (a.Length - 1);
			return result;
		}

		/// <summary>
		/// Student's t test comparing two samples.
		/// </summary>
		/// <param name="controlMean"></param>
		/// <param name="controlStdev"></param>
		/// <param name="controlCount"></param>
		/// <param name="treatmentMean"></param>
		/// <param name="treatmentStdev"></param>
		/// <param name="treatmentCount"></param>
		/// <returns></returns>
		public static StudentTSummary StudentTTest(double controlMean, double controlStdev, int controlCount, double treatmentMean, double treatmentStdev, int treatmentCount, DistributionTails tails)
		{
			double scaledControlStdev = Math.Pow(controlStdev,2)/controlCount;
			double scaledTreatmentStdev = Math.Pow(treatmentStdev,2)/treatmentCount;
			double t = ((controlMean - treatmentMean)/Math.Sqrt(scaledControlStdev + scaledTreatmentStdev));
			double df = (Math.Pow(scaledControlStdev + scaledTreatmentStdev, 2) /
				(Math.Pow(scaledControlStdev, 2) / (controlCount - 1) + Math.Pow(scaledTreatmentStdev, 2) / (treatmentCount - 1)));
			double p = 1.0f;
			switch(tails)
			{
				case DistributionTails.RightTailed:
					// p = the area to the right of t
					p = (1 - new StudentTDistribution(df).CDF(t));
					break;
				case DistributionTails.LeftTailed:
					// p = the area to the left of t
					p = (new StudentTDistribution(df).CDF(t));
					break;
				case DistributionTails.TwoTailed:
					if(t > 0)
					{
						// p = twice the area to the right of t
						p = 2 * (1 - new StudentTDistribution(df).CDF(t));
					}
					else
					{
						// p = twice the area to the left of t
						p = 2 * (new StudentTDistribution(df).CDF(t));
					}
					break;
			}
			return new StudentTSummary(t, p, df);
		}

		/// <summary>
		/// Student's t test comparing a sample to a reference mean.
		/// </summary>
		/// <param name="controlMean"></param>
		/// <param name="controlStdev"></param>
		/// <param name="controlCount"></param>
		/// <param name="referenceMean"></param>
		/// <returns></returns>
		public static StudentTSummary StudentTTest(double controlMean, double controlStdev, int controlCount, double referenceMean, DistributionTails tails)
		{
			double scaledControlStdev = (Math.Pow(controlStdev,2)/controlCount);
			double t = ((controlMean - referenceMean)/Math.Sqrt(scaledControlStdev));
			double df = controlCount - 1;
			double p = 1.0f;
			switch(tails)
			{
				case DistributionTails.RightTailed:
					// p = the area to the right of t
					p = (1 - new StudentTDistribution(df).CDF(t));
					break;
				case DistributionTails.LeftTailed:
					// p = the area to the left of t
					p = (new StudentTDistribution(df).CDF(t));
					break;
				case DistributionTails.TwoTailed:
					if(t > 0)
					{
						// p = twice the area to the right of t
						p = (2 * (1 - new StudentTDistribution(df).CDF(t)));
					}
					else
					{
						// p = twice the area to the left of t
						p = (2 * (new StudentTDistribution(df).CDF(t)));
					}
					break;
			}
			return new StudentTSummary(t, p, df);
		}

		/// <summary>
		/// WARNING: This method has not been verified correct
		/// </summary>
		/// <param name="observed"></param>
		/// <param name="expected"></param>
		/// <param name="df"></param>
		/// <returns></returns>
		public static double ChiSquareTest(int[] observed, double[] expected, double df)
		{
			double chiSquare = 0;
			for (int ix = 0; ix < observed.Length; ix++)
			{
				if (observed[ix] == 0 && expected[ix] == 0) continue;
				chiSquare += Math.Pow(observed[ix] - expected[ix], 2) / expected[ix];
			}
			return new ChiSquareDistribution(df).CDF(chiSquare);
		}

		#endregion
		

		#region Instance methods
		/// <summary>
		/// Resets all Statistics
		/// </summary>
		public void Reset() 
		{
			min = double.MaxValue;
			max = double.MinValue;
			sampleNumber = 0;
			sampleWeight = 0.0f;
			sum = 0.0f;
			quadraticSum = 0.0f;
			downsideQuadraticSum = 0.0f;
			cubicSum = 0.0f;
			fourthPowerSum = 0.0f;
		}

		public double Sum { get { return sum; } }

		/// <summary>
		/// Adds a datum to the set, weight is assumed to be 1.
		/// </summary>
		/// <param name="value"></param>
		public void Add(double value)
		{
			this.Add(value, 1.0f);
		}

		/// <summary>
		/// Adds a weighted item to the set.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="weight">Weight must be positive.</param>
		public void Add(double value, double weight) 
		{
			if (weight<0.0) 
				throw new ArgumentOutOfRangeException( "Weight cannot be zero.");
			if(sampleNumber == int.MaxValue)
				throw new OverflowException("Too many items.");

			sampleNumber++;
			sampleWeight += weight;

			double temp = weight*value;
			sum += temp;
			temp *= value;
			quadraticSum += temp;
			downsideQuadraticSum += value < 0.0f ? temp : 0.0f;
			temp *= value;
			cubicSum += temp;
			temp *= value;
			fourthPowerSum += temp;
			min=Math.Min(value, min);
			max=Math.Max(value, max);
		}

		/// <summary>
		/// Remove an item from the statistics. Useful for computing moving averages etc.
		/// </summary>
		/// <param name="val"></param>
		public void Remove(double val)
		{
			Remove(val, 1.0f);
		}
		/// <summary>
		/// Remove an item from the statistics. Useful for computing moving averages etc.
		/// </summary>
		/// <param name="val"></param>
		/// <param name="weight"></param>
		public void Remove(double val, double weight)
		{
			if (weight<0.0) 
				throw new ArgumentOutOfRangeException( "Weight cannot be zero.");
			if(sampleNumber == 0)
				throw new OverflowException("Nothing to remove.");

			this.hasRemoved = true;

			sampleNumber--;
			sampleWeight -= weight;

			double temp = weight*val;
			sum -= temp;
			temp *= val;
			quadraticSum -= temp;
			downsideQuadraticSum -= val < 0.0f ? temp : 0.0f;
			temp *= val;
			cubicSum -= temp;
			temp *= val;
			fourthPowerSum -= temp;
		}
		/// <summary>
		/// Accumulate a range of numbers.
		/// </summary>
		/// <param name="d">The array of numbers to add.</param>
		public void AddRange(double[] d)
		{
			for( int i=0; i<d.Length; i++)
				this.Add(d[i], 1.0f);
		}

		/// <summary>
		/// Number of items collected.
		/// </summary>
		public int Count
		{
			get { return sampleNumber; }
		}

		/// <summary>
		/// Sum of data weights.
		/// </summary>
		public double SumOfWeights
		{
			get { return sampleWeight; }
		}

		/// <summary>
		/// Returns the mean.
		/// </summary>
		public double Mean()
		{
			if(sampleNumber <= 0) return double.NaN;
			return sum/sampleWeight;
		}

		/// <summary>
		/// The variance.
		/// </summary>
		public double Variance()
		{
			if(sampleNumber <= 1) return double.NaN;

			double v = (sampleNumber/(sampleNumber-1.0f)) * (quadraticSum - sum*sum/sampleWeight)/sampleWeight;

			return v;
		}

		/// <summary>
		/// The standard deviation sigma.
		/// </summary>
		public double StandardDeviation()
		{
			return Math.Sqrt(this.Variance());
		}
	

		/// <summary>
		/// The error estimate epsilon, defined as the 
		/// square root of the ratio of the variance to 
		/// the number of samples.
		/// </summary>
		public double ErrorEstimate() 
		{
			return Math.Sqrt(this.Variance()/sampleNumber);
		}

		/// <summary>
		/// Skewness.
		/// </summary>
		public double Skewness()
		{
			if(sampleNumber <= 2)
				throw new InvalidOperationException("Too few items to calculate skewness.");

			double s = StandardDeviation();
			if(s == 0.0) return 0.0f;
			double m = this.Mean();

			return sampleNumber * sampleNumber /
				((sampleNumber - 1.0f) * (sampleNumber - 2.0f) * s * s * s) *
				(cubicSum - 3.0f * m * quadraticSum + 2.0f * m * m * sum) / sampleWeight;
		}

		/// <summary>
		/// Kurtosis.
		/// </summary>
		public double Kurtosis()
		{
			if(sampleNumber <= 3)
				throw new InvalidOperationException("Too few items to calculate kurtosis.");

			double m = this.Mean();
			double v = this.Variance();

			if(v == 0)
				return -3.0f * (sampleNumber - 1.0f) * (sampleNumber - 1.0f) /
					((sampleNumber - 2.0f) * (sampleNumber - 3.0f));

			return sampleNumber * sampleNumber * (sampleNumber + 1.0f) /
				((sampleNumber - 1.0f) * (sampleNumber - 2.0f) *
				(sampleNumber - 3.0f) * v * v) *
				(fourthPowerSum - 4.0f * m * cubicSum + 6.0f * m * m * quadraticSum -
				3.0f * m * m * m * sum) / sampleWeight -
				3.0f * (sampleNumber - 1.0f) * (sampleNumber - 1.0f) /
				((sampleNumber - 2.0f) * (sampleNumber - 3.0f));
		}

		/// <summary>
		/// The minimum sample value.
		/// </summary>
		public double Min()
		{
			if(sampleNumber <= 0) return double.NaN;
			if(hasRemoved)
				throw new InvalidOperationException("Cannot compute minimum after removing items.");
			return min;
		}

		/// <summary>
		/// The maximum sample value.
		/// </summary>
		public double Max()
		{
			if(sampleNumber <= 0) return double.NaN;
			if(hasRemoved)
				throw new InvalidOperationException("Cannot compute maximum after removing items.");
			return max;
		}

		/// <summary>
		/// Compute Student's t test summary comparing this instance to another (called
		/// the 'treatment', not that it really matters which is which).
		/// </summary>
		/// <param name="treatment">The other sample to campare with.</param>
		/// <returns>A summary of the Student's t test result.</returns>
		public StudentTSummary StudentTTest(DescriptiveStatistics treatment, DistributionTails tails)
		{
			return DescriptiveStatistics.StudentTTest(this.Mean(), this.StandardDeviation(), this.Count,
				treatment.Mean(), treatment.StandardDeviation(), treatment.Count, tails);
		}
		/// <summary>
		/// Compute Student's t test summary comparing this instance to a reference mean.
		/// </summary>
		/// <param name="mean">The mean to compare with.</param>
		/// <returns>A summary of the Student's t test result.</returns>
		public StudentTSummary StudentTTest(double mean, DistributionTails tails)
		{
			return DescriptiveStatistics.StudentTTest(this.Mean(), this.StandardDeviation(), this.Count, mean, tails);
		}

		#endregion
	}
}
