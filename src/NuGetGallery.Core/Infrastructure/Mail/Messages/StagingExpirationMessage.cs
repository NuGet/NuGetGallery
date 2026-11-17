// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Warns a staging owner about expiration or reports automatic deletion of private staging.
    /// </summary>
    public class StagingExpirationMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _owner;
        private readonly string _description;
        private readonly DateTime _expirationDate;
        private readonly bool _deleted;
        private readonly string _managePackagesUrl;
        private readonly string _emailSettingsUrl;

        public StagingExpirationMessage(IMessageServiceConfiguration configuration, User owner, string description, DateTime expirationDate,
            bool deleted, string managePackagesUrl, string emailSettingsUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("A staging description is required.", nameof(description));
            }

            _description = description;
            _expirationDate = expirationDate;
            _deleted = deleted;
            _managePackagesUrl = ValidateUrl(managePackagesUrl, nameof(managePackagesUrl));
            _emailSettingsUrl = ValidateUrl(emailSettingsUrl, nameof(emailSettingsUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            if (_owner.IsDeleted || !_owner.NotifyPackageStaged)
            {
                return new EmailRecipients(Array.Empty<MailAddress>());
            }

            return new EmailRecipients(new[] { _owner.ToMailAddress() });
        }

        public override string GetSubject()
        {
            var action = "expires soon";
            if (_deleted)
            {
                action = "expired";
            }

            var description = _description.Replace('\r', ' ').Replace('\n', ' ');
            return $"[{_configuration.GalleryOwner.DisplayName}] Your {description} {action}";
        }

        protected override string GetMarkdownBody() => GetBodyInternal(EmailFormat.Markdown);

        protected override string GetPlainTextBody() => GetBodyInternal(EmailFormat.PlainText);

        protected override string GetHtmlBody() => GetBodyInternal(EmailFormat.Html);

        private string GetBodyInternal(EmailFormat format)
        {
            var description = EscapeMarkdown(_description);
            var owner = $"`{_owner.Username}`";
            var manage = $"[manage your packages]({_managePackagesUrl})";
            if (format == EmailFormat.PlainText)
            {
                description = _description;
                owner = _owner.Username;
                manage = $"manage your packages: {_managePackagesUrl}";
            }

            string body;
            if (_deleted)
            {
                body = $"Your {description} has expired and its private staged content has been removed."
                    + "\n\nPublished packages and symbols have not been changed. Upload again if you still want to stage this content.";
            }
            else
            {
                var deadline = _expirationDate.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
                body = $"Your {description} expires at {deadline}. Its private staged content will be removed after that deadline."
                    + "\n\nReview the staged content and promote it before the deadline if you want to publish it.";
            }

            body += $"\n\nSign in with access to {owner} to {manage}.";
            if (format == EmailFormat.Html)
            {
                body = Markdown.ToHtml(body);
            }

            return body + EmailMessageFooter.ForPackageOwnerNotifications(format, _configuration.GalleryOwner.DisplayName, _emailSettingsUrl);
        }

        private static string ValidateUrl(string url, string parameterName)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("An absolute HTTP or HTTPS URL is required.", parameterName);
            }

            return parsed.AbsoluteUri;
        }
    }
}
