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
	public partial class MergeTool : Form
	{
		public int MinimumExpressionPPM { get; private set; }
		public int MinimumReadsPerCell { get; private set; }
		public bool MakeUnion { get; set; }
		public bool Normalize { get; set; }
		public bool IncludeRepeats { get; private set; }
		public string[] InputFiles { get; private set; }

		public MergeTool()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Multiselect = true;
			if(ofd.ShowDialog() == DialogResult.OK)
			{
				foreach(string file in ofd.FileNames)
				{
					listBox1.Items.Add(file);
				}
			}
		}

		private void button2_Click(object sender, EventArgs e)
		{
			List<string> remove = new List<string>();
			foreach(string f in listBox1.SelectedItems) remove.Add(f);
			foreach(string f in remove) listBox1.Items.Remove(f);
		}

		private void button3_Click(object sender, EventArgs e)
		{
			if(!cbRemoveLowCells.Checked) MinimumReadsPerCell = 0;
			else
			{
				int temp = 0;
				if(int.TryParse(tbMinimumReadsPerCell.Text, out temp)) MinimumReadsPerCell = temp;
				else MinimumReadsPerCell = 10000;
			}

			IncludeRepeats = cbIncludeRpts.Checked;
			Normalize = cbNormalize.Checked;

			InputFiles = new string[listBox1.Items.Count];
			for(int i = 0; i < listBox1.Items.Count; i++)
			{
				InputFiles[i] = (string)listBox1.Items[i];
			}


		}

        private void MergeTool_Load(object sender, EventArgs e)
        {

        }
	}
}
