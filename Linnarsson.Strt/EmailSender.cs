using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Linnarsson.Dna;

namespace Linnarsson.Strt
{
    public class EmailSender
    {
        public static void ReportFailureToAdmin(string subject, string body, bool bodyIsHtml)
        {
            SendMsg(Props.props.FailureReportAndAnonDownloadEmail, subject, body, bodyIsHtml);
        }

        public static void SendMsg(string toEmail, string subject, string body, bool bodyIsHtml)
        {
            string fromEmail = Props.props.OutgoingMailSender;
            MailMessage message = new MailMessage(fromEmail, toEmail, subject, body);
            message.IsBodyHtml = bodyIsHtml;
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
            SmtpClient mailClient = new SmtpClient(Props.props.OutgoingMailServer, Props.props.OutgoingMailPort);
            mailClient.EnableSsl = true;
            mailClient.Credentials = new System.Net.NetworkCredential(Props.props.OutgoingMailUser,
                                                                      Props.props.OutgoingMailPassword);
            mailClient.Send(message);
        }
    }
}
