// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;

namespace NuGet.SupportRequests.Notifications.Services
{
    internal class MessagingService
    {
        private readonly MailAddress _fromAddress;
        private readonly ILogger<MessagingService> _logger;
        private readonly string _smtpUri;
        private SmtpClient _smtpClient;

        private const string _noreplyAddress = "NuGet Gallery <noreply@nuget.org>";

        public MessagingService(ILoggerFactory loggerFactory, string smtpUri)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            if (smtpUri == null)
            {
                throw new ArgumentNullException(nameof(smtpUri));
            }

            _logger = loggerFactory.CreateLogger<MessagingService>();
            _fromAddress = new MailAddress(_noreplyAddress);
            _smtpUri = smtpUri;
        }

        internal void SendNotification(
            string subject,
            string body,
            DateTime referenceTime,
            string targetEmailAddress)
        {
            if (string.IsNullOrEmpty(subject))
            {
                throw new ArgumentException(nameof(subject));
            }

            if (string.IsNullOrEmpty(body))
            {
                throw new ArgumentException(nameof(body));
            }

            if (string.IsNullOrEmpty(targetEmailAddress))
            {
                throw new ArgumentException(nameof(targetEmailAddress));
            }

            var targetAddress = new MailAddress(targetEmailAddress);
            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.From = _fromAddress;
                mailMessage.ReplyToList.Add(_fromAddress);
                mailMessage.Body = body;
                mailMessage.To.Add(targetAddress);

                SendMessage(mailMessage);
            }

            _logger.LogInformation(
                "Successfully sent notification '{NotificationType}' for reference time '{ReferenceTimeUtc}'",
                subject,
                referenceTime.ToShortDateString());
        }

        private void SendMessage(MailMessage mailMessage)
        {
            var smtpClient = GetOrCreateSmtpClient();

            var alternateHtmlView = AlternateView.CreateAlternateViewFromString(mailMessage.Body, null, "text/html");
            mailMessage.AlternateViews.Add(alternateHtmlView);

            smtpClient.Send(mailMessage);
        }

        private SmtpClient GetOrCreateSmtpClient()
        {
            if (_smtpClient != null)
            {
                return _smtpClient;
            }

            var smtpUri = new SmtpUri(new Uri(_smtpUri));
            _smtpClient = new SmtpClient();
            _smtpClient.Host = smtpUri.Host;
            _smtpClient.Port = smtpUri.Port;
            _smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
            _smtpClient.EnableSsl = smtpUri.Secure;

            if (!string.IsNullOrEmpty(smtpUri.UserName))
            {
                _smtpClient.UseDefaultCredentials = false;
                _smtpClient.Credentials = new NetworkCredential(
                  smtpUri.UserName,
                  smtpUri.Password);
            }

            return _smtpClient;
        }
    }
}
