using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WellClicker
{
    public partial class ChipWellSelector : Form
    {
        private static string chipWellfileSeparator = "  -----> ";

        List<string> listBoxItems = new List<string>();
        public string selectedWellFile = "";

        public ChipWellSelector()
        {
            InitializeComponent();
            LoadSelectOptions(checkBox_ShowLoaded.Checked);
        }

        private void LoadSelectOptions(bool includeLoaded)
        {
            listBoxItems.Clear();
            foreach (string availableChipWellfile in ReadAvailableChipWellfileSubpaths(includeLoaded))
            {
                listBoxItems.Add(availableChipWellfile);
            }
            listBoxItems.Sort();
            listBoxSelect.DataSource = null;
            listBoxSelect.DataSource = listBoxItems;
        }

        private List<string> ReadAvailableChipWellfileSubpaths(bool includeLoaded)
        {
            List<string> availableFiles = new List<string>();
            foreach (string folder in Directory.GetDirectories(Form1.c1PlateFoldersLocation))
            {
                string chip = Path.GetFileName(folder);
                foreach (string wellfilePath in Directory.GetFiles(folder, Form1.wellFilePattern))
                {
                    if (includeLoaded || !File.Exists(wellfilePath + Form1.screenshotFileEnding))
                        availableFiles.Add(chip + chipWellfileSeparator + Path.GetFileName(wellfilePath));
                }
            }
            return availableFiles;
        }


        private void checkBox_ShowLoaded_CheckedChanged(object sender, EventArgs e)
        {
            LoadSelectOptions(checkBox_ShowLoaded.Checked);
        }

        private void button_ReadSelectedFile_Click(object sender, EventArgs e)
        {
            if (listBoxSelect.SelectedIndex == -1)
                selectedWellFile = "";
            else
            {
                string chipwell = listBoxItems[listBoxSelect.SelectedIndex];
                string chipwellpath = chipwell.Replace(chipWellfileSeparator, Path.DirectorySeparatorChar.ToString());
                selectedWellFile = Path.Combine(Form1.c1PlateFoldersLocation, chipwellpath);
            }
        }

        private void button_FreeFileSelection_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                selectedWellFile = ofd.FileName;
                this.Close();
            }
        }

    }
}
