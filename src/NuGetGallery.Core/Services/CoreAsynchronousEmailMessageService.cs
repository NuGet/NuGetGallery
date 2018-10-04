// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging;
using NuGet.Services.Validation;
using NuGetGallery.Infrastructure.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetGallery.Services
{
    public class CoreAsynchronousEmailMessageService : ICoreMessageService
    {
        private IEmailMessageEnqueuer _emailMessageEnqueuer;

        public CoreAsynchronousEmailMessageService(
            ICoreMessageServiceConfiguration configuration,
            IEmailMessageEnqueuer emailMessageEnqueuer)
        {
            _emailMessageEnqueuer = emailMessageEnqueuer ?? throw new ArgumentNullException(nameof(emailMessageEnqueuer));

            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            PlainTextEmailBodyBuilder = new PlainTextEmailBodyBuilder(configuration);
            HtmlEmailBodyBuilder = new HtmlEmailBodyBuilder(configuration);
        }

        protected ICoreMessageServiceConfiguration Configuration { get; }
        protected IEmailBodyBuilder PlainTextEmailBodyBuilder { get; }
        protected IEmailBodyBuilder HtmlEmailBodyBuilder { get; }

        protected Task EnqueueMessageAsync(EmailMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return _emailMessageEnqueuer.SendEmailMessageAsync(message);
        }

        public Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            var to = AddOwnersSubscribedToPackagePushedNotificationToRecipients(package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            bool hasWarnings = warningMessages != null && warningMessages.Any();

            var subject = EmailSubjectBuilder.ForSendPackageAddedNotice(
                Configuration.GalleryOwner.DisplayName,
                package,
                hasWarnings);

            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageAddedNotice(hasWarnings, warningMessages, package, packageUrl, packageSupportUrl, emailSettingsUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageAddedNotice(hasWarnings, warningMessages, package, packageUrl, packageSupportUrl, emailSettingsUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var to = AddOwnersSubscribedToPackagePushedNotificationToRecipients(package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageAddedWithWarningsNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageAddedWithWarningsNotice(package, packageUrl, packageSupportUrl, warningMessages);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageAddedWithWarningsNotice(package, packageUrl, packageSupportUrl, warningMessages);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendPackageValidationFailedNoticeAsync(Package package, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var to = AddAllOwnersToRecipients(package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendPackageValidationFailedNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendPackageValidationFailedNotice(package, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendPackageValidationFailedNotice(package, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendSymbolPackageAddedNoticeAsync(SymbolPackage symbolPackage, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            var to = AddOwnersSubscribedToPackagePushedNotificationToRecipients(symbolPackage.Package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var hasWarnings = warningMessages != null && warningMessages.Any();
            var subject = EmailSubjectBuilder.ForSendSymbolPackageAddedNotice(Configuration.GalleryOwner.DisplayName, hasWarnings, symbolPackage);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendSymbolPackageAddedNotice(symbolPackage, packageUrl, packageSupportUrl, emailSettingsUrl, warningMessages, hasWarnings);
            var htmlBody = HtmlEmailBodyBuilder.ForSendSymbolPackageAddedNotice(symbolPackage, packageUrl, packageSupportUrl, emailSettingsUrl, warningMessages, hasWarnings);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendSymbolPackageValidationFailedNoticeAsync(SymbolPackage symbolPackage, PackageValidationSet validationSet, string packageUrl, string packageSupportUrl, string announcementsUrl, string twitterUrl)
        {
            var to = AddAllOwnersToRecipients(symbolPackage.Package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendSymbolPackageValidationFailedNotice(Configuration.GalleryOwner.DisplayName, symbolPackage);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendSymbolPackageValidationFailedNotice(symbolPackage, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendSymbolPackageValidationFailedNotice(symbolPackage, validationSet, packageUrl, packageSupportUrl, announcementsUrl, twitterUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendValidationTakingTooLongNoticeAsync(Package package, string packageUrl)
        {
            var to = AddOwnersSubscribedToPackagePushedNotificationToRecipients(package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendValidationTakingTooLongNotice(Configuration.GalleryOwner.DisplayName, package);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendValidationTakingTooLongNotice(package, packageUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendValidationTakingTooLongNotice(package, packageUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        public Task SendValidationTakingTooLongNoticeAsync(SymbolPackage symbolPackage, string packageUrl)
        {
            var to = AddOwnersSubscribedToPackagePushedNotificationToRecipients(symbolPackage.Package.PackageRegistration);
            if (!to.Any())
            {
                return Task.CompletedTask;
            }

            var subject = EmailSubjectBuilder.ForSendValidationTakingTooLongNotice(Configuration.GalleryOwner.DisplayName, symbolPackage);
            var plainTextBody = PlainTextEmailBodyBuilder.ForSendValidationTakingTooLongNotice(symbolPackage, packageUrl);
            var htmlBody = HtmlEmailBodyBuilder.ForSendValidationTakingTooLongNotice(symbolPackage, packageUrl);

            var message = CreateMessage(
                Configuration.GalleryNoReplyAddress.Address,
                subject,
                plainTextBody,
                htmlBody,
                to);

            return EnqueueMessageAsync(message);
        }

        protected EmailMessageData CreateMessage(
            string fromAddress,
            string subject,
            string plainTextBody,
            string htmlBody,
            IReadOnlyList<string> to,
            IReadOnlyList<string> cc = null,
            IReadOnlyList<string> bcc = null,
            IReadOnlyList<string> replyTo = null,
            bool copySender = false,
            bool discloseSenderAddress = false)
        {
            return new EmailMessageData(
                subject,
                plainTextBody,
                htmlBody,
                fromAddress,
                to,
                cc: GenerateCC(fromAddress, cc, copySender, discloseSenderAddress),
                bcc: bcc,
                replyTo: replyTo,
                messageTrackingId: Guid.NewGuid());
        }

        protected static IReadOnlyList<string> AddAllOwnersToRecipients(PackageRegistration packageRegistration)
        {
            var recipients = new List<string>();
            foreach (var owner in packageRegistration.Owners)
            {
                recipients.Add(owner.ToMailAddress().Address);
            }

            return recipients;
        }

        protected static IReadOnlyList<string> AddOwnersSubscribedToPackagePushedNotificationToRecipients(PackageRegistration packageRegistration)
        {
            var recipients = new List<string>();
            foreach (var owner in packageRegistration.Owners.Where(o => o.NotifyPackagePushed))
            {
                recipients.Add(owner.ToMailAddress().Address);
            }

            return recipients;
        }

        private static IReadOnlyList<string> GenerateCC(string fromAddress, IReadOnlyList<string> cc, bool copySender, bool discloseSenderAddress)
        {
            var ccList = new List<string>();

            if (cc != null)
            {
                ccList.AddRange(cc);
            }

            if (copySender && discloseSenderAddress && !ccList.Contains(fromAddress))
            {
                ccList.Add(fromAddress);
            }

            return ccList;
        }

    }
}
