using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Mathematics
{
	public abstract class Matrix<T> where T: new()
	{
		public int Rows { get; set; }
		public int Columns { get; set; }

		protected T[] m_Values;
		public T this[int row, int col]
		{
			get { return m_Values[row * Columns + col]; }
			set { m_Values[row * Columns + col] = value; }
		}
		public T this[int index]
		{
			get { return m_Values[index]; }
			set { m_Values[index] = value; }
		}

		public int Count { get { return Rows * Columns; } }

		/// <summary>
		/// Gets an array representing the values of the matrix
		/// </summary>
		/// <returns></returns>
		public T[] GetInternalValues()
		{
			return m_Values;
		}

		public T[] GetRow(int ix)
		{
			T[] row = new T[Columns];
			for(int col = 0; col < Columns; col++)
			{
				row[col] = this[ix, col];
			}
			return row;
		}

		public T[] GetColumn(int ix)
		{
			T[] col = new T[Rows];
			for(int row = 0; row < Rows; row++)
			{
				col[row] = this[row, ix];
			}
			return col;
		}

		protected abstract Matrix<T> Create(int rows, int cols);

		public Matrix(int rows, int columns)
		{
			Rows = rows;
			Columns = columns;
			m_Values = new T[rows * columns];
		}

		public Matrix(int rows, int cols, T[] data)
		{
			if(rows * cols != data.Length) throw new IndexOutOfRangeException();

			Rows = rows;
			Columns = cols;
			m_Values = data;
		}

		public Matrix<T> Clone()
		{
			Matrix<T> clone = Create(Rows, Columns);
			for(int i = 0; i < Rows; i++)
			{
				for(int j = 0; j < Columns; j++)
				{
					clone[i, j] = this[i, j];
				}
			}

			return clone;
		}

		/// <summary>
		/// Creates a new matrix which is the transpose of this instance.
		/// </summary>
		public Matrix<T> Transpose()
		{
			Matrix<T> clone = Create(Columns, Rows);
			for(int i = 0; i < Rows; i++)
			{
				for(int j = 0; j < Columns; j++)
				{
					clone[j, i] = this[i, j];
				}
			}

			return clone;
		}

		/// <summary>Returns a sub matrix extracted from the current matrix.</summary>
		/// <param name="i0">Start row index</param>
		/// <param name="i1">End row index</param>
		/// <param name="j0">Start column index</param>
		/// <param name="j1">End column index</param>
		public Matrix<T> Submatrix(int i0, int i1, int j0, int j1)
		{
			if((i0 > i1) || (j0 > j1) || (i0 < 0) || (i0 >= Rows) || (i1 < 0) || (i1 >= Rows) || (j0 < 0) || (j0 >= Columns) || (j1 < 0) || (j1 >= Columns))
			{
				throw new ArgumentException();
			}

			Matrix<T> clone = Create(i1 - i0 + 1, j1 - j0 + 1);
			for(int i = i0; i <= i1; i++)
			{
				for(int j = j0; j <= j1; j++)
				{
					clone[i - i0, j - j0] = this[i, j];
				}
			}

			return clone;
		}

		/// <summary>Returns a sub matrix extracted from the current matrix.</summary>
		/// <param name="r">Array of row indices</param>
		/// <param name="c">Array of col indices</param>
		public Matrix<T> Submatrix(int[] r, int[] c)
		{
			Matrix<T> clone = Create(r.Length, c.Length);
			for(int i = 0; i < r.Length; i++)
			{
				for(int j = 0; j < c.Length; j++)
				{
					if((r[i] < 0) || (r[i] >= Rows) || (c[j] < 0) || (c[j] >= Columns))
					{
						throw new ArgumentException();
					}

					clone[i, j] = this[r[i], c[j]];
				}
			}

			return clone;
		}
	}
}
