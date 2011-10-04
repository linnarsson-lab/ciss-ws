using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Linnarsson.Dna;
using Linnarsson.Mathematics;
using Linnarsson.Utilities;  

namespace SNPs_Primers
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Select the FASTA sequence file");
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            MessageBox.Show("Select the selected SNPs file");
            OpenFileDialog ofd2 = new OpenFileDialog();
            if (ofd2.ShowDialog() != DialogResult.OK) return;
            MessageBox.Show("Select the location where you want to save your output file and give a name");
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() != DialogResult.OK) return;
            var result = sfd.FileName.OpenWrite();
            long totalLength = 0;
           
            foreach (string fname in ofd1.FileNames)
            {
                FastaFile ff = FastaFile.Load(fname);
                foreach (FastaRecord rec in ff.Records)
                {
                    totalLength += rec.Sequence.Count;
                    //MessageBox.Show(totalLength.ToString());
                    DnaSequence ds = new DnaSequence(rec.Sequence);
                    //MessageBox.Show(ds.SubSequence (45825266,4).ToString ());
                    string[] lines = System.IO.File.ReadAllLines(ofd2.FileName);
                    result.WriteLine("rs#" + "\t" + "alleles" + "\t" + "chrom" + "\t" + "pos" + "\t" + "strand" + "\t" + "chrom from seq" + "\t" + "seq with allele" + "\t" + "primer1 " + "\t" + "P1_Tm" + "\t" + "P1_GC%" + "\t" + "primer2" + "\t" + "P2_Tm" + "\t" + "P2_GC%" + "\t" + "P1 Start" + "\t" + "P2 Start");
                    //result.WriteLine("rs#" + "\t" + "alleles" + "\t" + "chrom" + "\t" + "pos" + "\t" + "strand" + "\t" + "chrom from seq" + "\t" + "seq with allele" + "\t" + "primer1 " + "\t" + "P1_Tm" + "\t" + "primer2" + "\t" + "P2_Tm");
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string oneLine = lines[i];
                        string[] lineItems = oneLine.Split('\t');
                        long x = Convert.ToInt64  (lineItems[3]); 
                        
                        DnaSequence subds = ds.SubSequence(x - 30, 65);
                        DnaSequence allelesSeq = ds.SubSequence(x - 5, 10);
                        double temp = 0; double temp2 = 0;
                        //DnaSequence testing = ds.SubSequence(x-1, 3);
                        //MessageBox.Show(lineItems[3] + " and " + testing.ToString ());
                        //int len1 = 18; int len2 = 7;
                        int len1 = 20; int len2 = 5;
                        DnaSequence dsP1 = subds.SubSequence(len2, len1);
                        
                        //bool nucleotide = true;
                        //for (int j = 0; j <= len1; j++)
                        //{
                        //    if (dsP1.GetNucleotide(j) == 'N')
                        //    {
                        //        nucleotide  = false;
                        //        break;
                        //    }
                        //}

                        //if (nucleotide != false)
                        {
                            TmCalculator tm = new TmCalculator();
                            temp = tm.GetTm(dsP1, 0.00000005, 0.00000005, 0.05);
                            while (temp < 50)
                            {
                                len1++; len2--;
                                if (len1 == 25) break;
                                dsP1 = subds.SubSequence(len2, len1);
                                if (dsP1.GetNucleotide(0) == 'N')
                                {
                                    break;
                                }
                                temp = tm.GetTm(dsP1, 0.00000005, 0.00000005, 0.05);
                                if (temp == 0) MessageBox.Show("check temp =0");
                            }
                        }
                        len2 = 20;
                        DnaSequence subds2 = ds.SubSequence(x + 5, 25) ;
                        //subds_rev.RevComp();
                        DnaSequence dsP2 = subds2.SubSequence(0, len2);
                        //nucleotide = true;
                        //for (int j = 0; j <= len2; j++)
                        //{
                        //    if (dsP2.GetNucleotide(j) == 'N')
                        //    {
                        //        nucleotide = false;
                        //        break;
                        //    }
                        //}
                        //if (nucleotide != false)
                        {
                            dsP2.RevComp(); 
                            TmCalculator tm2 = new TmCalculator();
                            temp2 = tm2.GetTm(dsP2, 0.00000005, 0.00000005, 0.05);
                            while (temp2 < 50)
                            {
                                len2++;
                                if (len2 == 25) break;
                                dsP2.RevComp(); 
                                dsP2 = subds2.SubSequence(0, len2);
                                dsP2.RevComp(); 
                                if (dsP2.GetNucleotide(len2-1) == 'N')
                                {
                                    break;
                                }
                                temp2 = tm2.GetTm(dsP2, 0.00000005, 0.00000005, 0.05);
                            }

                        }
                        //MessageBox.Show((dsP1.CountCases(IupacEncoding.GC) * 100 / dsP1.Count).ToString()); 
                        double P1_GC=dsP1.CountCases (IupacEncoding.GC)*100/dsP1.Count;
                        double P2_GC=dsP2.CountCases (IupacEncoding.GC)*100/dsP2.Count ;   
                        if ((temp >= 50 && temp <= 60) && (temp2 >= 50 && temp2 <= 60))
                        {
                            if((P1_GC >30 && P1_GC <70) && (P2_GC >30 && P2_GC <70)  )
                            {
                                  
                                result.WriteLine(lineItems[0] + "\t" + lineItems[1] + "\t" + lineItems[2] + "\t" + lineItems[3] + "\t" + lineItems[4] + "\t" + Path.GetFileNameWithoutExtension(ofd1.FileName) + "\t" + allelesSeq.ToString() + "\t" + dsP1.ToString() + "\t" + temp  + "\t" + P1_GC + "\t" + dsP2.ToString() + "\t" + temp2  + "\t" + P2_GC + "\t" + (Convert.ToInt64(lineItems[3])-5-dsP1.Count) + "\t" + (Convert.ToInt64(lineItems[3])+5));
                                //result.WriteLine(lineItems[0] + "\t" + lineItems[1] + "\t" + lineItems[2] + "\t" + lineItems[3] + "\t" + lineItems[4] + "\t" + Path.GetFileNameWithoutExtension(ofd1.FileName) + "\t" + allelesSeq.ToString() + "\t" + dsP1.ToString() + "\t" + temp + "\t" + dsP2.ToString() + "\t" + temp2);
                            }
                        }
                    }
                    
                    
                }
            }
            result.Close(); 
            MessageBox.Show("End of Run!!"); 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string fname = "";
            if (radioButton5.Checked == true) fname = "P1_FastQ.FQ";
            else if (radioButton6.Checked == true) fname = "P2_FastQ.FQ";

            MessageBox.Show("Select the selected SNPs files with primers");
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK) return;
            var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) + fname)).OpenWrite();
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            for (int i = 1; i < lines.Length; i++)
            {
                string oneLine = lines[i];
                string[] lineItems = oneLine.Split('\t');
                if (radioButton5.Checked == true)
                {
                    
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[2] + "_" + lineItems[13] + "_P1" + lineItems[4]), lineItems[7], new string('b', (lineItems[7].Count())));
                        output.WriteLine(rec1.ToString());
                }
                else if (radioButton6.Checked == true)
                {
                    
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[2] + "_" + lineItems[14] + "_P2" + lineItems [4]), lineItems[10], new string('b', (lineItems[10].Count())));
                        output.WriteLine(rec1.ToString());
                }

            }
            MessageBox.Show("End of Run!!");
            output.Close();
        }

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            button2.Enabled = true; 
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            button2.Enabled = true;
        }
    }
}
