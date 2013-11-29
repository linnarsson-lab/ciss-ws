using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace WellClicker
{
    public partial class Form1 : Form
    {
        List<string> clickLines = new List<string>();
        private static int XOffset = 416;
        private static double XDist = 27.0;
        private static int YOffset = 340;
        private static double YDist = 27.0;
        private static int msClickDelay = 200;

        public Form1()
        {
            InitializeComponent();
            buttonClickWells.Enabled = false;
        }

        private void buttonSelectWellFile_Click(object sender, EventArgs e)
        {
            
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ReadWellList(ofd);
                    buttonClickWells.Enabled = (clickLines.Count > 0);
                    linkSelectedWellFile.Text = (clickLines.Count > 0) ? "Click to view click script." : "No data in well file!";
                }
                catch (Exception exc)
                {
                    linkSelectedWellFile.Text = exc.Message; //"Format error in Well File!";
                }
                linkSelectedWellFile.LinkVisited = false;
            }
        }

        private void ReadWellList(OpenFileDialog ofd)
        {
            using (StreamReader reader = new StreamReader(ofd.FileName))
            {
                clickLines = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#"))
                        continue;
                    line = line.Trim();
                    char row = line[0];
                    int col = int.Parse(line.Substring(line.Contains("\t") ? line.IndexOf('\t') : 1)) - 1;
                    int clickY = (int)Math.Round(YOffset + YDist * "ABCDEFGH".IndexOf(row));
                    int clickX = (int)Math.Round(XOffset + XDist * col);
                    clickLines.Add(string.Format("{0} | {1} | {2} | Left Click", clickX, clickY, msClickDelay));
                }
            }
        }

        private void buttonClickWells_Click(object sender, EventArgs e)
        {
            if (clickLines.Count == 0)
            {
                MessageBox.Show("No clickings defined!. Please load a well file!");
                return;
            }
            string tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, clickLines.ToArray());
            string cmd = "c:\\Program Files\\MiniMouseMacro.exe";
            string args = "/m /e /d:2000 \"" + tempFile + "\"";
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(cmd, args);
            procStartInfo.CreateNoWindow = true;
            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = procStartInfo;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardError = true;
            this.Hide();
            proc.Start(); 
            string error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            linkSelectedWellFile.Text = "Ready.";
            this.Show();
            File.Delete(tempFile);
        }

        private void linkSelectedWellFile_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (clickLines.Count == 0)
                return;
            using (var myForm = new WellFileDisplay())
            {
                myForm.DisplayText(string.Join(Environment.NewLine, clickLines.ToArray()));
                myForm.ShowDialog();
            }
        }
    }

}
