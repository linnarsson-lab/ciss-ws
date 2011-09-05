using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Collections;
using Linnarsson.Utilities;

namespace Linnarsson.Mathematics.Data
{
	public interface IDataView
	{
		int RowCount { get;  }
		int ColumnCount { get; }

		double this[int row, int column] { get; }

		IList<string> GetRowAnnotationNames();
		IList<string> GetColumnAnnotationNames();
		string GetAnnotationForRow(string name, int row);
		string GetAnnotationForColumn(string name, int column);
		string GetColumnName(int column);
		string GetRowName(int row);
		double[] Fold(Func<double, double, double> f, double x);
	}

	public abstract class AbstractDataView : IDataView
	{
		public int RowCount { get; protected set; }
		public int ColumnCount { get; protected set; }
		
		public abstract double this[int row, int column] { get;  }

		public abstract IList<string> GetRowAnnotationNames();
		public abstract IList<string> GetColumnAnnotationNames();
		public abstract string GetAnnotationForRow(string name, int row);
		public abstract string GetAnnotationForColumn(string name, int column);
		public abstract string GetColumnName(int column);
		public abstract string GetRowName(int row);

		/// <summary>
		/// Returns f(f(f(x,a),b),c) for elements a,b,c in each row
		/// </summary>
		/// <param name="f"></param>
		/// <param name="x"></param>
		/// <returns>An array of ColumnCount numbers</returns>
		public double[] Fold(Func<double, double, double> f, double x)
		{
			double[] result = new double[ColumnCount];
			for (int i = 0; i < ColumnCount; i++)
			{
				result[i] = x;
			}
			for (int r = 0; r < RowCount; r++)
			{
				for (int i = 0; i < ColumnCount; i++)
				{
					result[i] = f(result[i], this[r, i]);
				}
			}
			return result;
		}

		public long Size { get { return RowCount * (long)ColumnCount; } }
	
	}

	/// <summary>
	/// A matrix of data together with row and column annotations
	/// </summary>
	public unsafe class DataFrame : AbstractDataView
	{
		/// <summary>
		/// The value used (e.g. when loading from file) when a value cannot be converted to double. This includes empty cells, non-numerical cells etc.
		/// </summary>
		public double MissingValue { get; set; }

		/// <summary>
		///  Name of the DataFrame for display purposes
		/// </summary>
		public string Name { get; set; }


		private List<string> ColumnKeys { get; set; }
		private List<string> RowKeys { get; set; }

		private Dictionary<string, string[]> RowAnnotations { get; set; }
		private Dictionary<string, string[]> ColumnAnnotations { get; set; }


		//private double[] data;	// Up to 268 million elements (~12000 cells by 21000 genes)
		double* data;				// Number of elements limited only by available virtual memory address space
		private long capacity;

		public override double this[int row, int col]
		{
			get
			{
				return data[row * ColumnCount + col];
			}
		}

		public override string GetRowName(int row)
		{
			return RowKeys[row];
		}

		public override string GetColumnName(int column)
		{
			return ColumnKeys[column];
		}


		private void grow(long newCapacity)
		{
			// Allocate the new space
			double *newdata = (double*)Marshal.AllocHGlobal((IntPtr)(newCapacity * 8));

			// Check that we got a good pointer back
			if (newdata == null)
			{
				throw new OutOfMemoryException("AllocHGlobal returned null in DataFrame.grow()");
			}

			// If there was data in the DataFrame before
			if (Size != 0)
			{
				// Copy it over to the new place
				for (int i = 0; i < Size; i++)
				{
					newdata[i] = data[i];
				}
				// Free up the old storage
				Marshal.FreeHGlobal((IntPtr)data);
			}
			data = newdata;
			capacity = newCapacity;
		}

		/// <summary>
		/// Create an empty DataFrame
		/// </summary>
		public DataFrame(string name)
		{
			Name = name;

			RowAnnotations = new Dictionary<string, string[]>();
			ColumnAnnotations = new Dictionary<string, string[]>();

			RowKeys = new List<string>();
			ColumnKeys = new List<string>();
		}


		/// <summary>
		/// Add a row to the DataFrame, growing the storage as necessary
		/// </summary>
		/// <param name="columnKeys"></param>
		/// <param name="data"></param>
		/// <param name="rowKey"></param>
		public void AddRow(string[] columnKeys, double[] rowData, string rowKey)
		{
			
			// See if we need to set up the keys and annotations
			if (Size == 0)
			{
				ColumnKeys = new List<string>(columnKeys);
				RowKeys = new List<string>();
				ColumnCount = ColumnKeys.Count;
			}

			// Check validity
			if (columnKeys.Length != ColumnCount || rowData.Length != ColumnCount) throw new IndexOutOfRangeException("Column keys or data are not same length as number of columns");

			// Grow if necessary
			if (capacity < (Size + ColumnCount)) grow(Math.Max(rowData.LongLength * 100, capacity + capacity / 2));

			// Add a row, copy the data, increase the row count
			RowKeys.Add(rowKey);
			for (int i = 0; i < rowData.Length; i++)
			{
				data[RowCount * ColumnCount + i] = rowData[i];
			}
			RowCount++;
		}

		public static DataFrame FromFile(string filename)
		{
			DataFrame result = new DataFrame(filename);
			result.loadFromFile(filename);
			return result;
		}

		/// <summary>
		/// Load data from tab-delimited text file:
		/// 
		///			ColKey1	ColKey2	ColKey3
		/// RowKey1	1.2		3.4		2.32
		/// RowKey2	78		4.6		2.34
		/// 
		/// </summary>
		/// <param name="filename"></param>
		private void loadFromFile(string filename)
		{
			if (Size != 0) throw new InvalidOperationException("Cannot load from file into a non-empty DataFrame");
			string[] columnheaders = null;

			var file = filename.OpenRead();
			int rowNum = 0;
			while (true)
			{
				string line = file.ReadLine();
				if (line == null || line.Trim() == "") break;

				string[] items = line.Split('\t');
				rowNum++;

				if (columnheaders == null)
				{
					columnheaders = new string[items.Length - 1];
					for (int i = 0; i < columnheaders.Length; i++)
					{
						columnheaders[i] = items[i + 1].Trim();
					}

					continue;
				}
				if (items.Length != columnheaders.Length + 1) throw new IndexOutOfRangeException("Row #" + rowNum + " does not have the right number of columns");

				string rowKey = items[0];
				double[] rowData = new double[columnheaders.Length];
				for (int i = 0; i < columnheaders.Length; i++)
				{
					double x;
					if (double.TryParse(items[i + 1], out x)) rowData[i] = x;
					else rowData[i] = MissingValue;
				}

				AddRow(columnheaders, rowData, rowKey);
			}
			file.Close();
		}

		/// <summary>
		/// Load annotations in Qlucore format. First column contains keys, first row contains names of annotations (name of first column is arbitrary but must be given). All
		/// names must be unique. Rest of file contains values for the keys.
		/// </summary>
		/// <param name="filename"></param>
		public void AddColumnAnnotations(string filename)
		{
			LoadAnnotations(filename, ColumnKeys, ColumnAnnotations);
		}

		/// <summary>
		/// Load annotations in Qlucore format. First column contains keys, first row contains names of annotations (name of first column is arbitrary but must be given). All
		/// names must be unique. Rest of file contains values for the keys.
		/// </summary>
		/// <param name="filename"></param>
		public void AddRowAnnotations(string filename)
		{
			LoadAnnotations(filename, RowKeys, RowAnnotations);
		}


		/// <summary>
		/// Add annnotations for the rows in the DataFrame. 
		/// </summary>
		/// <param name="annotationName">Name of the new annotation</param>
		/// <param name="annotations">Values, in the form of key-value pairs where the keys must match the Rowkeys</param>
		public void AddRowAnnotations(string annotationName, Dictionary<string, string> annotations)
		{
			string[] result = new string[RowCount];
			for (int i = 0; i < result.Length; i++)
			{
				if (annotations.ContainsKey(RowKeys[i])) result[i] = annotations[RowKeys[i]];
			}
			RowAnnotations[annotationName] = result;
			return;
		}
		/// <summary>
		/// Add annnotations for the columns in the DataFrame. 
		/// </summary>
		/// <param name="annotationName">Name of the new annotation</param>
		/// <param name="annotations">Values, in the form of key-value pairs where the keys must match the ColumnKeys</param>
		public void AddColumnAnnotations(string annotationName, Dictionary<string, string> annotations)
		{
			string[] result = new string[ColumnCount];
			for (int i = 0; i < result.Length; i++)
			{
				if (annotations.ContainsKey(ColumnKeys[i])) result[i] = annotations[ColumnKeys[i]];
			}
			ColumnAnnotations[annotationName] = result;
			return;
		}
		/// <summary>
		/// Load annotations in Qlucore format. First column contains keys, first row contains names of annotations (name of first column is arbitrary but must be given). All
		/// names must be unique. Rest of file contains values for the keys.
		/// </summary>
		/// <param name="filename"></param>
		/// <param name="keys"></param>
		/// <param name="target"></param>
		/// <returns></returns>
		private void LoadAnnotations(string filename, List<string> keys, Dictionary<string, string[]> target)
		{
			Dictionary<string, List<string>> result = new Dictionary<string,List<string>>();
			string[] names = null;

			var file = filename.OpenRead();
			int rowNum = 0;
			while (true)
			{
				string line = file.ReadLine();
				if (line == null || line.Trim() == "") break;

				string[] items = line.Split('\t');
				rowNum++;

				if (result.Count == 0)
				{
					names = items;
					foreach (var name in names) result[name] = new List<string>();	// Note that first column is the primary key
					continue;
				}
				if (items.Length != result.Count) throw new IndexOutOfRangeException("Row #" + rowNum + " does not have the right number of columns");

				for (int i = 0; i < items.Length; i++)
				{
					result[names[i]].Add(items[i]);	// Add the values (first value is the key for this row)
				}
			}
			file.Close();

			// Now merge the loaded annotations with the existing ones, by matching up the keys
			List<string> loadedKeys = result[names[0]];
			// Take each annotation 
			for (int i = 1; i < names.Length; i++)
			{
				// The annotation is a name with a list of values, one for each loadedKey
				string name = names[i];
				List<string> values = result[name];

				string[] fullValues = new string[keys.Count];	// These are the values that will go into the DataFrame
				for (int j = 0; j < fullValues.Length; j++)
				{
					fullValues[j] = "";	// Fill with empty strings to manage missing values
				}

				for (int j = 0; j < values.Count; j++)
				{
					string jthKey = loadedKeys[j];
					int index = keys.IndexOf(jthKey);
					if (index < 0) continue;	// Missing key
					fullValues[index] = values[j];	// Put the jth value where the jth key is
				}

				// Put the annotation in place (replacing any previous annotation with same name)
				target[name] = fullValues;
			}
		}

		~DataFrame()
		{
			if (capacity != 0) Marshal.FreeHGlobal((IntPtr)data);
		}


		public override IList<string> GetRowAnnotationNames()
		{
			return RowAnnotations.Keys.ToArray();			// TODO: optimize access if this is used frequently
		}

		public override IList<string> GetColumnAnnotationNames()
		{
			return ColumnAnnotations.Keys.ToArray();		// TODO: optimize access if this is used frequently
		}

		public override string GetAnnotationForRow(string name, int row)
		{
			if (name == null) return RowKeys[row];
			return RowAnnotations[name][row];
		}

		public override string GetAnnotationForColumn(string name, int column)
		{
			if (name == null) return ColumnKeys[column];
			return ColumnAnnotations[name][column];
		}
	}

	/// <summary>
	/// A view which is transposed, exchanging rows and columns
	/// </summary>
	public class TransposedDataView : DataView
	{
		public override IList<string> GetRowAnnotationNames()
		{
			return Parent.GetColumnAnnotationNames();
		}

		public override IList<string> GetColumnAnnotationNames()
		{
			return Parent.GetRowAnnotationNames();
		}

		/// <summary>
		/// Get a value through the view from the underlying DataFrame
		/// </summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public override double this[int row, int column]
		{
			get
			{
				return Parent[column, row];
			}
		}

		public override string GetAnnotationForRow(string name, int row)
		{
			return Parent.GetAnnotationForColumn(name, row);
		}
		public override string GetAnnotationForColumn(string name, int column)
		{
			return Parent.GetAnnotationForRow(name, column);
		}
		public override  string GetColumnName(int column)
		{
			return Parent.GetRowName(column);
		}
		public override string GetRowName(int row)
		{
			return Parent.GetColumnName(row);
		}
		public TransposedDataView(IDataView parent)
			: base(parent)
		{

		}
	}

	public class RpmNormalizedDataView : DataView
	{
		protected double[] NormalizationConstants;

		public override double this[int row, int column]
		{
			get
			{
				return base[row, column]*NormalizationConstants[column];
			}
		}

		public RpmNormalizedDataView(IDataView parent)
			: base(parent)
		{
			Recalculate();
		}

		public override void Recalculate()
		{
			base.Recalculate();
			
			// Calculate column sums
			NormalizationConstants = Parent.Fold((double a, double b) => a + b, 0);

			// Normalize
			for (int i = 0; i < NormalizationConstants.Length; i++)
			{
				NormalizationConstants[i] = 1000000 / NormalizationConstants[i];
			}
		}
	}
	public class AbsoluteNormalizedDataView : DataView
	{
		protected double[] NormalizationConstants;

		public override double this[int row, int column]
		{
			get
			{
				return base[row, column] * NormalizationConstants[column];
			}
		}

		public AbsoluteNormalizedDataView(IDataView parent)
			: base(parent)
		{
			Recalculate();
		}

		public override void Recalculate()
		{
			base.Recalculate();
			ColumnCount = Parent.ColumnCount;
			RowCount = Parent.RowCount;

			// Make a view filtered to show only the spikes, then calculate the column sums
			FilteredDataView fv = new FilteredDataView(Parent);
			fv.Filters.Add(new PredicateFilter((DataView view, int row) => view.GetRowName(row).StartsWith("RNA_SPIKE")));
			fv.Recalculate();
			double[] spikeSums = fv.Fold((double a, double b) => a + b, 0);

			// Calculate total column sums
			NormalizationConstants = Parent.Fold((double a, double b) => a + b, 0);

			// Normalize
			for (int i = 0; i < NormalizationConstants.Length; i++)
			{
				NormalizationConstants[i] = spikeSums[i] == 0 ? 0 : 1/(2500 / spikeSums[i]  * NormalizationConstants[i]);
			}
		}
	}


	/// <summary>
	/// A view with row filters and sorters
	/// </summary>
	public class FilteredDataView : DataView
	{
		/// <summary>
		/// A list of ViewSorter objects. The view will be sorted by these, in order (i.e. if first sorter determines that rows are equal, the next sorter is applied etc).
		/// Both row- and column-sorters can be used and will affect rows and columns independently.
		/// Changes to this collection take effect only after Recalculate() is called.
		/// </summary>
		public List<ViewSorter> Sorters { get; private set; }
		/// <summary>
		/// A list of ViewFilter objects. The view will be filtered by these, retaining ony those rows/columns that are accepted by all the filters.
		/// Changes to this collection take effect only after Recalculate() is called.
		/// </summary>
		public List<ViewFilter> Filters { get; private set; }


		// Indices into the DataFrame, which give the order of the rows and columns
		// Total number of indices is equal to RowCount (ColumnCount) but smaller than
		// the number of rows (columns) in the DataFrame, if filters are applied
		private int[] RowIndices;
		private int[] ColumnIndices;

		/// <summary>
		/// Get a value through the view from the underlying DataFrame
		/// </summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public override double this[int row, int column]
		{
			get
			{
				return Parent[RowIndices[row], ColumnIndices[column]];
			}
		}
		public override string GetAnnotationForRow(string name, int row)
		{
			return Parent.GetAnnotationForRow(name, RowIndices[row]);
		}
		public override string GetAnnotationForColumn(string name, int column)
		{
			return Parent.GetAnnotationForColumn(name, ColumnIndices[column]);
		}
		public override string GetColumnName(int column)
		{
			return Parent.GetColumnName(ColumnIndices[column]);
		}

		public override string GetRowName(int row)
		{
			return Parent.GetRowName(RowIndices[row]);
		}

		public override void Recalculate()
		{
			base.Recalculate();
			RowIndices = new int[RowCount];
			for (int i = 0; i < RowCount; i++)
			{
				RowIndices[i] = i;
			}
			ColumnIndices = new int[ColumnCount];
			for (int i = 0; i < ColumnCount; i++)
			{
				ColumnIndices[i] = i;
			}

			// Apply each filter
			foreach (var filter in Filters)
			{
				List<int> newIndices = new List<int>();

				for (int i = 0; i < RowCount; i++)
				{
					if (filter.Examine(this, i)) newIndices.Add(RowIndices[i]);
				}
				RowIndices = newIndices.ToArray();
				RowCount = RowIndices.Length;
			}

			// TODO: sort the view
		}
		public FilteredDataView(IDataView parent) : base(parent)
		{
			Sorters = new List<ViewSorter>();
			Filters = new List<ViewFilter>();
		}
	}

	/// <summary>
	/// A view with no filtering, sorting, or transposition
	/// </summary>
	public class DataView : AbstractDataView
	{
		protected IDataView Parent;

		public override IList<string> GetRowAnnotationNames()
		{
			return Parent.GetRowAnnotationNames();
		}

		public override IList<string> GetColumnAnnotationNames()
		{
			return Parent.GetColumnAnnotationNames();
		}


		/// <summary>
		/// Get a value through the view from the underlying DataFrame
		/// </summary>
		/// <param name="row"></param>
		/// <param name="column"></param>
		/// <returns></returns>
		public override double this[int row, int column]
		{
			get
			{
				return Parent[row, column];
			}
		}

		public DataView(IDataView parent)
		{
			Parent = parent;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name">Annotation name, or null to return the row ID</param>
		/// <param name="row"></param>
		/// <returns></returns>
		public override string GetAnnotationForRow(string name, int row)
		{
			return Parent.GetAnnotationForRow(name, row);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name">Annotation name, or null to return the colulmn ID</param>
		/// <param name="column"></param>
		/// <returns></returns>
		public override string GetAnnotationForColumn(string name, int column)
		{
			return Parent.GetAnnotationForColumn(name, column);
		}

		public override string GetColumnName(int column)
		{
			return Parent.GetColumnName(column);
		}

		public override string GetRowName(int row)
		{
			return Parent.GetRowName(row);
		}

	

		public virtual void Recalculate()
		{
			// Show the full view
			RowCount = Parent.RowCount;
			ColumnCount = Parent.ColumnCount;
		}
	}

	public abstract class ViewSorter
	{
		/// <summary>
		/// Function to compare two rows
		/// </summary>
		/// <param name="view"></param>
		/// <param name="index1"></param>
		/// <param name="index2"></param>
		/// <returns>-1, 0 or 1 if index1 <, > or equal to index2</returns>
		public abstract int Compare(DataView view, int index1, int index2);
	}

	public abstract class ViewFilter
	{
		/// <summary>
		/// Function to filter rows
		/// </summary>
		/// <param name="view"></param>
		/// <param name="index">The row to examine</param>
		/// <returns>True if the row should be accepted</returns>
		public abstract bool Examine(DataView view, int index);
	}


	/// <summary>
	/// Filter on annotation, collecting those values that are among a set of given values
	/// </summary>
	class EqualsOneOfFilter : ViewFilter
	{
		public string AnnotationName { get; private set; }
		public string[] Values { get; private set; }

		public override bool Examine(DataView view, int index)
		{
			string ann = view.GetAnnotationForRow(AnnotationName, index);
			if (Values.Contains(ann)) return true;
			return false;
		}

		public EqualsOneOfFilter(string name, string[] values)
		{
			AnnotationName = name;
			Values = values;
		}
	}
	/// <summary>
	/// Filter on annotation, collecting those values that are among a set of given values
	/// </summary>
	class PredicateFilter : ViewFilter
	{
		public Func<DataView, int, bool> Predicate { get; private set; }

		public override bool Examine(DataView view, int index)
		{
			return Predicate(view, index);
		}

		/// <summary>
		/// Filter based on a predicate (lambda)
		/// </summary>
		/// <param name="predicate">A function that takes a DataView and a row index, and returns a bool</param>
		public PredicateFilter(Func<DataView, int, bool> predicate)
		{
			Predicate = predicate;
		}
	}
}
