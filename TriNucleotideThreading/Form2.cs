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

            var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) +textBox1.Text +  "_lines.txt")).OpenWrite();

            
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            
            for (int i = 0; i < Convert.ToInt32((textBox1.Text))  ; i++) 
            {
                string oneLine = lines[i];
                output.WriteLine(lines [i]);
            }
            output.Close();
            MessageBox.Show("End of Run!!"); 
        }
    }
}
