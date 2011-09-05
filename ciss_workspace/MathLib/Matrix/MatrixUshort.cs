using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Linnarsson.Mathematics
{
	public class MatrixUshort : Matrix<ushort>
	{

		public MatrixUshort(ushort [] buffer, int rows, int cols) : base(rows, cols, buffer)
		{
		}

		protected override Matrix<ushort> Create(int rows, int cols)
		{
			return new MatrixUshort(rows, cols);
		}

		public MatrixUshort(int rows, int cols) : base(rows, cols)
		{
		}

		public void SaveAsGifImage(string fname)
		{
			Bitmap bm = new Bitmap(Columns, Rows);
			for(int r = 0; r < Rows; r++)
			{
				for(int c = 0; c < Columns; c++)
				{
					int v = this[r,c]/256;
					bm.SetPixel(c,r, Color.FromArgb(v,v,v));
				}
			}
			bm.Save(fname, ImageFormat.Gif);
		}

		public void Subtract(MatrixUshort usm)
		{
			int length = usm.Columns*usm.Rows;
			for (int i= 0; i < length; i++ )
			{
				this[i] = (ushort)Math.Max((int)this[i]-(int)usm[i],0);
			}

		}

		public void MaxElement(MatrixUshort usm)
		{
			int length = this.Columns * this.Rows;
			for(int i=0; i<length;i++)
			{
				this[i]=Math.Max(this[i],usm[i]);
			}
		}
		public void MinElement(MatrixUshort usm)
		{
			int length=this.Columns*this.Rows;
			for(int i=0; i<length;i++)
			{
				this[i]=Math.Min(this[i],usm[i]);
			}
		}

		/// <summary>
		/// Make the current matrix the weighted average of itself and the given matrix. Pixel
		/// values in this matrix are multiplied by the weight then added to the given matrix pixel
		/// value and divided by the weight + 1: this = (this*weight + usm)/(weight + 1)
		/// </summary>
		/// <param name="usm"></param>
		/// <param name="weight"></param>
		public void WeightedAverage(MatrixUshort usm, int weight)
		{
			int length = this.Columns * this.Rows;
			for(int i = 0; i < length; i++)
			{
				this[i] = (ushort)((this[i] * weight + usm[i]) / (weight + 1));
			}
		}

		public void Average(MatrixUshort usm)
		{
			WeightedAverage(usm, 1);
		}

		public long AggregateWith(MatrixUshort other, Func<ushort, ushort, long> function)
		{
			if(this.Columns != other.Columns || this.Rows != other.Rows)
			{
				throw new IndexOutOfRangeException("Matrices must be of same size.");
			}
			long sum = 0;
			int len = Columns * Rows;

			for(int i = 0; i < len; i++)
			{
				sum += function(this[i], other[i]);
			}
			return sum;
		}

		public long SumOfAbsDifference(MatrixUshort other)
		{
			return AggregateWith(other, (ushort a, ushort b) => Math.Abs(a - b));
		}
		public long SumOfDifference(MatrixUshort other)
		{
			return AggregateWith(other, (ushort a, ushort b) => a - b);
		}


		/// <summary>
		/// Returns the absolute sum of the first order differential along rows or columns.
		/// </summary>
		/// <param name="dim">dim = 1 (along rows), dim = 2 (along columns)</param>
		/// <returns></returns>
		public long SumOfFirstDifferential(int dim)
		{
			long sum = 0;
			int w = 0;
			int index = 0;
			if (dim == 1)
			{
				w = Columns - 1;
				for (int i = 0; i < Rows; i++)
				{
					for (int j = 0; j < w; j++)
					{
						if (this[index] > this[index+ 1])
						{
						    sum = sum + this[index] - this[index+1];
						}
						else { sum = sum - this[index] + this[index+1]; }
						index = index + 1;
					}
					index = index + 1;
				}
			}
			if (dim == 2)
			{
				MatrixUshort transp = (MatrixUshort)this.Transpose();
				int h = transp.Rows;
				w = transp.Columns-1;
				for (int i = 0; i < Rows; i++)
				{
					for (int j = 0; j < w; j++)
					{
						if (transp[index] > transp[index + 1])
						{
							sum = sum + transp[index] - transp[index + 1];
						}
						else { sum = sum - transp[index] + transp[index + 1]; }
						index = index + 1;
					}
					index = index + 1;
				}
			}
			return sum;
		}
		/// <summary>
		/// Returns the variance of the full matrix.
		/// </summary>
		/// <returns></returns>
		public double Variance2d()
		{
			double var = 0.0;
			double average = AverageValue();
			int len = this.Columns * this.Rows;
			for (int i = 0; i < len; i++)
			{
				var = var + (average - this[i]) * (average - this[i]);
			}
			
			var = var / (Rows * Columns - 1);
			return var;
		}

        /// <summary>
        /// Returns the average of the full matrix
        /// </summary>
        /// <returns></returns>
        public double AverageValue()
        {
            return this.Sum() / (double)(this.Columns * this.Rows);
        }
        /// <summary>
        /// Returns a percentile value derived from all values in matrix
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        public ushort PercentileValue(double percentile)
        {
            List<ushort> all_values = new List<ushort>(m_Values);
            all_values.Sort();
            return all_values[(int)(all_values.Count * percentile)];
        }

        public List<ushort> PercentileValues(double percentile)
        {
            return PercentileValues(0, percentile);
        }
        /// <summary>
        /// Returns a list of the lowest fraction of the values in the matrix
        /// </summary>
        /// <param name="percentile"></param>
        /// <returns></returns>
        public List<ushort> PercentileValues(double lowerPercentile, double upperPercentile)
        {
            List<ushort> all_values = new List<ushort>(m_Values);
            all_values.Sort();
            return all_values.GetRange((int)(all_values.Count * lowerPercentile), (int)(all_values.Count * upperPercentile));
        }

		/// <summary>
		/// Returns the average variance along rows or columns.
		/// </summary>
		/// <param name="dim">dim = 1 (along rows), dim = 2 (along columns)</param>
		/// <returns></returns>
		public double Variance1d(int dim)
		{
			double var = 0.0;
			double average = 0.0;
			int index = 0;
			double temp = 0.0;

			if (dim == 1)
			{
				for (int i = 0; i < Rows; i++)
				{
					temp = 0.0;
					index = i * Rows;
					average = 0.0;
					for (int j = 0; j < Columns; j++)
					{
						average = average + this[index+j];
					}
					average = average / (double)this.Columns;
					for (int j = 0; j < Columns; j++)
					{
						temp = temp + (average - this[index+j]) * (average - this[index + j]);
					}
					var = var + temp/ (double) (Columns -1);
				}
				var = var/ (double)Rows;
			}
			if (dim == 2)
			{
				MatrixUshort usm = (MatrixUshort)this.Transpose();
				for (int i = 0; i < usm.Rows; i++)
				{
					temp = 0.0;
					index = i * usm.Rows;
					average = 0.0;
					for (int j = 0; j < usm.Columns; j++)
					{
						average = average + usm[index + j];
					}
					average = average / (double)usm.Columns;
					for (int j = 0; j < usm.Columns; j++)
					{
						temp = temp + (average - usm[index + j]) * (average - usm[index + j]);
					}
					var = var + temp / (double)(usm.Columns - 1);
				}
				var = var / (double)Rows;
			}
			return var;
		}
		/// <summary>
		/// Returns the average standard deviatiation along rows or columns.
		/// </summary>
		/// <param name="dim">dim = 1 (along rows), dim = 2 (along columns)</param>
		/// <returns></returns>
		public double StdDev1d(int dim)
		{
			double std = 0.0;
			double average = 0.0;
			int index = 0;
			double temp = 0.0;

			if (dim == 1)
			{
				for (int i = 0; i < Rows; i++)
				{
					temp = 0.0;
					index = i * Rows;
					average = 0.0;
					for (int j = 0; j < Columns; j++)
					{
						average = average + this[index + j];
					}
					average = average / (double)this.Columns;
					for (int j = 0; j < Columns; j++)
					{
						temp = temp + (average - this[index + j]) * (average - this[index + j]);
					}
					std = std + Math.Sqrt(temp / (double)(Columns - 1));
				}
				std = std / (double)Rows;
			}
			if (dim == 2)
			{
				MatrixUshort usm = (MatrixUshort)this.Transpose();
				for (int i = 0; i < usm.Rows; i++)
				{
					temp = 0.0;
					index = i * usm.Rows;
					average = 0.0;
					for (int j = 0; j < usm.Columns; j++)
					{
						average = average + usm[index + j];
					}
					average = average / (double)usm.Columns;
					for (int j = 0; j < usm.Columns; j++)
					{
						temp = temp + (average - usm[index + j]) * (average - usm[index + j]);
					}
					std = std + Math.Sqrt(temp / (double)(usm.Columns - 1));
				}
				std = std / (double)Rows;
			}
			return std;
		}		/// <summary>
		/// Returns sum of co-related differentials along rows or columns.
		/// </summary>
		/// <param name="dim">dim = 1 (along rows), dim = 2 (along columns)</param>
		/// <param name="step">Step defines the distance dx in the differential dy/dx. Default: dx = 1. </param>
		/// <returns></returns>
		public double CoDiff(int dim, int step)
		{
			double sum = 0.0;
			int w;
			if (dim == 1)
			{
				w = Columns - step - 1;
				for (int i = 0; i < Rows; i++)
				{
					for (int j = 0; j < w; j = j + step)
					{
						sum = sum + (this[i, j + step] - this[i, j]) * (this[i, j + step + 1] - this[i, j + 1]);
					}
				}
			}
			if(dim == 2)
			{
				MatrixUshort transp = (MatrixUshort)this.Transpose();
				w = transp.Columns - step - 1;
				int h = transp.Rows;
				for (int i = 0; i < h; i++)
				{
					for (int j = 0; j < w; j = j + step)
					{
						sum = sum + (transp[i, j + step] - transp[i, j]) * (transp[i, j + step + 1] - transp[i, j + 1]);
					}
				}
			}
			return sum;
		}
		/// <summary>
		/// Returns the sum of squared weighted differences.
		/// </summary>
		/// <param name="usm">The Ushortmatrix to subtract</param>
        /// <param name="norm1">Weight 1</param>       
        /// <param name="usm">Weight 2 (on usm)</param>       
		/// <returns></returns>
        public double SumOfSquaresNormalized (MatrixUshort usm, double norm1, double norm2)
		{
			if (this.Columns != usm.Columns || this.Rows != usm.Rows)
			{
				throw new IndexOutOfRangeException("Matrices have to be of same size.");
			}
			double sum = 0;
			int len = Columns*Rows;
			for (int i = 0; i < len; i++)
			{
				sum += (double)(this[i]*norm1-usm[i]*norm2)*(this[i]*norm1-usm[i]*norm2)*(this[i]*norm1-usm[i]*norm2)*(this[i]*norm1-usm[i]*norm2);//temp*temp;
			}
			return sum;
		}
        /// <summary>
        /// Returns the sum of squared differences.
        /// </summary>
        /// <param name="usm">The Ushortmatrix to subtract</param>
        /// <returns></returns>
		public long SumOfSquares (MatrixUshort usm)
		{
			if (this.Columns != usm.Columns || this.Rows != usm.Rows)
			{
				throw new IndexOutOfRangeException("Matrices have to be of same size.");
			}
			long sum = 0;
			int len = Columns*Rows;
			for (int i = 0; i < len; i++)
			{
				sum += (long)(this[i]-usm[i])*(this[i]-usm[i]);
				//sum = sum + Math.Pow(this[i]-usm[i],2); långsamt!!!
			}
			return sum;
		}
        /// <summary>
        /// Returns the sum of squared squared differences.
        /// </summary>
        /// <param name="usm">The Ushortmatrix to subtract</param>
        /// <returns></returns>
		public double SumOfSquaredSquares (MatrixUshort usm)
		{
			if (this.Columns != usm.Columns || this.Rows != usm.Rows)
			{
				throw new IndexOutOfRangeException("Matrices have to be of same size.");
			}
			double sum = 0;
			int len = Columns*Rows;
			for (int i = 0; i < len; i++)
			{
				sum += (double)(this[i]-usm[i])*(this[i]-usm[i])/(this[i]+usm[i])/(this[i]+usm[i]);
			}
			return sum;
		}	
		/// <summary>
		/// Returns the sum of multiplied elements.
		/// </summary>
        /// <param name="usm">The Ushortmatrix to subtract</param>
        /// <returns></returns>
		public double SumOfMultiplication (MatrixUshort usm)
		{
			if (this.Columns != usm.Columns || this.Rows != usm.Rows)
			{
				throw new IndexOutOfRangeException("Matrices have to be of same size.");
			}
			//double sum = 0;
			double sum = 0;
			int len = Columns*Rows;
			for (int i = 0; i < len; i++)
			{
				sum += (double)this[i]*(double)usm[i];
			}	
			return sum;
		}	
		/// <summary>
		/// Returns the sum of the UshortMatrix.
		/// </summary>
		/// <returns></returns>
		public long Sum()
		{
			long sum = 0;
			int length = this.m_Values.Length;
			sum = 0;
			for (int element = 0; element < length; element++)
			{
				sum = sum + this[element];
			}
			return sum;
		}

		public MatrixUshort SubMatrix(int row1, int row2, int col1, int col2)
		{
			if (row1 >= Rows || row2 >= Rows || col1 >= Columns || col2 >= Columns)
			{
				throw new IndexOutOfRangeException("Index to big.");
			}
			if (row1 < 0 || row2 < 0 || col1 < 0 || col2 < 0)
			{
				throw new IndexOutOfRangeException("Indices cannot be less than 0.");
			}
			if (row1 > row2 || col1 > col2)
			{
				throw new ArithmeticException("Lower index larger than higher index");
			}
			ushort [] subArray = new ushort[(row2-row1+1)*(col2-col1+1)];
			for (int row = 0; row < (row2-row1+1); row++)
			{
				Array.Copy(m_Values, (row1+row)*this.Columns + col1 , subArray, row*(col2-col1+1), (col2-col1+1));
			}
			MatrixUshort m = new MatrixUshort(subArray, (row2-row1+1), (col2-col1+1));
			return m;
			
		}

		/// <summary>
		/// Copy this matrix into a (larger) matrix instance
		/// </summary>
		/// <param name="um"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		public void CopyTo(MatrixUshort um, int x, int y)
		{
			for(int ix = 0; ix < Columns; ix++)
			{
				for(int iy = 0; iy < Rows; iy++)
				{
					um[iy + y, ix + x] = this[iy, ix];
				}
			}
		}

		public void ApplyInPlace(MatrixUshort other, Func<ushort, ushort, ushort> function)
		{
			int len = Columns*Rows;
			for(int i = 0; i < len; i++)
			{
				this[i] = function(this[i],other[i]);				
			}
		}
	}
}
