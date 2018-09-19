// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using NuGet.Services.Validation;
using NuGetGallery.Infrastructure.Mail;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGetGallery.Services
{
    public class CoreMarkdownMessageService : ICoreMessageService
    {
        private static readonly ReadOnlyCollection<TimeSpan> RetryDelays = Array.AsReadOnly(new[] {
            TimeSpan.FromSeconds(0.1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10)
        });

        public CoreMarkdownMessageService(
            IMailSender mailSender,
            ICoreMessageServiceConfiguration configuration)
        {
            MailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public IMailSender MailSender { get; protected set; }
        public ICoreMessageServiceConfiguration Configuration { get; protected set; }

        public async Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            using (var mailMessage = CreateMailMessage(
                new PackageAddedMessage(Configuration, package, packageUrl, packageSupportUrl, emailSettingsUrl, warningMessages)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendSymbolPackageAddedNoticeAsync(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            using (var mailMessage = CreateMailMessage(
                new SymbolPackageAddedMessage(Configuration, symbolPackage, packageUrl, packageSupportUrl, emailSettingsUrl, warningMessages)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            using (var mailMessage = CreateMailMessage(
                new PackageAddedWithWarningsMessage(Configuration, package, packageUrl, packageSupportUrl, warningMessages)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendPackageValidationFailedNoticeAsync(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            using (var mailMessage = CreateMailMessage(
                new PackageValidationFailedMessage(Configuration, package, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendSymbolPackageValidationFailedNoticeAsync(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            using (var mailMessage = CreateMailMessage(
                  new SymbolPackageValidationFailedMessage(Configuration, symbolPackage, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendValidationTakingTooLongNoticeAsync(Package package, string packageUrl)
        {
            using (var mailMessage = CreateMailMessage(
                new PackageValidationTakingTooLongMessage(Configuration, package, packageUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        public async Task SendValidationTakingTooLongNoticeAsync(SymbolPackage symbolPackage, string packageUrl)
        {
            using (var mailMessage = CreateMailMessage(
                new SymbolPackageValidationTakingTooLongMessage(Configuration, symbolPackage, packageUrl)))
            {
                await SendMessageAsync(mailMessage);
            }
        }

        protected static void AddAllOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners)
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        protected static void AddOwnersSubscribedToPackagePushedNotification(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        protected virtual async Task SendMessageAsync(MailMessage mailMessage)
        {
            if (!mailMessage.To.Any())
            {
                return;
            }

            int attempt = 0;
            bool success = false;
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

        protected async Task SendMessageToSenderAsync(MailMessage mailMessage)
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
                await SendMessageAsync(senderCopy);
            }
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
    }
}
