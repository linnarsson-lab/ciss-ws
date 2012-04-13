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
using System.IO;

namespace TriNucleotideThreading
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a txt file to read";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            double startNo = Convert.ToInt64(StarttextBox2.Text);
            var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) +textBox1.Text +  "_lines.txt")).OpenWrite();

            
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            
            for (double i = startNo; i < Convert.ToInt64((textBox1.Text))  ; i++) 
            {
                //string oneLine = lines[(i)];
                output.WriteLine(lines[(int)i]);
            }
            output.Close();
            MessageBox.Show("End of Run!!"); 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a fasta file to read";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(">"))
                {
                    //MessageBox.Show(lines[i].ToString());
                    string filename = lines[i].TrimStart('>').ToString();
                    //MessageBox.Show(filename);
                    var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName),  filename + ".fa")).OpenWrite();
                    output.WriteLine(lines[i].ToString());
                    i++;
                    do
                    {
                        output.WriteLine(lines[i].ToString());
                        i++;
                        if (i >= lines.Length) break;
                    } while (!lines[i].StartsWith(">"));
                    output.Close();
                    i--;

                }
            }
            MessageBox.Show("end of run!");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a GenBank file to read";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            string[] gbkFilenameLine = System.IO.File.ReadAllLines(ofd.FileName);
            var refFlatoutput = (Path.Combine(Path.GetDirectoryName(ofd.FileName)) + '\\' + "refFlat" + ".txt").OpenWrite();
            char strand='+';
            for (int i = 0; i < gbkFilenameLine.Length; i++)
            {
                string[] gbkFilename = gbkFilenameLine[i].Split('\t');
                string fileNamePath = (Path.Combine(Path.GetDirectoryName(ofd.FileName)) +'\\' + gbkFilename[7]);
                //MessageBox.Show(fileNamePath);
                GenbankFile gf = GenbankFile.Load(fileNamePath);
                GenbankRecord gr = gf.Records[0];
                GenbankFeature feature = gr.Features[0];
                if (feature.Strand.ToString()!="Forward")
                {
                    strand='-';
                }
                
                var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName)) +'\\' + "fastaFiles" +'\\' + gr.LocusName.ToString() + ".fa").OpenWrite();
                output.WriteLine(">" + gr.LocusName.ToString());
                output.WriteLine(gr.Sequence.ToString());
                output.Close();
                refFlatoutput.WriteLine(gr.LocusName.ToString() + '\t' + gr.Accession.ToString() + '\t' + gr.LocusName + '\t' + strand.ToString() + '\t' + "1" + '\t' + gr.SequenceLength.ToString() + '\t' + "1" + '\t' + gr.SequenceLength.ToString() + '\t' + "1" + '\t' + "1" + '\t' +gr.SequenceLength.ToString());
                //MessageBox.Show(gr.LocusName.ToString() + " and " + gr.SequenceLength.ToString());
                
            }
            refFlatoutput.Close();
            MessageBox.Show("End of Run!!");
            
           

        }
    }
}
