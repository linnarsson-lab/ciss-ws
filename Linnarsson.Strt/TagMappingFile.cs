using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class TagMappingFile
    {
        /// <summary>
        /// Create a file where each line contains the alternative mappings that have the same
        /// sequence in genome when considering any allowed mismatches in bowtie
        /// </summary>
        /// <param name="mapFile"></param>
        /// <param name="tagMappingFile"></param>
        public static void TranslateMapFile(string mapFile, string tagMappingFile)
        {
            HashSet<string> usedFirstGroups = new HashSet<string>();
            string line;
            string lastReadId = null;
            int n = 0, dupl = 0;
            List<string> mapGroup = new List<string>();
            Console.WriteLine("Translating from " + mapFile + " to " + tagMappingFile);
            StreamWriter writer = new StreamWriter(tagMappingFile);
            writer.WriteLine("#Each line represents a multiread mapping: A set of identical seq stretches in the genome under given mapping settings.");
            writer.WriteLine("#Each mapping is: chromosome, strand, start position (5') on chromosome");
            using (StreamReader reader = new StreamReader(mapFile))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (++n % 10000000 == 0)
                        Console.Write(n + "..");
                    string[] fields = line.Split('\t');
                    string readId = fields[0];
                    if (readId != lastReadId)
                    {
                        if (mapGroup.Count > 1)
                        {
                            mapGroup.Sort();
                            if (usedFirstGroups.Contains(mapGroup[0]))
                            {
                                if (++dupl < 100)
                                    Console.WriteLine("Duplicate:" + mapGroup[0]);
                            }
                            else
                            {
                                usedFirstGroups.Add(mapGroup[0]);
                                writer.WriteLine(string.Join("\t", mapGroup.ToArray()));
                            }
                        }
                        mapGroup.Clear();
                        lastReadId = readId;
                    }
                    char strand = fields[1][0];
                    string hitChr = fields[2].Replace("chr", "");
                    string hitPos = fields[3];
                    mapGroup.Add(string.Format("{0},{1},{2}", hitChr, strand, hitPos));
                }
                if (mapGroup.Count > 1)
                {
                    mapGroup.Sort();
                    if (!usedFirstGroups.Contains(mapGroup[0]))
                        writer.WriteLine(string.Join("\t", mapGroup.ToArray()));
                }
                Console.WriteLine(dupl + " duplicated entries were replaced with one each.");
            }
        }

        public static void ReadMappingsFromFile(string tagMappingFile, Dictionary<string, ChrTagData> chrTagDatas)
        {
            Console.WriteLine("Reading pre-calculated exonic multiread mappings from " + tagMappingFile);
            int n = 0;
            using (StreamReader reader = new StreamReader(tagMappingFile))
            {
                string line = reader.ReadLine();
                while (line.StartsWith("#")) line = reader.ReadLine();
                while (line != null)
                {
                    if (++n % 1000000 == 0) Console.WriteLine(n + "...");
                    TagItem tagItem = new TagItem(true);
                    string[] groups = line.Split('\t');
                    foreach (string group in groups)
                    {
                        string[] parts = group.Split(',');
                        chrTagDatas[parts[0]].Setup(int.Parse(parts[2]), parts[1][0], tagItem);
                    }
                    line = reader.ReadLine();
                }
            }
        }

    }
}
