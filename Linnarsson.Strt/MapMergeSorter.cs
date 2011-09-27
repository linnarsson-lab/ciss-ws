using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Linnarsson.Strt
{
    public class MapMergeSorter
    {
        int tempFileIdx;
        string tempPath;
        int maxItems;

        public MapMergeSorter()
        {
            tempFileIdx = 1;
            maxItems = 10000000;
        }

        private class Item : IComparable<Item>
        {
            public static Dictionary<string, long> chrIndex = new Dictionary<string, long>();
            public long sorter;
            public string[] lines;
            public Item(long sorter, string[] lines)
            {
                this.sorter = sorter;
                this.lines = lines;
            }
            public static List<Item> MakeAll(List<long> sorters, List<string> lines)
            {
                List<Item> all = new List<Item>(sorters.Count);
                for (int s = 0; s < sorters.Count; s++)
                { // Order lines so that the first line always corresponds to each sorter.
                    string[] orderedLines = new string[lines.Count];
                    orderedLines[0] = lines[s];
                    int l = 1;
                    for (int i = 0; i < lines.Count; i++)
                        if (i != s) orderedLines[l++] = lines[i];
                    all.Add(new Item(sorters[s], orderedLines));
                }
                return all;
            }
            public static long MakeSorter(string chr, char strand, int pos)
            {
                if (!chrIndex.ContainsKey(chr))
                    chrIndex[chr] = chrIndex.Count;
                long s = (strand == '+')? 1 : 0;
                return (chrIndex[chr] << 51) + (s << 50) + pos; 
            }

            public int CompareTo(Item other)
            {
                long diff = sorter - other.sorter;
                return (diff < 0) ? -1 : ((diff > 0) ? 1 : 0);
            }
        }
        
        private static IEnumerable<Item> MultiMappings(string file)
        {
            StreamReader reader = new StreamReader(file);
            string line = reader.ReadLine();
            if (line == null) yield break;
            string[] fields = line.Split('\t');
            if (fields.Length < 8)
                throw new FormatException("Too few columns in input bowtie map file");
            List<long> sorters = new List<long>();
            List<string> lines = new List<string>();
            string id = fields[0];
            while (line != null)
            {
                fields = line.Split('\t');
                if (!line.StartsWith(id))
                {
                    foreach (Item i in Item.MakeAll(sorters, lines))
                        yield return i;
                    lines.Clear();
                    sorters.Clear();
                    id = fields[0];
                }
                string chr = fields[2];
                char strand = fields[1][0];
                int pos = int.Parse(fields[3]);
                sorters.Add(Item.MakeSorter(chr, strand, pos));
                lines.Add(line);
                line = reader.ReadLine();
            }
            reader.Close();
            foreach (Item i in Item.MakeAll(sorters, lines))
                yield return i;
            yield break;
        }

        private string WriteToTemp(Item[] items)
        {
            string tempFile = Path.Combine(tempPath, "temp_sorting_" + tempFileIdx + ".map");
            tempFileIdx++;
            using (StreamWriter w = new StreamWriter(tempFile))
                foreach (Item j in items)
                    w.WriteLine(string.Join("\n", j.lines));
            Console.WriteLine("Wrote " + items.Length + " items to " + tempFile);
            return tempFile;
        }

        private class MapFile : IComparable<MapFile>
        {
            private StreamReader reader;
            private string nextLine;
            private Item currentItem;
            public MapFile(string file)
            {
                this.reader = new StreamReader(file);
                nextLine = reader.ReadLine();
                ReadNext();
            }
            private void ReadNext()
            {
                if (nextLine == null)
                {
                    currentItem = null;
                    reader.Close();
                }
                else
                {
                    string[] fields = nextLine.Split('\t');
                    string id = fields[0];
                    string chr = fields[2];
                    char strand = fields[1][0];
                    int pos = int.Parse(fields[3]);
                    List<string> lines = new List<string>();
                    lines.Add(nextLine);
                    nextLine = reader.ReadLine();
                    while (nextLine != null && nextLine.StartsWith(id))
                    {
                        lines.Add(nextLine);
                        nextLine = reader.ReadLine();
                    }
                    currentItem = new Item(Item.MakeSorter(chr, strand, pos), lines.ToArray());
                }
            }
            public Item Peek()
            {
                return currentItem;
            }
            public Item Next()
            {
                Item current = currentItem;
                ReadNext();
                return current;
            }

            public int CompareTo(MapFile other)
            {
                if (other == null)
                    return 1;
                long diff = currentItem.sorter - ((MapFile)other).currentItem.sorter;
                return (diff < 0) ? -1 : ((diff > 0) ? 1 : 0);
            }
        }

        private void JoinFiles(List<string> tempFiles, string outFile)
        {
            int n = 0;
            List<MapFile> queue = new List<MapFile>();
            foreach (string tempFile in tempFiles)
                queue.Add(new MapFile(tempFile));
            queue.Sort();
            StreamWriter writer = new StreamWriter(outFile);
            while (queue.Count > 0)
            {
                MapFile currentFile = queue[0];
                Item next = currentFile.Next();
                n++;
                writer.WriteLine(string.Join("\n", next.lines));
                queue.RemoveAt(0);
                if (currentFile.Peek() != null)
                {
                    int insPos = queue.BinarySearch(currentFile);
                    if (insPos < 0) insPos = ~insPos;
                    queue.Insert(insPos, currentFile);
                }
            }
            writer.Close();
            Console.WriteLine("Wrote totally " + n + " multireads to " + outFile);
        }

        public void MergeSort(List<string> mapFiles, string outFile)
        {
            tempPath = Path.GetDirectoryName(outFile);
            List<string> tempFiles = new List<string>();
            Item[] items = new Item[maxItems];
            int nReads = 0;
            int n = 0;
            foreach (string file in mapFiles)
            {
                foreach (Item i in MultiMappings(file))
                {
                    nReads++;
                    items[n++] = i;
                    if (n == maxItems)
                    {
                        Array.Sort(items);
                        tempFiles.Add(WriteToTemp(items));
                        n = 0;
                    }
                }
            }
            if (n > 0)
            {
                Array.Resize(ref items, n);
                Array.Sort(items);
                tempFiles.Add(WriteToTemp(items));
            }
            Console.WriteLine("Totally " + nReads + " multireads in " + mapFiles.Count + " .map files.");
            if (tempFiles.Count == 1)
            {
                if (File.Exists(outFile))
                    File.Delete(outFile);
                File.Move(tempFiles[0], outFile);
            }
            else
            {
                JoinFiles(tempFiles, outFile);
                foreach (string tempFile in tempFiles)
                    File.Delete(tempFile);
            }
        }

    }
}
