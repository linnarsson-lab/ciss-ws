using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Utilities
{
	/// <summary>
	/// A line-based file reader that supports PeekLine()
	/// </summary>
	public class LineReader
	{
		private StreamReader stream;
		private string nextLine;

		public bool AtEOF { get { return nextLine == null; } }

		public LineReader(string path)
		{
			stream = path.OpenRead();
			nextLine = stream.ReadLine();
		}

		public LineReader(StreamReader sr)
		{
			stream = sr;
			nextLine = stream.ReadLine();
		}

		public string ReadLine()
		{
			string temp = nextLine;
			nextLine = stream.ReadLine();
			return temp;
		}

		public string PeekLine()
		{
			return nextLine;
		}

		public void Close()
		{
			stream.Close();
            stream.Dispose();
		}
	}
}
