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
	public partial class QcForm : Form
	{
		public QcForm()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			string hdr = "";
			StringBuilder sb = new StringBuilder();
			foreach(string line in richTextBox1.Lines)
			{
				if(line.StartsWith(">")) hdr = line.Trim();
				else sb.Append(line.Trim());
			}
			richTextBox1.Text = hdr + "\r\n" + sb.ToString();
			richTextBox1.SelectAll();
			richTextBox1.SelectionFont = richTextBox1.Font;
			Colorize("AATGATACGGCGACCACCGAGATCTACACTCTTTCCCTACACGACGCTCTTCCGATCT", Color.Yellow, true, true);
			Colorize("AGATCGGAAGAGCGTCGTGTAGGGAAAGAGTGTAGATCTCGGTGGTCGCCGTATCATT", Color.Yellow, false, true);
			Colorize("AGATCGGAAGAGCTCGTATGCCGTCTTCTGCTTG", Color.LightGreen, false, false);
			Colorize("CAAGCAGAAGACGGCATACGAGCTCTTCCGATCT", Color.LightGreen, true, false);
		}

		private void Colorize(string text, Color col, bool head, bool ts)
		{
			int pos = richTextBox1.Find(text);
			if(pos < 0) return;
			richTextBox1.Select(pos, text.Length);
			richTextBox1.SelectionBackColor = col;
			richTextBox1.SelectionColor = Color.Black;
			if(head)
			{
				richTextBox1.Select(richTextBox1.Lines[0].Length, pos - richTextBox1.Lines[0].Length);
				richTextBox1.SelectionColor = Color.Gray;

				if(ts)
				{
					richTextBox1.Select(pos + text.Length, 5);
					richTextBox1.SelectionBackColor = Color.Blue;
					richTextBox1.Select(pos + text.Length + 5, 3);
					if(richTextBox1.SelectedText == "GGG") richTextBox1.SelectionBackColor = Color.LightGray;
					else richTextBox1.SelectionBackColor = Color.Red;
				}
			}
			else
			{
				richTextBox1.Select(pos + text.Length, richTextBox1.Text.Length - (pos + text.Length));
				richTextBox1.SelectionColor = Color.Gray;

				if(ts)
				{
					richTextBox1.Select(pos - 5, 5);
					richTextBox1.SelectionBackColor = Color.Blue;
					richTextBox1.Select(pos - 8, 3);
					if(richTextBox1.SelectedText == "CCC") richTextBox1.SelectionBackColor = Color.LightGray;
					else richTextBox1.SelectionBackColor = Color.Red;
				}
			}
			richTextBox1.Select(0, 0);

		}

		private void richTextBox1_SelectionChanged(object sender, EventArgs e)
		{
			label1.Text = richTextBox1.SelectedText.Length.ToString() + " bp";
		}
	}
}
