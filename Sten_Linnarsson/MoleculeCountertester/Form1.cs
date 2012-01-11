using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Linnarsson.Strt;

namespace MoleculeCountertester
{
	public partial class Form1 : Form
	{
		public Form1()
		{
			InitializeComponent();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Random rnd = new Random();
			foreach (var m in new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 18, 25, 50, 75, 100, 150, 200 })
			{
				var mc = MoleculeCounter.Simulate(m, 256, 0.2, 10, 0.9, 10);
				var p1 = mc.PosteriorMode();
				var p2 = mc.PosteriorMean();
				Console.WriteLine((m*(1+rnd.NextDouble()/10-0.05)) + "\t" + p1 + "\t" + p2);
			}
		}
	}
}
