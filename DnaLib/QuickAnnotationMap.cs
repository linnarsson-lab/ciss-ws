using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Mathematics;
using System.Runtime.Serialization;
using System.IO;
using Linnarsson.Utilities;

namespace Linnarsson.Dna
{
	/// <summary>
	/// A class that allocates intervals to bins, for quick access by position
	/// </summary>
	public class QuickAnnotationMap
	{
		public int BinSize { get; set; }
		List<List<FtInterval>> bins;

		public QuickAnnotationMap(int binSize)
		{
            bins = new List<List<FtInterval>>(300000000 / binSize);
			BinSize = binSize;
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
		public void Add(FtInterval item)
		{
			int startBin = item.Start / BinSize;
			int endBin = item.End / BinSize;
			while(bins.Count <= endBin)
			{
                bins.Add(new List<FtInterval>());
			}
			for(int i = startBin; i < endBin + 1; i++)
			{
                if (bins[i] == null) bins[i] = new List<FtInterval>();
				bins[i].Add(item);
			}
		}

        public IEnumerable<FtInterval> IterItems(int pos)
		{
			int bin = pos / BinSize;
			if (bin >= bins.Count) yield break;
			foreach (FtInterval item in bins[bin])
                if(item.Contains(pos)) yield return item;
		}
    }
}
