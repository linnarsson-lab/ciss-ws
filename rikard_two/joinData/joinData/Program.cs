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
            string Data = args[0];

            Dictionary<string, string> position = new Dictionary<string, string>();
            Dictionary<string, string> symbol = new Dictionary<string, string>();
            using (StreamReader r = new StreamReader(Data))
            {
                string line;
                 
                while ((line = r.ReadLine()) != null)
                {
                    if (!line.Contains("Uploaded Variation") && !line.Contains("#"))
                    {
                        string[] column = line.Split('\t');
                        if (!position.ContainsKey(column[1]))
                        {
                            position[column[1]] = line.Replace("\t", "_");
                            symbol[column[1]] = "";
                        }
                        else
                        {
                            position[column[1]] += "+" + line.Replace("\t", "_");
                        }
                        if (column.Last().Contains("HGNC"))
                        {
                            string[] name = column.Last().Split(new Char[] { ';' });
                            if (!symbol.ContainsKey(column[1]))
                                symbol[column[1]] = "_";
                            foreach (string hgnc in name)
                            {
                                if (hgnc.Contains("HGNC"))
                                {
                                    if (!symbol[column[1]].Contains(hgnc.Replace("HGNC=", "")))
                                        symbol[column[1]] += "+" + hgnc.Replace("HGNC=", "");
                                }
                            }
                        }
                    }
                }
            }
            StreamWriter sw;
            sw = File.CreateText(Data + ".joined");
            foreach (KeyValuePair<string, string> item in position)
            {
                sw.WriteLine(item.Key + "\t" + symbol[item.Key] + "\t" + item.Value);
            }
            sw.Close();
        }
    }
}