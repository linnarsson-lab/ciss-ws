using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Linnarsson.Utilities
{

	/// <summary>
	/// Converted (again) from VB code by Paul Kimmel 
	/// </summary>
	public class TextBoxWriter : System.IO.TextWriter
	{
		private TextBoxBase control;
		private StringBuilder builder;

		public TextBoxWriter(TextBox control)
		{
			this.control = control;
			control.HandleCreated += new EventHandler(OnHandleCreated);
		}


		public override void Write(char ch)
		{
			Write(ch.ToString());
		}

		public override void Write(string s)
		{
			if (control.IsHandleCreated) AppendText(s);
			else BufferText(s);
		}

		public override void WriteLine(string s)
		{
			Write(s + Environment.NewLine);
		}

		private void BufferText(string s)
		{
			if (builder == null) builder = new StringBuilder();
			builder.Append(s);
		}

		delegate void StringInvoker(string s);
		private void AppendText(string s)
		{
			if (control.InvokeRequired)
			{
				control.BeginInvoke(new StringInvoker(AppendText), s);
			}
			else
			{
				if (builder != null)
				{
					control.AppendText(builder.ToString());
					builder = null;
				}
				control.AppendText(s);
			}
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if (builder != null)
			{
				control.AppendText(builder.ToString());
				builder = null;
			}
		}

		public override Encoding Encoding
		{
			get { return Encoding.Default; }
		}
	}
}
