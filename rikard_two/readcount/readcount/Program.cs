using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace readcount
{
    class Program
    {
        static void Main(string[] args)
        {
            string filepath = args[0];
            string regionsfile = args[1];
            Dictionary<string, Int32[]> region = new Dictionary<string, Int32[]>();
            using (StreamReader r = new StreamReader(regionsfile))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.IndexOf('#', 0) != 0)
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        region["chr" + entries[0] + "p" + entries[1]] = new Int32[2];
                        region["chr" + entries[0] + "p" + entries[1]][0] = Int32.Parse(entries[1]);
                        region["chr" + entries[0] + "p" + entries[1]][1] = Int32.Parse(entries[2]);
                    }
                }
            }
            Dictionary<string, Int32> counter = new Dictionary<string, Int32>();
            using (StreamReader r = new StreamReader(filepath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.IndexOf('@', 0) != 0)
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        //                        Console.WriteLine("chr" + entries[2] + "pos" + entries[3] + " " + entries[5] + " ");
                        if (!counter.ContainsKey("chr" + entries[2] + "p" + entries[3]))
                            counter["chr" + entries[2] + "p" + entries[3]] = 1;
                        else
                            counter["chr" + entries[2] + "p" + entries[3]]++;
                    }
                }
                var list = counter.Keys.ToList();
                list.Sort();
                foreach (KeyValuePair<string, Int32> item in counter)
                {
                    string[] position = item.Key.Split(new Char[] { 'p' });
                    string chr = position[0];
                    Int32 pos = Int32.Parse(position[1]);
                    foreach (KeyValuePair<string, Int32[]> area in region)
                    {
                        string[] posit = area.Key.Split(new Char[] { 'p' });
                        string CHR = posit[0];
                        Int32 start = area.Value[0];
                        Int32 stop = area.Value[1];
                        if ((chr == CHR) && (pos >= start) && (pos <= stop))
                        {
                            Console.WriteLine(area.Key + "\t" + item.Key + "\t" + counter[item.Key]);
                        }
                    } 
                }
            }
        }
    }
}