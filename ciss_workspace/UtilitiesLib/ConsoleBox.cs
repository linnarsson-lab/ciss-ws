using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Linnarsson.Utilities
{
	public partial class ConsoleBox : UserControl
	{
		public override string Text
		{
			set { textBox1.Text = value; }
			get { return textBox1.Text; }
		}

		public ConsoleBox()
		{
			InitializeComponent();
		}

		private void ConsoleBox_Load(object sender, EventArgs e)
		{
			Console.SetOut(new TextBoxWriter(textBox1));
		}
	}
}
