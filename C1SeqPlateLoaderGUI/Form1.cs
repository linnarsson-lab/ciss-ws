using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Linnarsson.Strt;
using Linnarsson.Dna;
using C1SeqPlateLoader;
using C1;

namespace C1SeqPlateLoader
{
    public partial class Form1 : Form
    {
        List<string> availableC1Plates;
        List<string> loadedC1Plates;
        List<string> listBoxItems = new List<string>();

        public Form1()
        {
            InitializeComponent();
        }

        private void LoadSelectOptions()
        {
            listBoxItems.Clear();
            foreach (string availPlate in availableC1Plates)
            {
                if (!loadedC1Plates.Contains(C1Props.C1ProjectPrefix + availPlate) || checkBox1.Checked)
                {
                    listBoxItems.Add(availPlate);
                }
            }
            listBoxItems.Sort();
            listBoxSelect.DataSource = null;
            listBoxSelect.DataSource = listBoxItems;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            loadedC1Plates = new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix + "%", "plateid");
            availableC1Plates = new C1DB().GetLoadedChips();
            while (!Directory.Exists(C1Props.props.C1SeqPlatesFolder))
            {
                FolderBrowserDialog plateFolderBrowserDialog1 = new FolderBrowserDialog();
                plateFolderBrowserDialog1.Description = "Please locate the c1-seqplates folder!";
                if (plateFolderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    C1Props.props.C1SeqPlatesFolder = plateFolderBrowserDialog1.SelectedPath;
                }
                plateFolderBrowserDialog1.Dispose();
            }
            foreach (string file in Directory.GetFiles(C1Props.props.C1SeqPlatesFolder, C1Props.props.C1SeqPlateFilenamePattern))
            {
                string chipName = Path.GetFileNameWithoutExtension(file);
                availableC1Plates.Add(chipName);
            }
            LoadSelectOptions();
            Console.WriteLine("Select chip/plate above!");
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            LoadSelectOptions();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            while (!Directory.Exists(Props.props.ProjectsFolder))
            {
                FolderBrowserDialog strtFolderBrowserDialog = new FolderBrowserDialog();
                strtFolderBrowserDialog.Description = "Please locate the STRT projects folder!";
                if (strtFolderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    Props.props.ProjectsFolder = strtFolderBrowserDialog.SelectedPath;
                }
                strtFolderBrowserDialog.Dispose();
            }
            Console.WriteLine("Loading...");
            string selPlate = listBoxItems[listBoxSelect.SelectedIndex];
            try
            {
                string barcodeSet = radioButtonBc1To96.Checked? C1Props.props.C1BarcodeSet1 : C1Props.props.C1BarcodeSet2;
                new C1SeqPlateLoader(false).LoadC1SeqPlate(selPlate, barcodeSet);
                Console.WriteLine("Ready.");
                availableC1Plates.Remove(selPlate);
            }
            catch (Exception exc)
            {
                Console.WriteLine("ERROR: " + exc.Message);
            }
            LoadSelectOptions();
        }

        private void radioButtonBc1To96_CheckedChanged(object sender, EventArgs e)
        {

        }

    }
}
