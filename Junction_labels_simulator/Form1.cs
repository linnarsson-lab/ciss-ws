using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;
//using Linnarsson.Strt; 

namespace Junction_labels_simulator
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
        public int CountN(DnaSequence ds, long ds_len) // counting no of Ns in a sequence
        {
            int N = 0;
            for (int i = 0; i < ds_len; i++)
            {
                if (ds.GetNucleotide(i) == 'N' || ds.GetNucleotide(i) == 'n')
                    N++;

            }
            return N;
        }

        public int CountN(ShortDnaSequence ds, long ds_len) // counting no of Ns in a sequence
        {
            int N = 0;
            for (int i = 0; i < ds_len; i++)
            {
                if (ds.GetNucleotide(i) == 'N' || ds.GetNucleotide(i) == 'n')
                    N++;

            }
            return N;
        }

        public int CountN(LongDnaSequence ds, long ds_len) // counting no of Ns in a sequence
        {
            int N = 0;
            for (int i = 0; i < ds_len; i++)
            {
                if (ds.GetNucleotide(i) == 'N' || ds.GetNucleotide(i) == 'n')
                    N++;

            }
            return N;
        }
        public string[] CreateRandomN(string elements, int len, int times)
        {
            // <summary>
            // returns a array of strings. "elements" should contain ACGT if the random primer contains ACGT. "elements" should be ACG if the primer is without T , etc. 
            //"len" indicates the length of the primer and "times" indicates the no of random primer. 
            // </summary>

            int element_len = elements.Length;
            char[] Element = elements.ToCharArray();
            int [] result_Array=new int[len];
            string[] results= new string[times];
            Random rnd = new Random();
           
            for (int j = 0; j < times; j++)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(Element[rnd.Next(0, element_len)]);
                }
                results[j]=sb.ToString();
            }
            return results; 
        }

        public string Fwd100(string text) // selecting first 100bp from a string. String should be > 100 bp
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = 0; i < 100; i++)
            {
                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        public string Reverse(string text) // returns a string in a reverse order
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = text.Length - 1; i >= 0; i--)
            {
                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        public string Reverse100(string text) // returns last 100bp of a lond string. String should be > 100 bp
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            StringBuilder builder = new StringBuilder(text.Length);
            for (int i = text.Length - 1; i >= text.Length -100 ; i--)
            {
                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        private void LoadGenome_Click(object sender, EventArgs e)
        {
            int num_molecule = Convert.ToInt32(num_molecule_TXB.Text);
            long molecule_len = Convert.ToInt64(mol_length_TXB.Text) * 1000;
            double tsp_probability = Convert.ToDouble(tsp_probablt_txt.Text);
            int Discard_percentage = Convert.ToInt32(Discardtxt.Text);
            int Reads_in_million = Convert.ToInt32(ReadmTxt.Text) * 1000000;
            int count_N = 0;
            long molecule_id = 1;
            //MessageBox.Show("Select the Genome/Chromosome sequence files for test Chr21 from hg18");
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            //MessageBox.Show("Select the SNP file(s) for the same Genome/Chromosome i.e. chr21 URN sample");
            OpenFileDialog ofd2 = new OpenFileDialog();
            if (ofd2.ShowDialog() != DialogResult.OK) return;
            //MessageBox.Show("Select the location where you want to save your output file and give a name");
            //SaveFileDialog sfd = new SaveFileDialog();
            //if (sfd.ShowDialog() != DialogResult.OK) return;
            //var result = sfd.FileName.OpenWrite();

            
            
            long totalLength=0;
            foreach (string fname in ofd1.FileNames)
            {
                
                FastaFile ff = FastaFile.Load(fname);
                foreach (FastaRecord rec in ff.Records)
                {
                    
                    totalLength += rec.Sequence.Count;
                    if (num_molecule * molecule_len > totalLength)
                    {
                        MessageBox.Show("selection more than length.");
                        break;
                    }
                    //MessageBox.Show(totalLength.ToString());
                    //DnaSequence ds1 = new DnaSequence(rec.Sequence); // to get data from 1st strand
                    Random Tsp_rnd = new Random();
                    DialogResult dr2 = MessageBox.Show("Do you want to continue from Quality correction?", "Yes to continue", MessageBoxButtons.YesNo, MessageBoxIcon.Hand);
                    if (dr2 == DialogResult.Yes) goto qualitysection;

                    LongDnaSequence ds1 = new LongDnaSequence(rec.Sequence);
                    LongDnaSequence ds2 = new LongDnaSequence(rec.Sequence); // to get data from 1st strand and set SNPs and convert into 2nd strand 
                    string[] lines = System.IO.File.ReadAllLines(ofd2.FileName);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string oneLine = lines[i];
                        string[] lineItems = oneLine.Split('\t');
                        char[] SNPs = lineItems[1].ToCharArray();
                        //MessageBox.Show(lineItems[1].ToString());
                        //MessageBox.Show(ds.GetNucleotide(Convert.ToInt64 (lineItems[3])).ToString());    
                        if (SNPs[0] == ds2.GetNucleotide(Convert.ToInt64(lineItems[3])))
                        {
                            //MessageBox.Show("matched");
                            ds2.SetNucleotide(Convert.ToInt64(lineItems[3]), SNPs[2]); // replacing the nucleotide with 2nd SNP
                        }
                        else 
                        {
                            if (SNPs[0] != '-')
                            {
                                ds2.SetNucleotide(Convert.ToInt64(lineItems[3]), SNPs[0]);
                            }
                            else 
                            { 
                                ds2.SetNucleotide(Convert.ToInt64(lineItems[3]), SNPs[2]); 
                            }
                        } //MessageBox.Show(ds.GetNucleotide(Convert.ToInt64(lineItems[3])).ToString());    
                    }
                    //result.Write(ds2.ToString());
                    
                    //MessageBox.Show(num_molecule + " and " + molecule_len); 
                    var output = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "molecules_info.txt")).OpenWrite();
                    output.WriteLine("Molecule_id" + "\t" + "Strand" + "\t" + "Molecule Start" + "\t" + "Molecule End" + "\t" + "No of Ns" + "\t" + "Molecule Seq"/* + "\t" + "Read Count"*/);
                    long molecule_start = 0;
                    
                    Random chr_selection = new Random(); // selecting molecules from 2 strands randomly
                    Random frag_selection = new Random(); // Selecting fragments randomly
                    Random rndRead = new Random();
                    //int readCount = 0; 
                    int[] chrno_frag_array=new int[num_molecule];
                    for (int chr_frag = 0; chr_frag <= num_molecule; chr_frag++)
                    {
                        //chrno_frag_array[chr_frag] = chr_selection.Next(1, 3);
                        //readCount = rndRead.Next(1000000);
                        if (chr_selection.Next(1, 3) == 1)
                        {
                            //MessageBox.Show("1"); 
                            //ds1.SubSequence(molecule_start, molecule_len);
                            // writing in a file which contains fragmented sequence information: the file contains following cols. Strand, seqStart, Stop and Sequence
                            //ShortDnaSequence ds1_tmp = ds1.SubSequence(molecule_start, molecule_len);
                            DnaSequence ds1_tmp = ds1.SubSequence(molecule_start, molecule_len);
                            
                            count_N = CountN(ds1_tmp, molecule_len);
                            output.WriteLine(("M" + molecule_id) + "\t" + "1" + "\t" + molecule_start + "\t" + (molecule_start + molecule_len) + "\t" + count_N + "\t" + ds1.SubSequence(molecule_start, molecule_len) /*+ "\t" + readCount */);
                                        molecule_start = molecule_start + molecule_len;
                                        molecule_id++;
                            count_N = 0;
                            
                        }
                        else
                        {
                            DnaSequence ds2_tmp = ds2.SubSequence(molecule_start, molecule_len);
                            count_N = CountN(ds2_tmp, molecule_len);
                                        ShortDnaSequence ds2_rev = new ShortDnaSequence(ds2.SubSequence(molecule_start, molecule_len));
                                        //ds2_rev.RevComp();
                                        output.WriteLine(("M" + molecule_id) + "\t" + "2" + "\t" + molecule_start + "\t" + (molecule_start + molecule_len) + "\t" + count_N + "\t" + ds2_rev.ToString()/*+ "\t" + readCount */);
                                        molecule_start = molecule_start + molecule_len;
                                        molecule_id++;
                            count_N = 0;
                        }
                        
                    }
                    
                    output.Close();
                    // following code will generate a file with  sequence and transposons at each side of a sequence  
                    string Molecules_file =(Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "molecules_info.txt"));
                    var output2 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_transposones.txt")).OpenWrite();
                    output2.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "Seq" + "\t"  + "Common Fragment 9L" + "\t"+ "ME19L"  + "\t" +"ME19R"+ "\t" + "Common Fragment 9R" + "\t" + "Total Seq" + "\t" + "Read Count");
                    string[] Tsp_lines = System.IO.File.ReadAllLines(Molecules_file);
                    
                    int readCount = 0;
                    int Tsp_start = 0;
                    int Tsp_count = 0;
                    string ME19L = "CTGTCTCTTATACACATCT";
                    string ME19R = "AGATGTGTATAAGAGACAG";
                    
                    string Tsp_molecule_id = "";
                    
                    
                    int Tsp_N = 0;
                    int Tsp_len = 0;
                    
                    for (int i = 1; i < Tsp_lines.Length; i++)
                    {
                        int first_Tsp = 0, second_Tsp = 0;
                        
                        string Tsp_oneLine = Tsp_lines[i];
                        string[] Tsp_lineItems = Tsp_oneLine.Split('\t');
                        ShortDnaSequence molecule_ds = new ShortDnaSequence(Tsp_lineItems[5]);
                        ShortDnaSequence Tsp_ds= new ShortDnaSequence();
                        ShortDnaSequence CF9L = new ShortDnaSequence();
                        ShortDnaSequence CF9R = new ShortDnaSequence();
                        if ((((Convert.ToInt32(Tsp_lineItems[3])) - (Convert.ToInt32(Tsp_lineItems[2]))) / 2) <= Convert.ToInt32(Tsp_lineItems[4]))
                        {
                            goto Next_loop; // if more than hallf of the sequences are represented by N  
                        }
                        else 
                        {
                            Tsp_start = Convert.ToInt32(Tsp_lineItems[2]);
                            

                            //MessageBox.Show(Tsp_rnd.NextDouble().ToString() + "and" + (1 / tsp_probability).ToString ()); 
                            
                            for (int j = 0; j < (Convert.ToInt32(Tsp_lineItems[3]) - Convert.ToInt32(Tsp_lineItems[2]) ); j++)
                            {
                                first_Tsp = second_Tsp;
                                if (Tsp_rnd.NextDouble() < 1 / tsp_probability)
                                {
                                    // insert the transposon
                                    second_Tsp = j;

                                    if (Tsp_count != 0 &&  Tsp_molecule_id == Tsp_lineItems [0])
                                    {
                                        //MessageBox.Show("I at Tsp_count=" + Tsp_count); 
                                        Tsp_len = second_Tsp - first_Tsp;
                                        if (Tsp_len < 100) goto Next_Tsp;
                                        int len = Tsp_len;
                                        Tsp_ds=(ShortDnaSequence )molecule_ds.SubSequence(first_Tsp,Tsp_len);
                                        CF9L = CF9R;
                                        if (Tsp_ds.Count >= 9) // new changed
                                        {
                                            //ME19L = Tsp_ds.SubSequence(0, 19);
                                            CF9R = (ShortDnaSequence )Tsp_ds.SubSequence(Tsp_ds.Count - 9, 9);
                                            Tsp_ds = (ShortDnaSequence)molecule_ds.SubSequence(first_Tsp, (Tsp_len - 9));
                                            len = Tsp_len - 9;
                                        }
                                        else
                                        {
                                            //ME19L = Tsp_ds;
                                            CF9R = Tsp_ds;
                                        }
                                        //Tsp_N = CountN(Tsp_ds, Tsp_len);
                                        Tsp_N = CountN(Tsp_ds, len);
                                        //MessageBox.Show((Tsp_N / 2).ToString() + "and" + Tsp_len.ToString()); 
                                        //if (Tsp_N  <= Tsp_len/2)
                                        if (Tsp_N <= len / 2)
                                        {
                                            readCount = rndRead.Next(10); // Need to change here *************************************************************************************************************************************************************************************************************************************************************************************************************************************************************************
                                            output2.WriteLine(Tsp_count - 1 + "\t" + Tsp_count + "\t" + Tsp_lineItems[0] + "\t" + Tsp_lineItems[1] + "\t" + (first_Tsp + Convert.ToInt64(Tsp_lineItems[2])) + "\t" + (second_Tsp + Convert.ToInt64(Tsp_lineItems[2])) + "\t" + /*Tsp_len*/len + "\t" + Tsp_ds + "\t" + CF9L + "\t" + ME19L  + "\t" + ME19R  + "\t" + CF9R + "\t" + (/*ME19L +*/ CF9L.ToString() + Tsp_ds + CF9R /*+ME19R*/) + "\t" + readCount);
                                        }
                                        Tsp_count++;
                                        Tsp_molecule_id = Tsp_lineItems[0];
                                    }
                                    else if (Tsp_count == 0 || Tsp_molecule_id != Tsp_lineItems[0]) // || Tsp_molecule_id != Tsp_lineItems[0])
                                    {
                                        if (Tsp_ds.Count >= 9)
                                        {
                                            CF9L = (ShortDnaSequence)Tsp_ds.SubSequence(0, 9);
                                            Tsp_ds = (ShortDnaSequence)Tsp_ds.SubSequence(9, (Tsp_ds.Count - 9));

                                            CF9R = (ShortDnaSequence)Tsp_ds.SubSequence(Tsp_ds.Count - 9, 9);
                                        }
                                        else
                                        {
                                            CF9L = Tsp_ds;
                                            CF9R = Tsp_ds;
                                        }
                                        Tsp_count++;
                                        Tsp_molecule_id = Tsp_lineItems[0];
                                    }
                                    
                                }
                            Next_Tsp:
                                ;
                            }
                            
                        }
                    Next_loop:;
                    }
                     
                    output2.Close();

                    // this code will introduce a 6bp random sequece at each side of a sequece
                    string Tsp_file = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_transposones.txt"));
                    var output3 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_transposones_N6.txt")).OpenWrite();
                    //output3.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "ME1_19" + "\t" + "Common Fragment 9L" + "\t" + "Seq" + "\t" + "Common Fragment 9R" + "\t" + "ME2_19" + "\t" + "Total Seq");
                    output3.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "ME19L" + "\t" + "N6_1" + "\t" + "ComnFrg 9L" + "\t" + "Seq" + "\t" + "ComnFrg 9R" + "\t" + "N6_2" + "\t" + "ME19R" + "\t" + "Read Count" + "\t" + "N6_1&ComnFrg9L" + "\t" + "ComnFrg9R&N6_2" + "\t" + "Total Seq");
                    string[] TspN6_lines = System.IO.File.ReadAllLines(Tsp_file);
                    string[] N6 = CreateRandomN("ACGT",6,(TspN6_lines.Length+1));
                    int discardFrg = 0;
                    for (int i = 1; i < TspN6_lines.Length; i++)
                    {
                        string TspN6_oneLine = TspN6_lines[i];
                        string[] TspN6_lineItems = TspN6_oneLine.Split('\t');
                        int per = TspN6_lines.Length * Discard_percentage / 100;
                        double random_no = Tsp_rnd.NextDouble(); 
                        //MessageBox.Show(discart.ToString() + "and" + per.ToString() + "and" + random_no.ToString() );
                        if (discardFrg <= per && random_no > 0.5) //tsp_probability)
                        {
                            discardFrg = discardFrg + 1;
                        }

                    //    //if (Convert.ToInt32(TspN20_lineItems[6]) > 30 && Convert.ToInt32(TspN20_lineItems[6]) < 250)
                        else
                            output3.WriteLine(TspN6_lineItems[0] + "\t" + TspN6_lineItems[1] + "\t" + TspN6_lineItems[2] + "\t" + TspN6_lineItems[3] + "\t" + TspN6_lineItems[4] + "\t" + TspN6_lineItems[5] + "\t" + TspN6_lineItems[6] + "\t" + TspN6_lineItems[9] + "\t" + N6[i] + "\t" + TspN6_lineItems[8] + "\t" + TspN6_lineItems[7] + "\t" + TspN6_lineItems[11] + "\t" + N6[i + 1] + "\t" + TspN6_lineItems[10] + "\t" + TspN6_lineItems[13] + "\t" + (N6[i] + TspN6_lineItems[8]) + "\t" + (N6[i + 1] + TspN6_lineItems[11]) + "\t" + (N6[i] + TspN6_lineItems[12] + N6[i+1]));
                    }
                    
                    output3.Close();
                    DialogResult dr=MessageBox.Show("Do you want to continue....","Yes to continue", MessageBoxButtons.YesNo,MessageBoxIcon.Hand);
                    if (dr==DialogResult.No)
                    {
                        break;
                    } 
                    // this code will generate 2 fastq files with fixed random quality score. To check the output replace the output4 and output5 with some other name******* 
                    //string Totseq_file = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_transposones.txt"));
                    //var output4 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_reads_1.fq")).OpenWrite();
                    //var output5 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_reads_2.fq")).OpenWrite();
                    //string[] Totseq_lines = System.IO.File.ReadAllLines(Totseq_file);
                    ////Random rndReads = new Random();
                    //long totReads = 0;
                    //int readCounting = 0;
                    //int readNo=0;
                    //for (int i = 1; i < Totseq_lines.Length; i++)
                    //{
                    //    string Totseq_oneline = Totseq_lines[i];
                    //    string[] Totseq_lineitems = Totseq_oneline.Split('\t');
                    //    int readlen =Convert.ToInt32(readLenTxt.Text);
                    //    //int readC = Convert.ToInt32(Totseq_lineitems[13]); 
                    //    double random_no = Tsp_rnd.NextDouble();

                    //    //double quality_scr = 30; //-10 * Math.Log10(random_no/(1-random_no));
                    //    int[] quality_scr = new int[1000];
                    //    DnaSequence fwdseq = new DnaSequence(Totseq_lineitems[12]);
                    //    DnaSequence revSeq = new DnaSequence(Totseq_lineitems[12]);
                    //    //string fwdseq = "", revSeq = "";
                    //    //double quality_scr = -10 * Math.Log10(0.05 / (1 - 0.05));
                    //    //MessageBox.Show(quality_scr.ToString()); 
                    //    if (readlen >= Totseq_lineitems[12].Length)
                    //    {
                            
                    //        //fwdseq = Totseq_lineitems[12];
                    //        //revSeq = Reverse(Totseq_lineitems[12]);
                    //        quality_scr = new int[readlen];
                    //        for (int k = 0; k < Totseq_lineitems[12].Length; k++)
                    //        {
                    //            quality_scr[k] = 94;

                    //        }
                    //    }
                    //    else if (readlen < Totseq_lineitems[12].Length)
                    //    {
                    //        //fwdseq = Fwd100(Totseq_lineitems[12]);
                    //        //revSeq = Reverse100(Totseq_lineitems[12]);
                    //        fwdseq = fwdseq.SubSequence(0, 100);
                    //        revSeq = revSeq.SubSequence(0, 100);
                    //        revSeq.RevComp();
                    //        quality_scr = new int[100];
                    //        for (int k = 0; k < 100; k++)
                    //        {
                    //            quality_scr[k] = 94;

                    //        }
                    //    }
                    //    //readCount = rndReads.Next(Reads_in_million);
                    //    readCounting = Convert.ToInt32(Totseq_lineitems[13]);

                    //    //totReads = totReads + (2 * readCount);
                    //    totReads = (totReads + readCounting)/2;
                    //    //MessageBox.Show(readCount.ToString() + "and " + totReads.ToString());  
                    //    if (totReads <= Reads_in_million) readNo = readCounting;
                    //    else readNo = 0; // 
                    //    //MessageBox.Show(readNo.ToString());
                    //    char q = ' ';
                    //    StringBuilder quality_scr_all = new StringBuilder();
                    //    for (int x = 0; x < quality_scr.Length; x++)
                    //    {
                    //        q=Convert.ToChar(quality_scr[x]);
                    //        //MessageBox.Show(q + "and" + quality_scr[x]); 
                    //        quality_scr_all.Append(q);
                    //    }
                    //    //MessageBox.Show(quality_scr_all.ToString()); 
                    //    //FastQRecord rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), fwdseq, quality_scr);
                    //    //output4.WriteLine(rec1.ToString());
                    //    //rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), revSeq, quality_scr);
                    //    //output4.WriteLine(rec1.ToString());
                    //    FastQRecord rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), fwdseq.ToString(), quality_scr_all.ToString());
                    //    output4.WriteLine(rec1.ToString());
                    //    rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), revSeq.ToString(), quality_scr_all.ToString());
                    //    output5.WriteLine(rec1.ToString());
 
 
                    //}
                    ////MessageBox.Show(totReads.ToString()); 
                    //output4.Close();
                    //output5.Close();
                qualitysection: ;
                    string Totseq_file = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_transposones_N6.txt"));
                    //string QualityScore=("\\192.168.1.12\data\reads\Run00007_L1_1_100521_GA2X_0007.fq");
                    FastQFile fq = FastQFile.Load("C:\\Indranil\\2011 work and activity\\denovo\\xaaz.fq", 64);
                    int errorcount = 0;
                    var output4 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_reads_1.fq")).OpenWrite();
                    var output5 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_reads_2.fq")).OpenWrite();
                    var errorOutput = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_errorOutput.txt")).OpenWrite();
                    var outputF = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_F_seq.fq")).OpenWrite();
                    var outputR = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_R_seq.fq")).OpenWrite();
                    string[] Totseq_lines = System.IO.File.ReadAllLines(Totseq_file);
                    
                    //Random rndReads = new Random();
                    long totReads = 0;
                    int readCounting = 0;
                    int readNo = 0;
                    int record = 0;
                    //MessageBox.Show(Totseq_lines.Length.ToString());
                    for (int i = 1; i < Totseq_lines.Length; i++)
                    //for (int i = 1; i < 3; i++)  //added for test **********************************************
                    {
                        //MessageBox.Show("start="+i);
                        string Totseq_oneline = Totseq_lines[i];
                        string[] Totseq_lineitems = Totseq_oneline.Split('\t');
                        int readlen = Convert.ToInt32(readLenTxt.Text);
                        int readC = Convert.ToInt32(Totseq_lineitems[14]); //##############
                        string totSeq = Totseq_lineitems[17].ToString();  //##############
                        ShortDnaSequence totDseq=new ShortDnaSequence(totSeq);
                        ShortDnaSequence tot100Fseq = (ShortDnaSequence)totDseq.SubSequence(0, 100);
                        ShortDnaSequence tot100Rseq = (ShortDnaSequence)totDseq.SubSequence(totSeq.Length-100,totSeq.Length);
                        tot100Rseq.RevComp();
                        double random_no = Tsp_rnd.NextDouble();
                        
                        int recordSeq = 0;
                        if (fq.Records.Count <= record) record = 0;
                        FastQRecord fqR = fq.Records[record];
                        
                        for (int rc = 0; rc < readC; rc++)
                        {
                            ShortDnaSequence fwdSeq = new ShortDnaSequence();
                            ShortDnaSequence revSeq = new ShortDnaSequence();
                            totDseq = new ShortDnaSequence(totSeq);
                            Random rnd = new Random();
                            //string errorReport = "";
                            for (int k = 0; k < 100 /*totSeq.Length*/; k++) //##############
                            {
                                recordSeq++;
                                if (recordSeq >= 100) //##############
                                {
                                    recordSeq = 0;
                                    record++;
                                    //if (record >= 24) record = 0;
                                    if (fq.Records.Count <= record) record = 0;
                                    fqR = fq.Records[record];
                                }
                                //MessageBox.Show( fqR.Sequence[k].ToString() + " and  " + fqR.Qualities[k].ToString());
                                do
                                {
                                    if (fq.Records.Count <= record) record = 0;
                                    fqR = fq.Records[record];
                                    fqR.TrimBBB();
                                    record++;
                                    //MessageBox.Show("I am here and the record is =" + record + "  and the quality.length is = " + fqR.Qualities.Length);
                                } while (fqR.Qualities.Length <= 100);

                                //MessageBox.Show("I am outside the do loop");
                                string header=Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5];
                                double error_prob = FastQRecord.QualityToProbability(fqR.Qualities[k]);
                                double rndDouble=rnd.NextDouble();
                                if (rndDouble < error_prob)
                                {
                                    //MessageBox.Show(rndDouble + "  <  " + error_prob);
                                    if (totDseq.GetNucleotide(k) == 'A' || totDseq.GetNucleotide(k) == 'a')
                                    {
                                        totDseq.SetNucleotide(k, 'C');
                                        errorOutput.WriteLine(header + "\t" + (rc+1) + "\t" + (k+1) + "\t" + "A" + "\t" + "C");
                                        //if(errorReport.Length>=1)
                                        //errorReport = errorReport + "," + header + "-" + rc + "-" + k + "-" + "A->C";
                                        //else errorReport = header + "-" + rc + "-" + k + "-" + "A->C";
                                    }
                                    else if (totDseq.GetNucleotide(k) == 'C' || totDseq.GetNucleotide(k) == 'c')
                                    {
                                        totDseq.SetNucleotide(k, 'A');
                                        errorOutput.WriteLine(header + "\t" + (rc + 1) + "\t" + (k + 1) + "\t" + "C" + "\t" + "A");
                                        //if (errorReport.Length >= 1)
                                        //    errorReport = errorReport + "," + header + "-" + rc + "-" + k + "-" + "C->A";
                                        //else errorReport = header + "-" + rc + "-" + k + "-" + "C->A";
                                    }
                                    else if (totDseq.GetNucleotide(k) == 'G' || totDseq.GetNucleotide(k) == 'g')
                                    {
                                        totDseq.SetNucleotide(k, 'T');
                                        errorOutput.WriteLine(header + "\t" + (rc + 1) + "\t" + (k + 1) + "\t" + "G" + "\t" + "T");
                                        //if (errorReport.Length >= 1)
                                        //    errorReport = errorReport + "," + header + "-" + rc + "-" + k + "-" + "G->T";
                                        //else errorReport = header + "-" + rc + "-" + k + "-" + "G->T";
                                    }
                                    else if (totDseq.GetNucleotide(k) == 'T' || totDseq.GetNucleotide(k) == 't')
                                    {
                                        totDseq.SetNucleotide(k, 'G');
                                        errorOutput.WriteLine(header + "\t" + (rc + 1) + "\t" + (k + 1) + "\t" + "T" + "\t" + "G");
                                        //if (errorReport.Length >= 1)
                                        //    errorReport = errorReport + "," + header + "-" + rc + "-" + k + "-" + "T->G";
                                        //else errorReport = header + "-" + rc + "-" + k + "-" + "T->G";
                                    }
                                    errorcount++;
                                }

                            }
                            if (readlen >= Totseq_lineitems[17].Length) //##############
                            {
                                fwdSeq = new ShortDnaSequence(totDseq);
                                revSeq = new ShortDnaSequence(totDseq);
                                revSeq.RevComp();
                            }
                            else if (readlen < Totseq_lineitems[17].Length) //##############
                            {
                                fwdSeq = (ShortDnaSequence)totDseq.SubSequence(0, 100);
                                revSeq = (ShortDnaSequence)totDseq.SubSequence((totSeq.Length-100), 100);
                                revSeq.RevComp();
                            
                            }
                        //readCount = rndReads.Next(Reads_in_million);
                            readCounting = Convert.ToInt32(Totseq_lineitems[14]); //##############

                        //totReads = totReads + (2 * readCount);
                        totReads = (totReads + readCounting) / 2;
                        //MessageBox.Show(readCount.ToString() + "and " + totReads.ToString());  
                        if (totReads <= Reads_in_million) readNo = readCounting;
                        else readNo = 0; // 
                        FastQRecord rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "\t" + readNo /* + "\t"+ errorReport*/ ), fwdSeq.ToString(), fqR.Qualities);
                        output4.WriteLine(rec1.ToString(64));
                        FastQRecord rec2 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "\t" + readNo /*+ "\t" + errorReport*/), revSeq.ToString(), fqR.Qualities);
                        output5.WriteLine(rec2.ToString(64));
                        //MessageBox.Show("End=" + i);
                       
                    }
                        //byte[] B=new byte[100];
                        //for (int b = 0; b < 100; b++)
                        //{
                        //    B[b] =2;
                        //}
                        //if (readC>=1)
                        //{
                        //    FastQRecord rec3 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "\t" + readC), tot100Fseq.ToString(), B);
                        //    outputF.WriteLine(rec3.ToString(64));
                        //    FastQRecord rec4 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "\t" + readC), tot100Rseq.ToString(), B);
                        //    outputR.WriteLine(rec4.ToString(64)); 
                        //}
                        

                        record++;
                        //if (record >= 24) record = 0;
                        if (fq.Records.Count == record) record = 0;
                }
                    //MessageBox.Show(totReads.ToString()); 
                    output4.Close();
                    output5.Close();
                    errorOutput.Close();
                    outputF.Close();
                    outputR.Close();
                    MessageBox.Show(errorcount.ToString());
                    

                }

            }
            //result.Close();
             
            MessageBox.Show("End of Run!!"); 
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //string[] N1;
            //List<string> N2=new List<string>();
            //SaveFileDialog sfd = new SaveFileDialog();
            //if (sfd.ShowDialog() != DialogResult.OK) return;
            //var output2 = sfd.FileName.OpenWrite();
            ////var output2 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_ME19.txt")).OpenWrite();
            //output2.WriteLine("T20_1" + "\t" + "T20_2");
            //N1=CreateRandomN("ACGT", 20,10);
            ////N2 = CreateRandomN("ACGT", 20);
            //FastQFile ff = new FastQFile();
            //Random rnd = new Random();
            
            //for (int i = 0; i < 100; i++) 
            //{
            //    //ff.Records[i] = N2.Add(N1[i]);
            //    //N2.
            //    //output2.WriteLine(N1[i]); 
            //    output2.WriteLine(rnd.Next(1,1000));
            //}

            //output2.Close(); 
            //MessageBox.Show("End of Run!!");
            //FastaFile ff = new FastaFile();
            //FastaRecord fr = new FastaRecord();
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            var output1 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_max2_connections.fa")).OpenWrite();
            string[] lines = System.IO.File.ReadAllLines(ofd1.FileName);
            DnaSequence ds2 = new ShortDnaSequence();
            string header2 = "";
            int countseq = 0;
            string N5="NNNNN";
            for (int i = 1; i < lines.Length-1; i++)
            {
                string firstLine = lines[i];
                string secondLine = lines[i+1];
                string[] firstlineItems = firstLine.Split('\t');
                string[] secondlineItems = secondLine.Split('\t');
                int start1 = Convert.ToInt32(firstlineItems[0]);
                int start2 = Convert.ToInt32(secondlineItems[0]);
                DnaSequence ds1 = new ShortDnaSequence(firstlineItems[12]);
                ShortDnaSequence ds1F_sub = (ShortDnaSequence)ds1.SubSequence(0, 100);
                ShortDnaSequence ds1R_sub = (ShortDnaSequence)ds1.SubSequence(ds1.Count-100,ds1.Count);
                //ds1.Append(firstlineItems[7]);
                //string header1 = firstlineItems[0] + "_" + firstlineItems[1] + "_" + firstlineItems[2] + "_" + firstlineItems[3] + "_" + firstlineItems[4] + "_" + firstlineItems[5] + "_" + firstlineItems[6];
                string header1 = firstlineItems[0] + "_"  + firstlineItems[4] + "_" + firstlineItems[5];
                if (ds2==null)
                {
                    ds2 = new ShortDnaSequence(ds1F_sub);
                    ds2.Append(N5);
                    ds2.Append(ds1R_sub);
                    header2 = header1;
                }
                if (i>=2 && (start1+1) == start2)
                {
                    
                    if (countseq>=1)
                    {
                        ds2.Append(ds1F_sub.SubSequence(9));
                        ds2.Append(N5);
                        ds2.Append(ds1R_sub);
                        header2 = header2 + " & " + header1;    
                    }
                    else
                    {
                        ds2 = new ShortDnaSequence(ds1F_sub);
                        ds2.Append(N5);
                        ds2.Append(ds1R_sub);
                        header2 = header1;       
                    }
                    countseq++;
                    
                    //ds2.Append(secondlineItems[8]);
                    //ds2.Append(secondlineItems[9]);
                }
                else if (i >= 2 && (start1 + 1) != start2)
                {
                    if (countseq >= 1)
                    {
                        ds2.Append(ds1F_sub.SubSequence(9));
                        ds2.Append(N5);
                        ds2.Append(ds1R_sub);
                        header2 = header2 + " & " + header1;
                    }
                    FastaRecord rec1 = new FastaRecord(header2, ds2);
                    output1.Write(rec1.ToString());
                    ds2 = null;
                    header2 = "";
                    countseq = 0;
                }
                else if (i == 1 && (start1 + 1) != start2)
                {
                    //ds2.Append(ds1);
                    //header2 = header1;
                    //ds1.Append(firstlineItems[9]);
                    ds2 = new ShortDnaSequence(ds1F_sub);
                    ds2.Append(N5);
                    ds2.Append(ds1R_sub);
                    FastaRecord rec1 = new FastaRecord(header1, ds2);
                    output1.Write(rec1.ToString());
                    //ds2 = null;
                    //header2 = "";
                }
                else if (i == 1 && (start1 + 1) == start2)
                {
                    ds2.Append(ds1F_sub);
                    ds2.Append(N5);
                    ds2.Append(ds1R_sub);
                    header2 = header1;
                    
                }
            }
            
            output1.Close();
            MessageBox.Show("End of Run!");
            
        }

       

        private void UnConnButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            var output1 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_max2_connections.txt")).OpenWrite();
            output1.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "Seq" + "\t" + "Common Fragment 9L" + "\t" + "ME19L" + "\t" + "ME19R" + "\t" + "Common Fragment 9R" + "\t" + "Total Seq" + "\t" + "Read Count" + "\t" + "CF_9L_count");
            string[] lines = System.IO.File.ReadAllLines(ofd1.FileName);
            string[] CF9L_Array = new string[lines.Length];
            string[] CF9R_Array = new string[lines.Length];
            for (int i = 1; i < lines.Length; i++)
            {
                string firstLine = lines[i];
                string[] firstlineItems = firstLine.Split('\t');
                CF9L_Array[i] = firstlineItems[8];
                CF9R_Array[i] = firstlineItems[11];
            }
            for (int i = 1; i < CF9L_Array.Length; i++)
            {
                int CF9LCount = 0;
                for (int j = 1; j < lines.Length; j++)
                {
                    if (CF9L_Array[i] == CF9L_Array[j] || CF9L_Array[i] == CF9R_Array[j])
                    {
                        CF9LCount++;
                        if (CF9LCount >= 3)
                            break;
                    }
                }
                if (CF9LCount <= 2)
                {
                    output1.WriteLine(lines[i] + "\t " + CF9LCount.ToString());
                }

            }


            output1.Close();
            MessageBox.Show("End of Run!");
        }

        private void ButtonErrorCorr_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            var output = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_errorCorrected.fq")).OpenWrite();


            string[] lines = System.IO.File.ReadAllLines(ofd1.FileName);
            //string[] seqLines = new string[lines.Length / 4];
            string[] headerLines = new string[lines.Length / 4];
            ShortDnaSequence seq = new ShortDnaSequence();
            ShortDnaSequence Array32bp=new ShortDnaSequence();

           
            int seqCount = 0;
            ulong score = 0;
            //ulong de = 0;
            
            for (int i = 0; i < lines.Length; i = i + 4)
            {
                headerLines[seqCount] = lines[i];
                string errorScore=lines[i+3];
                string[] HLItems = lines[i].Split('\t');
                int readCount = Convert.ToInt32(HLItems[1]);
                string resultLine = "";
                Dictionary<ulong, string> d = new Dictionary<ulong, string>();
                Dictionary<ulong, int> d1 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d2 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d3 = new Dictionary<ulong, int>();
                Dictionary<ulong, int> d4 = new Dictionary<ulong, int>();
                for (int ij = 0; ij < (readCount*4); ij = ij + 4)
                {

                    seq = new ShortDnaSequence(lines[i + 1]);
                    //MessageBox.Show(seq.ToString());
                    int seqfrag = 0;
                    int deCount = 0;
                    //for (int j = 0; j < 4; j++)
                    //{
                    //for dictionary d1*********************
                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    
                    if (d1.ContainsKey(score) == true)
                    {
                        d1.TryGetValue(score, out deCount);
                        deCount++;
                        d1.Remove(score);
                        d1.Add(score, deCount);
                        deCount = 0;
                           
                    }
                    else d1.Add(score, deCount + 1);

                    //for dictionary d2*********************
                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d2.ContainsKey(score) == true)
                    {
                        d2.TryGetValue(score, out deCount);
                        deCount++;
                        d2.Remove(score);
                        d2.Add(score, deCount);
                        deCount = 0;

                    }
                    else d2.Add(score, deCount + 1);

                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d3.ContainsKey(score) == true)
                    {
                        d3.TryGetValue(score, out deCount);
                        deCount++;
                        d3.Remove(score);
                        d3.Add(score, deCount);
                        deCount = 0;

                    }
                    else d3.Add(score, deCount + 1);

                    Array32bp = (ShortDnaSequence)seq.SubSequence(seqfrag, 32);
                    //seqfrag = seqfrag + 32;
                    score = Array32bp.ToIndex();
                    if (!d.ContainsKey(score)) d.Add(score, Array32bp.ToString());
                    if (d4.ContainsKey(score) == true)
                    {
                        d4.TryGetValue(score, out deCount);
                        deCount++;
                        d4.Remove(score);
                        d4.Add(score, deCount);
                        deCount = 0;

                    }
                    else d4.Add(score, deCount + 1);
                        
                    //}
                    //MessageBox.Show("value of i=" + i);
                    i = i + 4;
                }
                //output1.WriteLine(d1.Values);
                //output1.Close();
                //var sortedD1 = (from entry in d1 orderby entry.Value ascending select entry).ToDictionary(pair => pair.Key, pair => pair.Value);
                var sortedd1 = d1.OrderByDescending (x1 => x1.Value);
                var sortedd2 = d2.OrderByDescending(x2 => x2.Value);
                var sortedd3 = d3.OrderByDescending(x3 => x3.Value);
                var sortedd4 = d4.OrderByDescending(x4 => x4.Value);
                string tempseq="";
                var seq1=sortedd1.ElementAt(0);
                d.TryGetValue(seq1.Key,out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq2 = sortedd2.ElementAt(0);
                d.TryGetValue(seq2.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq3 = sortedd3.ElementAt(0);
                d.TryGetValue(seq3.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                var seq4 = sortedd4.ElementAt(0);
                d.TryGetValue(seq4.Key, out tempseq);
                resultLine = resultLine + tempseq;
                tempseq = "";
                //MessageBox.Show(resultLine);
                //foreach (var item in sortedd1)
                //{
                //    MessageBox.Show("key=" + (item.Key).ToString());
                //    MessageBox.Show("value=" + (item.Value).ToString());
                //} 

                output.WriteLine(headerLines[seqCount]);
                output.WriteLine(resultLine);
                output.WriteLine("+");
                output.WriteLine(errorScore);
                seqCount++;
                i = i - 4;
            }
            output.Close();
            MessageBox.Show("End of Run!");
            
        }

        private void CompErrorbutton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd1 = new OpenFileDialog();
            if (ofd1.ShowDialog() != DialogResult.OK) return;
            OpenFileDialog ofd2 = new OpenFileDialog();
            if (ofd2.ShowDialog() != DialogResult.OK) return;
            FastQFile fq = FastQFile.Load(ofd2.FileName, 64);
            
            var output1 = (Path.Combine(Path.GetDirectoryName(ofd1.FileName), Path.GetFileNameWithoutExtension(ofd1.FileName) + "_errorReport.txt")).OpenWrite();
            output1.WriteLine("Header" + "\t" + "readNo" + "\t" + "PositionAt" + "\t" + "From" + "\t" + "To" + "\t" + "ErrorReport" + "\t" + "Total no of reads");
            string[] Errorlines = System.IO.File.ReadAllLines(ofd1.FileName);
            //string[] ErrorCorrlines = System.IO.File.ReadAllLines(ofd2.FileName);
            
            string errorReport = "";
            //string[] errorHead2 = fq.Records[0].Header.Split('\t');
            //for (int j = 0; j < ErrorCorrlines.Length; j++)
            //{
            //   FastQRecord fr = fq.Records[j];    
            //}
            for (int i = 0; i <Errorlines.Length; i++)
            {
                string[] errorHead =Errorlines[i].Split('\t');
                string[] errorHead2;
                int fqRecord = -1;
                
                do
                {
                    fqRecord++;
                    errorHead2=fq.Records[fqRecord].Header.Split('\t');
                } while (errorHead[0]!= errorHead2[0]);
                MessageBox.Show(errorHead[0] + "and " + errorHead2[0]);
                ShortDnaSequence errorseq = new ShortDnaSequence(fq.Records[fqRecord].Sequence);
                char charPosition1=errorseq.GetNucleotide(Convert.ToInt64(errorHead[2])-1);
                char charPosition2 = Convert.ToChar(errorHead[3]);
                if (charPosition1 == charPosition2)
                {
                    errorReport = "Error Corrected";
                }
                else errorReport = "Error not corrected";
                output1.WriteLine(Errorlines[i].ToString() + "\t" /*+ charPosition1 + "\t" + charPosition2 + "\t"*/ + errorReport + "\t" + errorHead2[1]);
                errorHead2 = null;
                
            }
            output1.Close();
            MessageBox.Show("End of Run!");


        }

       
            

        

       
    }
}
