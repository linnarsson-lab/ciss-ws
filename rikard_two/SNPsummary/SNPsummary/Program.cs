using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace SNPsummary
{
    public class sampleentry
    {
        public string Cons { get; set; }
        public Int32 Cov { get; set; }
        public Int32 Reads1 { get; set; }
        public Int32 Reads2 { get; set; }
        public double MAF { get; set; }
        public double PValue { get; set; }
        public sampleentry(string row)
        {

            row = row.Replace("E-", "BB");
            row = row.Replace("-", "0");
            row = row.Replace("BB", "E-");
            string[] entry = row.Split(new Char[] { ':' });
            Console.WriteLine(row);

            Cons = entry[0];
            Cov = Int32.Parse(entry[1]);
            Reads1 = Int32.Parse(entry[2]);
            Reads2 = Int32.Parse(entry[3]);
//            Console.WriteLine(Reads2);
            MAF = double.Parse(entry[4].Replace("%",""));
            PValue = double.Parse(entry[5]);
//            Console.WriteLine(PValue);
        }
    }
    public class sample : Dictionary<string, sampleentry>
    {
        public string position { get; set; }
        public sampleentry smpl { get; set; }
        public Int32 poscount { get; set; }

        public sample()
        {
            poscount = 0;
        }
        
        public void addsample(string chrpos, string smpl)
        {
            sampleentry SAMP = new sampleentry(smpl);
            this.Add(chrpos, SAMP);
            poscount++;
        }
        public void removesample(string chrpos)
        {
            this.Remove(chrpos);
            poscount--;
        }

    }

    public class varscan
    {
        public string chr { get; set; }
        public Int32 pos { get; set; }
        public char refb { get; set; }
        public string var { get; set; }
        public string poolcall { get; set; }
        public string strandfilt { get; set; }
        public int r1plus { get; set; }
        public int r1minus { get; set; }
        public int r2plus { get; set; }
        public int r2minus { get; set; }
        public string pval { get; set; }
        public int samplecount { get; set; }
        public varscan(string row)
        {
            string[] entry = row.Split(new Char[] { '\t' });
            chr = entry[0];
            pos = Int32.Parse(entry[1]);
            refb = char.Parse(entry[2]);
            var = entry[3];
            poolcall = entry[4];
            strandfilt = entry[5];
            r1plus = int.Parse(entry[6]);
            r1minus = int.Parse(entry[7]);
            r2plus = int.Parse(entry[8]);
            r2minus = int.Parse(entry[9]);
            pval = entry[10];
        }
    }

    public class varscanfile : Dictionary<string, varscan>
    {
        public string filepath { get; set; }
        public int samplecount { get; set; }
        public int poscount;

        public varscanfile(string Filepath, sample[] SMP)
        {
            filepath = Filepath;
            poscount = 0;
            using (StreamReader r = new StreamReader(filepath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 5) continue;
                    if (line.Substring(0, 1) != "C")
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        if (entries.Length > 10)
                        {
                            varscan temp = new varscan(line);
                            string[] sampl = temp.pval.Split(new Char[] { ' ' });
                            string name = "c" + temp.chr + "m" + temp.pos;
                            samplecount = sampl.Length;
        //                    name + " " + sampl.Length);
                            if (sampl.Length != SMP.Length) continue;
                            this.addpos(name, temp);
                            Console.WriteLine(name);
                            for (int i = 0; i < samplecount; i++)
                            {
                                Console.WriteLine(i + sampl[i] + name); 
                                {
                                    if (SMP[i] == null)
                                    {
                                        sample MAS = new sample();//    SMP[i];
                                        // sampleentry hej = sampl[i];
                                        MAS.addsample(name, sampl[i]);
                                        SMP[i] = MAS;
                                    }
                                    else
                                    {
                                        SMP[i].addsample(name, sampl[i]);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        public void addpos(string chrpos, varscan posread)
        {
            this.Add(chrpos, posread);
            poscount++;
        }
        public void removepos(string chrpos)
        {
            this.Remove(chrpos);
            poscount--;
        }
    }


    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine(Process.GetCurrentProcess().ProcessName);
                usage();
                return (1);
            }

            sample[] SMP = new sample[1];
            varscanfile VSFILE = new varscanfile(args[0], SMP);
            sample[] Samples = new sample[VSFILE.samplecount];
            varscanfile VarScan = new varscanfile(args[0], Samples);

            printallpos(VarScan, Samples);

            Console.WriteLine(Environment.NewLine + VarScan.samplecount);

            return (0);
        }

        static void usage()
        {
//            Console.WriteLine(Environment.NewLine + "\t" + Process.GetCurrentProcess().ProcessName);
            Console.WriteLine(Environment.NewLine + "\t" + System.AppDomain.CurrentDomain.FriendlyName);
        }

        static int printallpos(varscanfile varpos, sample[] allsamples)
        {
            foreach (KeyValuePair<string CPS, sampleentry > in varpos)
            {
                Console.WriteLine(allsamples[3][CPS][]);

            }

            return 0;
        }

    }
}
