using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Linnarsson.Utilities;
using Linnarsson.Dna;
using System.IO;

namespace IndranilsProject
{
    public partial class P53MseI : Form
    {
        public P53MseI()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            histogram.Clear();
            RestrictionEnzyme MseI = RestrictionEnzymes.MseI;
            //DnaSequence polyG = new DnaSequence("GGGGGGGGGG");
            //DnaSequence polyC = new DnaSequence("CCCCCCCCCC");
            //DnaSequence polyA = new DnaSequence("AAAAAAAAAA");
            //DnaSequence polyT = new DnaSequence("TTTTTTTTTT");

            MessageBox.Show("Select the FASTA sequence files for target genes, i.e. P53 etc.");
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            if (ofd.ShowDialog() != DialogResult.OK) return;
            MessageBox.Show("Select the location where you want to save your output file ");
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog() != DialogResult.OK) return;
            var result = sfd.FileName.OpenWrite();
            //char polyType = ' ';    // to get the type of poly repeats
            result.WriteLine("Sequence" + "\t" + "Seq length" + "\t" + "%GC" + "\t" + "G/C/A/T Length" + "\t" + "leftDistance" + "\t" + "rightDistance" + "\t" + "PolyType" + "\t" + "Chromosome Name" + "\t" + "Start Position in Chr");

            long totalLength = 0;
            int countFrags = 0;
            foreach (string fname in ofd.FileNames)
            {
                FastaFile ff = FastaFile.Load(fname);
                foreach (FastaRecord rec in ff.Records)
                {
                    List<int> MseISites = new List<int>();
                    totalLength += rec.Sequence.Count;
                    string ChrName = rec.HeaderLine; // assuming Chromosome name will be in the header line of the record


                    // Find all MseI sites
                    int offset = (int)rec.Sequence.Match(MseI.Sequence, 0);
                    do
                    {
                        MseISites.Add(offset);

                        offset = (int)rec.Sequence.Match(MseI.Sequence, offset + 1);

                    } while (offset > 0 && offset < rec.Sequence.Count);
                    
                    char polyType = '-';
                    int leftDistance = 0;
                    MessageBox.Show(MseISites.Count.ToString()); 
                    for (int m = 1; m < MseISites.Count;m++ )
                    {
                        Report(result, rec.Sequence.SubSequence(MseISites[m - 1] + 1, MseISites[m] - MseISites[m - 1]), leftDistance , polyType, ChrName, MseISites[m-1] );
                    }

                }
                Console.WriteLine("Fragments: " + countFrags.ToString() + " length: " + (totalLength / 1e6).ToString() + " Mbp");
                ff = null;
            }
            foreach (var kvp in histogram)
            {
                Console.WriteLine(kvp.Key + "\t" + kvp.Value);
            }
            result.Close();
            MessageBox.Show("Programme executed successfully. Please click the next button to calculate the Tm.");
        }

        SortedDictionary<int, int> histogram = new SortedDictionary<int, int>();
        public bool Report(StreamWriter result, DnaSequence seq, int leftDistance, char polyType, string ChrName, int OFFSET)
        {
            if (seq.Count < 75 || seq.Count > 600) return false;
            double perGC = seq.CountCases(IupacEncoding.GC) * 100 / seq.Count;
            if (perGC < 30 || perGC > 70) return false;
            result.WriteLine(seq.ToString() + "\t" + seq.Count + "\t" + perGC  + "\t" + "poly type not selected" + "\t" + "none" + "\t" + "not calculated" + "\t" + "-" + "\t" + ChrName + "\t" + (OFFSET - leftDistance + 1));
            return true;
        }

    }
}
