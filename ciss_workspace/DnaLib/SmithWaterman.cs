using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;


namespace Linnarsson.Dna
{
	public class TopHitsTracker<T> where T : IComparable<T>
	{
		public List<T> Hits { get; set; }
		public int MaxHits { get; set; }
		public void Examine(T hit)
		{
			if(Hits.Count == 0) Hits.Add(hit);
			else
			{
				int ix = Hits.Count;
				while(ix > 0 && hit.CompareTo(Hits[ix - 1]) > 0) ix--;
				if(ix < MaxHits) Hits.Insert(ix, hit);
				if(Hits.Count > MaxHits) Hits.RemoveAt(Hits.Count - 1);
			}
		}

		public TopHitsTracker(int maxHits)
		{
			MaxHits = maxHits;
			Hits = new List<T>();
		}
	}

	public class SmithWaterman
	{
		private enum MatrixPointer { QGap, TGap, Align, None };



		private double m_GapPenalty = 2;
		public double GapPenalty
		{
			get { return m_GapPenalty; }
			set { m_GapPenalty = value; }
		}

		private double m_MatchScore = 2;
		public double MatchScore
		{
			get { return m_MatchScore; }
			set { m_MatchScore = value; }
		}

		private double m_MismatchScore = -4;
		public double MismatchScore
		{
			get { return m_MismatchScore; }
			set { m_MismatchScore = value; }
		}

		public bool ShowAsHybridization { get; set; }
		public SmithWaterman()
		{
			ShowAsHybridization = false;
		}

		public SmithWatermanResult FindBestAlignment(DnaSequence query, List<DnaSequence> targets)
		{
			SmithWatermanResult best = null;
			foreach(DnaSequence target in targets)
			{
				SmithWatermanResult current = Align(query, target);
				if(best == null || current.Score > best.Score) best = current;
			}
			return best;
		}

		/// <summary>
		/// Perform a Smith-Waterman using the highly optimized SSE2-version of William Pearson's SSEARCH. 
		/// Returns the human-readable result as a string.
		/// </summary>
		/// <param name="query"></param>
		/// <param name="fastaFileTarget"></param>
		/// <returns></returns>
		public static string SSEARCH(string program, DnaSequence query, string fastaFileTarget, string options)
		{
			string result = null;
			try
			{
				// Save the query as a temp file
				string temp = Path.GetFullPath(@"C:\query.fa");
				File.WriteAllText(temp, ">query\r\n" + query.ToString());

				if(options == null || options == "") options = "-n -q -H -L";
				// Find the fasta program folder
				string ssearchFile = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "fasta-35.2.3"), program);
				Process proc = new Process();
				proc.EnableRaisingEvents = false;
				proc.StartInfo.FileName = ssearchFile;
				proc.StartInfo.Arguments = options + " \"" + temp + "\" \"" + fastaFileTarget + "\"";
				proc.StartInfo.CreateNoWindow = true;
				proc.StartInfo.RedirectStandardOutput = true;
				proc.StartInfo.UseShellExecute = false;
				proc.Start();
				result = proc.StandardOutput.ReadToEnd();
			}
			catch(Exception exp)
			{
				result = "SSEARCH failed beacause: " + exp.Message + "\r\nMake sure you have installed fasta-35.2.3 in your Program Files folder";
			}
			return result;
		}

		public SmithWatermanResult Align(DnaSequence Query, DnaSequence Target)
		{
			double[,] matrix = new double[Query.Count + 1, Target.Count + 1];
			MatrixPointer[,] pointers = new MatrixPointer[Query.Count + 1, Target.Count + 1];
			int maxQ = 0, maxT = 0;
			double maxScore = double.NegativeInfinity;
			for(int qix = 0; qix <= Query.Count; qix++)
			{
				for(int tix = 0; tix <= Target.Count; tix++)
				{
					// Calculate the possible scores
					double qGapScore = -1;
					double tGapScore = -1;
					double matchScore = -1;
					if(qix > 0) qGapScore = matrix[qix - 1, tix] - GapPenalty;
					if(tix > 0) tGapScore = matrix[qix, tix - 1] - GapPenalty;
					if(qix > 0 && tix > 0) matchScore = matrix[qix - 1, tix - 1] + (Query[qix - 1] == Target[tix - 1] ? MatchScore : MismatchScore);

					// Update the matrix
					double score = -1;
					if(qGapScore > tGapScore && qGapScore > matchScore && qGapScore > 0)
					{
						score = qGapScore;
						matrix[qix, tix] = qGapScore;
						pointers[qix, tix] = MatrixPointer.QGap;
					}
					else if(tGapScore > matchScore && tGapScore > 0)
					{
						score = tGapScore;
						matrix[qix, tix] = tGapScore;
						pointers[qix, tix] = MatrixPointer.TGap;
					}
					else if(matchScore > 0)
					{
						score = matchScore;
						matrix[qix, tix] = matchScore;
						pointers[qix, tix] = MatrixPointer.Align;
					}
					else
					{
						score = 0;
						matrix[qix, tix] = 0;
						pointers[qix, tix] = MatrixPointer.None;
					}

					// Keep track of max score
					if(score > maxScore)
					{
						maxScore = score;
						maxQ = qix;
						maxT = tix;
					}
				}
			}

			// Backtrack
			StringBuilder qString = new StringBuilder();
			StringBuilder tString = new StringBuilder();
			int q = maxQ;
			int t = maxT;
			double currentScore = maxScore;
			double identity = 0;

			// Append the remaining sequences
			long temp = Query.Count;
			while(temp > q) qString.Append(char.ToLower(Query.GetNucleotide(temp-- - 1)));
			long qEnd = temp;
			temp = Target.Count;
			while(temp > t) tString.Append(char.ToLower(Target.GetNucleotide(temp-- - 1)));
			long tEnd = temp;

			while(currentScore > 0)
			{
				switch(pointers[q, t])
				{
					case MatrixPointer.Align:
						if(Query[q - 1] != Target[t - 1])
						{
							int gapLength = 0;
							while(Query[q - 1] != Target[t - 1] && pointers[q, t] == MatrixPointer.Align)
							{
								gapLength++;
								qString.Append("-");
								q--;
								t--;
							}
							for(int ix = 0; ix < gapLength; ix++)
							{
								qString.Append(Query.GetNucleotide(q - 1 + gapLength - ix));
								tString.Append(Target.GetNucleotide(t - 1 + gapLength - ix));
							}
							for(int ix = 0; ix < gapLength; ix++)
							{
								tString.Append("-");
							}
						}
						else
						{
							identity++;
							qString.Append(Query.GetNucleotide(q - 1));
							tString.Append(Target.GetNucleotide(t - 1));
							q--;
							t--;
						}
						break;
					case MatrixPointer.TGap:
						qString.Append("-");
						tString.Append(Target.GetNucleotide(t - 1));
						t--;
						break;
					case MatrixPointer.QGap:
						qString.Append(Query.GetNucleotide(q - 1));
						tString.Append("-");
						q--;
						break;
					case MatrixPointer.None:
						break;
				}
				currentScore = matrix[q, t];
			}
			int qStart = q;
			int tStart = t;
			// Append the remaining sequences
			temp = q;
			while(temp > 0) qString.Append(char.ToLower(Query.GetNucleotide(temp-- - 1)));
			temp = t;
			while(temp > 0) tString.Append(char.ToLower(Target.GetNucleotide(temp-- - 1)));
			if(q > t)
			{
				for(int ix = 0; ix < q - t; ix++)
				{
					tString.Append(" ");
				}
			}
			else
			{
				for(int ix = 0; ix < t - q; ix++)
				{
					qString.Append(" ");
				}
			}
			// Reverse and join
			StringBuilder pretty = new StringBuilder();
			for(int ix = 0; ix < qString.Length; ix++)
			{
				pretty.Append(qString[qString.Length - ix - 1]);
			}
			pretty.Append("\r\n");
			for(int ix = 0; ix < tString.Length; ix++)
			{
				if(ShowAsHybridization) pretty.Append(complement(tString[tString.Length - ix - 1]));
				else pretty.Append(tString[tString.Length - ix - 1]);
			}
			return new SmithWatermanResult(Query, Target, maxScore, Math.Truncate(identity / (double)Query.Count * 100), qStart, qEnd, tStart, tEnd, pretty.ToString());
		}

		private char complement(char c)
		{
			switch(c)
			{
				case 'A':
					return 'T';
				case 'C':
					return 'G';
				case 'G':
					return 'C';
				case 'T':
					return 'A';
				default:
					return c;
			}
		}
	}

	public class SmithWatermanResult : IComparable<SmithWatermanResult>
	{
		private DnaSequence m_Query;
		public DnaSequence Query
		{
			get { return m_Query; }
			set { m_Query = value; }
		}
		public long QStart { get; set; }
		public long QEnd { get; set; }

		private DnaSequence m_Target;
		public DnaSequence Target
		{
			get { return m_Target; }
			set { m_Target = value; }
		}
		public long TStart { get; set; }
		public long TEnd { get; set; }

		private string m_PrettyString;
		public string PrettyString
		{
			get { return m_PrettyString; }
			set { m_PrettyString = value; }
		}

		private double m_Score;
		public double Score
		{
			get { return m_Score; }
			set { m_Score = value; }
		}

		public double PercentIdentity { get; set; }

		public SmithWatermanResult(DnaSequence q, DnaSequence t, double score, double pct, long qs, long qe, long ts, long te, string pretty)
		{
			m_Query = q;
			m_Target = t;
			m_Score = score;
			m_PrettyString = pretty;
			PercentIdentity = pct;
			QStart = qs;
			QEnd = qe;
			TStart = ts;
			TEnd = te;
		}


		#region IComparable<SmithWatermanResult> Members

		public int CompareTo(SmithWatermanResult other)
		{
			return Score.CompareTo(other.Score);
		}

		#endregion
	}
}
