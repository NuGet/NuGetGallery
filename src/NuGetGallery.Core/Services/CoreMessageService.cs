// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using AnglicanGeek.MarkdownMailer;
using NuGet.Services.Validation;
using NuGet.Services.Validation.Issues;

namespace NuGetGallery.Services
{
    public class CoreMessageService : ICoreMessageService
    {
        private static readonly ReadOnlyCollection<TimeSpan> RetryDelays = Array.AsReadOnly(new[] {
            TimeSpan.FromSeconds(0.1),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10)
        });

        public CoreMessageService(IMailSender mailSender, ICoreMessageServiceConfiguration coreConfiguration)
        {
            MailSender = mailSender ?? throw new ArgumentNullException(nameof(mailSender));
            CoreConfiguration = coreConfiguration ?? throw new ArgumentNullException(nameof(coreConfiguration));
        }

        public IMailSender MailSender { get; protected set; }
        public ICoreMessageServiceConfiguration CoreConfiguration { get; protected set; }

        public async Task SendPackageAddedNoticeAsync(Package package, string packageUrl, string packageSupportUrl, string emailSettingsUrl, IEnumerable<string> warningMessages = null)
        {
            bool hasWarnings = warningMessages != null && warningMessages.Any();

            string subject;
            var warningMessagesPlaceholder = string.Empty;
            if (hasWarnings)
            {
                subject = $"[{CoreConfiguration.GalleryOwner.DisplayName}] Package published with warnings - {package.PackageRegistration.Id} {package.Version}";
                warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);
            }
            else
            {
                subject = $"[{CoreConfiguration.GalleryOwner.DisplayName}] Package published - {package.PackageRegistration.Id} {package.Version}";
            }

            string body = $@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) was recently published on {CoreConfiguration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({packageSupportUrl}).
{warningMessagesPlaceholder}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving emails as an owner of this package, sign in to the {CoreConfiguration.GalleryOwner.DisplayName} and
    [change your email notification settings]({emailSettingsUrl}).
</em>";

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

        public async Task SendPackageAddedWithWarningsNoticeAsync(Package package, string packageUrl, string packageSupportUrl, IEnumerable<string> warningMessages)
        {
            var subject = $"[{CoreConfiguration.GalleryOwner.DisplayName}] Package pushed with warnings - {package.PackageRegistration.Id} {package.Version}";
            var warningMessagesPlaceholder = Environment.NewLine + string.Join(Environment.NewLine, warningMessages);

            string body = $@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) was recently pushed to {CoreConfiguration.GalleryOwner.DisplayName} by {package.User.Username}. If this was not intended, please [contact support]({packageSupportUrl}).
{warningMessagesPlaceholder}
";

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
            var validationIssues = validationSet.GetValidationIssues();

            var subject = $"[{CoreConfiguration.GalleryOwner.DisplayName}] Package validation failed - {package.PackageRegistration.Id} {package.Version}";
            var bodyBuilder = new StringBuilder();
            bodyBuilder.Append($@"The package [{package.PackageRegistration.Id} {package.Version}]({packageUrl}) failed validation because of the following reason(s):
");

            foreach (var validationIssue in validationIssues)
            {
                bodyBuilder.Append($@"
- {ParseValidationIssue(validationIssue, announcementsUrl, twitterUrl)}");
            }

            bodyBuilder.Append($@"

Your package was not published on {CoreConfiguration.GalleryOwner.DisplayName} and is not available for consumption.

");

            if (validationIssues.Any(i => i.IssueCode == ValidationIssueCode.Unknown))
            {
                bodyBuilder.Append($"Please [contact support]({packageSupportUrl}) to help fix your package.");
            }
            else
            {
                var issuePluralString = validationIssues.Count() > 1 ? "all the issues" : "the issue";
                bodyBuilder.Append($"You can reupload your package once you've fixed {issuePluralString} with it.");
            }

            using (var mailMessage = new MailMessage())
            {
                mailMessage.Subject = subject;
                mailMessage.Body = bodyBuilder.ToString();
                mailMessage.From = CoreConfiguration.GalleryNoReplyAddress;

                AddAllOwnersToMailMessage(package.PackageRegistration, mailMessage);

                if (mailMessage.To.Any())
                {
                    await SendMessageAsync(mailMessage);
                }
            }
        }

        private static string ParseValidationIssue(ValidationIssue validationIssue, string announcementsUrl, string twitterUrl)
        {
            switch (validationIssue.IssueCode)
            {
                case ValidationIssueCode.PackageIsSigned:
                    return $"This package could not be published since it is signed. We do not accept signed packages at this moment. To be notified about package signing and more, watch our [Announcements]({announcementsUrl}) page or follow us on [Twitter]({twitterUrl}).";
                case ValidationIssueCode.ClientSigningVerificationFailure:
                    var clientIssue = (ClientSigningVerificationFailure)validationIssue;
                    return clientIssue != null 
                        ? $"**{clientIssue.ClientCode}**: {clientIssue.ClientMessage}" 
                        : "This package's signature was unable to be verified.";
                case ValidationIssueCode.PackageIsZip64:
                    return "Zip64 packages are not supported.";
                case ValidationIssueCode.OnlyAuthorSignaturesSupported:
                    return "Signed packages must only have an author signature. Other signature types are not supported.";
                case ValidationIssueCode.AuthorAndRepositoryCounterSignaturesNotSupported:
                    return "Author countersignatures and repository countersignatures are not supported.";
                case ValidationIssueCode.OnlySignatureFormatVersion1Supported:
                    return "**NU3007:** Package signatures must have format version 1.";
                case ValidationIssueCode.AuthorCounterSignaturesNotSupported:
                    return "Author countersignatures are not supported.";
                case ValidationIssueCode.PackageIsNotSigned:
                    return "This package must be signed with a registered certificate. [Read more...](https://aka.ms/nuget-signed-ref)";
                case ValidationIssueCode.PackageIsSignedWithUnauthorizedCertificate:
                    var certIssue = (UnauthorizedCertificateFailure)validationIssue;
                    return $"The package was signed, but the signing certificate {(certIssue != null ? $"(SHA-1 thumbprint {certIssue.Sha1Thumbprint})" : "")} is not associated with your account. You must register this certificate to publish signed packages. [Read more...](https://aka.ms/nuget-signed-ref)";
                default:
                    return "There was an unknown failure when validating your package.";
            }
        }

        public async Task SendValidationTakingTooLongNoticeAsync(Package package, string packageUrl)
        {
            string subject = "[{0}] Package validation taking longer than expected - {1} {2}";
            string body = "It is taking longer than expected for your package [{1} {2}]({3}) to get published.\n\n" +
                "We are looking into it and there is no action on you at this time. We’ll send you an email notification when your package has been published.\n\n" +
                "Thank you for your patience.";

            body = string.Format(
                CultureInfo.CurrentCulture,
                body,
                CoreConfiguration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version,
                packageUrl);

            subject = string.Format(
                CultureInfo.CurrentCulture,
                subject, 
                CoreConfiguration.GalleryOwner.DisplayName,
                package.PackageRegistration.Id,
                package.Version);

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
