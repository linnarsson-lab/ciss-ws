using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace readcount
{
    /*
     * read in sam file and count number of reads at each position 
     * use a probe regions file to determine which ones to count
     * mono readcount.exe <regions file> <sam file>
     * output goes to standard out
     */
    public class samread
    {
        public string QNAME { get; set; }
        public int FLAG { get; set; }
        public string RNAME { get; set; }
        public Int32 POS { get; set; }
        public Int32 MAPQ { get; set; }
        public string CIGAR { get; set; }
        public string RNEXT { get; set; }
        public Int32 PNEXT { get; set; }
        public int TLEN { get; set; }
        public string SEQ { get; set; }
        public string QUAL { get; set; }

        public samread(string row)
        {
            if (!row.StartsWith("#"))
            {
                string[] entry = row.Split(new Char[] { '\t' });
                string QNAME = entry[0];
                int FLAG = int.Parse(entry[1]);
                string RNAME = entry[2];
                Int32 POS = Int32.Parse(entry[3]);
                int MAPQ = int.Parse(entry[4]);
                string CIGAR = entry[5];
                string RNEXT = entry[6];
                Int32 PNEXT = Int32.Parse(entry[7]);
                int TLEN = int.Parse(entry[8]);
                string SEQ = entry[9];
                string QUAL = entry[10];
            }
        }
    }
    public class samfile : List<samread>
    {
        public string path { get; set; }
        public samfile(string filename)
        {
            if (File.Exists(filename))
            {
                path = filename;
                using (StreamReader r = new StreamReader(path))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (line.Length > 3)
                        {
                            this.Add(new samread(line));
                        }
                    }
                }
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            string samfilepath = args[0];
            string regionsfile = args[1];
            Dictionary<string, Int32> fragmentcnt = new Dictionary<string, Int32>();
            Dictionary<string, Int32[]> region = new Dictionary<string, Int32[]>();
            using (StreamReader r = new StreamReader(regionsfile))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if ((line.IndexOf('#', 0) != 0) && (line.Length > 5))
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        string[] chr = entries[0].Split(new Char[] { '.' });
                        //                        Console.WriteLine(line, entries[2], entries[3]);
                        region["chr" + int.Parse(chr[0].Replace("NC_0", "")) + "p" + entries[2]] = new Int32[2];
                        region["chr" + int.Parse(chr[0].Replace("NC_0", "")) + "p" + entries[2]][0] = Int32.Parse(entries[2]);
                        region["chr" + int.Parse(chr[0].Replace("NC_0", "")) + "p" + entries[2]][1] = Int32.Parse(entries[3]);
                    }
                }
            }


            samfile SAMIN = new samfile(samfilepath);
            foreach (samread item in SAMIN)
	{
		 
	}
        }
    }
}







/*
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
                        if (!fragmentcnt.ContainsKey("chr" + entries[2] + "p" + entries[3]))
                            fragmentcnt["chr" + entries[2] + "p" + entries[3] + "-c" + entries[6] + "p" + entries[7]] = 1;
                        else
                            fragmentcnt["chr" + entries[2] + "p" + entries[3] + "-c" + entries[6] + "p" + entries[7]]++;
                    }
                }
                var list = counter.Keys.ToList();
                list.Sort();
                foreach (KeyValuePair<string, Int32> item in fragmentcnt)
                //                    foreach (KeyValuePair<string, Int32> item in counter)
                {
                    string[] pstn = item.Key.Split(new Char[] { '-' });
                    string[] poio = pstn[0].Split(new Char[] { 'p' });
                    string chr = poio[0];

                    Int32 pos = Int32.Parse(poio[1]);
                    foreach (KeyValuePair<string, Int32[]> area in region)
                    {
                        string[] posit = area.Key.Split(new Char[] { 'p' });
                        string CHR = posit[0];
                        Int32 start = area.Value[0];
                        Int32 stop = area.Value[1];
                        if ((chr == CHR) && (pos >= start) && (pos <= stop))
                        {
                            Console.WriteLine(area.Key + "[" + region[area.Key][0] + "-" + region[area.Key][1] + "]" + "\t" + item.Key + "\t" + fragmentcnt[item.Key]);
                        }
                    }
                }
                //                list = fragmentcnt.Keys.ToList();
                //list.Sort();
                //                foreach (string item in list)
                {

                }
            }
        }
    }
}