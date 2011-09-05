namespace Linnarsson.Mathematics
{
	using System;

	/// <summary>
	///	  QR decomposition for a rectangular matrix.
	/// </summary>
	/// <remarks>
	///   Adapted from Lutz Roeder's Mapack by Sten Linnarsson.
	///   For an m-by-n matrix <c>A</c> with <c>m &gt;= n</c>, the QR decomposition is an m-by-n
	///   orthogonal matrix <c>Q</c> and an n-by-n upper triangular 
	///   matrix <c>R</c> so that <c>A = Q * R</c>.
	///   The QR decompostion always exists, even if the matrix does not have
	///   full rank, so the constructor will never fail.  The primary use of the
	///   QR decomposition is in the least squares solution of nonsquare systems
	///   of simultaneous linear equations.
	///   This will fail if <see cref="IsFullRank"/> returns <see langword="false"/>.
	/// </remarks>
	public class QrDecomposition
	{
		private MatrixDouble QR;
		private double[] Rdiag;

		/// <summary>Construct a QR decomposition.</summary>	
		public QrDecomposition(MatrixDouble A)
		{
			QR = (MatrixDouble)A.Clone();
			int m = A.Rows;
			int n = A.Columns;
			Rdiag = new double[n];

			for(int k = 0; k < n; k++)
			{
				// Compute 2-norm of k-th column without under/overflow.
				double nrm = 0;
				for(int i = k; i < m; i++)
				{
					nrm = this.Hypotenuse(nrm, QR[i, k]);
				}

				if(nrm != 0.0)
				{
					// Form k-th Householder vector.
					if(QR[k,k] < 0)
						nrm = -nrm;
					for(int i = k; i < m; i++)
						QR[i,k] /= nrm;
					QR[k,k] += 1.0;

					// Apply transformation to remaining columns.
					for(int j = k + 1; j < n; j++)
					{
						double s = 0.0;
						for(int i = k; i < m; i++)
							s += QR[i,k] * QR[i,j];
						s = -s / QR[k,k];
						for(int i = k; i < m; i++)
							QR[i, j] += s * QR[i, k];
					}
				}
				Rdiag[k] = -nrm;
			}
		}

		/// <summary>Least squares solution of <c>A * X = B</c></summary>
		/// <param name="rhs">Right-hand-side matrix with as many rows as <c>A</c> and any number of columns.</param>
		/// <returns>A matrix that minimized the two norm of <c>Q * R * X - B</c>.</returns>
		/// <exception cref="T:System.ArgumentException">Matrix row dimensions must be the same.</exception>
		/// <exception cref="T:System.InvalidOperationException">Matrix is rank deficient.</exception>
		public MatrixDouble Solve(MatrixDouble rhs)
		{
			if(rhs.Rows != QR.Rows) throw new ArgumentException("Matrix row dimensions must agree.");
			if(!IsFullRank) throw new InvalidOperationException("Matrix is rank deficient.");

			// Copy right hand side
			int count = rhs.Columns;
			MatrixDouble clone = (MatrixDouble)rhs.Clone();
			int m = QR.Rows;
			int n = QR.Columns;

			// Compute Y = transpose(Q)*B
			for(int k = 0; k < n; k++)
			{
				for(int j = 0; j < count; j++)
				{
					double s = 0.0;
					for(int i = k; i < m; i++)
						s += QR[i,k] * clone[i, j];
					s = -s / QR[k,k];
					for(int i = k; i < m; i++)
						clone[i, j] += s * QR[i,k];
				}
			}

			// Solve R*X = Y;
			for(int k = n - 1; k >= 0; k--)
			{
				for(int j = 0; j < count; j++)
					clone[k, j] /= Rdiag[k];

				for(int i = 0; i < k; i++)
					for(int j = 0; j < count; j++)
						clone[i, j] -= clone[k, j] * QR[i, k];
			}

			return (MatrixDouble)clone.Submatrix(0, n - 1, 0, count - 1);
		}

		/// <summary>Shows if the matrix <c>A</c> is of full rank.</summary>
		/// <value>The value is <see langword="true"/> if <c>R</c>, and hence <c>A</c>, has full rank.</value>
		public bool IsFullRank
		{
			get
			{
				int columns = QR.Columns;
				for(int j = 0; j < columns; j++)
					if(Rdiag[j] == 0)
						return false;
				return true;
			}
		}

		/// <summary>Returns the upper triangular factor <c>R</c>.</summary>
		public MatrixDouble UpperTriangularFactor
		{
			get
			{
				int n = QR.Columns;
				MatrixDouble clone = new MatrixDouble(n, n);
				for(int i = 0; i < n; i++)
					for(int j = 0; j < n; j++)
						if(i < j)
							clone[i, j] = QR[i, j];
						else if(i == j)
							clone[i,j] = Rdiag[i];
						else
							clone[i,j] = 0.0;

				return clone;
			}
		}

		/// <summary>Returns the orthogonal factor <c>Q</c>.</summary>
		public MatrixDouble OrthogonalFactor
		{
			get
			{
				MatrixDouble clone = new MatrixDouble(QR.Rows, QR.Columns);
				for(int k = QR.Columns - 1; k >= 0; k--)
				{
					for(int i = 0; i < QR.Rows; i++)
					{
						clone[i,k] = 0.0;
					}

					clone[k,k] = 1.0;
					for(int j = k; j < QR.Columns; j++)
					{
						if(QR[k,k] != 0)
						{
							double s = 0.0;
							for(int i = k; i < QR.Rows; i++)
							{
								s += QR[i, k] * clone[i, j];
							}

							s = -s / QR[k, k];
							for(int i = k; i < QR.Rows; i++)
							{
								clone[i, j] += s * QR[i, k];
							}
						}
					}
				}

				return clone;
			}
		}

		private double Hypotenuse(double a, double b)
		{
			if(Math.Abs(a) > Math.Abs(b))
			{
				double r = b / a;
				return Math.Abs(a) * Math.Sqrt(1 + r * r);
			}

			if(b != 0)
			{
				double r = a / b;
				return Math.Abs(b) * Math.Sqrt(1 + r * r);
			}

			return 0.0;
		}
	}
}
