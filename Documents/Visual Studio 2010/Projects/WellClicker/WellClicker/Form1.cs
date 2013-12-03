using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace WellClicker
{
    public partial class Form1 : Form
    {
        string currentWellFile;
        List<string> clickLines = new List<string>();

        private static int XOffset = 416;
        private static double XDist = 27.0;
        private static int YOffset = 340;
        private static double YDist = 27.0;
        private static int msClickDelay = 200;

        public static string c1PlateFoldersLocation = "Z:\\c1-runs";
        public static string wellFilePattern = "wells_to_exclude*.txt";

        public static string screenshotFileEnding = "_screenshot.png";
        private static int screenshotX = 382;
        private static int screenshotW = 730 - screenshotX;
        private static int screenshotY = 300;
        private static int screenshotH = 544 - screenshotY;

        public Form1()
        {
            InitializeComponent();
            buttonClickWells.Enabled = false;
        }

        private void buttonSelectWellFile_Click(object sender, EventArgs e)
        {
            ChipWellSelector cws = new ChipWellSelector();
            cws.ShowDialog();
            string wellfile = cws.selectedWellFile;
            if (wellfile == "")
                return;
            try
            {
                ReadWellFile(wellfile);
                buttonClickWells.Enabled = (clickLines.Count > 0);
                linkSelectedWellFile.Text = (clickLines.Count > 0) ? Path.GetFileName(wellfile) : "No data in well file!";
            }
            catch (Exception)
            {
                linkSelectedWellFile.Text = "Format error in " + Path.GetFileName(wellfile) + "!";
            }
            linkSelectedWellFile.LinkVisited = false;
        }

        private void OLD_buttonSelectWellFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    ReadWellFile(ofd.FileName);
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

        private void ReadWellFile(string filename)
        {
            using (StreamReader reader = new StreamReader(filename))
            {
                clickLines = new List<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line == "" || line.StartsWith("#") || line.Contains("row"))
                        continue;
                    line = line.Trim();
                    char row = line[0];
                    int col = int.Parse(line.Substring(line.Contains("\t") ? line.IndexOf('\t') : 1)) - 1;
                    int clickY = (int)Math.Round(YOffset + YDist * "ABCDEFGH".IndexOf(row));
                    int clickX = (int)Math.Round(XOffset + XDist * col);
                    clickLines.Add(string.Format("{0} | {1} | {2} | Left Click", clickX, clickY, msClickDelay));
                }
            }
            currentWellFile = filename;
        }


        private void buttonClickWells_Click(object sender, EventArgs e)
        {
            if (clickLines.Count == 0)
            {
                MessageBox.Show("No clickings defined!. Please load a well file!");
                return;
            }
            List<string> allLines = AddFirstClicks();
            allLines.AddRange(clickLines);
            string tempFile = Path.GetTempFileName();
            File.WriteAllLines(tempFile, allLines.ToArray());
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
            SaveScreenShot();
            linkSelectedWellFile.Text = "Ready.";
            buttonClickWells.Enabled = false;
            clickLines.Clear();
            this.Show();
            File.Delete(tempFile);
        }

        private void SaveScreenShot()
        {
            this.Hide();
            Bitmap bmpScreenshot;
            Graphics gfxScreenshot;
            bmpScreenshot = new Bitmap(screenshotW, screenshotH, PixelFormat.Format32bppArgb);
            gfxScreenshot = Graphics.FromImage(bmpScreenshot);
            gfxScreenshot.CopyFromScreen(screenshotX, screenshotY, 0, 0, new Size(screenshotW, screenshotH), CopyPixelOperation.SourceCopy);
            string screenshotFile = currentWellFile + screenshotFileEnding;
            bmpScreenshot.Save(screenshotFile, ImageFormat.Png);
            this.Show();
        }

        private List<string> AddFirstClicks()
        {
            List<string> prefixLines = new List<string>();
            prefixLines.AddRange(new string[]
              {
                    "766 | 1012 | 200 | Left Click",
                    "68 | 37 | 300 | Left Click",
                    "85 | 176 | 200 | Left Click",
                    "85 | 176 | 60 | Left Click",
                    "428 | 501 | 300 | Left Click",
                    "835 | 457 | 300 | Left Click",
                    "835 | 457 | 63 | Left Click",
                    "756 | 345 | 200 | Left Click",
                    "792 | 521 | 200 | Left Click",
                    "792 | 533 | 200 | Left Click",
                    "792 | 533 | 63 | Left Click",
                    "414 | 342 | 300 | Left Click",
                    "414 | 342 | 63 | Left Click" });
            return prefixLines;
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
