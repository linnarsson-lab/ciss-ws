using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;
using System.Diagnostics;

namespace Linnarsson.Strt
{
	public class MoleculeCounter
	{
		/// <summary>
		/// Total number of reads (r)
		/// </summary>
		public int NumberOfReads { get; private set; }
		/// <summary>
		/// Number of distinct labels (k)
		/// </summary>
		public int NumberOfLabels { get; private set; }
		/// <summary>
		/// Vector of known label frequencies (Y)
		/// </summary>
		public double[] LabelFrequencies { get; set; }
		/// <summary>
		/// The overall label efficiency, = sum of label freqs
		/// </summary>
		public double LabelEfficiency { get; private set; }
		/// <summary>
		/// Vector of observed read counts (P)
		/// </summary>
		public int[] ObservedReads { get; set; }

		public double PriorExpectation { get; set; }
		public double PriorVariance { get; set; }

		public double[] PValues;
		public int[] MValues;
		public double Milliseconds;

		/// <summary>
		/// Use an approximation that doesn't take into account the uncertainty in labeling frequency
		/// </summary>
		public bool SuppressOuterPoisson { get; set; }

		public MoleculeCounter(int[] observedReads, double[] labelFreqs)
		{
			NumberOfLabels = labelFreqs.Length;
			NumberOfReads = observedReads.Sum();
			LabelFrequencies = labelFreqs;
			ObservedReads = observedReads;
			LabelEfficiency = labelFreqs.Sum();


		}

		/// <summary>
		/// Estimate the true number of molecules from the number of observed labels, under the
		/// assumption that no labels are unobserved (i.e. the sample has been sequenced to saturation)
		/// </summary>
		/// <param name="numberOfLabelsObserved">The number of distinct labels observed</param>
		/// <param name="totalNumberOfLabels">The total number of labels</param>
		/// <param name="labelingEfficiency">The efficiency of labeling, i.e. the fraction (0 - 1.0) of all mRNA molecules that are labeled</param>
		/// <returns></returns>
		public static int EstimateFromSaturatedLabels(int numberOfLabelsObserved, int totalNumberOfLabels, double labelingEfficiency)
		{
			return (int)Math.Round(Math.Log(1 - numberOfLabelsObserved / totalNumberOfLabels) / Math.Log(1 - labelingEfficiency / totalNumberOfLabels));
		}

		/// <summary>
		/// Find the mean (expectation) of the posterior distribution. This method integrates over the 
		/// full distribution (until the posterior is less than 5% of the peak value). It may be slower than
		/// PosteriorMode() but will be more accurate.
		/// </summary>
		/// <returns></returns>
		public int PosteriorMean()
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();

			double maxLogP = double.NegativeInfinity;
			int m = 1;
			List<double> posteriors = new List<double>();

			while (true)
			{
				double logP = LogPosterior(m);
				posteriors.Add(logP);
				if (logP < maxLogP) break; // We reached the mode
				maxLogP = logP;
				m++;
			}

			while (true)
			{
				double logP = LogPosterior(m);
				posteriors.Add(logP);
				if (logP < 0.05*maxLogP) break; // We reached the tail
				m++;
			}

			// Compute the mean
			double sum = 0;
			double mean = 0;
			for (int i = 0; i < posteriors.Count; i++)
			{
				double p = Math.Exp(posteriors[i] - maxLogP);
				sum += p;
				mean += p * (i + 1);
			}

			sw.Stop();
			Milliseconds = sw.Elapsed.TotalMilliseconds;

			return (int)Math.Round(mean / sum);
		}


		/// <summary>
		/// Find the value of m that maximizes the posterior distribution (i.e. find its mode)
		/// This method is guaranteed to find the exact mode and uses an efficient Fibonacci search
		/// </summary>
		public int PosteriorMode()
		{
			int density = 5;

			Stopwatch sw = new Stopwatch();
			sw.Start();

			List<double> p_values = new List<double>();
			List<int> m_values = new List<int>();

			// Bracket the maximum: evaluate the posterior at m-values spaced more and more sparsely, until we pass the maximum (mode)
			// We ensure that m is increased always by a Fibonacci number, so that the bracket has a Fibonacci length (for the search, below)
			int k = 5; // The k'th Fibonacci number is added at each step, with k slowly increasing 
			int a = 1;	// left side of the bracket
			int b = 6;	// right side of the bracket

			double pA = LogPosterior(a);	// Posterior at a
			m_values.Add(a);
			p_values.Add(pA);

			double pB = LogPosterior(b);	// Posterior at b
			m_values.Add(b);
			p_values.Add(pB);

			double maxValue = Math.Max(pA, pB);

			while (pB >= pA)	// Jump right until the posterior decreases
			{
				a = b;
				pA = pB;

				// Jump by a fibonacci step, slowly increasing in size
				if (b / density > SpecialFunctions.Fibonacci[k]) k++;
				b += SpecialFunctions.Fibonacci[k];
				pB = LogPosterior(b);
				m_values.Add(b);
				p_values.Add(pB);

				if (pB < pA) break; // we just passed the mode
			}

			// Now use a fibonacci search to find the actual maximum, based on the Fibonacci numbers (F0, F1, .. Fn)
			// The Fibonacci search uses the smallest number of function evaluations to arrive at the desired uncertainty interval
			// See figures at http://math.fullerton.edu/mathews/n2003/fibonaccisearchmod.html to help understand the algorithm


			// First evaluate the posterior at two internal points
			// The two points are selected so that they intersect the original interval (of length Fibonacci[k]), 
			// forming intervals of length Fibonacci[k-2], Fibonacci[k-3] and Fibonacci[k-2].
			// This ensures that both points can be reused in the next iteration
			int c = a + SpecialFunctions.Fibonacci[k - 2];
			double pC = LogPosterior(c);
			m_values.Add(c);
			p_values.Add(pC);

			int d = b - SpecialFunctions.Fibonacci[k - 2];	// The interval between c and d will be Fibonacci[k - 2]
			double pD = LogPosterior(d);
			m_values.Add(d);
			p_values.Add(pD);

			while (true)
			{
				// Are we there yet? With k = 4, Fibonacci[4] = 3, which is the last iteration (at the next iteration, c and d would converge)
				if (k == 4) break;

				// Decrease the search interval 
				k--;

				// Then decide which way to go for the next iteration, right or left?
				if (pC > pD) // Go left
				{
					b = d;
					pB = pD;
					d = c;
					pD = pC;

					c = a + SpecialFunctions.Fibonacci[k - 2];
					pC = LogPosterior(c);
					m_values.Add(c);
					p_values.Add(pC);
				}
				else // Go right
				{
					a = c;
					pA = pC;
					c = d;
					pC = pD;

					d = b - SpecialFunctions.Fibonacci[k - 2];
					pD = LogPosterior(d);
					m_values.Add(d);
					p_values.Add(pD);
				}
			}
			MValues = m_values.ToArray();
			PValues = p_values.ToArray();
			sw.Stop();
			Milliseconds = new TimeSpan(sw.ElapsedTicks).TotalMilliseconds;

			// Not sure if a and b are possible results, or only c and d? 
			if (pA > pC) return a;
			if (pC > pD) return c;
			if (pD > pB) return d;
			return b;
		}

		public static MoleculeCounter Simulate(int m, int numLabels, double labelEfficiency, double coverage, double pcrEfficiency, int pcrCycles)
		{
			
			double[] labelFreqs = new double[numLabels];
			for (int i = 0; i < numLabels; i++)
			{
				labelFreqs[i] = (1d / numLabels)*labelEfficiency;	// equal efficiencies
			}

			int[] observedReads = new int[numLabels];
			int totalMolecules = 0;
			for (int i = 0; i < numLabels; i++)
			{
				// Sample the number of labeled molecules for this label (binomial distribution)
				int molecules = new BinomialDistribution(m, labelFreqs[i]).Sample();
				totalMolecules += molecules;

				// Amplify by PCR, each cycle doubling the number of molecules (or less if pcrEfficiency < 100%)
				for (int j = 0; j < pcrCycles; j++)
				{
					molecules += new BinomialDistribution(molecules, pcrEfficiency).Sample();	
				}
				// Sample from the amplified fragments to get desired level of read coverage relative to (labeled) initial molecules
				observedReads[i] = new BinomialDistribution(molecules, coverage * m / molecules * labelEfficiency).Sample();
			}
			var mc = new MoleculeCounter(observedReads, labelFreqs);
			mc.PriorExpectation = Math.Max(1, totalMolecules);
			mc.PriorVariance = mc.PriorExpectation * 2;
			mc.SuppressOuterPoisson = true;
			return mc;
		}

		/// <summary>
		/// Calculate the natural log of the likelihood for m molecules
		/// </summary>
		/// <param name="m"></param>
		/// <returns></returns>
		public double LogLikelihood(int m)
		{
			double result = 0;
			double fm = LabelEfficiency * m;

			double start = 1; 
			double end = m;
			double step = 1;
			if (SuppressOuterPoisson)
			{
				start = Math.Round(fm);
				if (start < 1) start = 1;
				end = start;
			}
			for (double z = start; z <= end; z+= step)
			{
				double logproduct = 0;
				for (int i = 0; i < NumberOfLabels; i++)
				{
					double factor = 0;
					for (int n = 0; n <= Math.Floor(z) - 1; n++) 
					{
						// Use log-pdf so as to not hit the ceiling
						double temp = 0;
						temp += new BinomialDistribution(m, LabelFrequencies[i]).LogPDF(n); 
						temp += new BinomialDistribution(NumberOfReads, n / z).LogPDF(ObservedReads[i]);
						factor += Math.Exp(temp);
					}
					logproduct += Math.Log(factor);
				}
				if (!SuppressOuterPoisson)
				{
					logproduct += new PoissonDistribution(fm).LogPDF((int)Math.Round(z));
					result += Math.Exp(logproduct);
				}
				else result = logproduct;
			}
			if(!SuppressOuterPoisson) return Math.Log(result);
			return result;
		}

		/// <summary>
		/// Calculate the log of the prior. Make sure the prior parameters have been set to sensible values.
		/// </summary>
		/// <param name="m"></param>
		/// <returns></returns>
		public double LogPrior(int m)
		{
			double p = PriorExpectation / PriorVariance;
			double r = -PriorExpectation * PriorExpectation / (PriorExpectation - PriorVariance);
			//r = Math.Max(r, 1);
			double result = SpecialFunctions.LogGamma(r + m) - SpecialFunctions.LogGamma(r) - SpecialFunctions.LogGamma(m + 1);
			result += r * Math.Log(p);
			result += m * Math.Log(1 - p);
			return result;
		}

		public double LogPosterior(int m)
		{
			return LogLikelihood(m) + LogPrior(m);
		}


	}
}
