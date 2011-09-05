using System;
using System.Collections.Generic;
using System.Text;

namespace Linnarsson.Mathematics
{
    [Serializable]
	public struct Pair<A,B>
	{
		private A m_First;
		public A First
		{
			get { return m_First; }
            set { m_First = value; }
		}

		private B m_Second;
		public B Second
		{
			get { return m_Second; }
            set { m_Second = value; }
		}

		public Pair(A first, B second)
		{
			m_First = first;
			m_Second = second;
		}
	
	}

    [Serializable]
	public struct Triplet<A, B, C>
	{
		private A m_First;
		public A First
		{
			get { return m_First; }
		}

		private B m_Second;
		public B Second
		{
			get { return m_Second; }
		}

		private C m_Third;
		public C Third
		{
			get { return m_Third;}
		}
	
		public Triplet(A first, B second, C third)
		{
			m_First = first;
			m_Second = second;
			m_Third = third;
		}

	}
}
