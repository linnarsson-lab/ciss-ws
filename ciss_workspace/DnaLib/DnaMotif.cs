using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Dna
{
	/// <summary>
	/// Represents a probabilistic DNA motif by measuring the frequency of each letter at each position
	/// </summary>
	public class DnaMotif
	{
		public int Length { get; private set; }
		int[,] counts;

		public DnaMotif(int length)
		{
			counts = new int[5, length];
			Length = length;
		}


		// Adds a sequence fragment to the motif
		public void Add(DnaSequence sequence, long start, char strand)
		{
			if(strand == '+')
			{
				for(long i = 0; i < Length; i++)
				{
					byte nt = DnaSequence.NtToIndex(sequence[start + i]);
					counts[nt, i]++;
					counts[4, i]++;
				}
			}
			else
			{
				for(long i = 0; i < Length; i++)
				{
					byte nt = DnaSequence.NtToIndex(IupacEncoding.Complement(sequence[start + Length - i - 1]));
					counts[nt, i]++;
					counts[4, i]++;
				}
			}
		}

		public double Probability(int index, byte nt)
		{
			return counts[nt, index] / (double)counts[4, index];
		}

		public double Conservation(int index)
		{
			return 2 - (
				- Entropy(index, 0)
				- Entropy(index, 1)
				- Entropy(index, 2)
				- Entropy(index, 3)
				);
		}

		public double Entropy(int index, byte nt)
		{
			double p = Probability(index, nt);
			if(p == 0) return 0;
			return p * Math.Log(p, 2);
		}

		public void Save(string filename)
		{
			StreamWriter writer = new StreamWriter(filename);
			writer.WriteLine("A\tC\tG\tT");
			for(int i = 0; i < Length; i++)
			{
				double C = Conservation(i);
				for(byte j = 0; j < 4; j++)
				{
					writer.Write(Probability(i, j) * C);
					writer.Write("\t");
				}
				writer.WriteLine();
			}
			writer.WriteLine("*** 100 sampled motifs ***");
			for(int j = 0; j < 100; j++)
			{
				writer.WriteLine(DnaSequence.RandomMotif(this).ToString());
			}
			writer.Close();
		}
	}
}
