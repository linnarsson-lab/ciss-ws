using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SilverBullet
{
    public partial class GeneVariantsDialog : Form
    {
        [Browsable(false)]
        public bool AnalyzeAllGeneVariants { get; set; }

        public GeneVariantsDialog()
        {
            InitializeComponent();
        }

        private void buttonGeneVariantsOK_Click(object sender, EventArgs e)
        {
            if (radioButtonAllGeneVariants.Checked)
                AnalyzeAllGeneVariants = true;
            else
                AnalyzeAllGeneVariants = false;
        }
    }
}
