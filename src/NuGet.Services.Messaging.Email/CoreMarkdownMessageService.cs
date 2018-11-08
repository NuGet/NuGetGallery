// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;

namespace NuGet.Services.Messaging.Email
{
    public class CoreMarkdownMessageService : IMessageService
    {
        private static readonly ReadOnlyCollection<TimeSpan> RetryDelays = Array.AsReadOnly(new[] {
            TimeSpan.FromSeconds(0.1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10)
        });

        public CoreMarkdownMessageService(
            IMailSender mailSender,
            IMessageServiceConfiguration configuration)
        {
            MailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public IMailSender MailSender { get; protected set; }
        public IMessageServiceConfiguration Configuration { get; protected set; }

        public async Task SendMessageAsync(IEmailBuilder emailBuilder, bool copySender = false, bool discloseSenderAddress = false)
        {
            if (emailBuilder == null)
            {
                throw new ArgumentNullException(nameof(emailBuilder));
            }

            using (var email = CreateMailMessage(emailBuilder))
            {
                if (email == null || !email.To.Any())
                {
                    // A null email or one without recipients cannot be sent.
                    return;
                }

                await SendMessageInternalAsync(email);

                if (copySender && !discloseSenderAddress)
                {
                    await SendMessageToSenderAsync(email);
                }
            }
        }

        protected virtual async Task SendMessageInternalAsync(MailMessage mailMessage)
        {
            var attempt = 0;
            var success = false;
            while (!success)
            {
                try
                {
                    await AttemptSendMessageAsync(mailMessage, attempt + 1);
                    success = true;
                }
                catch (SmtpException)
                {
                    if (attempt < RetryDelays.Count)
                    {
                        await Task.Delay(RetryDelays[attempt]);
                        attempt++;
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        protected virtual Task AttemptSendMessageAsync(MailMessage mailMessage, int attemptNumber)
        {
            // AnglicanGeek.MarkdownMailer doesn't have an async overload
            MailSender.Send(mailMessage);
            return Task.CompletedTask;
        }

        protected static MailMessage CreateMailMessage(IEmailBuilder emailBuilder)
        {
            if (emailBuilder == null)
            {
                throw new ArgumentNullException(nameof(emailBuilder));
            }

            var mailMessage = new MailMessage();
            mailMessage.From = emailBuilder.Sender;
            mailMessage.Subject = emailBuilder.GetSubject();
            mailMessage.Body = emailBuilder.GetBody(EmailFormat.Markdown);

            var recipients = emailBuilder.GetRecipients();
            foreach (var toAddress in recipients.To)
            {
                mailMessage.To.Add(toAddress);
            }

            foreach (var ccAddress in recipients.CC)
            {
                mailMessage.CC.Add(ccAddress);
            }

            foreach (var bccAddress in recipients.Bcc)
            {
                mailMessage.Bcc.Add(bccAddress);
            }

            foreach (var replyToAddress in recipients.ReplyTo)
            {
                mailMessage.ReplyToList.Add(replyToAddress);
            }

            return mailMessage;
        }

        private async Task SendMessageToSenderAsync(MailMessage mailMessage)
        {
            using (var senderCopy = new MailMessage(
                Configuration.GalleryOwner,
                mailMessage.ReplyToList.First()))
            {
                senderCopy.Subject = mailMessage.Subject + " [Sender Copy]";
                senderCopy.Body = string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        Configuration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        mailMessage.Body);
                senderCopy.ReplyToList.Add(mailMessage.ReplyToList.First());
                await SendMessageInternalAsync(senderCopy);
            }
        }
    }
}
