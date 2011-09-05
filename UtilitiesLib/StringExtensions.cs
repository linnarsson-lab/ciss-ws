using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace Linnarsson.Utilities
{
	public static class StringExtensions
	{
		/// <summary>
		/// Open a file for reading. Gzip-compressed files (*.gz) can be read directly, with
		/// decompression on the fly.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static StreamReader OpenRead(this string path)
		{
			if(Path.GetExtension(path) == ".gz")
			{
				FileStream fs = new FileStream(path, FileMode.Open);
				GZipStream gzip = new GZipStream(fs, CompressionMode.Decompress);
				return new StreamReader(gzip);
			}
			else
			{
				return new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
			}
		}

		/// <summary>
		/// Open a file for writing. Gzip-compressed files (*.gz) can be written directly, with
		/// compression on the fly.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public static StreamWriter OpenWrite(this string path)
		{
			if(Path.GetExtension(path) == ".gz")
			{
				FileStream fs = new FileStream(path, FileMode.Create);
				GZipStream gzip = new GZipStream(fs, CompressionMode.Compress);
				return new StreamWriter(gzip);
			}
			else
			{
				return new StreamWriter(path);
			}
		}

		public static string Reverse(this string input)
		{
			char[] result = new char[input.Length];
			for(int i = 0; i < input.Length; i++)
			{
				result[input.Length - i - 1] = input[i];
			}
			return new string(result);
		}

	}
}
