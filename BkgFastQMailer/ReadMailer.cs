using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Linnarsson.Utilities;
using Linnarsson.Strt;
using Linnarsson.Dna;

namespace BkgFastQMailer
{
    class ReadMailer
    {
        private string readsFolder;
        private StreamWriter logWriter;

        public ReadMailer(string readsFolder, StreamWriter logWriter)
        {
            this.readsFolder = readsFolder;
            this.logWriter = logWriter;
        }

        public void Scan()
        {
            Dictionary<string, List<MailTaskDescription>> mds = new ProjectDB().GetQueuedMailTasksByEmail();
            string[] readsFiles = Directory.GetFiles(readsFolder);
            foreach (string email in mds.Keys)
            {
                Dictionary<string, string> resultByTaskId = new Dictionary<string, string>();
                List<string> links = new List<string>();
                foreach (MailTaskDescription md in mds[email])
                {
                    string pattern = string.Format(PathHandler.readFileAndLaneFolderCreatePattern,
                                                   int.Parse(md.runNo), md.laneNo, "[0-9]", ".+fq");
                    string status = "filemissing";
                    foreach (string readsFile in readsFiles)
                    {
                        string filename = Path.GetFileName(readsFile);
                        Match m = Regex.Match(filename, pattern);
                        if (m.Success)
                        {
                            new ProjectDB().UpdateMailTaskStatus(md.id, "processing");
                            try
                            {
                                string url = PublishReadsForDownload(readsFile);
                                links.Add(url);
                                status = "sent";
                            }
                            catch (Exception e)
                            {
                                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Could not publish " + readsFile + " on www server: " + e);
                                logWriter.Flush();
                                status = "failed";
                            }
                        }
                    }
                    if (status == "filemissing")
                    {
                        logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Can not locate a fq file for run=" + md.runNo + " and lane=" + md.laneNo);
                        logWriter.Flush();
                    }
                    resultByTaskId[md.id] = status;
                }
                string allStatus = "sent";
                try
                {
                    if (links.Count > 0)
                        NotifyClient(email, links);
                }
                catch (Exception e)
                {
                    logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR mailing " + email + ":" + e);
                    logWriter.Flush();
                    allStatus = "emailfailure";
                }
                foreach (string id in resultByTaskId.Keys)
                {
                    string status = (allStatus != "sent")? allStatus : resultByTaskId[id];
                    new ProjectDB().UpdateMailTaskStatus(id, status);
                }
            }
        }

        private string PublishReadsForDownload(string readsPath)
        {
            string readsFileLink = "";
            string destFilename = Path.GetFileName(readsPath);
            bool useTempGzReadsFile = !readsPath.EndsWith(".gz");
            string gzReadsPath = readsPath;
            if (useTempGzReadsFile)
            {
                gzReadsPath = MakeGzReadsFile(readsPath);
                destFilename += ".gz";
            }
            string destPath = Path.Combine(Props.props.ResultDownloadUrl, destFilename);
            string cpCmd = "rsync";
            string cpCmdArg = string.Format("--ignore-existing -e 'ssh -p {0}' {1} {2}", Props.props.ResultDownloadScpPort, gzReadsPath, destPath);
            Console.WriteLine(DateTime.Now.ToString() + ": " + cpCmd + " " + cpCmdArg);
            CmdCaller cpCmdCaller = new CmdCaller(cpCmd, cpCmdArg);
            if (cpCmdCaller.ExitCode != 0)
                throw new IOException("Failed OS call, exit code " + cpCmdCaller.ExitCode.ToString() + ":\n" + cpCmd + " " + cpCmdArg
                                      + "\n" + cpCmdCaller.StdError);
            readsFileLink = Props.props.ResultDownloadFolderHttp + destFilename;
            if (useTempGzReadsFile)
                RemoveTempFile(gzReadsPath);
            return readsFileLink;
        }

        private static string MakeGzReadsFile(string readsPath)
        {
            string tempReadsPath = readsPath + ".temp_copy";
            if (File.Exists(tempReadsPath))
            {
                CmdCaller.Run("chmod", " u+w " + tempReadsPath);
                File.Delete(tempReadsPath);
            }
            string gzReadsPath = readsPath + ".gz";
            if (File.Exists(gzReadsPath))
            {
                CmdCaller.Run("chmod", " u+w " + gzReadsPath);
                File.Delete(gzReadsPath);
            }
            File.Copy(readsPath, tempReadsPath);
            CmdCaller cmd = new CmdCaller("gzip", tempReadsPath);
            if (cmd.ExitCode != 0)
                throw new IOException("Could not call OS 'gzip' command: " + cmd.StdError);
            File.Delete(tempReadsPath);
            return gzReadsPath;
        }

        private void RemoveTempFile(string gzReadsPath)
        {
            try
            {
                CmdCaller chmodCmd = new CmdCaller("chmod", "u+w " + gzReadsPath);
                if (chmodCmd.ExitCode != 0)
                    logWriter.WriteLine(DateTime.Now.ToString() + " Could not 'chmod u+w': " + gzReadsPath + " Error: " + chmodCmd.StdError);
                File.Delete(gzReadsPath);
            }
            catch (IOException e)
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " Could not delete tmp file " + gzReadsPath + " Error: " + e);
            }
            catch (UnauthorizedAccessException e)
            {
                logWriter.WriteLine(DateTime.Now.ToString() + " Could not delete tmp file " + gzReadsPath + " Error: " + e);
            }
        }

        private static void NotifyClient(string email, List<string> links)
        {
            string subject = "Read files available for download.";
            StringBuilder sb = new StringBuilder();
            sb.Append("<html>");
            sb.Append("\n<p>You can download sequence reads from the following link(s):</p>");
            foreach (string link in links)
                sb.Append(string.Format("\n<a href=\"{0}\">{0}</a><br />", link, link));
            sb.Append("\n<p>The data will be available for 14 days.</p>");
            sb.Append("\n</html>");
            string body = sb.ToString();
            EmailSender.SendMsg(email, subject, body, true);
        }

    }
}