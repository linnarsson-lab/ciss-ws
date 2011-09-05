using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Linnarsson.Utilities
{
    public class CmdCaller
    {
		public string StdError { get; set; }
		public string StdOutput { get; set; }
		public int ExitCode { get; set; }

		public CmdCaller(string command, string arguments) : this(command, arguments, true)
		{
		}
		/// <summary>
		/// Runs the command with the specified argument string in a separate process. When
		/// the command exits, the exit code and any output generated can be accessed 
		/// using the properties of this object.
		/// </summary>
        /// <param name="command">Command. If running on Windows, ".exe." will be added to
        ///                        a command that has no extension.</param>
        /// <param name="arguments">A string of arguments to the command</param>
		public CmdCaller(string command, string arguments, bool captureOutput)
		{
			Process cmdProcess = new Process();
			ProcessStartInfo cmdStartInfo = new ProcessStartInfo();
			if(Environment.OSVersion.VersionString.Contains("Microsoft")
				&& !command.EndsWith(".exe") && !command.Contains("."))
				command = command + ".exe";
			cmdStartInfo.FileName = command;

			if(captureOutput)
			{
				cmdStartInfo.RedirectStandardError = true;
				cmdStartInfo.RedirectStandardOutput = true;
			}
			cmdStartInfo.RedirectStandardInput = false;
			cmdStartInfo.UseShellExecute = false;
			cmdStartInfo.CreateNoWindow = true;

			cmdStartInfo.Arguments = arguments;

			cmdProcess.EnableRaisingEvents = true;
			cmdProcess.StartInfo = cmdStartInfo;
			cmdProcess.Start();

			// Wait for exiting the process
			if(captureOutput)
			{
				StdError = cmdProcess.StandardError.ReadToEnd();
				StdOutput = cmdProcess.StandardOutput.ReadToEnd();
			}
			cmdProcess.WaitForExit(); // IMPORTANT: this must be after ReadToEnd()
			ExitCode = cmdProcess.ExitCode;
			cmdProcess.Close();
			cmdProcess.Dispose();
			cmdStartInfo = null;

		}

        /// <summary>
        /// Runs the command with the specified argument string in a separate process
        /// </summary>
        /// <param name="command">Command. If running on Windows, ".exe." will be added to
        ///                        a command that has no extension.</param>
        /// <param name="arguments">A string of arguments to the command</param>
        /// <returns>The exit code of the process. 0 indicates success.</returns>
        public static int Run(string command, string arguments)
        {
			var cmd = new CmdCaller(command, arguments, true);

            if (cmd.ExitCode != 0) Console.Error.WriteLine(cmd.StdError);
            return cmd.ExitCode;
        }
    }
}
