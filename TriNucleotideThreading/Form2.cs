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
    }
}
