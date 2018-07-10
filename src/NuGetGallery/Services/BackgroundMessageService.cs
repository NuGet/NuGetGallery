// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using NuGetGallery.Configuration;

namespace NuGetGallery.Services
{
    public class BackgroundMessageService : MessageService
    {
        public BackgroundMessageService(IMailSender mailSender, IAppConfiguration config, ITelemetryClient telemetryClient)
            :base(mailSender, config, telemetryClient)
        {
        }

        protected override void SendMessage(MailMessage mailMessage)
        {
            // Send email as background task, as we don't want to delay the HTTP response.
            // Particularly when sending email fails and needs to be retried with a delay.
            Task.Run(() =>
            {
                try
                {
                    base.SendMessage(mailMessage);
                }
                catch (Exception ex)
                {
                    // Log but swallow the exception.
                    QuietLog.LogHandledException(ex);
                }
            });
        }
    }
}