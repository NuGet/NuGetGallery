// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using AnglicanGeek.DbExecutor;
using NuGetGallery.Operations.Infrastructure;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Mime;
using System.Web;
using System.Web.Helpers;
using System.Web.UI;
using System.IO;
using System.Data;

namespace NuGetGallery.Operations
{
    [Command("sendmail", "sends mail to the package owners", AltName = "sm", MaxArgs = 0)]
    public class SendMailTask : DatabaseTask
    {
        [Option("Email account to be used to send the mail", AltName = "ua")]
        public string UserAccount { get; set; }

        [Option("Email password to be used to send the mail", AltName = "p")]
        public string Password { get; set; }

        [Option("Email host to be used to send the mail", AltName = "eh")]
        public string EmailHost { get; set; }

        [Option("Comma separated list of To addresses", AltName = "to")]
        public string ToList { get; set; }

        [Option("Comma separated list of ReplyTo addresses", AltName = "replyto")]
        public string ReplyToList { get; set; }

        [Option("Subject of the email", AltName = "ms")]
        public string MailSubject { get; set; }
        
        [Option("Body of the email", AltName = "mc")]
        public string MailContent { get; set; }

        [Option("Comma separated list of packages whose owners need to be contacted", AltName = "pn")]
        public string PackageIds { get; set; }

        [Option("Full path to the file which has the formatted mail content. Used if MailContent Arg is not specified.", AltName = "mcf")]
        public string MailContentFilePath { get; set; }

        [Option("Full path to the file which has the list of package names - with individual package name represented in a line. USed if PackageIds Arg is not specified", AltName = "pnf")]
        public string PackageIdsFilePath { get; set; }
                

        public override void ExecuteCommand()
        {
            System.Threading.Thread.Sleep(30 * 1000);
            //Construct the SMTP host.
            SmtpClient sc = new SmtpClient("smtphost");
            NetworkCredential nc = new NetworkCredential(UserAccount, Password);
            sc.UseDefaultCredentials = false;
            sc.Credentials = nc;
            sc.Host = EmailHost;
            sc.EnableSsl = true;
            sc.Port = 25;
            sc.EnableSsl = true;
            ServicePointManager.ServerCertificateValidationCallback = delegate(object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) { return true; };
            System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();

            //Set the from and to mail addressess.
            message.From = new MailAddress(UserAccount, "NuGet Gallery Support");

            string[] replyTo = ReplyToList.Split(new char[] { ',' });
            foreach(string replyToAddress in replyTo)
             message.ReplyToList.Add(new MailAddress(replyToAddress,replyToAddress));

            string[] to = ToList.Split(new char[] { ',' });
            foreach (string toAddress in to)
                message.To.Add(new MailAddress(toAddress, toAddress));

            //Get the list of packages if present and add the owner email Ids to Bcc.
            List<string> PackagesList = GetPackageIds();
            if(PackagesList != null && PackagesList.Count > 0)
            message.Bcc.AddRange(GetOwnerMailAddressess(this.ConnectionString.ToString(),PackagesList));

            message.Subject = string.Format(MailSubject);
            message.IsBodyHtml = true;
            message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(@"<html><body></br></br>" + GetMailContent() + "</body></html>", new ContentType("text/html")));

            try
            {
                sc.Send(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in sending mail : {0}", ex.Message);
            }
            
        }

        #region PrivateMethods
        private List<string> GetPackageIds()
        {
            //Get the list of packages from either PackageIds or PackageIdsFilePath Argument.
            List<string> PackagesList = new List<string>();
            if (!string.IsNullOrEmpty(PackageIds))
            {
                PackagesList = PackageIds.Split(new char[] { ',' }).ToList();
            }
            else  if (!string.IsNullOrEmpty(PackageIdsFilePath))
            {
                StreamReader packages = new StreamReader(PackageIdsFilePath);
                while (packages.EndOfStream == false)
                {
                    PackagesList.Add(packages.ReadLine());
                }
                packages.Close();
            }
            return PackagesList;
        }

        private  string GetMailContent()
        {
            //Get the content from the specified argument.
            string body = string.Empty; 
            if(!string.IsNullOrEmpty(MailContent))
            {
                body = MailContent;
            }
            else
            {
               StreamReader sr = new StreamReader(MailContentFilePath);
               body = sr.ReadToEnd();
               sr.Close();
            }           
            //wrap it using htmlwriter so that the format of the text is preserved.
            stringwriter = new StringWriter();
            htmlWriter = new HtmlTextWriter(stringwriter);
            htmlWriter.RenderBeginTag(HtmlTextWriterTag.Pre);
            htmlWriter.Write(body);
            htmlWriter.RenderEndTag();
            htmlWriter.WriteLine("");
            return stringwriter.ToString();
        }

        private MailAddressCollection GetOwnerMailAddressess(string connectionString,List<string> packageNames)
        {
            MailAddressCollection ownerEmailCollection = new MailAddressCollection();        
            foreach(string package in packageNames)
            {   
                //Get the owner mail address for each of the package.
                    string sqlemailaddress = @"
                SELECT 
                      [EmailAddress],
                      [Username],
                      [EmailAllowed]      
                  FROM [dbo].[Users] where [key] IN (Select pro.[UserKey] from  [dbo].[PackageRegistrationOwners] pro  JOIN [dbo].[PackageRegistrations] pr on pro.[PackageRegistrationKey] = pr.[Key] where pr.[Id] = '{0}')
                ";
           
            string emailaddress = string.Empty;
            string username = string.Empty;
            bool emailallowed = true;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(string.Format(sqlemailaddress, package), connection);
                command.CommandType = CommandType.Text;
                SqlDataReader reader = command.ExecuteReader();               
                while (reader.Read())
                {
                    emailaddress = (string)reader.GetValue(0);
                    username = (string)reader.GetValue(1);
                    emailallowed = (bool)reader.GetValue(2);
                    //Include it only if EmailAllowed option is turned on.
                    if(emailallowed)
                    ownerEmailCollection.Add(new MailAddress(emailaddress, username));
                }
            }        
         }        
        return ownerEmailCollection;
      }
        #endregion PrivateMethods

        #region PrivateMembers
        private static StringWriter stringwriter;
        private static HtmlTextWriter htmlWriter;

        #endregion PrivateMembers
    }
}
