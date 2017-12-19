// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// <see cref="IMailSender"/> implementation that saves mail to local disk
    /// </summary>
    public class DiskMailSender : IMailSender
    {
        private const string OutputSubdir = @"..\Data\Email";

        public void Send(string fromAddress, string toAddress, string subject, string markdownBody)
        {
            SaveMessage(fromAddress, toAddress, subject, markdownBody);
        }

        public void Send(MailAddress fromAddress, MailAddress toAddress, string subject, string markdownBody)
        {
            SaveMessage(
                ToString(fromAddress),
                ToString(toAddress),
                subject,
                markdownBody);
        }

        public void Send(MailMessage mailMessage)
        {
            SaveMessage(
                ToString(mailMessage.From),
                string.Join("; ", mailMessage.To.Select(ToString)),
                mailMessage.Subject,
                mailMessage.Body);
        }

        private static string ToString(MailAddress mailAddress)
            => string.Format("{0} <{1}>", mailAddress.DisplayName, mailAddress.Address);

        private void SaveMessage(string fromLine, string toLine, string subject, string messageBody)
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var filename = string.Format("{0:yyyy-MM-dd-HH-mm-ss}-{1}.txt", DateTimeOffset.UtcNow, Guid.NewGuid());
            var outputDir = Path.Combine(exeDir, OutputSubdir);
            Directory.CreateDirectory(outputDir);
            var outputFile = Path.Combine(outputDir, filename);
            using (var f = File.CreateText(outputFile))
            {
                f.WriteLine("From: {0}", fromLine);
                f.WriteLine("  To: {0}", toLine);
                f.WriteLine("Subj: {0}", subject);
                f.WriteLine("{0}", messageBody);
            }
        }
    }
}
