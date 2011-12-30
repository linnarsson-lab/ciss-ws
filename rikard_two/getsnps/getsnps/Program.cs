using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace getsnps
{
    public class Project
    {
        public Dictionary<string, Position> PROJ { get; set; }
        public string projectName { get; set; }
        public int basecounts;

        public Project(string PROJNAME)
        {
            projectName = PROJNAME;
            basecounts = 0;
        }

        public void addbase(string snp, Position POS)
        {
            PROJ.Add(snp, POS);
            basecounts++;
        }

        public void removebase(string snp)
        {
            PROJ.Remove(snp);
            basecounts--;
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

        public Position(string Seq_name, int Pos, int Bcalls_used, int Bcalls_filt, char Reference, int QSnp, string Max_gt,
                      int QMax_gt, string Poly_site, int QPoly_site, int A_Used, int C_Used, int G_Used, int T_Used)
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
        }

    }
    
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                usage();
                return(1);
            }

            string path = args[0];

            if (Directory.Exists(path))
            {
                Console.WriteLine(Environment.NewLine + "\tThe '" + path + "' is set as root.");
            }
            else
            {
                Console.WriteLine(Environment.NewLine + "\tFolder '" + path + "' not found!");
                return(2);
            }


            List<string> allsitefiles = new List<string>();

            Console.WriteLine();

            try
            {
                // Only get certain subdirectories 
                string[] dirs = Directory.GetDirectories(@path, "Project_*_*");
                Console.WriteLine("\tThe number of directories starting with 'Project' is {0}.", dirs.Length);

                foreach (string dir in dirs)
                {
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
                                    allsitefiles.Add(file);
                                }
                            }
                            
                        }
                      
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }


            Console.WriteLine();
            Dictionary<string, Project> TheProjects = new Dictionary<string, Project>();
            foreach (string element in allsitefiles)
            {
                string subfolders = element.Replace(path, "");
                string[] folders = subfolders.Split(new Char [] {'/'});
                string TheProject = folders[0];
                Project tmproj = new Project ("null");
                if (TheProjects.TryGetValue(TheProject, out tmproj))
                {
                    Console.WriteLine(element);
                    using (StreamReader r = new StreamReader(element))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            string[] entries = line.Split(new Char[] { '\t', ' ' });
                            Position temp = new Position(entries[0], Int32.Parse(entries[1]), Int32.Parse(entries[2]), Int32.Parse(entries[3]),
                                          Char.Parse(entries[4]), Int32.Parse(entries[5]), entries[6], Int32.Parse(entries[7]), entries[8],
                                          Int32.Parse(entries[9]), Int32.Parse(entries[10]), Int32.Parse(entries[11]), Int32.Parse(entries[12]),
                                          Int32.Parse(entries[13]));
                            tmproj.addbase(entries[0] + entries[1], temp);
                        }
                    }
                }
                else
                {
                    Console.WriteLine(TheProject);
                    Project NewProject = new Project(TheProject);
                    TheProjects.Add(TheProject, NewProject);
                    Console.WriteLine("[" + element + "]");
                    Console.WriteLine(NewProject);
                    using (StreamReader r = new StreamReader(element))
                    {
                        string line;
                        int linecounter = 0;
                        while ((line = r.ReadLine()) != null)
                        {
                            linecounter++;
                            Console.WriteLine(linecounter + " " + line);
                            if (line.Substring(0, 1) != "#")
                            {
                                Console.WriteLine(linecounter + " " + line);
                                string[] entries = line.Split(new Char[] { '\t', ' ' });
                                Position temp = new Position(entries[0], Int32.Parse(entries[1]), Int32.Parse(entries[2]), Int32.Parse(entries[3]),
                                              Char.Parse(entries[4]), Int32.Parse(entries[5]), entries[6], Int32.Parse(entries[7]), entries[8],
                                              Int32.Parse(entries[9]), Int32.Parse(entries[10]), Int32.Parse(entries[11]), Int32.Parse(entries[12]),
                                              Int32.Parse(entries[13]));
                                Console.WriteLine(NewProject);
                                Console.WriteLine(NewProject.projectName + "  counts: " + NewProject.basecounts);
                                Console.WriteLine(temp + " " + entries[0] + entries[1]);
                                NewProject.addbase(entries[0] + entries[1], temp);
                            }
                        }
                    }
                }
            }
            Console.WriteLine();
            foreach(KeyValuePair<string, Project> entry in TheProjects)
	        {
		        Console.WriteLine(entry.Key + " " + entry.Value.basecounts);
	        }
            Console.WriteLine();

            return(0);
        }


        static void usage()
        {
            Console.WriteLine(Environment.NewLine + "\treadFileContent Usage:");
            Console.WriteLine("\treadFileContent <path-to-variantDectionDir>" + Environment.NewLine);
        }
    }
}



/*
Unhandled Exception: System.NullReferenceException: Object reference not set to an instance of an object
  at getsnps.Project.addbase (System.String snp, getsnps.Position POS) [0x00000] in <filename unknown>:0
  at getsnps.Program.Main (System.String[] args) [0x00000] in <filename unknown>:0
[ERROR] FATAL UNHANDLED EXCEPTION: System.NullReferenceException: Object reference not set to an instance of an object
  at getsnps.Project.addbase (System.String snp, getsnps.Position POS) [0x00000] in <filename unknown>:0
  at getsnps.Program.Main (System.String[] args) [0x00000] in <filename unknown>:0
*/