using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;

namespace Linnarsson.Strt
{
    public class BowtieMapFileSorter
    {
        public void SortMapFile(string mapFile)
        {
            List<long> positions = new List<long>();
            List<long> fileIndexes = new List<long>();
            Dictionary<string, int> chrToNo = new Dictionary<string, int>();
            StreamReader reader = new StreamReader(mapFile);
            long filepos = reader.BaseStream.Position;
            long blockFilePos = reader.BaseStream.Position;
            string line = reader.ReadLine();
            string[] fields = line.Split('\t');
            List<long> allCombPositions = new List<long>();
            while (line != null)
            {
                if (!line.StartsWith(fields[0]))
                {
                    foreach (long p in allCombPositions)
                    {
                        int idx = positions.BinarySearch(p);
                        if (idx < 0) idx = ~idx;
                        positions.Insert(idx, p);
                        fileIndexes.Insert(idx, blockFilePos);
                    }
                    blockFilePos = filepos;
                    allCombPositions.Clear();
                }
                fields = line.Split('\t');
                string chr = fields[2].StartsWith("chr") ? fields[2].Substring(3) : fields[2];
                if (!chrToNo.ContainsKey(chr))
                {
                    chrToNo[chr] = chrToNo.Count;
                }
                int position = int.Parse(fields[3]);
                long combPosition = position | (chrToNo[chr] << 32);
                allCombPositions.Add(combPosition);
                filepos = reader.BaseStream.Position;
                line = reader.ReadLine();
            }
            reader.Close();
            Console.WriteLine(positions.Count + " blocks to sort.");
            StreamWriter outfile = new StreamWriter(mapFile + ".sorted");
            reader = new StreamReader(mapFile);
            int n = 0;
            foreach (long fp in fileIndexes)
            {
                reader.BaseStream.Position = fp;
                line = reader.ReadLine();
                string id = line.Split('\t')[0];
                while (line.StartsWith(id))
                {
                    outfile.WriteLine(line);
                    line = reader.ReadLine();
                }
                if (++n % 100000 == 0)
                    Console.WriteLine(n);
            }
            outfile.Close();
        }
    }
}
