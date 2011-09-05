using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Linnarsson.Dna;

namespace SilverBullet
{
	public partial class GenomeDialog : Form
	{
		[Browsable(false)]
		public StrtGenome Genome { get; set; }

		public GenomeDialog()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
            if (rbMouse.Checked) Genome = StrtGenome.Mouse;
            else if (rbChicken.Checked) Genome = StrtGenome.Chicken;
            else Genome = StrtGenome.Human;
            if (rbVEGA.Checked) Genome.Annotation = "VEGA";
            else if (rbUCSC.Checked) Genome.Annotation = "UCSC";
            else if (rbOldAnnot.Checked) Genome.Annotation = "";
            else if (rbENSE.Checked) Genome.Annotation = "ENSEMBL";
		}

	}
}
