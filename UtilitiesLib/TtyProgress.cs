using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Linnarsson.Utilities
{
	public class TtyProgress
	{
		private int m_Progress;
		public int Progress
		{
			get { return m_Progress; }
			set { m_Progress = value; redraw(); }
		}

		private int m_Total;
		public int Total
		{
			get { return m_Total; }
			set { m_Total = value; m_Progress = 0;  redraw(); }
		}

		private string m_Message;
		public string Message
		{
			get { return m_Message; }
			set { m_Message = value; redraw(); }
		}

		private bool shown = false;
		private DateTime started;

		/// <summary>
		/// Show a progress bar in the TTY. The display will update each time any of the properties change
		/// </summary>
		public void Start()
		{
			shown = true;
			Tty.LineWrap(false);
			Tty.CursorVisible(false);
			started = DateTime.Now;
			redraw();
		}

		/// <summary>
		/// Show 100% completion and move to the next line. Further updates will be shown as a new progress bar
		/// </summary>
		public void Complete()
		{
			Progress = Total;
			Message = Message + " - done.";
			Tty.LineWrap(true);
			Tty.CursorVisible(true);
			longestBar = 0;
			Console.WriteLine();
			shown = false;
		}

		int longestBar = 0;
		private void redraw()
		{
			if (!shown) return;

			// Calcuate percent completed
			int percentComplete = 0;
			if(Total > 0) percentComplete = (int)((100*(long)Progress)/Total);
			percentComplete = Math.Min(100, percentComplete);

			// Calculate the ETA
			long elapsedTicks = (DateTime.Now - started).Ticks;
			TimeSpan eta = new TimeSpan(0);
			if(percentComplete > 0 && percentComplete < 100) eta = new TimeSpan(elapsedTicks / percentComplete * (100 - percentComplete));
			if (percentComplete == 100) eta = new TimeSpan(elapsedTicks);

			string etaDisplay = "";
			if (eta.TotalDays >= 1) etaDisplay += eta.TotalDays.ToString("#####") + (eta.TotalDays >= 2 ? " days " : " day ");
			etaDisplay += eta.Hours.ToString("00") + ":" + eta.Minutes.ToString("00") + ":" + eta.Seconds.ToString("00");

			string bar = new string('=', percentComplete/5);			// A bar made of ========, ranging from 0 to 20 characters
			string nonBar = new string('-', 20 - (percentComplete/5));	// The rest of the bar

			string progressBarRest =  nonBar + "| " + percentComplete + "% | " + etaDisplay  + " | " + Message;
			int pBLength = progressBarRest.Length + bar.Length + 1;
			if (pBLength < longestBar)
			{
				string padding = new string(' ', longestBar - pBLength);
				progressBarRest = progressBarRest + padding;
			}
			else
			{
				longestBar = pBLength;
			}

			Console.Write("|");
			// Show the progress bar in green while it's active
			if(percentComplete != 100) Tty.SetAttribute(TtyAttribute.ForegroundGreen);
			Console.Write(bar);
			if (percentComplete != 100) Tty.SetAttribute(TtyAttribute.None);
			Console.Write(progressBarRest);

			Tty.Left(longestBar);
		}
	}
}
