using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Linnarsson.Mathematics;
using Linnarsson.Dna;
using Linnarsson.Utilities;




namespace TriNucleotideThreading
{
    public partial class triNucleotide : Form
    {
        public triNucleotide()
        {
            InitializeComponent();
        }

        private void triNucleotide_Load(object sender, EventArgs e)
        {
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int triNId = 0;
            char triN = ' ';
            int triNLen =Convert.ToInt32 (textBox1.Text);
            if (radioButton1.Checked == true) triN = 'C';
            else if (radioButton2.Checked == true) triN = 'G';
            else if (radioButton3.Checked == true) triN = 'T';
            else if (radioButton4.Checked == true) triN = 'A';
            
            MessageBox.Show("Select the FASTA sequence files ");
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK) return;
            MessageBox.Show("Select the location where you want to save your output file ");
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() != DialogResult.OK) return;
            var result = sfd.FileName.OpenWrite();
            result.WriteLine("ID" + "\t" + "Chromosome" + "\t" + "TriNucleotide Thread" + "\t" + "TNT Start" + "\t" + "Primer 1" + "\t" + "P1 Start" + "\t" + "P1 Tm" + "\t" + "TNT revcom" + "\t" + "Primer 2" + "\t" +"P2 Start" +"\t" + "P2 Tm" + "\t" + "Strand");
            //FastaFile ff = FastaFile.Load(ofd.FileName);
            //MessageBox.Show("Start");
            //MessageBox.Show("END");
            long totalLength=0;
            //char[] tn;
            foreach (string fname in ofd.FileNames)
            {
                FastaFile ff = FastaFile.Load(fname);
                foreach (FastaRecord rec in ff.Records)
                {
                    totalLength += rec.Sequence.Count;
                    MessageBox.Show(totalLength.ToString());  
                    DnaSequence ds = new DnaSequence(rec.Sequence);
                    //DnaSequence ds_revcom = new DnaSequence(rec.Sequence);
                    //ds_revcom.RevComp();
                    
                    long dss_start=0;
                    do 
                    {
                        DnaSequence dss= ds.SubSequence(dss_start , triNLen);
                        bool thread = true;
                        for (int j = 0; j <= triNLen; j++)
                        {
                            if (dss.GetNucleotide(j) == triN || dss.GetNucleotide(j)== 'N')
                            {
                                dss_start = dss_start + j + 1;
                                thread = false;
                                break;
                            }
                             
                        }
                        if (thread != false)
                        {
                            
                            double temp = 0;
                            //MessageBox.Show(dss.ToString());
                            int len=18;
                            DnaSequence dssP = ds.SubSequence(dss_start+triNLen, len);
                            for (int i = 0; i <= dssP.Count ; i++)
                            {
                                if (dssP.GetNucleotide(i) == 'N')
                                {
                                    dss_start = dss_start + triNLen + i;
                                    thread = false;
                                    break;
                                      
                                }
                            }
                            if (dssP.GetNucleotide(0) == triN || dssP.GetNucleotide(1) == triN || dssP.GetNucleotide(2) == triN)
                            {
                                dss_start = dss_start + 3;
                                thread = false;
                                //break;

                            }

                            if (thread != false)
                            {
                                TmCalculator tm = new TmCalculator();
                                temp = tm.GetTm(dssP, 0.00000005, 0.00000005, 0.05);
                                while (temp < 50)
                                {
                                    len++;
                                    if (len == 25) break;
                                    dssP = ds.SubSequence(dss_start + triNLen, len);
                                    temp = tm.GetTm(dssP, 0.00000005, 0.00000005, 0.05);
                                }
                            
                                int len2 = 22;
                                DnaSequence dss_rev = new DnaSequence(dss.ToString ());
                                dss_rev.RevComp();
                                DnaSequence dssP2 = dss_rev.SubSequence(len2,triNLen);
                           
                                TmCalculator tm2 = new TmCalculator();
                                double temp2 = tm2.GetTm(dssP2, 0.00000005, 0.00000005, 0.05);
                                while (temp2 < 50)
                                {
                                    len2--;
                                    if (len2 == 15) break;
                                    dssP2 = dss_rev.SubSequence(len2 , triNLen);
                                    temp2 = tm2.GetTm(dssP2, 0.00000005, 0.00000005, 0.05);
                                }
                                if ((temp >= 50 && temp <= 60) && (temp2 >= 50 && temp2 <= 60))
                                {
                                
                                    triNId = triNId + 1;
                                    result.WriteLine(triNId.ToString() + "\t" + Path.GetFileNameWithoutExtension(ofd.FileName) + "\t" + dss.ToString() + "\t" + dss_start + "\t" + dssP.ToString() + "\t" + (dss_start + triNLen) + "\t" + temp + "\t" + dss_rev.ToString() + "\t" + dssP2.ToString() + "\t" + (dss_start) + "\t" + temp2 + "\t" + "+");
                                }
                                dss_start = dss_start + triNLen + (dssP.ToString ()).Length  ;
                            }
                        }
                        //MessageBox.Show ("i am outside");
                        //tn = null;
                        
                    } while( (dss_start +triNLen) <= totalLength ) ;

                    DnaSequence ds_revcom = new DnaSequence(rec.Sequence);
                    ds_revcom.RevComp();
                    //ds_revcom.Complement(); 
                    dss_start = 0;
                    do
                    {
                        triN = ' ';
                        if (radioButton1.Checked == true) triN = 'G';
                        else if (radioButton2.Checked == true) triN = 'C';
                        else if (radioButton3.Checked == true) triN = 'A';
                        else if (radioButton4.Checked == true) triN = 'T';
                        DnaSequence dss = ds_revcom.SubSequence(dss_start, triNLen);
                        bool thread = true;
                        for (int j = 0; j <= triNLen; j++)
                        {
                            if (dss.GetNucleotide(j) == triN || dss.GetNucleotide(j) == 'N')
                            {
                                dss_start = dss_start + j + 1;
                                thread = false;
                                break;
                            }
                        }
                        if (thread != false)
                        {
                            double temp = 0;
                            //MessageBox.Show(dss.ToString());
                            int len = 18;
                            DnaSequence dssP = ds_revcom.SubSequence(dss_start + triNLen, len);
                            for (int i = 0; i <= dssP.Count; i++)
                            {
                                if (dssP.GetNucleotide(i) == 'N')
                                {
                                    dss_start = dss_start + triNLen + i;
                                    thread = false;
                                    break;

                                }
                            }
                            if (dssP.GetNucleotide(0) == triN || dssP.GetNucleotide(1) == triN || dssP.GetNucleotide(2) == triN)
                            {
                                dss_start = dss_start + 3;
                                thread = false;
                                //break;

                            }
                            if (thread != false)
                            {
                                TmCalculator tm = new TmCalculator();
                                temp = tm.GetTm(dssP, 0.00000005, 0.00000005, 0.05);
                                while (temp < 50)
                                {
                                    len++;
                                    if (len == 25) break;
                                    dssP = ds_revcom.SubSequence(dss_start + triNLen, len);
                                    temp = tm.GetTm(dssP, 0.00000005, 0.00000005, 0.05);
                                }
                            
                                int len2 = 22;
                                DnaSequence dss_rev = new DnaSequence(dss.ToString());
                                dss_rev.RevComp();
                                DnaSequence dssP2 = dss_rev.SubSequence(len2,triNLen);
                                TmCalculator tm2 = new TmCalculator();
                                double temp2 = tm2.GetTm(dssP2, 0.00000005, 0.00000005, 0.05);
                                while (temp2 < 50)
                                {
                                    len2--;
                                    if (len2 == 15) break;
                                    dssP2 = dss_rev.SubSequence(len2,triNLen);
                                    temp2 = tm2.GetTm(dssP2, 0.00000005, 0.00000005, 0.05);
                                }
                                dss_start = dss_start + triNLen + (dssP.ToString()).Length;
                                if ((temp >= 50 && temp <= 60) && (temp2 >= 50 && temp2 <= 60))
                                {
                                    triNId = triNId + 1;
                                    result.WriteLine(triNId + "\t" + Path.GetFileNameWithoutExtension(ofd.FileName) + "\t" + dss.ToString() + "\t" + (totalLength - dss_start) + "\t" + dssP.ToString() + "\t" + (totalLength - dss_start + triNLen) + "\t" + temp + "\t" + dss_rev.ToString() + "\t" + dssP2.ToString() + "\t" + (totalLength - (dss_start - (dssP.ToString()).Length)) + "\t" + temp2 + "\t" + "-");
                                }
                            }
                            
                            
                        }
                        //MessageBox.Show ("i am outside");
                        //tn = null;

                    } while ((dss_start + triNLen) <= totalLength);


                }
            }
            MessageBox.Show("End of run!!");
            result.Close(); 
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            button1.Enabled = true; 
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            button1.Enabled = true; 
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            button1.Enabled = true; 
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            button1.Enabled = true; 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string fname = "";
            if (radioButton5.Checked == true) fname = "P1_FastQ.FQ";
            else if (radioButton6.Checked == true) fname = "P2_FastQ.FQ";
             
            MessageBox.Show("Select the TriNucleotide sequence files ");
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK) return;
            var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) + fname )).OpenWrite();
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            //output.WriteLine("selectorSeq" + "\t" + "selectorLen" + "\t" + "SelectorChr" + "\t" + "selectorStart" + "\t" + "Left Arm" + "\t" + "LA_Tm " + "\t" + "Right Arm " + "\t" + "RA_Tm " + "\t" + "%GC" + "\t" + "G/C/A/T Len" + "\t" + "Left Dis." + "\t" + "Right Dis." + "\t" + "Poly Type" + "\t" + "ConcatRA_LA" + "\t" + "Reverse Complement");
            for (int i = 1; i < lines.Length; i++)
            { 
                string oneLine = lines[i];
                string[] lineItems = oneLine.Split('\t');
                //string aa="abcde";
                //int b=aa.Count(); 
                //DnaSequence P2_RevSeq = new DnaSequence(lineItems[6]);
                //P2_RevSeq.RevComp(); 
                if (radioButton5.Checked == true)
                {
                    if (lineItems[11].ToString() == "+")
                    {
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[1] + "_" + lineItems[5] + "_P1" + lineItems[11]), lineItems[4], new string('b', (lineItems[4].Count())));
                        output.WriteLine(rec1.ToString());
                    }
                    else 
                    {
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[1] + "_" + lineItems[3] + "_P1" + lineItems[11]), lineItems[4], new string('b', (lineItems[4].Count())));
                        output.WriteLine(rec1.ToString());
 
                    }
                }
                else if (radioButton6.Checked == true)
                {
                    string strand="";
                    if (lineItems[11].ToString() == "+") 
                    { 
                        strand = "-";
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[1] + "_" + lineItems[9] + "_P2" + strand), lineItems[8], new string('b', (lineItems[8].Count())));
                        output.WriteLine(rec1.ToString());
                    }
                    else if (lineItems[11].ToString() == "-")
                    {
                        strand = "+";
                        FastQRecord rec1 = new FastQRecord((lineItems[0] + "_" + lineItems[1] + "_" + lineItems[9] + "_P2" + strand), lineItems[8], new string('b', (lineItems[8].Count())));
                        output.WriteLine(rec1.ToString());
                    }
                    
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

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            
            //if(textBox1.) MessageBox.Show("Please enter numaric values only.");
                
        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        
    }
}
