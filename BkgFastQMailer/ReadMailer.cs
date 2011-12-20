using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Mail;
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
                string allStatus = "sent";
                Dictionary<string, string> resultByTaskId = new Dictionary<string, string>();
                List<string> links = new List<string>();
                foreach (MailTaskDescription md in mds[email])
                {
                    string pattern = string.Format("Run{0:00000}_L{1}_[0-9].+fq", int.Parse(md.runNo), md.laneNo);
                    string status = "filemissing";
                    foreach (string readsFile in readsFiles)
                    {
                        string filename = Path.GetFileName(readsFile);
                        Match m = Regex.Match(filename, pattern);
                        if (m.Success)
                        {
                            new ProjectDB().UpdateMailTaskStatus(md.id, "processing");
                            string url = PublishReadsForDownload(readsFile);
                            if (url == "")
                            {
                                logWriter.WriteLine(DateTime.Now.ToString() + " *** ERROR: Could not publish " + readsFile + " on www server!");
                                logWriter.Flush();
                                status = "failed";
                            }
                            else
                            {
                                links.Add(url);
                                status = "copied";
                            }
                        }
                    }
                    if (status != "copied") allStatus = "";
                    resultByTaskId[md.id] = status;
                }
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
                    string status = (allStatus != "")? allStatus : resultByTaskId[id];
                    new ProjectDB().UpdateMailTaskStatus(id, status);
                }
            }
        }

        private static string PublishReadsForDownload(string readsFile)
        {
            string readsFileLink = "";
            string tempFqPath = readsFile + ".temp_copy";
            if (File.Exists(tempFqPath))
                File.Delete(tempFqPath);
            File.Copy(readsFile, tempFqPath);
            int gzipResult = CmdCaller.Run("gzip", tempFqPath);
            if (gzipResult == 0)
            {
                File.Delete(tempFqPath);
                string tempGzPath = tempFqPath + ".gz";
                string destFilename = Path.GetFileName(readsFile) + ".gz";
                string destPath = Path.Combine(Props.props.ResultDownloadUrl, destFilename);
                string scpArg = string.Format("-P 9952 {0} {1}", tempGzPath, destPath);
                Console.WriteLine("scp " + scpArg);
                int scpResult = CmdCaller.Run("scp", scpArg);
                if (scpResult == 0)
                {
                    readsFileLink = Props.props.ResultDownloadFolderHttp + destFilename;
                }
                File.Delete(tempGzPath);
            }
                return readsFileLink;
        }

        private static void NotifyClient(string email, List<string> links)
        {
            string from = "linnarsson-lab@ki.se";
            string subject = "Read files available for download.";
            string smtp = "localhost";
            StringBuilder sb = new StringBuilder();
            sb.Append("<html>");
            sb.Append("\n<p>You can download sequence reads from the following link(s):</p>");
            foreach (string link in links)
                sb.Append(string.Format("\n<a href=\"{0}\">{0}</a><br />", link, link));
            sb.Append("\n<p>The data will available for 14 days.</p>");
            sb.Append("\n</html>");
            MailMessage message = new MailMessage(from, email, subject, sb.ToString());
            message.IsBodyHtml = true;
            SmtpClient mailClient = new SmtpClient(smtp, 25);
            mailClient.Send(message);
        }

    }
}