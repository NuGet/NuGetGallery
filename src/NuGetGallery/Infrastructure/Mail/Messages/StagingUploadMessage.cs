// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Markdig;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class StagingUploadMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _owner;
        private readonly StagedPackageResource _stagedPackage;

        public StagingUploadMessage(IMessageServiceConfiguration configuration, User owner, StagedPackageResource stagedPackage)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));
        }

        public override System.Net.Mail.MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            var recipients = _owner.NotifyPackagePushed ? new[] { _owner.ToMailAddress() } : Enumerable.Empty<System.Net.Mail.MailAddress>();
            return new EmailRecipients(recipients.ToArray());
        }

        public override string GetSubject()
        {
            return $"[{_configuration.GalleryOwner.DisplayName}] Package staged - {_stagedPackage.Id} {_stagedPackage.Version}";
        }

        protected override string GetMarkdownBody() => BuildBody(EmailFormat.Markdown);
        protected override string GetPlainTextBody() => BuildBody(EmailFormat.PlainText);
        protected override string GetHtmlBody() => BuildBody(EmailFormat.Html);

        private string BuildBody(EmailFormat format)
        {
            var operations = new[]
                {
                    GetOperation("package", _stagedPackage.Package),
                    GetOperation("symbols", _stagedPackage.Symbols),
                }
                .Where(x => x != null);
            var markdown = $@"A staging upload for **{EscapeMarkdown(_stagedPackage.Id)} {_stagedPackage.Version}** was accepted for **{EscapeMarkdown(_owner.Username)}**.

{string.Join(Environment.NewLine, operations)}";

            switch (format)
            {
                case EmailFormat.Markdown:
                    return markdown;
                case EmailFormat.PlainText:
                    return ToPlainText(markdown);
                case EmailFormat.Html:
                    return Markdown.ToHtml(markdown);
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        private static string GetOperation(string artifact, StagingArtifactResource resource)
        {
            var isContentChange = resource?.Operation == StagingResourceValues.OperationCreated || resource?.Operation == StagingResourceValues.OperationReplaced;
            if (!isContentChange)
            {
                return null;
            }

            return $"- {artifact}: {resource.Operation}";
        }
    }
}
