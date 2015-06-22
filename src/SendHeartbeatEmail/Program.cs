// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
//********************************************************************************************************************
// IMPORTANT: Set the email account password and recipient address in App.Config before deploying
//********************************************************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Configuration;
using System.IO;

namespace SendHeartbeatMail
{
    class Program
    {
        private static string AlertSubject = String.Format("[Action Required]: Alerts for {0}/{1}", DateTime.UtcNow.Month, DateTime.UtcNow.Day);
        private static string ConciseSubject = String.Format("[Summary]: Job Restarts for {0}/{1}", DateTime.UtcNow.Month, DateTime.UtcNow.Day);
        private static string VerboseSubject = String.Format("[Info]: Heartbeat Info for {0}/{1}", DateTime.UtcNow.Month, DateTime.UtcNow.Day);

        private static string PreviousText = string.Empty;
        private static DateTimeOffset LastUpdatedTime = DateTime.MinValue;

        private static string AlertFileToMonitor = string.Empty;
        private static string ConciseFileToMonitor = string.Empty;
        private static string VerboseFileToMonitor = string.Empty;

        private static int CurrentDay = DateTime.UtcNow.Day;
        private static string LogFileSuffix = string.Empty;
        private static string AlertMailText = string.Empty;
        private static string ConciseMailText = string.Empty;
        private static string VerboseMailText = string.Empty;
        private static string DashboardStorageAccount = string.Empty;

        private static int AlertEmailCount = 0;


        private static string SmtpUserName;
        private static string SmtpPassword;
        private static string MailRecipientAddress;

        static void Main(string[] args)
        {
            if (args.Count() < 2)
            {
                Console.WriteLine("USAGE: Please specify the LogFileSuffix for the files to monitor and the Dashboard Storage Connection String");
                return;
            }

            LogFileSuffix = string.IsNullOrEmpty(args[0]) ? "V3" : args[0];
            DashboardStorageAccount = args[1];
            AlertFileToMonitor = GenerateLogFileName("ProcessRecyle_Alert_");
            ConciseFileToMonitor = GenerateLogFileName("ProcessRecyle_Concise_");

            var csa = CloudStorageAccount.Parse(DashboardStorageAccount);
            var cbc = csa.CreateCloudBlobClient().GetContainerReference("int0");
            var alertBlob = cbc.GetBlockBlobReference(AlertFileToMonitor);
            
            //AppSettings
            SmtpUserName = ConfigurationManager.AppSettings["SmtpUserName"];
            SmtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
            MailRecipientAddress = ConfigurationManager.AppSettings["MailRecipientAddress"];

            while (true)
            {
                if (alertBlob.Exists())
                {
                    alertBlob.FetchAttributes();
                    if (!alertBlob.Properties.LastModified.Equals(LastUpdatedTime))
                    {
                        LastUpdatedTime = alertBlob.Properties.LastModified.Value.ToUniversalTime();
                        string currentText = alertBlob.DownloadText();

                        string newText = !String.IsNullOrEmpty(PreviousText) ? currentText.Replace(PreviousText, string.Empty) : currentText;
                        StreamWriter sw = new StreamWriter(AlertFileToMonitor);
                        sw.Write(newText);
                        sw.Close();

                        if (!String.IsNullOrEmpty(newText))
                        {
                            if (AlertEmailCount % 5 == 0)
                            {
                                AlertMailText = GetMailContent(MakeTableRows(AlertFileToMonitor), "red");
                                SendEmail(AlertSubject, AlertMailText, string.Empty);
                                PreviousText = currentText;
                            }
                            AlertEmailCount = (AlertEmailCount == 500) ? 0 : AlertEmailCount + 1;
                        }
                    }
                }
                
                //Send Concise  summary mail for the day
                //Update Log File names
                if (CurrentDay != DateTime.UtcNow.Day)
                {
                    //Send mail using concise file
                    var conciseBlob = cbc.GetBlockBlobReference(ConciseFileToMonitor);
                    if (conciseBlob.Exists())
                    {
                        string path = ConciseFileToMonitor;
                        conciseBlob.DownloadToFile(path, FileMode.CreateNew);

                        ConciseMailText = GetMailContent(MakeTableRows(path), "blue");
                        SendEmail(ConciseSubject, ConciseMailText, string.Empty);
                    }
                    else
                    {
                        VerboseFileToMonitor = GenerateLogFileName("ProcessRecyle_Verbose_");
                        var verboseBlob = cbc.GetBlockBlobReference(VerboseFileToMonitor);

                        string path = VerboseFileToMonitor;
                        verboseBlob.DownloadToFile(path, FileMode.CreateNew);

                        VerboseMailText = GetMailContent("<tr>All Jobs are running fine</tr>", "green");
                        SendEmail(VerboseSubject, VerboseMailText, path);
                    }

                    CurrentDay = DateTime.UtcNow.Day;
                    ConciseFileToMonitor = GenerateLogFileName("ProcessRecyle_Concise_");
                    AlertFileToMonitor = GenerateLogFileName("ProcessRecyle_Alert_");
                }

                //Check every minute to see if there are entries in Alert Log
                Thread.Sleep(60000);

            }
        }

        private static string MakeTableRows(string path)
        {
            var stream = new StreamReader(path);
            StringBuilder sb = new StringBuilder();
            while (!stream.EndOfStream)
            {
                string line = stream.ReadLine();
                sb.Append("<tr>");
                sb.Append(line);
                sb.Append("</tr>");
            }
            stream.Close();
            return sb.ToString();
        }

        private static string GenerateLogFileName(string fileNamePrefix)
        {
            return
                fileNamePrefix
                + LogFileSuffix
                + DateTime.UtcNow.Month.ToString()
                + CurrentDay.ToString()
                + ".txt";
        }

        private static void SendEmail(string subject, string mailContent, string attachment)
        {
            var sc = new SmtpClient("smtphost");
            var nc = new NetworkCredential(SmtpUserName, SmtpPassword);
            sc.UseDefaultCredentials = true;
            sc.Credentials = nc;
            sc.Host = "outlook.office365.com";
            sc.EnableSsl = true;
            sc.Port = 587;

            var message = new System.Net.Mail.MailMessage();
            message.From = new MailAddress(SmtpUserName, "Heartbeat Monitor");
            message.To.Add(new MailAddress(MailRecipientAddress, MailRecipientAddress));
            message.Subject = string.Format(subject);
            message.IsBodyHtml = true;
            if (!String.IsNullOrEmpty(attachment))
            {
                message.Attachments.Add(new Attachment(attachment));
            }
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body>" + mailContent + "</body></html>", new ContentType("text/html")));

            try
            {
                sc.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Error in sending mail : {0}", ex.Message);
                Console.ReadKey();
            }
        }

        private static string GetMailContent(string message, string color)
        {
            string style = "<table style='font-family:Arial; color: " + color + "; font-size: medium'>"
                             + "<td>"
                             + message
                             + "</td>"
                             + "</table>";
            return style;
        }
    }
}
