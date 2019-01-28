// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Mail;
using AnglicanGeek.MarkdownMailer;

namespace NuGetGallery
{
    // Normally I don't like hand-written mocks, but this actually seems appropriate - anurse
    public class TestMailSender : IMailSender
    {
        public IList<MailMessage> Sent { get; private set; }

        public TestMailSender()
        {
            Sent = new List<MailMessage>();
        }

        public void Send(MailMessage mailMessage)
        {
            Sent.Add(mailMessage);
        }

        public void Send(MailAddress fromAddress, MailAddress toAddress, string subject, string markdownBody)
        {
            Send(new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = markdownBody
            });
        }

        public void Send(string fromAddress, string toAddress, string subject, string markdownBody)
        {
            Send(new MailMessage(fromAddress, toAddress, subject, markdownBody));
        }
    }
}