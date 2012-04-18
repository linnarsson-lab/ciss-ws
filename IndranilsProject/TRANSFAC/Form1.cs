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

namespace TRANSFAC
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select a TRANSFAC file to read";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
            var output = (Path.Combine(Path.GetDirectoryName(ofd.FileName), Path.GetFileNameWithoutExtension(ofd.FileName) + "_filtered.txt")).OpenWrite();
            output.WriteLine("Accession" + "\t" + "Secondary Accession No" + "\t" + "ID" + "\t" + "FactorName" + "\t" + "Synonyms" + "\t" + "Species");
            string Accession="";
            string SecAcc = "";
            string ID = "";
            string FactorName = "";
            string Synonyms = "";
            string Species = "";
            int recordCount = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string oneLine = lines[i];
                string[] lineItems = oneLine.Split(' ');
                //MessageBox.Show(oneLine + " " + lineItems.Length.ToString());
                if (lineItems[0] == "//")
                {
                    //MessageBox.Show("I am here inside the loop!");
                    recordCount = recordCount + 1;
                    if (recordCount >= 2)
                    {
                        output.WriteLine(Accession + "\t" + SecAcc + "\t" + ID + "\t" + FactorName + "\t" + Synonyms + "\t" + Species);
                        recordCount = 1;
                    }
                    Accession = "";
                    SecAcc = "";
                    ID = "";
                    FactorName = "";
                    Synonyms = "";
                    Species = "";
                    goto NextLoop;
                } 
                if (lineItems[0] == "AC") Accession = lineItems[2];
                if (lineItems[0] == "AS")
                {
                    if (SecAcc == "") SecAcc = lineItems[2];
                    else SecAcc = SecAcc + " ; " + lineItems[2];
                }
                if (lineItems[0] == "ID") ID = lineItems[2];
                if (lineItems[0] == "FA") FactorName = lineItems[2];
                if (lineItems[0] == "AS")
                {
                    if (Synonyms == "") Synonyms = lineItems[2];
                    else Synonyms = Synonyms + " ; " + lineItems[2];
                }
                if (lineItems[0] == "OS")
                {
                    Species = lineItems[2];
                    //recordCount = recordCount + 1;
                }
               
                
                NextLoop: ;
                    
            }
            output.Close();
            MessageBox.Show("End of Run!!");
        }
    }
}
