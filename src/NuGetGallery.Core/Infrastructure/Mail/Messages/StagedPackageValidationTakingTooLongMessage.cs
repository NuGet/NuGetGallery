// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Reports delayed private artifact validation without implying publication.
    /// </summary>
    public class StagedPackageValidationTakingTooLongMessage : StagedPackageMessage
    {
        private readonly string _emailSettingsUrl;

        public StagedPackageValidationTakingTooLongMessage(IMessageServiceConfiguration configuration, User owner, Package package, bool symbols,
            string stagingUrl, string emailSettingsUrl)
            : base(configuration, owner, package, symbols, stagingUrl)
        {
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
        }

        public override string GetSubject()
        {
            return $"[{Configuration.GalleryOwner.DisplayName}] Staged {ArtifactName.ToLowerInvariant()} validation taking longer than expected - {Package.Id} {Package.Version}";
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
            var markdown = $@"Validation of the staged {ArtifactName.ToLowerInvariant()} [{EscapeMarkdown(Package.Id)} {EscapeMarkdown(Package.Version)}]({StagingUrl}) is taking longer than expected.

The staged content remains private. There is no need to upload it again. Sign in with access to **{EscapeMarkdown(Owner.Username)}** to check its validation status.";

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
