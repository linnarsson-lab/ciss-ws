using System;
using System.IO;
using System.Collections.Generic;


namespace Linnarsson.Mathematics
{
	public class MatrixDouble : Matrix<double>
	{
		public MatrixDouble(double [] buffer, int rows, int cols) : base(rows, cols, buffer)
		{
		}

		protected override Matrix<double> Create(int rows, int cols)
		{
			return new MatrixDouble(rows, cols);
		}

		public MatrixDouble(int rows, int cols)
			: base(rows, cols)
		{
		}


		public static double[] SolveTridiagonal(IList<double> a, IList<double> b, IList<double> c, IList<double> r)
		{
			int N = a.Count;
			double[] temp = new double[N];
			double[] u = new double[N];
			double bet = b[0];
			if (b[0] == 0d) throw new ArgumentException("b[0] cannot be zero in SolveTridiagonal");

			// forward substitution
			u[0] = r[0] / b[0];
			for (int ix = 1; ix < N; ix++)
			{
				temp[ix] = c[ix - 1] / bet;
				bet = b[ix] - a[ix] * temp[ix];
				if (bet == 0d) throw new InvalidProgramException("Zero pivot in SolveTridiagonal");
				u[ix] = (r[ix] - a[ix] * u[ix - 1]) / bet;
			}

			for (int ix = N-2; ix >= 0; ix--)
			{
				u[ix] -= temp[ix + 1] * u[ix + 1];
			}
			return u;
		}

		public static MatrixDouble Ones(int rows, int cols)
		{
			MatrixDouble m = new MatrixDouble(rows, cols);
			for(int ix = 0; ix < rows * cols; ix++)
			{
				m.m_Values[ix] = 1d;
			}
			return m;
		}

		public static MatrixDouble Zeros(int rows, int cols)
		{
			MatrixDouble m = new MatrixDouble(rows, cols);
			for(int ix = 0; ix < rows * cols; ix++)
			{
				m.m_Values[ix] = 0d;
			}
			return m;
		}
		public static MatrixDouble DiagonalOnes(int rows, int cols)
		{
			MatrixDouble m = new MatrixDouble(rows, cols);
			for(int ix = 0; ix < rows && ix < cols; ix++)
			{
				m[ix, ix] = 1d;
			}
			return m;
		}

		public double RowSum(int row)
		{
			double result = 0d;
			for(int ix = 0; ix < Columns; ix++)
			{
				result += this[row, ix];
			}
			return result;
		}

		public double ColumnSum(int column)
		{
			double result = 0d;
			for(int ix = 0; ix < Rows; ix++)
			{
				result += this[ix, column];
			}
			return result;
		}

	
		public bool IsSquare
		{
			get
			{
				return (Rows == Columns);
			}
		}

		public bool IsSymmetric
		{
			get
			{
				if(this.IsSquare)
				{
					for(int i = 0; i < Rows; i++)
					{
						for(int j = 0; j <= i; j++)
						{
							if(this[i, j] != this[j,i])
							{
								return false;
							}
						}
					}

					return true;
				}

				return false;
			}
		}


		public static MatrixDouble operator -(MatrixDouble a)
		{
			MatrixDouble clone = new MatrixDouble(a.Rows, a.Columns);
			for(int i = 0; i < a.Rows; i++)
			{
				for(int j = 0; j < a.Columns; j++)
				{
					clone[i, j] = -a[i, j];
				}
			}

			return clone;
		}

		/// <summary>
		/// Negates all elements of the matrix
		/// </summary>
		public void Negate()
		{
			for(int ix = 0; ix < m_Values.Length; ix++)
			{
				m_Values[ix] = -m_Values[ix];
			}
		}

		/// <summary>Matrix addition.</summary>
		public static MatrixDouble operator +(MatrixDouble a, MatrixDouble b)
		{
			if((a.Rows != b.Rows) || (a.Columns != b.Columns))
			{
				throw new InvalidOperationException("Matrix dimensions do not match.");
			}

			MatrixDouble clone = new MatrixDouble(a.Rows, a.Columns);
			for(int i = 0; i < a.Rows; i++)
			{
				for(int j = 0; j < a.Columns; j++)
				{
					clone[i, j] = a[i, j] + b[i, j];
				}
			}
			return clone;
		}

		/// <summary>Matrix subtraction.</summary>
		public static MatrixDouble operator -(MatrixDouble a, MatrixDouble b)
		{
			if((a.Rows != b.Rows) || (a.Columns != b.Columns))
			{
				throw new InvalidOperationException("Matrix dimensions do not match.");
			}

			MatrixDouble clone = new MatrixDouble(a.Rows, a.Columns);
			for(int i = 0; i < a.Rows; i++)
			{
				for(int j = 0; j < a.Columns; j++)
				{
					clone[i, j] = a[i, j] - b[i, j];
				}
			}
			return clone;
		}

		/// <summary>Matrix-scalar multiplication.</summary>
		public static MatrixDouble operator *(MatrixDouble a, double s)
		{
			MatrixDouble clone = new MatrixDouble(a.Rows, a.Columns);
			for(int i = 0; i < a.Rows; i++)
			{
				for(int j = 0; j < a.Columns; j++)
				{
					clone[i, j] = a[i, j] * s;
				}
			}
			return clone;
		}
		/// <summary>
		/// Elementwise multiplication. Compare '.*' in Matlab
		/// </summary>
		public static MatrixDouble ElementMultiplication(MatrixDouble a, MatrixDouble b)
		{
			if ((a.Rows != b.Rows) || (a.Columns != b.Columns))
			{
				throw new InvalidOperationException("Matrix dimensions do not match.");
			}
			MatrixDouble clone = new MatrixDouble(a.Rows, a.Columns);
			for (int i = 0; i < a.Rows; i++)
			{
				for (int j = 0; j < a.Columns; j++)
				{
					clone[i, j] = a[i, j] * b[i, j];
				}
			}
			return clone;
		}


		/// <summary>Matrix-matrix multiplication.</summary>
		public static MatrixDouble operator *(MatrixDouble a, MatrixDouble b)
		{
			if(b.Rows != a.Columns)
			{
				throw new InvalidOperationException("Matrix dimensions do not match.");
			}

			MatrixDouble clone = new MatrixDouble(a.Rows, b.Columns);

			for(int i = 0; i < clone.Rows; i++)
			{
				for(int j = 0; j < clone.Columns; j++)
				{
					double sum = 0;
					for(int k = 0; k < a.Columns; k++)
					{
						sum += a[i, k] * b[k, j];
					}
					clone[i, j] = sum;
				}
			}
			return clone;
		}


		/// <summary>Returns the LHS solution vetor if the matrix is square or the least squares solution otherwise.</summary>
		public MatrixDouble Solve(MatrixDouble rhs)
		{
			return (Rows == Columns) ? new LuDecomposition(this).Solve(rhs) : new QrDecomposition(this).Solve(rhs);
		}

		/// <summary>Inverse of the matrix if matrix is square, pseudoinverse otherwise.</summary>
		public MatrixDouble Inverse
		{
			get
			{
				return this.Solve(Diagonal(Rows, Rows, 1.0));
			}
		}

		/// <summary>Determinant if matrix is square.</summary>
		public double Determinant
		{
			get
			{
				return new LuDecomposition(this).Determinant;
			}
		}

		/// <summary>Returns the trace of the matrix.</summary>
		/// <returns>Sum of the diagonal elements.</returns>
		public double Trace
		{
			get
			{
				double trace = 0;
				for(int i = 0; i < Math.Min(Rows, Columns); i++)
				{
					trace += this[i, i];
				}
				return trace;
			}
		}

		/// <summary>Returns a matrix filled with random values sampled from the given distribution.</summary>
		public static MatrixDouble Random(int rows, int columns, IContinuousDistribution distr)
		{
			MatrixDouble m = new MatrixDouble(rows, columns);
			for(int i = 0; i < rows; i++)
			{
				for(int j = 0; j < columns; j++)
				{
					m[i, j] = distr.Sample();
				}
			}
			return m;
		}

		/// <summary>Returns a matrix filled with random values sampled from the given distribution.</summary>
		public static MatrixDouble Random(int rows, int columns, IDiscreteDistribution distr)
		{
			MatrixDouble m = new MatrixDouble(rows, columns);
			for(int i = 0; i < rows; i++)
			{
				for(int j = 0; j < columns; j++)
				{
					m[i, j] = distr.Sample();
				}
			}
			return m;
		}

		/// <summary>Returns a diagonal matrix of the given size.</summary>
		public static MatrixDouble Diagonal(int rows, int columns, double value)
		{
			MatrixDouble clone = new MatrixDouble(rows, columns);
			for(int i = 0; i < rows; i++)
			{
				for(int j = 0; j < columns; j++)
				{
					clone[i, j] = ((i == j) ? value : 0d);
				}
			}
			return clone;
		}

		/// <summary>Returns a matrix with values off the diagonal replaced by zeroes.</summary>
		public MatrixDouble Diagonal()
		{
			MatrixDouble clone = new MatrixDouble(Rows, Columns);
			for(int i = 0; i < Rows; i++)
			{
				for(int j = 0; j < Columns; j++)
				{
					clone[i, j] = ((i == j) ? this[i,j] : 0d);
				}
			}
			return clone;
		}


		/// <summary>Returns a sub matrix extracted from the current matrix.</summary>
		/// <param name="i0">Starttial row index</param>
		/// <param name="i1">End row index</param>
		/// <param name="c">Array of row indices</param>
		public MatrixDouble Submatrix(int i0, int i1, int[] c)
		{
			if((i0 > i1) || (i0 < 0) || (i0 >= Rows) || (i1 < 0) || (i1 >= Rows))
			{
				throw new ArgumentException();
			}

			MatrixDouble clone = new MatrixDouble(i1 - i0 + 1, c.Length);
			for(int i = i0; i <= i1; i++)
			{
				for(int j = 0; j < c.Length; j++)
				{
					if((c[j] < 0) || (c[j] >= Columns))
					{
						throw new ArgumentException();
					}

					clone[i - i0, j] = this[i, c[j]];
				}
			}

			return clone;
		}

		/// <summary>Returns a sub matrix extracted from the current matrix.</summary>
		/// <param name="r">Array of row indices</param>
		/// <param name="j0">Start column index</param>
		/// <param name="j1">End column index</param>
		public MatrixDouble Submatrix(int[] r, int j0, int j1)
		{
			if((j0 > j1) || (j0 < 0) || (j0 >= Columns) || (j1 < 0) || (j1 >= Columns))
			{
				throw new ArgumentException();
			}

			MatrixDouble clone = new MatrixDouble(r.Length, j1 - j0 + 1);
			for(int i = 0; i < r.Length; i++)
			{
				for(int j = j0; j <= j1; j++)
				{
					if((r[i] < 0) || (r[i] >= Rows))
					{
						throw new ArgumentException();
					}

					clone[i, j - j0] = this[r[i], j];
				}
			}

			return clone;
		}


		public double Sum()
		{
			double sum = 0;
			for(int ix = 0; ix < Rows; ix++)
			{
				sum += RowSum(ix);
			}
			return sum;
		}
	}
}
