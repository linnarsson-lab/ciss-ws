using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace PickSnpFromVarscan
{

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

        public varscan(string row)
            
//            Chr, Int32 Pos, char Refb, char Var, string Poolcall, string Strandfilt,
//                       int R1plus, int R1minus, int R2plus, int R2minus, double Pval)
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
        public int poscount;

        public varscanfile(string Filepath)
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
                            this.addpos(entries[0].Replace("chr", "") + '.' + entries[1], temp);
                            poscount++;
                        }
                    }
                }
            }
        }

        public void addpos(string chrpos, varscan posread)
        {
            this.Add(chrpos, posread);
            Console.WriteLine(Environment.NewLine + "\t" + posread.chr + " " + posread.pos + Environment.NewLine);
            poscount++;
        }

        public void removepos(string chrpos)
        {
            this.Remove(chrpos);
            poscount--;
        }

    }

    public class Fragment
    {
        public string seqname { get; set; }
        public string source { get; set; }
        public string feature { get; set; }
        public Int32 start { get; set; }
        public Int32 end { get; set; }
        public string score { get; set; }
        public string strand { get; set; }
//        public int frame { get; set; }
//        public string attribute { get; set; }

        public Fragment(string Seqname, string Source, string Feature, Int32 Start, Int32 End, string Score, string Strand)
        {
            seqname = Seqname;
            source = Source;
            feature = Feature;
            start = Start;
            end = End;
            score = Score;
            strand = Strand;
//            frame = Frame;
//            attribute = Attribute;
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
                                 entries[5], entries[6]);
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

    public class dbsnp
    {
        public int chr { get; set; }
        public Int32 pos { get; set; }
        public string gene { get; set; }

        public dbsnp(string Gene, int Chr, Int32 Pos)
        {
            chr = Chr;
            pos = Pos;
            gene = Gene;
        }
    }
    public class dbsnpfile : Dictionary<string, dbsnp>
    {
        public string filepath { get; set; }
        public int dbsnpcount;

        public dbsnpfile(string Filepath)
        {
            filepath = Filepath;
            dbsnpcount = 0;
            using (StreamReader r = new StreamReader(filepath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 6) continue;
                    if (line.Substring(0, 1) != "#")
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        if ((entries.Length > 2) && (!this.ContainsKey(entries[1] + '.' + entries[2])))
                        {
                            dbsnp temp = new dbsnp(entries[0], int.Parse(entries[1]), Int32.Parse(entries[2]));
                            this.adddbsnp(entries[1] + '.' + entries[2], temp);
                            dbsnpcount++;
                        }
                    }
                }
            }
        }

        public void adddbsnp(string snppos, dbsnp SNP)
        {
            this.Add(snppos, SNP);
            dbsnpcount++;
        }

        public void removedbsnp(string snppos)
        {
            this.Remove(snppos);
            dbsnpcount--;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 5)
            {
                usage();
                return (1);
            }



            dbsnpfile DB = new dbsnpfile(args[1]);
//            Console.WriteLine("\tFile: '" + args[1] + "' scanned for DBSNPs: " + DB.dbsnpcount + " found.");
            string ToOut = "beta"; 
//          ToOut - 'main', 'method' or 'beta'
            if (ToOut.Equals("beta"))
            {
                Console.Write("Beta_1\tBeta_2\tBeta_3\tBeta_4\tBeta_5\tBeta_6\tBeta_7\tBeta_8\tBeta_9\tBeta_10\tBeta_11\tBeta_12\tBeta_15\tBeta_17\tBeta_18\tBeta_19\tBeta_20\tBeta_21\tBeta_22\tBeta_24\tBeta_25\tBeta_26\tBeta_27\t");
                // for beta
            }
            if (ToOut.Equals("method"))
            {
                Console.Write("Method_1\tMethod_2\tMethod_3\tMethod_4\tMethod_5\tMethod_6\tMethod_7\tMethod_8\tMethod_9\tMethod_10\tMethod_11\tMethod_12\tMethod_13\tMethod_14\tMethod_15\tMethod_16\tMethod_17\tMethod_18\tMethod_19\tMethod_20\tMethod_21\tMethod_22\tMethod_23\tMethod_24\t");
                // for method
            }
            if (ToOut.Equals("main"))
            {
                Console.Write("1\t2\t3\t4\t5\t6\t7\t8\t9\t10\t11\t12\t13\t14\t15\t16\t17\t18\t19\t20\t21\t22\t23\t24\t25\t26\t27\t28\t29\t");
                // for main
            }
            Console.Write("Chrom\tPosition\tRef\tVar\tCons:Cov:Reads1:Reads2:Freq:P-value\tStrandFilter:R1+:R1-:R2+:R2-:pval\tSamplesRef\t");
            Console.WriteLine("SamplesHet\tSamplesHom\tSamplesNC\toutfracfalse\toutfractrue5\toutfractrue1\tdbsnpgene\troigene");
            //            Console.WriteLine(Environment.NewLine + DB.dbsnpcount + Environment.NewLine);
//            varscanfile VS = new varscanfile(args[0]);
            double detectionlimit = 5;
            int minsample = int.Parse(args[2]);
            int mintotal = int.Parse(args[3]);
            GFFfile ROI = new GFFfile(args[4]);
            int cnt = readvarscan(args[0], DB, minsample, mintotal, ROI, detectionlimit);
//            Console.WriteLine(cnt);

            return (0);
        }


        static int readvarscan(string Filepath, dbsnpfile DBSNP, int ms, int ts, GFFfile roi, double dl)
        {
            int poscount = 0;
            string outsnps = "";

            using (StreamReader r = new StreamReader(Filepath))
            {
                string line;
//                int[] tokeep = new int[] { 10, 13, 15, 17, 18, 22, 23, 24, 25, 26 };
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 5) continue;
                    if (line.Substring(0, 1) != "C")
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        if (entries.Length > 10)
                        {
                   //         Console.WriteLine(entries.Length + line);
                            string lineout = "";
                            int outfalse = 0;
                            int outfracfalse = 0;
                            int outfractrue1 = 0;
                            int outfractrue5 = 0;
                            int minfreqcount = 0;
                            string[] sample = entries[10].Split(new Char[] { ' ' });
                            int counter = 1;
                            foreach (string item in sample)
                            {
                                string[] samplevars = item.Split(new Char[] { ':' });
                                if (samplevars[4] == "-")                            // picks out %
                                {
                                    samplevars[4] = "0";
                                }


//                              lineout = lineout + samplevars[4].Replace("%", "") + "\t";
                                // for percentage out
//                                lineout = lineout + samplevars[2] + " " + samplevars[3] + "\t";

                                // for absolute count 'ref var' output complete, BUT with '-' converted to '0'
                                item.Replace("-", "0");
                                lineout = lineout + item + "\t";

                                // for all count values out
                       

                                if (double.Parse(samplevars[1]) < ms - 1)
                                {
                                    outfalse++;
                                }
                                if (double.Parse(samplevars[1]) < dl)
                                {
                                    outfracfalse++;
                                }
                                else
                                {
                                    outfractrue5++;
                                }
                                if (double.Parse(samplevars[1]) < 1)    // to count all below 1%
                                {
                                }
                                else
                                {
                                    outfractrue1++;
                                }

/*                                if (!(tokeep.Contains(counter)))    // count only tumors, not nomrals
                                {

                                    if (!(double.Parse(samplevars[4].Replace("%", "")) < dl))
                                    {
                                        minfreqcount++;             // add to count tumors with variant allele, we did not want too many
                                    }
                                }
                                */
                                counter++;
                                //         Console.Write(samplevars[0] + " " + samplevars[4] + "\t");
                       //         C:586:586:0:0%:1E0 C:1085:1085:0:0%:1E0 C:1336:1336:0:0%:1E0 
                            }
                            if (minfreqcount > 3) continue;
                       //     Console.WriteLine();
                            lineout = lineout + entries[0] + "\t" + entries[1] + "\t" + entries[2] + "\t" + entries[3] + "\t" + entries[4] + "\t" + entries[5];
                            lineout = lineout  + "\t"+ entries[6] + "\t" + entries[7] + "\t" + entries[8] + "\t" + entries[9];
                            //     Console.Write(entries[0] + "\t" + entries[1] + "\t" + entries[2] + "\t" + entries[3] + "\t" + entries[4] + "\t" + entries[5]);
                            //     Console.Write(entries[6] + "\t" + entries[7] + "\t" + entries[8] + "\t" + entries[9]);
                            outsnps = outsnps + entries[0].Replace("chr", "") + "\t" + entries[1] + "\t" + entries[1] + "\t" + entries[2] + "/" + entries[3] + "\t+\n";
                            string[] tottest = entries[4].Split(new Char[] { ':' });
                            if (int.Parse(tottest[1]) < ts - 1) continue;

                            lineout = lineout + "\t" + outfracfalse + "\t" + outfractrue5 + "\t" + outfractrue1;
                            if (DBSNP.ContainsKey(entries[0].Replace("chr", "") + '.' + entries[1]))
                            {
                                lineout = lineout + "\t" + DBSNP[entries[0].Replace("chr", "") + '.' + entries[1]].gene;
                        //        Console.WriteLine("\t" + DBSNP[entries[0].Replace("chr", "") + '.' + entries[1]].gene);
                            }
                            else
                            {
                                lineout = lineout + "\t" + "not-in-dbsnp";
                        //        Console.WriteLine("\t" + "not-in-dbsnp");
                            }
                            lineout = lineout + "\t" + genefromgff(entries[0], Int32.Parse(entries[1]), roi);
//                            if (outfalse != 29)
//                            {                                               Take out all positions, the ones removed are so few anyway, simplifies cross-project comparison
                                poscount++;
                                Console.WriteLine(lineout);
//                            }
                            
                        }
                    }
                }
            }
            System.IO.File.WriteAllText(@"WriteText.txt", outsnps);
            return poscount;
        }

        static string genefromgff(string chr, Int32 pos, GFFfile roi)
        {
            string gene = "not-in-roi";
            foreach (KeyValuePair<string,Fragment> kvp in roi)
            {
                if ((chr == kvp.Value.seqname) && (pos > kvp.Value.start - 1) && (pos < kvp.Value.end + 1))
                    gene = kvp.Value.source;
            }
            
            return gene;
        }

        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\tPickSnpFromVarscan Usage:");
            Console.WriteLine("\tPickSnpFromVarscan <varscanoutput> <dbsnpoutput> <int min-readcount-sample> <int min-readcount-total> <design.gff>" + Environment.NewLine);
        }
    }
}
