using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
	public class ScoreTracker<S,D> where S: IComparable<S>
	{
		private S m_MaxScore;
		public S MaxScore
		{
			get { return m_MaxScore; }
			set { m_MaxScore = value; }
		}

		private D m_MaxItem;
		public D MaxItem
		{
			get { return m_MaxItem; }
			set { m_MaxItem = value; }
		}

		private S m_MinScore;
		public S MinScore
		{
			get { return m_MinScore; }
			set { m_MinScore = value; }
		}

		private D m_MinItem;
		public D MinItem
		{
			get { return m_MinItem; }
			set { m_MinItem = value; }
		}
		public bool HasBestScore { get { return !firstItem; } }

		private bool firstItem = true;
		public void Examine(S score, D item)
		{
			if(firstItem)
			{
				m_MaxItem = item;
				m_MaxScore = score;
				m_MinItem = item;
				m_MinScore = score;
				firstItem = false;
			}
			else
			{
				if(score.CompareTo(m_MaxScore) > 0)
				{
					m_MaxScore = score;
					m_MaxItem = item;
				}
				if(score.CompareTo(m_MinScore) < 0)
				{
					m_MinScore = score;
					m_MinItem = item;
				}
			}
		}


	}

	public class ScoreTracker<S> where S : IComparable<S>
	{
		private S m_MaxScore;
		public S MaxScore
		{
			get { return m_MaxScore; }
			set { m_MaxScore = value; }
		}


		private S m_MinScore;
		public S MinScore
		{
			get { return m_MinScore; }
			set { m_MinScore = value; }
		}
		public bool HasBestScore { get { return !firstItem; } }

		private bool firstItem = true;
		public void Examine(S score)
		{
			if(firstItem)
			{
				m_MaxScore = score;
				m_MinScore = score;
				firstItem = false;
			}
			else
			{
				if(score.CompareTo(m_MaxScore) > 0)
				{
					m_MaxScore = score;
				}
				if(score.CompareTo(m_MinScore) < 0)
				{
					m_MinScore = score;
				}
			}
		}

		public static ScoreTracker<S, int> MinMax(IList<S> data)
		{
			ScoreTracker<S,int> tracker = new ScoreTracker<S,int>();
			for (int ix = 0; ix < data.Count; ix++)
			{
				tracker.Examine(data[ix], ix);
			}
			return tracker;
		}

		public void Clear()
		{
			firstItem = true;
		}
	}

}
