using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Net;

namespace Linnarsson.Utilities
{
	/// <summary>
	/// A file stored at an URL, with a local cache. The file will only download once.
	/// </summary>
	public class RemoteFile
	{
		/// <summary>
		/// Get a local filename for a remotely stored file. If a local copy
		/// doesn't yet exist, the file is first downloaded.
		/// </summary>
		/// <param name="url">URL of the remote file (must be http or ftp)</param>
		/// <returns></returns>
		public static string Get(string url)
		{
			// Compute an MD5 hash as string
			var bytes = Encoding.Unicode.GetBytes(url);
			var hashBytes = new MD5CryptoServiceProvider().ComputeHash(bytes);
			StringBuilder sb = new StringBuilder();
			foreach (byte b in hashBytes) sb.Append(b.ToString("X2"));
			var hash = sb.ToString();

			var localFile = Path.Combine(Path.GetTempPath(), hash);
			if (!File.Exists(localFile))
			{
				// Download the file
				var uri = new Uri(url);
				var web = new WebClient();
				if (uri.Scheme == "ftp") web.Credentials = new NetworkCredential("anonymous", "sten.linnarsson@ki.se");
				web.DownloadFile(url, localFile);
			}
			return localFile;			
		}
	}
}
