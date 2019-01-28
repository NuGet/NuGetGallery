// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactOwnersMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ContactOwnersMessage(
            IMessageServiceConfiguration configuration,
            MailAddress fromAddress,
            Package package,
            string packageUrl,
            string htmlEncodedMessage,
            string emailSettingsUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FromAddress = fromAddress ?? throw new ArgumentNullException(nameof(fromAddress));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            PackageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            HtmlEncodedMessage = htmlEncodedMessage ?? throw new ArgumentNullException(nameof(htmlEncodedMessage));
            EmailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
        }

        public MailAddress FromAddress { get; }
        public Package Package { get; }
        public string PackageUrl { get; }
        public string HtmlEncodedMessage { get; }
        public string EmailSettingsUrl { get; }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: EmailRecipientsHelper.GetAllOwners(
                    Package.PackageRegistration,
                    requireEmailAllowed: true),
                replyTo: new[] { FromAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Message for owners of the package '{Package.PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            return GetBodyInternal(EmailFormat.Markdown);
        }

        protected override string GetPlainTextBody()
        {
            return GetBodyInternal(EmailFormat.PlainText);
        }

        protected override string GetHtmlBody()
        {
            return GetBodyInternal(EmailFormat.Html);
        }

        private string GetBodyInternal(EmailFormat format)
        {
            var markdown = $@"_User {FromAddress.DisplayName} &lt;{FromAddress.Address}&gt; sends the following message to the owners of Package '[{Package.PackageRegistration.Id} {Package.Version}]({PackageUrl})'._

{HtmlEncodedMessage}";

            string body;
            switch (format)
            {
                case EmailFormat.PlainText:
                    body = ToPlainText(markdown);
                    break;
                case EmailFormat.Markdown:
                    body = markdown;
                    break;
                case EmailFormat.Html:
                    body = Markdown.ToHtml(markdown);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }

            return body + EmailMessageFooter.ForContactOwnerNotifications(format, _configuration.GalleryOwner.DisplayName, EmailSettingsUrl);
        }
    }
}
