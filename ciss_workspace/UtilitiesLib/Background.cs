using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Linnarsson.Utilities
{
	public static class Background
	{
		private static ToolStripStatusLabel MessageCtrl;
		private static ToolStripProgressBar ProgressCtrl;
		private static Form Form;
		public static bool CancellationPending { get; private set; }
		private static int threadsRunning;
		public static bool IsBusy { get { return threadsRunning > 0; } }
 
		// TODO: find these automatically
		public static void Register(ToolStripStatusLabel msg,ToolStripProgressBar progress)
		{
			MessageCtrl = msg;
			ProgressCtrl = progress;
            if (msg != null)
			    Form = msg.GetCurrentParent().FindForm();
		}

		public static void Progress(int percent)
		{
			if(ProgressCtrl != null)
			{
				if (Form != null && Form.InvokeRequired)
                    Form.BeginInvoke((ParameterizedThreadStart) ((p) =>  Progress((int)p)) , percent);
				else ProgressCtrl.Value = percent;
			}

		}

		public static void Message(string msg)
		{
			if(MessageCtrl != null)
			{
				if (Form != null && Form.InvokeRequired)
                    Form.BeginInvoke((ParameterizedThreadStart)((p) => Message((string)p)), msg);
				else MessageCtrl.Text = msg;
			}
		}

		public static void Cancel()
		{
			CancellationPending = true;
		}

		public static void RunAsync(Action task)
		{
			ThreadPool.QueueUserWorkItem((WaitCallback)((state)=> {
				threadsRunning++;
				try
				{
					task();
				}
				finally
				{
					threadsRunning--;
					if(threadsRunning == 0) CancellationPending = false;
				}
			}));		
		}

		public static void RunWindowsExeAsync(string cmd, string args, string runInFolder)
		{
			ThreadPool.QueueUserWorkItem((WaitCallback)((state) =>
			{
				threadsRunning++;
				try
				{
					ProcessStartInfo info = new ProcessStartInfo(cmd, args);
					info.WorkingDirectory = runInFolder;
					info.CreateNoWindow = true;
					info.RedirectStandardOutput = true;
					info.RedirectStandardInput = true;
					info.RedirectStandardError = true;
					info.UseShellExecute = false;
					info.ErrorDialog = false;
					var p = Process.Start(info);
					StreamReader reader = p.StandardOutput;
					while(!reader.EndOfStream)
					{
						Console.WriteLine(reader.ReadLine());
					}
				}
				finally
				{
					threadsRunning--;
					if(threadsRunning == 0) CancellationPending = false;
				}
			}));
		}
	}
}
