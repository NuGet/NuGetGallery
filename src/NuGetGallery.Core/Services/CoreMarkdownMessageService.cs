// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using AnglicanGeek.MarkdownMailer;
using NuGet.Services.Validation;
using NuGetGallery.Infrastructure.Mail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public class CoreMarkdownMessageService : ICoreMessageService
    {
        protected readonly IEmailBodyBuilder MarkdownEmailBodyBuilder;
        private static readonly ReadOnlyCollection<TimeSpan> RetryDelays = Array.AsReadOnly(new[] {
            TimeSpan.FromSeconds(0.1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10)
        });

        public CoreMarkdownMessageService(
            IMailSender mailSender,
            ICoreMessageServiceConfiguration coreConfiguration)
        {
            MailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            CoreConfiguration = coreConfiguration ?? throw new ArgumentNullException(nameof(coreConfiguration));
            MarkdownEmailBodyBuilder = new MarkdownEmailBodyBuilder(coreConfiguration);
        }

        public IMailSender MailSender { get; protected set; }
        public ICoreMessageServiceConfiguration CoreConfiguration { get; protected set; }

        public async Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            bool hasWarnings = warningMessages != null && warningMessages.Any();

            var subject = EmailSubjectBuilder.ForSendPackageAddedNotice(
                CoreConfiguration.GalleryOwner.DisplayName,
                package,
                hasWarnings);

            var body = MarkdownEmailBodyBuilder.ForSendPackageAddedNotice(hasWarnings, warningMessages, package, packageUrl, packageSupportUrl, emailSettingsUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendSymbolPackageAddedNoticeAsync(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            var hasWarnings = warningMessages != null && warningMessages.Any();
            var subject = EmailSubjectBuilder.ForSendSymbolPackageAddedNotice(CoreConfiguration.GalleryOwner.DisplayName, hasWarnings, symbolPackage);
            var body = MarkdownEmailBodyBuilder.ForSendSymbolPackageAddedNotice(symbolPackage, packageUrl, packageSupportUrl, emailSettingsUrl, warningMessages, hasWarnings);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(symbolPackage.Package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var subject = EmailSubjectBuilder.ForSendPackageAddedWithWarningsNotice(CoreConfiguration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageAddedWithWarningsNotice(package, packageUrl, packageSupportUrl, warningMessages);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendPackageValidationFailedNoticeAsync(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var subject = EmailSubjectBuilder.ForSendPackageValidationFailedNotice(CoreConfiguration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendPackageValidationFailedNotice(package, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddAllOwnersToMailMessage(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendSymbolPackageValidationFailedNoticeAsync(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var subject = EmailSubjectBuilder.ForSendSymbolPackageValidationFailedNotice(CoreConfiguration.GalleryOwner.DisplayName, symbolPackage);
            var body = MarkdownEmailBodyBuilder.ForSendSymbolPackageValidationFailedNotice(symbolPackage, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddAllOwnersToMailMessage(symbolPackage.Package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendValidationTakingTooLongNoticeAsync(Package package, string packageUrl)
        {
            var subject = EmailSubjectBuilder.ForSendValidationTakingTooLongNotice(CoreConfiguration.GalleryOwner.DisplayName, package);
            var body = MarkdownEmailBodyBuilder.ForSendValidationTakingTooLongNotice(package, packageUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        public async Task SendValidationTakingTooLongNoticeAsync(SymbolPackage symbolPackage, string packageUrl)
        {
            var subject = EmailSubjectBuilder.ForSendValidationTakingTooLongNotice(CoreConfiguration.GalleryOwner.DisplayName, symbolPackage);
            var body = MarkdownEmailBodyBuilder.ForSendValidationTakingTooLongNotice(symbolPackage, packageUrl);

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddOwnersSubscribedToPackagePushedNotification(symbolPackage.Package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        protected static void AddAllOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners)
            {
                mailMessage.To.Add(owner.ToMailAddress());
            }
        }

        protected static void AddOwnersToMailMessage(PackageRegistration packageRegistration, MailMessage mailMessage)
        {
            foreach (var owner in packageRegistration.Owners.Where(o => o.EmailAllowed))
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
                CoreConfiguration.GalleryOwner,
                mailMessage.ReplyToList.First()))
            {
                senderCopy.Subject = mailMessage.Subject + " [Sender Copy]";
                senderCopy.Body = string.Format(
                        CultureInfo.CurrentCulture,
                        "You sent the following message via {0}: {1}{1}{2}",
                        CoreConfiguration.GalleryOwner.DisplayName,
                        Environment.NewLine,
                        mailMessage.Body);
                senderCopy.ReplyToList.Add(mailMessage.ReplyToList.First());
                await SendMessageAsync(senderCopy);
            }
        }
    }
}
