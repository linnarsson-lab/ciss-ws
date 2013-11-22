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
            labelResultText.Text = "Select chip/plate above!";
            foreach (string availPlate in availableC1Plates)
            {
                if (!loadedC1Plates.Contains(availPlate) || checkBox1.Checked)
                    listBoxItems.Add(availPlate);
            }
            listBoxSelect.DataSource = listBoxItems;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            loadedC1Plates = new ProjectDB().GetProjectColumn("plateid", C1Props.C1ProjectPrefix + "%", "plateid");
            availableC1Plates = new C1DB().GetLoadedChips();
            foreach (string file in Directory.GetFiles(C1Props.props.C1SeqPlatesFolder))
            {
                string chipName = Path.GetFileNameWithoutExtension(file);
                availableC1Plates.Add(chipName);
            }
            LoadSelectOptions();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            LoadSelectOptions();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            labelResultText.Text = "Loading...";
            string selPlate = listBoxItems[listBoxSelect.SelectedIndex];
            string result = new C1SeqPlateLoader().LoadC1SeqPlate((selPlate));
            labelResultText.Text = result;
        }

    }
}
