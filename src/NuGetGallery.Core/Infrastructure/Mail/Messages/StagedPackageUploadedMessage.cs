// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Alerts the staging owner to a distinct accepted private artifact upload.
    /// </summary>
    public class StagedPackageUploadedMessage : StagedPackageMessage
    {
        private readonly string _emailSettingsUrl;

        public StagedPackageUploadedMessage(IMessageServiceConfiguration configuration, User owner, Package package, bool symbols, string stagingUrl, string emailSettingsUrl)
            : base(configuration, owner, package, symbols, stagingUrl)
        {
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
        }

        public override string GetSubject()
        {
            return $"[{Configuration.GalleryOwner.DisplayName}] {ArtifactName} staged - {Package.Id} {Package.Version}";
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
            var markdown = $@"{ArtifactName} [{EscapeMarkdown(Package.Id)} {EscapeMarkdown(Package.Version)}]({StagingUrl}) was uploaded to private staging under the owner account **{EscapeMarkdown(Owner.Username)}**.

This upload does not publish the staged content. Sign in with access to the owner account to review its validation status and manage it.

If this upload was unexpected, review the account's API keys and trusted publishing settings and [contact support](mailto:{Configuration.GalleryOwner.Address}).";

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

            return body + EmailMessageFooter.ForPackageOwnerNotifications(format, Configuration.GalleryOwner.DisplayName, _emailSettingsUrl);
        }
    }
}
