// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageAddedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly string _packageUrl;
        private readonly string _packageSupportUrl;
        private readonly string _emailSettingsUrl;
        private readonly IEnumerable<string> _warningMessages;
        private readonly bool _hasWarnings;

        public PackageAddedMessage(
            IMessageServiceConfiguration configuration,
            Package package,
            string packageUrl,
            string packageSupportUrl,
            string emailSettingsUrl,
            IEnumerable<string> warningMessages)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            _packageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            _packageSupportUrl = packageSupportUrl ?? throw new ArgumentNullException(nameof(packageSupportUrl));
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
            _warningMessages = warningMessages;
            _hasWarnings = warningMessages != null && warningMessages.Any();
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Package Package { get; }

        public override IEmailRecipients GetRecipients()
        {
            var to = EmailRecipientsHelper.GetOwnersSubscribedToPackagePushedNotification(Package.PackageRegistration);
            return new EmailRecipients(to);
        }

        public override string GetSubject()
        {
            if (_hasWarnings)
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published with warnings - {Package.PackageRegistration.Id} {Package.Version}";
            }
            else
            {
                return $"[{_configuration.GalleryOwner.DisplayName}] Package published - {Package.PackageRegistration.Id} {Package.Version}";
            }
        }

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
            var warningMessages = GetWarningMessages();

            var markdown = $@"The package [{Package.PackageRegistration.Id} {Package.Version}]({_packageUrl}) was recently published on {_configuration.GalleryOwner.DisplayName} by {Package.User.Username}. If this was not intended, please [contact support]({_packageSupportUrl}).";

            if (!string.IsNullOrEmpty(warningMessages))
            {
                markdown += warningMessages;
            }

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

            return body + EmailMessageFooter.ForPackageOwnerNotifications(format, _configuration.GalleryOwner.DisplayName, _emailSettingsUrl);
        }

        private string GetWarningMessages()
        {
            var warningMessagesPlaceholder = string.Empty;
            if (_hasWarnings)
            {
                warningMessagesPlaceholder = Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, _warningMessages);
            }
            return warningMessagesPlaceholder;
        }
    }
}