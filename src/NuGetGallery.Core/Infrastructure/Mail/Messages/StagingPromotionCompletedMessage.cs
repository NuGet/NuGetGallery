// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Text;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Reports one staged artifact's promotion result without implying that its group has finished.
    /// </summary>
    public class StagingPromotionCompletedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _owner;
        private readonly StagingPromotionArtifact _artifact;
        private readonly string _packageUrlTemplate;
        private readonly string _managePackagesUrl;
        private readonly string _emailSettingsUrl;

        public StagingPromotionCompletedMessage(IMessageServiceConfiguration configuration, User owner, StagingPromotionArtifact artifact,
            string packageUrlTemplate, string managePackagesUrl, string emailSettingsUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
            _packageUrlTemplate = packageUrlTemplate ?? throw new ArgumentNullException(nameof(packageUrlTemplate));
            _managePackagesUrl = managePackagesUrl ?? throw new ArgumentNullException(nameof(managePackagesUrl));
            _emailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            if (_owner.IsDeleted || (_artifact.Succeeded && !_owner.NotifyPackageStaged))
            {
                return new EmailRecipients(Array.Empty<MailAddress>());
            }

            return new EmailRecipients(new[] { _owner.ToMailAddress() });
        }

        public override string GetSubject()
        {
            var result = "failed";
            if (_artifact.Succeeded)
            {
                result = "succeeded";
            }

            var description = $"{_artifact.PackageId} {_artifact.Version}";
            description = description.Replace('\r', ' ').Replace('\n', ' ');
            return $"[{_configuration.GalleryOwner.DisplayName}] Staged {GetArtifactKind()} promotion {result} - {description}";
        }

        protected override string GetMarkdownBody() => GetBodyInternal(EmailFormat.Markdown);

        protected override string GetPlainTextBody() => GetBodyInternal(EmailFormat.PlainText);

        protected override string GetHtmlBody() => GetBodyInternal(EmailFormat.Html);

        private string GetBodyInternal(EmailFormat format)
        {
            var markdown = new StringBuilder();
            var description = $"`{_artifact.PackageId} {_artifact.Version}`";
            if (_artifact.Succeeded)
            {
                var url = string.Format(_packageUrlTemplate, Uri.EscapeDataString(_artifact.PackageId), Uri.EscapeDataString(_artifact.Version));
                markdown.Append($"Your staged {GetArtifactKind()} [{description}]({url}) has been published.");
            }
            else
            {
                markdown.Append($"Your staged {GetArtifactKind()} {description} could not be published.");
                markdown.AppendLine().AppendLine().Append("Review its staging status and resolve any blockers before retrying promotion.");
            }

            markdown.AppendLine().AppendLine().Append($"Sign in with access to `{_owner.Username}` to [manage your packages]({_managePackagesUrl}).");
            string body;
            switch (format)
            {
                case EmailFormat.Markdown:
                    body = markdown.ToString();
                    break;
                case EmailFormat.PlainText:
                    body = ToPlainText(markdown.ToString());
                    break;
                case EmailFormat.Html:
                    body = Markdown.ToHtml(markdown.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }

            if (_artifact.Succeeded)
            {
                body += EmailMessageFooter.ForPackageOwnerNotifications(format, _configuration.GalleryOwner.DisplayName, _emailSettingsUrl);
            }

            return body;
        }

        private string GetArtifactKind()
        {
            if (_artifact.Symbols)
            {
                return "symbol package";
            }

            return "package";
        }
    }
}
