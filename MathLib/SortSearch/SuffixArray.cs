using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Mathematics.SortSearch
{
	public class IntervalHit
	{
		public long From { get; set; }
		public long To { get; set; }
		public double Mismatches { get; set; }
	}

	/// <summary>
	/// A space-efficient representation of a suffix tree, with upper bound on the suffix length
	/// </summary>
	[Serializable]
	public class SuffixArray
	{
		public Blob2 Sequence;	// 1.5 GB
		public Blob33 Suffixes;	// 24.75 GB
		public long MaxSuffixLength;

		public void Serialize(BinaryWriter writer)
		{
			writer.Write(MaxSuffixLength);
			Sequence.Serialize(writer);
			Suffixes.Serialize(writer);
		}

		public static SuffixArray Deserialize(BinaryReader reader)
		{
			SuffixArray result = new SuffixArray();
			Background.Message("Loading compressed sequence data...");
			result.MaxSuffixLength = reader.ReadInt64();
			result.Sequence = Blob2.Deserialize(reader);
			Background.Message("Loading suffix array...");
			result.Suffixes = Blob33.Deserialize(reader);
			return result;
		}

		private SuffixArray()
		{
		}

		/// <summary>
		/// Create a suffix array representing the suffix tree of the given sequence, with suffixes longer
		/// than maxSuffixLength considered equal. Ok so it's not strictly a suffix array.
		/// </summary>
		/// <param name="maxSuffixLength">The maximum suffix (=read) length that will be used in searching</param>
		/// <param name="sequence">The sequence itself, as a 2-bit blob</param>
		/// <param name="partitions">The partitions in the sequence, i.e. the contigs; suffixes spanning partitions are not added to the index</param>
		public SuffixArray(Blob2 sequence, IPartitionCollection<long> partitions, int maxSuffixLength)
		{
			MaxSuffixLength = (long)maxSuffixLength;
			Sequence = sequence;

			// Calculate total length of suffix array
			var p = partitions.GetIntervalStarts().GetEnumerator();
			p.MoveNext();
			long start = p.Current;
			long totalLength = 0;
			while(p.MoveNext())
			{
				long end = p.Current;
				if(start < end - MaxSuffixLength + 1) totalLength += end - MaxSuffixLength - start + 1;
				start = end;
			}
			
			totalLength += sequence.Length - MaxSuffixLength - start + 1; // Add the last contig

			// 2. Create a vector of all the indices
			Suffixes = new Blob33(totalLength);

			// 3. Fill in all the indices, skipping junctions between contigs
			p = partitions.GetIntervalStarts().GetEnumerator();
			p.MoveNext();
			start = p.Current;
			long ix = 0;
			while(p.MoveNext())
			{
				long end = p.Current;
				if(start < end - MaxSuffixLength + 1)
				{
					for(long i = start; i < end - MaxSuffixLength + 1; i++)
					{
						Suffixes[ix++] = i;
					}
				}
				start = end;
			}
			for(long i = start; i < sequence.Length - MaxSuffixLength + 1; i++)
			{
				Suffixes[ix++] = i;
			}

#if DEBUG
			Debug.Assert(ix == totalLength, "Unexpected discrepancy in number of suffixes");
#endif

			// 2. Sort it in O(n log n) using a radix quicksort
			Console.WriteLine("Sorting suffixes (started {0})...", DateTime.Now);
			DateTime startTime = DateTime.Now;
#if !TestRelease
			SplitEndRadixQuickSort(0, Suffixes.Length - 1, 0);
#endif
			Console.WriteLine("Sorting completed at {0} (elapsed: {1})", DateTime.Now, DateTime.Now - startTime);
		}

		// Inspired by Bentley & McIlroy
		private void SplitEndRadixQuickSort(long left, long right, long depth)
		{
			// Invariants:  left is the first element of the interval
			//				right is the last element of the interval
			//				depth is the current depth in the suffix 
			//				That is, strings in the interval are equal up to and including d-1
			//
			//				v is the pivot element
			//				a points to the first element on the left which is not equal to v
			//				b points to the last element on the left which is not yet proven to be less than v
			//				c points to the first element on the right which is not yet proven to be greater than v
			//				d points to the last element on the right which is not equal to v
			long a = left;
			long b = left;
			long c = right;
			long d = right;
			byte v = Sequence[Suffixes[left] + depth];	// partitioning element

			while(b <= c) // until middle pointers cross
			{
				// Increment b until we find a greater element
				// Swap equal elements to the left
				while(b <= c)
				{
					byte elm = Sequence[Suffixes[b] + depth];
					if(elm > v) break;
					else if(elm == v) { Suffixes.Exchange(a, b); a++; }
					b++;
				}

				// Decrement c until we find a lesser element
				// Swap equal elements to the right
				while(c >= b)
				{
					byte elm = Sequence[Suffixes[c] + depth];
					if(elm < v) break;
					else if(elm == v) { Suffixes.Exchange(c, d); d--; }
					c--;
				}

				// Swap elements b and c
				if(b > c) break;
				Suffixes.Exchange(b,c);
				b++;
				c--;
			}
			// Swap back all the equal elements to the middle
			while(a > left)
			{
				a--;
				b--;
				Suffixes.Exchange(a, b);
			}
			while(d < right)
			{
				d++;
				c++;
				Suffixes.Exchange(c,d);
			}

			// Recurse on the three subranges, checking to see that they contain at least two elements
			// First, the two unequal ranges
			if(b > left + 1) SplitEndRadixQuickSort(left, b - 1, depth);
			if(right > c + 1) SplitEndRadixQuickSort(c + 1, right, depth);

			// Then the equal range, but only if we haven't exhausted the suffix length
			if(depth == MaxSuffixLength) return;
			if(c > b) SplitEndRadixQuickSort(b, c, depth + 1);
		}

		//// Find the longest perfect match at each offset
		//public IntervalHit[] FindLongest(byte[] key, int minLength)
		//{
		//    IntervalHit[] hits = new IntervalHit[key.Length - minLength];
		//    for(int i = 0; i < key.Length - minLength; i++)
		//    {
		//        hits[i] = FindLongest(key, 0, Suffixes.Length - 1, i, 0);
		//    }
		//    return hits;
		//}

		//private IntervalHit FindLongest(byte[] key, long top, long bottom, long d, int length)
		//{
		//    // match anything
		//    if(key[d] == 255)
		//    {
		//        // Check if we reached the end of the key
		//        if(d == MaxSuffixLength - 1) return new IntervalHit { From = top, To = bottom, Length = length, Mismatches = 0 };

		//        // Try to find a longer hit
		//        IntervalHit hit = FindLongest(key, top, bottom, d + 1, length);
		//        if(hit != null) return hit;

		//        // Nothing longer was found, so we return what we have
		//        return new IntervalHit { From = top, To = bottom, Length = length, Mismatches = 0 };
		//    }
		//    else while(true) // match specific base
		//    {
		//        byte k = Sequence[Suffixes[top] + d];
		//        long last_k = FindLastTopElement(top, bottom, d);
		//        // Interval containing k is now from top to last_k
		//        if(k == key[d])
		//        {
		//            // Check if we reached the end of the key
		//            if(d == MaxSuffixLength - 1) return new IntervalHit { From = top, To = last_k, Length = length, Mismatches = 0 };

		//            // Try to find a longer hit
		//            IntervalHit hit = FindLongest(key, top, last_k, d + 1, length + 1);
		//            if(hit != null) return hit;

		//            // Nothing longer was found, so we return what we have
		//            return new IntervalHit { From = top, To = last_k, Length = length, Mismatches = 0 };
		//        }
		//        top = last_k + 1;
		//        if(top > bottom) break;
		//    }
		//    return null;
		//}

		// Find the best hits, allowing mismatches
		public List<IntervalHit> FindBest(byte[] key, int maxMismatches, int maxHits)
		{
			List<IntervalHit> hits = new List<IntervalHit>();
			int maxM = 0;
			while(hits.Count == 0 && maxM <= maxMismatches)
			{
				FindBest(key, 0, Suffixes.Length - 1, 0, 0, maxM, 0, maxHits, hits);
				maxM++;
			}
			return hits;
		}

		// Find the best hits (intervals) allowing mismatches
		// Uses a branch-and-bound search on the suffix array
		private int FindBest(byte[] key, long top, long bottom, int d, int mismatches, int maxMismatches, int numHits, int maxHits, List<IntervalHit> result)
		{
			while(true)
			{
				byte k = Sequence[Suffixes[top] + d];
				long last_k = FindLastTopElement(top, bottom, d);
				// Interval containing k is now from top to last_k
				int mm = (k == key[d] ? 0 : 1);
#if DEBUG
				if(key[d] == 255) throw new InvalidOperationException("key cannot contain 255");
#endif
				if(mismatches + mm <= maxMismatches)
				{
					if(d == key.Length - 1) // WAS: MaxSuffixLength - 1
					{
						// Add the interval to the hit list
						result.Add(new IntervalHit { From = top, To = last_k, Mismatches = mismatches + mm });
						numHits += (int)(last_k - top + 1);						
					}
					else numHits = FindBest(key, top, last_k, d + 1, mismatches + mm, maxMismatches, numHits, maxHits,result);
				}
				if(numHits >= maxHits) return numHits;
				top = last_k + 1;
				if(top > bottom) break;
			}
			return numHits;
		}

		public IntervalHit FindExact(byte[] key)
		{
			return FindExact(key, 0, Suffixes.Length - 1, 0);
		}

		private IntervalHit FindExact(byte[] key, long top, long bottom, int d)
		{
			while(true)
			{
				byte k = Sequence[Suffixes[top] + d];
				long last_k = FindLastTopElement(top, bottom, d);
				if(k == key[d])
				{
					if(d == key.Length - 1) return new IntervalHit { From = top, To = last_k, Mismatches = 0 };
					else return FindExact(key, top, last_k, d + 1);
				}
				top = last_k + 1;
				if(top > bottom) break;
			}
			return null;
		}

		// Find the last element equal to the top element of the interval
		private long FindLastTopElement(long top, long bottom, long d)
		{
			byte key = Sequence[Suffixes[top] + d];
			while(bottom > top + 1)
			{
				long m = (top + bottom) / 2;
				if(key < Sequence[Suffixes[m] + d]) bottom = m;
				else top = m;
			}
			return top;
		}

	
	}
}
