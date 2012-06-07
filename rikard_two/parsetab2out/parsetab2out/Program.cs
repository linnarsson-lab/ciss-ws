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

//            Console.WriteLine(EFILE.poscount);

            int counter = 0;
            foreach (string[] rw in filerows1)
            {
                counter++;
                if (counter == 1)
                {
                    Console.WriteLine(String.Join("\t", rw) + "\tEnsemblCount\tHGNC\tConsequence\tposcDNA\tposCDS\tposProt\tcodon\tAA\tHGVSc\tHGVSp\tPolyPhen\tSIFT\tColocVar");
                    continue;
                }
                else
                {
                    Console.Write(String.Join("\t", rw));
                }
                Console.Write(rw[24] + " pos " + rw[25]);
               // Console.Write(rw[23] + " pos " + rw[24]); for beta
                //                Console.Write(rw[29] + " pos " + rw[30]);        For Main
                int hitcount = 0;
                string CodonChange = "";
                string HGNC = "";
                string HGVSc = "";
                string HGVSp = "";
                string polyphen = "";
                string SIFT = "";
                string Consequence = "";
                string AAchange = "";
                string poscdna = "";
                string poscds = "";
                string posprot = "";
                string colocv = "";
                foreach (KeyValuePair<string, EnsemblRow> kvp in EFILE)
                {

                    if ("chr" + kvp.Value.Location == rw[24] + ":" + rw[25])
//                        if ("chr" + kvp.Value.Location == rw[29] + ":" + rw[30])
                        {
                        //                      Console.Write(" " + kvp.Value.Location);
                        if (kvp.Value.Codonchange.Length > 3)
                        {
                            CodonChange += ":" + kvp.Value.Codonchange;
                        }
                        if (kvp.Value.Aminoacidchange.Length > 1)
                        {
                            AAchange += ":" + kvp.Value.Aminoacidchange;
                        }
                        if (kvp.Value.PositionincDNA.Length > 1)
                        {
                            poscdna += ":" + kvp.Value.PositionincDNA;
                        }
                        if (kvp.Value.PositioninCDS.Length > 1)
                        {
                            poscds += ":" + kvp.Value.PositioninCDS;
                        }
                        if (kvp.Value.ColocatedVariation.Length > 1)
                        {
                            colocv += ":" + kvp.Value.ColocatedVariation;
                        }

                        if (kvp.Value.Positioninprotein.Length > 1)
                        {
                            posprot += ":" + kvp.Value.Positioninprotein;
                        }

                        if (kvp.Value.Extra.Length > 2)
                        {
                            string[] extras = kvp.Value.Extra.Split(';');
                            foreach (string item in extras)
                            {
                                if (item.Substring(0, 4) == "HGNC")
                                    HGNC += item.Replace("HGNC=", ":");
                                if (item.Substring(0, 5) == "HGVSc")
                                    HGVSc += item.Replace("HGVSc=", ":");
                                if (item.Substring(0, 5) == "HGVSp")
                                    HGVSp += item.Replace("HGVSp=", ":");
                                if (item.Substring(0, 8) == "PolyPhen")
                                    polyphen += item.Replace("PolyPhen=", ":");
                                if (item.Substring(0, 4) == "SIFT")
                                    SIFT += item.Replace("SIFT=", ":");
                            }
                        }
                        Consequence += ":" + kvp.Value.Consequence;
                        hitcount++;
                    }
                }
                Console.Write("\t" + hitcount);
                if (HGNC.Length > 3)
                {
                    Console.Write("\t" + HGNC);
                }
                else
                    Console.Write("\tno gene");
                if (Consequence.Length > 3)
                {
                    Console.Write("\t" + Consequence);
                }
                else
                    Console.Write("\tnot-known");

                if (poscdna.Length > 0)
                {
                    Console.Write("\t" + poscdna);
                }
                else
                    Console.Write("\tNOcDNApos");
                if (poscds.Length > 0)
                {
                    Console.Write("\t" + poscds);
                }
                else
                    Console.Write("\tNOcdspos");
                if (posprot.Length > 3)
                {
                    Console.Write("\t" + posprot);
                }
                else
                    Console.Write("\tNOposprot");

                if (CodonChange.Length > 3)
                {
                    Console.Write("\t" + CodonChange);
                }
                else
                    Console.Write("\tno-codon");
                if (AAchange.Length > 1)
                {
                    Console.Write("\t" + AAchange);
                }
                else
                    Console.Write("\tno-aa");
                if (HGVSc.Length > 1)
                {
                    Console.Write("\t" + HGVSc);
                }
                else
                    Console.Write("\tnoHGVSc");
                if (HGVSp.Length > 1)
                {
                    Console.Write("\t" + HGVSp);
                }
                else
                    Console.Write("\tnoHGVSp");
                if (polyphen.Length > 1)
                {
                    Console.Write("\t" + polyphen);
                }
                else
                    Console.Write("\tnoPolyPhen");
                if (SIFT.Length > 1)
                {
                    Console.Write("\t" + SIFT);
                }
                else
                    Console.Write("\tnoSIFT");
                if (colocv.Length > 1)
                {
                    Console.Write("\t" + colocv);
                }
                else
                    Console.Write("\tnoColocV");
                Console.WriteLine();
            }
        }
    }
}