using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Linnarsson.Dna;
using Linnarsson.Utilities;
using Linnarsson.Mathematics;
using System.IO;


namespace Junction_lables_simulator_console
{
    public class Simulator
    {
        public int num_molecule { get; set; }
        public long molecule_len { get; set; }
        public double tsp_probability { get; set; }
        public int Discard_percentage { get; set; }
        public int readlen { get; set; }
        public string refSeqFilepath {get; set; } // in previous version ofd1
        public string snpfilepath { get; set; }  // in previous version ofd2
        public string BuildFolder { get; set; } // new line added
        public int count_N {get; set;}
        public long molecule_id { get; set; }
        public long totalLength { get; set; }
        public long Reads_in_million { get; set; }
        public Simulator() 
        {
             count_N = 0;
             molecule_id = 1;
             totalLength=0;
             //num_molecule = 400;
             //BuildFolder = "Build_" + DateTime.Now.ToPathSafeString();
             //if (!Directory.Exists(BuildFolder)) Directory.CreateDirectory(BuildFolder);
            //foreach (string fname in ofd1)
            //{
            
            
        }
        public void analyze()
        {
            if (!File.Exists(refSeqFilepath))
            {
                Console.WriteLine("Reference .fa file not found at " + refSeqFilepath);
                return;
            }
            string fname =refSeqFilepath; // ***********************************new line added
            molecule_len = molecule_len * 1000;
            Reads_in_million = Reads_in_million * 1000000;
            FastaFile ff = FastaFile.Load(fname);
            foreach (FastaRecord rec in ff.Records)
            {
                totalLength += rec.Sequence.Count;
                if (num_molecule * molecule_len > totalLength)
                {
                    //MessageBox.Show("selection more than length.");
                    break;
                }
                DnaSequence ds1 = new DnaSequence(rec.Sequence); // to get data from 1st strand
                DnaSequence ds2 = new DnaSequence(rec.Sequence); // to get data from 1st strand and set SNPs and convert into 2nd strand 
                string[] lines = System.IO.File.ReadAllLines(snpfilepath);
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
                var output = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "molecules_info.txt")).OpenWrite();
                output.WriteLine("Molecule_id" + "\t" + "Strand" + "\t" + "Molecule Start" + "\t" + "Molecule End" + "\t" + "No of Ns" + "\t" + "Molecule Seq");
                long molecule_start = 0;

                Random chr_selection = new Random(); // selecting molecules from 2 strands randomly
                Random frag_selection = new Random(); // Selecting fragments randomly
                int[] chrno_frag_array = new int[num_molecule];
                for (int chr_frag = 0; chr_frag <= num_molecule; chr_frag++)
                {
                    //chrno_frag_array[chr_frag] = chr_selection.Next(1, 3);
                    if (chr_selection.Next(1, 3) == 1)
                    {
                        //MessageBox.Show("1"); 
                        //ds1.SubSequence(molecule_start, molecule_len);
                        // writing in a file which contains fragmented sequence information: the file contains following cols. Strand, seqStart, Stop and Sequence
                        DnaSequence ds1_tmp = ds1.SubSequence(molecule_start, molecule_len);
                        count_N = CountN(ds1_tmp, molecule_len);
                        output.WriteLine(("M" + molecule_id) + "\t" + "1" + "\t" + molecule_start + "\t" + (molecule_start + molecule_len) + "\t" + count_N + "\t" + ds1.SubSequence(molecule_start, molecule_len));
                        molecule_start = molecule_start + molecule_len;
                        molecule_id++;
                        count_N = 0;

                    }
                    else
                    {
                        DnaSequence ds2_tmp = ds2.SubSequence(molecule_start, molecule_len);
                        count_N = CountN(ds2_tmp, molecule_len);
                        DnaSequence ds2_rev = new DnaSequence(ds2.SubSequence(molecule_start, molecule_len));
                        //ds2_rev.RevComp();
                        output.WriteLine(("M" + molecule_id) + "\t" + "2" + "\t" + molecule_start + "\t" + (molecule_start + molecule_len) + "\t" + count_N + "\t" + ds2_rev.ToString());
                        molecule_start = molecule_start + molecule_len;
                        molecule_id++;
                        count_N = 0;
                    }

                }

                output.Close();

                // following code will generate a file with  sequence and transposons at each side of a sequence  
                string Molecules_file = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "molecules_info.txt"));
                var output2 = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_transposones.txt")).OpenWrite();
                output2.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "Seq" + "\t" + "ME19L" + "\t" + "ME19R");
                string[] Tsp_lines = System.IO.File.ReadAllLines(Molecules_file);
                Random Tsp_rnd = new Random();
                int Tsp_start = 0;
                int Tsp_count = 0;


                string Tsp_molecule_id = "";


                int Tsp_N = 0;
                int Tsp_len = 0;

                for (int i = 1; i < Tsp_lines.Length; i++)
                {
                    int first_Tsp = 0, second_Tsp = 0;
                    string Tsp_oneLine = Tsp_lines[i];
                    string[] Tsp_lineItems = Tsp_oneLine.Split('\t');
                    DnaSequence molecule_ds = new DnaSequence(Tsp_lineItems[5]);
                    DnaSequence Tsp_ds = new DnaSequence();
                    DnaSequence ME19L = new DnaSequence();
                    DnaSequence ME19R = new DnaSequence();
                    if ((((Convert.ToInt32(Tsp_lineItems[3])) - (Convert.ToInt32(Tsp_lineItems[2]))) / 2) <= Convert.ToInt32(Tsp_lineItems[4]))
                    {
                        goto Next_loop; // if more than hallf of the sequences are represented by N  
                    }
                    else
                    {
                        Tsp_start = Convert.ToInt32(Tsp_lineItems[2]);


                        //MessageBox.Show(Tsp_rnd.NextDouble().ToString() + "and" + (1 / tsp_probability).ToString ()); 

                        for (int j = 0; j < (Convert.ToInt32(Tsp_lineItems[3]) - Convert.ToInt32(Tsp_lineItems[2])); j++)
                        {
                            first_Tsp = second_Tsp;
                            if (Tsp_rnd.NextDouble() < 1 / tsp_probability)
                            {
                                // insert the transposon
                                second_Tsp = j;

                                if (Tsp_count != 0 && Tsp_molecule_id == Tsp_lineItems[0])
                                {
                                    //MessageBox.Show("I at Tsp_count=" + Tsp_count); 
                                    Tsp_len = second_Tsp - first_Tsp;
                                    Tsp_ds = molecule_ds.SubSequence(first_Tsp, Tsp_len);
                                    ME19L = ME19R;
                                    if (Tsp_ds.Count >= 19)
                                    {
                                        //ME19L = Tsp_ds.SubSequence(0, 19);
                                        ME19R = Tsp_ds.SubSequence(Tsp_ds.Count - 19, 19);
                                        Tsp_ds = molecule_ds.SubSequence(first_Tsp, (Tsp_len - 19));
                                    }
                                    else
                                    {
                                        //ME19L = Tsp_ds;
                                        ME19R = Tsp_ds;
                                    }
                                    Tsp_N = CountN(Tsp_ds, Tsp_len);
                                    //MessageBox.Show((Tsp_N / 2).ToString() + "and" + Tsp_len.ToString()); 
                                    if (Tsp_N <= Tsp_len / 2)
                                    {
                                        output2.WriteLine(Tsp_count - 1 + "\t" + Tsp_count + "\t" + Tsp_lineItems[0] + "\t" + Tsp_lineItems[1] + "\t" + (first_Tsp + Convert.ToInt64(Tsp_lineItems[2])) + "\t" + (second_Tsp + Convert.ToInt64(Tsp_lineItems[2])) + "\t" + Tsp_len + "\t" + Tsp_ds + "\t" + ME19L + "\t" + ME19R);
                                    }
                                    Tsp_count++;
                                    Tsp_molecule_id = Tsp_lineItems[0];
                                }
                                else if (Tsp_count == 0 || Tsp_molecule_id != Tsp_lineItems[0]) // || Tsp_molecule_id != Tsp_lineItems[0])
                                {
                                    if (Tsp_ds.Count >= 19)
                                    {
                                        ME19L = Tsp_ds.SubSequence(0, 19);
                                        Tsp_ds = Tsp_ds.SubSequence(19, (Tsp_ds.Count - 19));

                                        ME19R = Tsp_ds.SubSequence(Tsp_ds.Count - 19, 19);
                                    }
                                    else
                                    {
                                        ME19L = Tsp_ds;
                                        ME19R = Tsp_ds;
                                    }
                                    Tsp_count++;
                                    Tsp_molecule_id = Tsp_lineItems[0];
                                }

                            }
                        }
                    
                        }
                    Next_loop: ;
                    }
                    output2.Close();

                    // this code will introduce a 20bp random sequece at each side of a sequece
                    string Tsp_file = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_transposones.txt"));
                    var output3 = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_Tsp_rndN20.txt")).OpenWrite();
                    output3.WriteLine("Tsp_id1" + "\t" + "Tsp_id2" + "\t" + "Molecule_id" + "\t" + "Strand" + "\t" + "First Tsp" + "\t" + "Second Tsp" + "\t" + "Seq Len" + "\t" + "N1_20" + "\t" + "ME19L" + "\t" + "Seq" + "\t" + "ME19R" + "\t" + "N2_20" + "\t" + "Total Seq");
                    string[] TspN20_lines = System.IO.File.ReadAllLines(Tsp_file);
                    string[] N20 = CreateRandomN("ACGT", 20, (TspN20_lines.Length + 1));
                    int discart = 0;
                    for (int i = 1; i < TspN20_lines.Length; i++)
                    {
                        string TspN20_oneLine = TspN20_lines[i];
                        string[] TspN20_lineItems = TspN20_oneLine.Split('\t');
                        int per = TspN20_lines.Length * Discard_percentage / 100;
                        double random_no = Tsp_rnd.NextDouble();
                        //MessageBox.Show(discart.ToString() + "and" + per.ToString() + "and" + random_no.ToString() );
                        if (discart <= per && random_no > 0.5) //tsp_probability)
                        {
                            discart = discart + 1;
                        }

                        //if (Convert.ToInt32(TspN20_lineItems[6]) > 30 && Convert.ToInt32(TspN20_lineItems[6]) < 250)
                        else
                            output3.WriteLine(TspN20_lineItems[0] + "\t" + TspN20_lineItems[1] + "\t" + TspN20_lineItems[2] + "\t" + TspN20_lineItems[3] + "\t" + TspN20_lineItems[4] + "\t" + TspN20_lineItems[5] + "\t" + TspN20_lineItems[6] + "\t" + N20[i] + "\t" + TspN20_lineItems[8] + "\t" + TspN20_lineItems[7] + "\t" + TspN20_lineItems[9] + "\t" + N20[i + 1] + "\t" + N20[i] + TspN20_lineItems[8] + TspN20_lineItems[7] + TspN20_lineItems[9] + N20[i + 1]);
                    }

                    output3.Close();

                    // this code will generate a fastq file with random quality score
                    string Totseq_file = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_Tsp_rndN20.txt"));
                    var output4 = (Path.Combine(Path.GetDirectoryName(refSeqFilepath), Path.GetFileNameWithoutExtension(refSeqFilepath) + "_reads.fq")).OpenWrite();
                    string[] Totseq_lines = System.IO.File.ReadAllLines(Totseq_file);
                    Random rndReads = new Random();
                    long totReads = 0;
                    int readCount = 0;
                    int readNo = 0;
                    //char q; 
                    for (int i = 1; i < Totseq_lines.Length; i++)
                    {
                        string Totseq_oneline = Totseq_lines[i];
                        string[] Totseq_lineitems = Totseq_oneline.Split('\t');
                        //int readlen = Convert.ToInt32(readLenTxt.Text);
                        double random_no = Tsp_rnd.NextDouble();
                        //double quality_scr = 30; //-10 * Math.Log10(random_no/(1-random_no));
                        int[] quality_scr=new int[1000]; 
                        DnaSequence fwdseq = new DnaSequence(Totseq_lineitems[12]);
                        DnaSequence revSeq = new DnaSequence(Totseq_lineitems[12]);
                        revSeq.RevComp();
                        //double quality_scr = -10 * Math.Log10(0.05 / (1 - 0.05));
                        //MessageBox.Show(quality_scr.ToString()); 
                        if (readlen >= Totseq_lineitems[12].Length)
                        {
                            //fwdseq = Totseq_lineitems[12];
                            //revSeq = Reverse(Totseq_lineitems[12]);
                            quality_scr = new int[readlen];
                            for (int k = 0; k < Totseq_lineitems[12].Length; k++)
                            {
                                quality_scr[k] = 94;
 
                            }
                            
                        }
                        else if (readlen < Totseq_lineitems[12].Length)
                        {
                            //fwdseq = Fwd100(Totseq_lineitems[12]);
                            //revSeq = Reverse100(Totseq_lineitems[12]);
                            fwdseq = fwdseq.SubSequence(0, 100);
                            revSeq = revSeq.SubSequence(0, 100);
                            quality_scr = new int[100];
                            for (int k = 0; k < 100; k++)
                            {
                                quality_scr[k] = 94;

                            }
                            
                        }
                        readCount = rndReads.Next(1, 1000);
                        totReads = totReads + (2 * readCount);
                        if (totReads <= Reads_in_million) readNo = readCount;
                        else readNo = 1;
                        char q = ' ';
                        StringBuilder quality_scr_all = new StringBuilder();
                        for (int x = 0; x < quality_scr.Length; x++)
                        {
                            q=Convert.ToChar(quality_scr[x]);
                            quality_scr_all.Append(q);
                        }
                        
                        //FastQRecord rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), fwdseq.ToString(), quality_scr.ToString());
                        FastQRecord rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), fwdseq.ToString(), quality_scr_all.ToString());
                        output4.WriteLine(rec1.ToString());
                        rec1 = new FastQRecord((Totseq_lineitems[0] + "_" + Totseq_lineitems[1] + "_" + Totseq_lineitems[2] + "_" + Totseq_lineitems[3] + "_" + Totseq_lineitems[4] + "_" + Totseq_lineitems[5] + "_" + readNo), revSeq.ToString(), quality_scr_all.ToString());
                        output4.WriteLine(rec1.ToString());


                    }
                    //MessageBox.Show(totReads.ToString()); 
                    output4.Close();
                Console.WriteLine("End of run!!");
            }
                
            
        }
        public int CountN(DnaSequence ds, long ds_len) // counting no of Ns in a sequence
        {
            int N = 0;
            for (int i = 0; i <= ds_len; i++)
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
            int[] result_Array = new int[len];
            string[] results = new string[times];
            Random rnd = new Random();

            for (int j = 0; j < times; j++)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < len; i++)
                {
                    sb.Append(Element[rnd.Next(0, element_len)]);
                }
                results[j] = sb.ToString();
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
            for (int i = text.Length - 1; i >= text.Length - 100; i--)
            {
                builder.Append(text[i]);
            }

            return builder.ToString();
        }



    }
}
