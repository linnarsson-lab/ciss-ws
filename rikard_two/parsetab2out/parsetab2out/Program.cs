using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace parsetab2out
{

    public class EnsemblRow
    {

        public string UploadedVariation { get; set; }
        public string Location { get; set; }
        public string Allele { get; set; }
        public string Gene { get; set; }
        public string Feature { get; set; }
        public string Featuretype { get; set; }
        public string Consequence { get; set; }
        public string PositionincDNA { get; set; }
        public string PositioninCDS { get; set; }
        public string Positioninprotein { get; set; }
        public string Aminoacidchange { get; set; }
        public string Codonchange { get; set; }
        public string ColocatedVariation { get; set; }
        public string Extra { get; set; }

        public EnsemblRow(string row)
        {
            string[] entry = row.Split(new Char[] { '\t' });
            UploadedVariation = entry[0];
            Location = entry[1];
            Allele = entry[2];
            Gene = entry[3];
            Feature = entry[4];
            Featuretype = entry[5];
            Consequence = entry[6];
            PositionincDNA = entry[7];
            PositioninCDS = entry[8];
            Positioninprotein = entry[9];
            Aminoacidchange = entry[10];
            Codonchange = entry[11];
            ColocatedVariation = entry[12];
            Extra = entry[13];

        }
    }

    public class EnsemblFile : Dictionary<string, EnsemblRow>
    {
        public string filepath { get; set; }
        public int poscount;

        public EnsemblFile(string Filepath)
        {
            filepath = Filepath;
            poscount = 0;
            int linecount = 0;
            using (StreamReader r = new StreamReader(filepath))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 5) continue;
                    if (line.Substring(0, 1) != "U")
                    {
                        linecount++;
                        string[] entries = line.Split(new Char[] { '\t' });
                        if (entries.Length > 10)
                        {
                            EnsemblRow temp = new EnsemblRow(line);
                            string[] genomepos = entries[0].Split(new Char[] { '_' });
                            this.addrow(genomepos[0] + '.' + genomepos[1] + '.' + linecount, temp);
//                           Console.WriteLine(line + " " + temp.UploadedVariation);
                            poscount++;
                        }
                    }
                }
            }
        }

        public void addrow(string chrpos, EnsemblRow theRow)
        {
            this.Add(chrpos, theRow);
//            Console.WriteLine(Environment.NewLine + "\t" + theRow.Location + " " + theRow.Gene + Environment.NewLine);
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
        //        static List<string[]> tabfile(string filepath)
        static List<string[]> tabfile(string filepath)
        {

            List<string[]> rows = new List<string[]>();

            using (StreamReader r = new StreamReader(filepath))
            {
                string line;

                while ((line = r.ReadLine()) != null)
                {
                    if (line.Length < 3) continue;
                    {
                        string[] entries = line.Split(new Char[] { '\t' });
                        rows.Add(entries);
                        int cnt = entries.GetLength(0);
                        //                        Console.Write(cnt.ToString() + " ");
                    }
                }
            }

            return rows;
        }

        static void Main(string[] args)
        {
            List<string[]> filerows1 = tabfile(args[0]);
            //            Console.WriteLine(filerows1.Count);
            EnsemblFile EFILE = new EnsemblFile(args[1]);

            Console.WriteLine(EFILE.poscount);

            int counter = 0;
            foreach (string[] rw in filerows1)
            {
                counter++;
                if (counter == 1)
                {
                    Console.WriteLine(String.Join("\t", rw));
                    continue;
                }
                else
                {
                    Console.Write(String.Join("\t", rw));
                }
                Console.Write(rw[29] + " pos " + rw[30]);
                int hitcount = 0;
                string CodonChange = "";
                string HGNC = "";
                string Consequence = "";
                string AAchange = "";
                foreach (KeyValuePair<string, EnsemblRow> kvp in EFILE)
                {

                    if ("chr" + kvp.Value.Location == rw[29] + ":" + rw[30])
                    {
                        //                      Console.Write(" " + kvp.Value.Location);
                        if (kvp.Value.Codonchange.Length > 5)
                        {
                            if (kvp.Value.Codonchange != CodonChange)
                            {
                                CodonChange += kvp.Value.Codonchange;
                            }
                        }
                        if (kvp.Value.Aminoacidchange.Length > 5)
                        {
                            if (kvp.Value.Aminoacidchange != AAchange)
                            {
                                AAchange += kvp.Value.Aminoacidchange;
                            }
                        }

                        if (kvp.Value.Extra.Length > 5)
                        {
                            if (kvp.Value.Extra.Substring(0, 4) == "HGNC")
                                if (HGNC != kvp.Value.Extra.Replace("HGNC=", ":"))
                                    HGNC += kvp.Value.Extra.Replace("HGNC=", ":");
                        }
                        Consequence += ":" + kvp.Value.Consequence;
                        hitcount++;
                    }
                }
                Console.Write(" InEnsembl:" + hitcount);
                if (HGNC.Length > 3)
                {
                    Console.Write(" Gene" + HGNC);
                }
                if (Consequence.Length > 3)
                {
                    Console.Write(" Consequence" + Consequence);
                }
                if (CodonChange.Length > 3)
                {
                    Console.Write(" CodonChange: " + CodonChange);
                }
                if (AAchange.Length > 3)
                {
                    Console.Write(" AAChange: " + AAchange);
                }
                Console.WriteLine();
            }
        }
    }
}