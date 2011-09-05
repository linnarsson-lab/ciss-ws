using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
	/// <summary>
	/// Represents a column-oriented matrix with annotations along rows and columns
	/// </summary>
	public class Dataset
	{
		private List<double[]> m_Columns = new List<double[]>();
		public List<double[]> Columns
		{
			get { return m_Columns; }
		}

		private List<string> m_ColumnNames = new List<string>();
		public List<string> ColumnNames
		{
			get { return m_ColumnNames; }
		}

		private List<string> m_RowNames = new List<string>();
		public List<string> RowNames
		{
			get { return m_RowNames; }
		}

	}

	public class AnnotatedDataset<R, C> : Dataset 
	{
		private List<C> m_ColumnAnnotations = new List<C>();
		public List<C> ColumnAnnotations
		{
			get { return m_ColumnAnnotations; }
		}

		private List<R> m_RowAnnotations = new List<R>();
		public List<R> RowAnnotations
		{
			get { return m_RowAnnotations; }
		}
	}
}
