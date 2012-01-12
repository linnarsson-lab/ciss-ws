using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Linnarsson.Dna;
using System.Diagnostics;

namespace readFileContent
{

    public class Fragment
    {
        public string seqname { get; set; }
        public string source { get; set; }
        public string feature { get; set; }
        public int start { get; set; }
        public int end { get; set; }
        public string score { get; set; }
        public char strand { get; set; }
        public int frame { get; set; }
        public string attribute { get; set; }

        public Fragment(string Seqname, string Source, string Feature, int Start, int End, string Score,
                       char Strand, int Frame, string Attribute)
        {
            seqname = Seqname;
            source = Source;
            feature = Feature;
            start = Start;
            end = End;
            score = Score;
            strand = Strand;
            frame = Frame;
            attribute = Attribute;
        }
    }

    public class GFFfile : Dictionary<string, Fragment> 
    {
        public string filepath { get; set; }
        public int fragmentcount;

        public GFFfile(string Filepath)
        {
            filepath = Filepath;
            fragmentcount = 0;
            using (StreamReader r = new StreamReader(filepath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 5) continue;
                    if (line.Substring(0, 1) != "#")
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        if (entries.Length > 4)
                        {
                            Fragment temp = new Fragment(entries[0], entries[1], entries[2], Int32.Parse(entries[3]), Int32.Parse(entries[4]),
                                 entries[5], char.Parse(entries[6]), Int32.Parse(entries[7]), entries[8]);
                            this.addfragment(entries[0] + entries[3], temp);
                            fragmentcount++;
                        }
                    }
                }
            }
        }

        public void addfragment(string frag, Fragment Frg)
        {
            this.Add(frag, Frg);
            fragmentcount++;
        }

        public void removefragment(string frag)
        {
            this.Remove(frag);
            fragmentcount--;
        }

    }


    public class chromosomeparts : Dictionary<string, scanpart>
    {
        public string chromo { get; set; }
        public int partcount { get; set; }

        public chromosomeparts(string CHR)
        {
            chromo = CHR;
            partcount = 0;
        }
    }

    public class scanpart
    {
        public int beginning { get; set; }
        public int ending { get; set; }

        public scanpart(int BEG, int END)
        {
            beginning = BEG;
            ending = END;
        }

    }

    class Program
    {

        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                usage();
                return (1);
            }

            string path = args[0];
            GFFfile Design = new GFFfile("/media/ext5tb1/runs/111111_SN893_0092_BD036JACXX/Design-Report-id-488-1314174777.gff");
            if (Directory.Exists(path))
            {
                Console.WriteLine(Environment.NewLine + "\tThe '" + path + "' is set as root.");
            }
            else
            {
                Console.WriteLine(Environment.NewLine + "\tFolder '" + path + "' not found!");
                return (2);
            }

            string[] TheBams = new string[0];
            Console.WriteLine();

            try
            {
                // Only get certain subdirectories 
                string[] dirs = Directory.GetDirectories(@path, "Project_*_*");
                Console.WriteLine("\tThe number of directories starting with 'Project' is {0}.", dirs.Length);
                TheBams = new string[dirs.Length];
                int i = 0;
                foreach (string dir in dirs)
                {
                    Console.WriteLine("\t" + dir);
                    if (File.Exists(dir + "/genome/bam/sorted.bam"))
                    {
                        TheBams[i] = dir + "/genome/bam/sorted.bam";
                        i++;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }

            Console.WriteLine();
            DateTime now = DateTime.Now;
            Process proc = Process.GetCurrentProcess();
            Console.WriteLine("\tStarting " + now + " memory allocated " + proc.PrivateMemorySize64);
            Console.WriteLine();

  
/*            List<string> chromos = new List<string>();
            foreach (KeyValuePair<string, Fragment> kvm in Design)
            {
                if (chromos.IndexOf(kvm.Value.seqname) == -1)
                {
                    chromos.Add(kvm.Value.seqname);
                chromosomeparts temp = new chromosomeparts();
                Console.WriteLine("{0}, {1}, {2}, {3}", kvm.Key, kvm.Value.seqname, kvm.Value.start, kvm.Value.end);

            }
*/

            foreach (string bFile in TheBams)
            {
                Console.WriteLine("\t" + bFile);
                BamFile BF = new BamFile(bFile);
                Console.WriteLine(bFile);
                for (int i = 0; i < BF.Chromosomes.Length; i++)
                {
                    Dictionary<int, int> onposcounter = new Dictionary<int, int>();
                    Console.WriteLine(BF.Chromosomes[i] + " - " + BF.ChromosomeLengths[i]);
                    foreach (KeyValuePair<string, Fragment> KVP in Design)
                    {
                        string[] CHbam = BF.Chromosomes[i].Split('.');
                        string[] CHgff = KVP.Value.seqname.Split('r');
                        if (CHbam[0] != CHgff[1]) continue;
                        int strt = KVP.Value.start - 250;
                        if (strt < 1)
                            strt = 1;
                        int ende = KVP.Value.end + 250;
                        if (ende > BF.ChromosomeLengths[i])
                            ende = BF.ChromosomeLengths[i];
                        List<BamAlignedRead> MyList = BF.Fetch(BF.Chromosomes[i], strt, ende);
                        Console.WriteLine("\t\t" + BF.Chromosomes[i] + " - " + BF.ChromosomeLengths[i] + " - " + MyList.Count);
                        if (!(MyList.Count < 1))
                        {
                            for (int ii = 0; ii < MyList.Count; ii++)
                            {
                                if (onposcounter.ContainsKey(MyList[ii].Position))
                                    onposcounter[MyList[ii].Position]++;
                                else
                                    onposcounter.Add(MyList[ii].Position, 1);
                                //                  Console.WriteLine("{0}, {1}, {2}, {3}, {4}, {5}", i, BF.Chromosomes[i], , MyList[ii].MappingQuality, 
                                //                                                     MyList[ii].Strand, MyList[ii].IsMatePair, MyList[ii].MateStrand);

                            }
                        }

                        foreach (KeyValuePair<int, int> kvp in onposcounter)
                            Console.WriteLine("{0}, {1,10:G}, {2,10:G}", BF.Chromosomes[i], kvp.Key, kvp.Value);
                        onposcounter.Clear();
                        MyList.Clear();
                    }
                }

                Console.WriteLine();
            }


            Console.WriteLine();
            return (0);
        }

        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\treadFileContent Usage:");
            Console.WriteLine("\treadFileContent <path-to-variantDectionDir>" + Environment.NewLine);
        }
    }
}
