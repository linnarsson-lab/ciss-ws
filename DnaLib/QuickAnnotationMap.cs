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
            bins = new List<List<FtInterval>>();
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

        public IEnumerable<FtInterval> GetItems(int pos)
		{
			int bin = pos / BinSize;
			if (bin >= bins.Count) yield break;
			foreach (FtInterval item in bins[bin])
                if(item.Contains(pos)) yield return item;
		}

        /*
                public void Serialize(BinaryWriter writer)
                {
                    long numItems = Count;
                    writer.Write(numItems);
                    writer.Write(BinSize);

                    foreach(var bin in bins)
                    {
                        foreach(var item in bin)
                        {
                            writer.Write(item.Start);
                            writer.Write(item.End);
                            writer.Write(item.Item.Name);
                        }
                    }
                }

                public static IEnumerable<Feature> Loci(BinaryReader reader)
                {
                    // Careful! The order of these two statements matters!
                    long numItems = reader.ReadInt64();
                    int reportfreq = Math.Max((int)(numItems / 100), 1);
                    for (long i = 0; i < numItems; i++)
                    {
                        Feature loc = new Feature();
                        loc.Start = (int)reader.ReadInt64();
                        loc.End = (int)reader.ReadInt64();
                        loc.Name = reader.ReadString();
                        yield return loc;
                    }
                    yield break;
                }

                public static QuickAnnotationMap Deserialize(BinaryReader reader)
                {
                    // Careful! The order of these two statements matters!
                    long numItems = reader.ReadInt64();
                    int reportfreq = Math.Max((int)(numItems / 100),1);
                    QuickAnnotationMap result = new QuickAnnotationMap((int)reader.ReadInt64());
                    for(long i = 0; i < numItems; i++)
                    {
                        IntInterval<DoMarkHit> interval = new IntInterval<DoMarkHit>(
                            (int)reader.ReadInt64(),
                            (int)reader.ReadInt64(),
                            reader.ReadString()
                            );
                        result.Add(interval);
                        if(i % reportfreq == 0) Background.Progress((int)(i * 100 / numItems));
                    }
                    return result;
                }*/
    }
}
