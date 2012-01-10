using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace basecomp
{
    public class CompProject : RefProject
    {
        public CompProject(string PROJDIR)
            : base(PROJDIR)
        {
        }
    }
    
    public class RefProject : Dictionary<string, Position>
    {
        public string projectName { get; set; }
        public string projectDir { get; set; }
        public long totalbcalls { get; set; }
        public int basecounts;
        public int homozygotes;

        public RefProject(string PROJDIR)
            : base()
        {
            projectDir = PROJDIR;
            totalbcalls = 0;
            basecounts = 0;
            homozygotes = 0;
        }

        public void addbase(string snp, Position POS)
        {
            this.Add(snp, POS);
            if (POS.homo)
                homozygotes++;
            basecounts++;
        }

        public void removebase(string snp)
        {
            this.Remove(snp);
            basecounts--;
        }

        public int countComp(double min, double max)
        {
            int ct = this.Count(p => (p.Value.comp <= max && p.Value.comp >= min));
            return (ct);
        }

    }
    
    public class Position
    {
        public string seq_name { get; set; }
        public int pos { get; set; }
        public int bcalls_used { get; set; }
        public int bcalls_filt { get; set; }
        public char reference { get; set; }
        public int Qsnp { get; set; }
        public string max_gt { get; set; }
        public int Qmax_gt { get; set; }
        public string poly_site { get; set; }
        public int Qpoly_site { get; set; }
        public int A_used { get; set; }
        public int C_used { get; set; }
        public int G_used { get; set; }
        public int T_used { get; set; }
        public int highcount { get; set; }
        public int nextcount { get; set; }
        public double relhigh { get; set; }
        public double relnext { get; set; }
        public int[] rds { get; set; }
        public double comp { get; set; }
        public bool homo { get; set; }
        public double[] relreadcount { get; set; }

        public Position(string Seq_name, int Pos, int Bcalls_used, int Bcalls_filt, char Reference, int QSnp, string Max_gt,
                      int QMax_gt, string Poly_site, int QPoly_site, int A_Used, int C_Used, int G_Used, int T_Used, int QSCORE)
        {
            seq_name = Seq_name;
            pos = Pos;
            bcalls_used = Bcalls_used;
            bcalls_filt = Bcalls_filt;
            reference = Reference;
            Qsnp = QSnp;
            max_gt = Max_gt;
            Qmax_gt = QMax_gt;
            poly_site = Poly_site;
            Qpoly_site = QPoly_site;
            A_used = A_Used;
            C_used = C_Used;
            G_used = G_Used;
            T_used = T_Used;
            int[] rds = new int[4] { A_used, C_used, G_used, T_used };
            Array.Sort(rds);
            nextcount = rds[2];
            highcount = rds.Max();
            relhigh = (double)highcount / ((double)A_used + (double)C_used + (double)G_used + (double)T_used);
            relnext = (double)nextcount / ((double)A_used + (double)C_used + (double)G_used + (double)T_used);
            relreadcount = new double[4];
            relreadcount[0] = (double)rds[0] / ((double)bcalls_used + 0.0001);
            relreadcount[1] = (double)rds[1] / ((double)bcalls_used + 0.0001);
            relreadcount[2] = (double)rds[2] / ((double)bcalls_used + 0.0001);
            relreadcount[3] = (double)rds[3] / ((double)bcalls_used + 0.0001);
            comp = -1;
            homo = poly_site.Substring(0, 1).Equals(poly_site.Substring(1, 1)) && Qpoly_site > QSCORE;
        }


    }


    class Program
    {
        static int Main(string[] args)

        {
            List<RefProject> projectsL = new List<RefProject>();
            List<string> positionsToUse = new List<string>();
            int qscore = 200;

            if (args.Length != 1)
            {
                usage();
                return (1);
            }

            if (File.Exists(args[0]))
            {
                Console.WriteLine(Environment.NewLine + "\tReading configuration from '" + args[0] + "'.");
            }
            else
            {
                Console.WriteLine(Environment.NewLine + "\tFile '" + args[0] + "' not found!");
                return (2);
            }

            using (StreamReader r = new StreamReader(args[0]))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Substring(0, 1) != "#")
                    {
//                        Console.WriteLine(line);
                        string[] param = line.Split(new Char[] { '=' });
                        switch (param[0])
                        {
                            case "REF":
                                RefProject temp = new RefProject(param[1]);
                                projectsL.Add(temp);
                                break;
                            case "COMP":
                                RefProject tmp = new RefProject(param[1]);
                                projectsL.Add(tmp);
                                break;
                            case "POS":
                                positionsToUse.Add(param[1]);
                                break;
                            case "QSCORE":
                                qscore = int.Parse(param[1]);
                                break;
                            default:
                                break;
                        }

                    }
                }
            }
            RefProject[] projectsA = projectsL.ToArray();
            Dictionary<string, int> allsites = new Dictionary<string, int>();
            foreach (RefProject proj in projectsA)
            {
                List<string> sitefiles = new List<string>();
                try
                {
                    // Only get certain subdirectories 
                    string dir = proj.projectDir;
                    string[] pn = dir.Split(new Char[] { '/' });

                    Console.Write("\tReading in {0} ", pn[pn.Length - 2], pn.Length);
                    proj.projectName = pn[pn.Length - 1];
                    string[] pars = Directory.GetDirectories(@dir, "Parsed*");
                    foreach (string par in pars)
                    {
                        string[] chrs = Directory.GetDirectories(@par, "*.fa");
                        foreach (string chr in chrs)
                        {
                            string[] zeros = Directory.GetDirectories(@chr, "00*");
                            foreach (string zero in zeros)
                            {
                                string[] files = Directory.GetFiles(@zero, "sites.txt");
                                foreach (string file in files)
                                {
                                    sitefiles.Add(file);
                                }
                            }

                        }

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("The process failed: {0}", e.ToString());
                }
                foreach (string element in sitefiles)
                {
                    using (StreamReader r = new StreamReader(element))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            if (line.Substring(0, 1) != "#")
                            {
                                string[] entries = line.Split(new Char[] { '\t', ' ' });
                                Position temp = new Position(entries[0], Int32.Parse(entries[1]), Int32.Parse(entries[2]), Int32.Parse(entries[3]),
                                              Char.Parse(entries[4]), Int32.Parse(entries[5]), entries[6], Int32.Parse(entries[7]), entries[8],
                                              Int32.Parse(entries[9]), Int32.Parse(entries[10]), Int32.Parse(entries[11]), Int32.Parse(entries[12]),
                                              Int32.Parse(entries[13]), qscore);
                                proj.addbase(entries[0] + entries[1], temp);
                                int slsk = 0;
                                if (!allsites.TryGetValue(entries[0] + entries[1], out slsk))
                                    allsites.Add(entries[0] + entries[1], 1);
                                else
                                    allsites[entries[0] + entries[1]]++;
                            }
                        }
                    }
                    Console.Write(".");
                }
                Console.WriteLine();
            }

            int[] withSite = new int[projectsA.Length];
            int allAgain = 0;
            Console.WriteLine();
            int cnt = 0;
            foreach (var item in withSite)
            {
                cnt++;
                int Counting = allsites.Count(p => p.Value == cnt);
                allAgain += Counting;
                if (cnt < 2)
                {
                    Console.WriteLine("\tThere are {0,10:G} Sites present in only one Project", Counting);
                }
                else
                {
                    Console.WriteLine("\tThere are {0,10:G} Sites present in exactly {1,2:G} Projects", Counting, cnt);
                }
            }
            Console.WriteLine("\tThere are {0,10:G} different Sites present in all Projects" + Environment.NewLine, allAgain);

            foreach (KeyValuePair<string, int> site in allsites)
                if (site.Value == projectsA.Length)
                {
                    if (projectsA[0][site.Key].homo)
                    for (int i = 1; i < projectsA.Length; i++)
                    {
                        if (projectsA[0][site.Key].poly_site.Equals(projectsA[i][site.Key].poly_site))
                            projectsA[i][site.Key].comp = (double)projectsA[i][site.Key].Qpoly_site / (double)projectsA[0][site.Key].Qpoly_site;
                        else
                            projectsA[i][site.Key].comp = 0.0;
//                        Console.Write(projectsA[i][site.Key].comp + " " + projectsA[0][site.Key].Qpoly_site + " ");
                    }
                }

            Console.Write("\tCounts based on relative Qscore" + Environment.NewLine +"\tc|h|l");
            for (int i = 0; i < projectsA.Length; i++)
                Console.Write("\t" + i);
            Console.Write(Environment.NewLine + "\t>1.01\t" + projectsA[0].Count(p => (p.Value.homo == true)));
            // count number of bases in 'comp' that have the same homozygote bases as 'ref' at this level [=compQpolyscore/refQpolyscore]
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].countComp(1.01, 99999999));
            }
            Console.Write(Environment.NewLine + "\t1\t" + projectsA[0].Count(p => (p.Value.homo == true)));
            // count number of bases in 'comp' that have the same homozygote bases as 'ref' at this level [=compQpolyscore/refQpolyscore]
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].countComp(0.99, 1.01));
            }
            Console.Write(Environment.NewLine + "\t.9-.99\t0");// + projectsA[0].Count(p => (p.Value.homo == true)));
            // Count high scoring positions
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].countComp(0.9, 0.99));
            }
            Console.Write(Environment.NewLine + "\t001-01\t0");// + projectsA[0].Count(p => (p.Value.homo == true)));
            // Count low scoring positions
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].countComp(0.001, 0.1));
            }
            Console.Write(Environment.NewLine + "\tother\t0");//  + projectsA[0].Count(p => (p.Value.homo == true)));
            // Count positions with diferent base count
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].countComp(-0.01, 0.01));
            }
            Console.Write(Environment.NewLine + "\tHiN<90%\t" + projectsA[0].Count(p => (p.Value.relhigh < .9)));
            // Count of positions where the highest base has at least 90% of the reads 
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].Count(p => (p.Value.relhigh < .9)));
            }
            Console.Write(Environment.NewLine + "\t2dN>20%\t" + projectsA[0].Count(p => (p.Value.relnext > .2)));
            // Count of positions where the second highest base has at least 90% of the reads 
            for (int i = 1; i < projectsA.Length; i++)
            {
                Console.Write("\t" + projectsA[i].Count(p => (p.Value.relnext > .2)));
            }
            Console.WriteLine(Environment.NewLine);

            // print used bases to file
            StreamWriter writeTo = new StreamWriter("output.txt");
            // print used bases to file
            StreamWriter write2dTop = new StreamWriter("output2dTop.txt");

            // Take out the interesting positions
            Console.Write("\tQscore at the suggested positions" + Environment.NewLine + "\tposition");
            for (int i = 0; i < projectsA.Length; i++)
            {
                Console.Write("\t" + i);
                write2dTop.Write("\t" + i);
            }
            Console.WriteLine();
            write2dTop.WriteLine();
            foreach (string pos in positionsToUse)
            {
                Console.Write("\t" + pos);
                Boolean toout = false;
                writeTo.WriteLine(pos);
                for (int i = 0; i < projectsA.Length; i++)
                {
                    Console.Write("\t{0} {1}", projectsA[i][pos].poly_site, projectsA[i][pos].Qpoly_site);
                    writeTo.Write("\t" + projectsA[i][pos].A_used);
                    writeTo.Write("\t" + projectsA[i][pos].C_used);
                    writeTo.Write("\t" + projectsA[i][pos].G_used);
                    writeTo.Write("\t" + projectsA[i][pos].T_used);
                    writeTo.WriteLine("\t{0}\t{1}", projectsA[i][pos].A_used + projectsA[i][pos].C_used + projectsA[i][pos].G_used + projectsA[i][pos].T_used,
                        projectsA[i].basecounts);
                    if (projectsA[i][pos].relreadcount[2] > .2)
                        toout = true;
                }
                Console.WriteLine();
                if ((projectsA[0][pos].relreadcount[2] < .1 || projectsA[0][pos].relreadcount[2] > .3) && toout)
                {
                    write2dTop.Write(pos);
                    for (int i = 0; i < projectsA.Length; i++)
                    {
                        write2dTop.Write("\t" + projectsA[i][pos].relreadcount[2].ToString());
                    }
                    write2dTop.WriteLine();
                }
            }
            writeTo.Close();
            write2dTop.Close();
            Console.Write("\tTotalReads");
            for (int i = 0; i < projectsA.Length; i++)
                {
                    Console.Write("\t{0}", projectsA[i].basecounts/100);
                }
            Console.WriteLine();

            // print used bases to file
            StreamWriter writeall = new StreamWriter("outputall.txt");
            foreach (KeyValuePair<string, int> kvp in allsites)
            {
                if (kvp.Value == 6)
                {
                    bool toout = false;
                    bool allbelow = true;
                    bool counthigh = true;
                    for (int i = 1; i < projectsA.Length; i++)
                    {
                        if (projectsA[i][kvp.Key].relreadcount[2] > .2)
                            allbelow = false;
                        if (projectsA[i][kvp.Key].bcalls_used < 1000)
                            counthigh = false;

                    }
                    if ((projectsA[0][kvp.Key].relreadcount[2] < .1 || projectsA[0][kvp.Key].relreadcount[2] > .35) && !(allbelow) && counthigh)
                        toout = true;
                    if (toout)
                    {
                        writeall.Write(kvp.Key);
                        for (int ii = 0; ii < projectsA.Length; ii++)
                        {
                            writeall.Write("\t" + projectsA[ii][kvp.Key].relreadcount[2].ToString());
                        }
                        for (int ii = 0; ii < projectsA.Length; ii++)
                        {
                            writeall.Write("\t" + projectsA[ii][kvp.Key].bcalls_used);
                        }
                        writeall.WriteLine();
                    }
                }

            }
            writeall.Close();

            Console.WriteLine();
            return (0);
        }



        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\tUsage:\tmono basecomp <config.txt>");
            Console.WriteLine("\tThe Config file should contain the following parameters: e.g." + Environment.NewLine);
            Console.WriteLine("\tREF=/media/ext5tb1/runs/111111_SN893_0092_BD036JACXX/Aligned.new.20111208/variantDetection/Project_A_100pCCRFCEM1M/");
            Console.WriteLine("\tCOMP=/media/ext5tb1/runs/111111_SN893_0092_BD036JACXX/Aligned.new.20111208/variantDetection/Project_B_20pCCRFCEM1M/");
            Console.WriteLine("\tCOMP=/media/ext5tb1/runs/111111_SN893_0092_BD036JACXX/Aligned.new.20111208/variantDetection/Project_C_10pCCRFCEM1M/");
            Console.WriteLine("\tQSCORE\te.g. QSCORE=200" + Environment.NewLine);
        }

    }
}
