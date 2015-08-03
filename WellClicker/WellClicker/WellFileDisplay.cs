using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WellClicker
{
    public partial class WellFileDisplay : Form
    {
        public WellFileDisplay()
        {
            InitializeComponent();
        }

        public void DisplayText(string text)
        {
            textBoxWellFile.Text = text;
        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}
