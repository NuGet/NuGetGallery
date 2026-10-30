// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Mail;
using System.Text;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGet.Services.Validation;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    /// <summary>
    /// Reports private artifact validation failures to non-deleted staging owners.
    /// </summary>
    public class StagedPackageValidationFailedMessage : StagedPackageMessage
    {
        private readonly PackageValidationSet _validationSet;
        private readonly string _announcementsUrl;
        private readonly string _twitterUrl;

        public StagedPackageValidationFailedMessage(IMessageServiceConfiguration configuration, User owner, Package package, bool symbols,
            string stagingUrl, PackageValidationSet validationSet, string announcementsUrl, string twitterUrl)
            : base(configuration, owner, package, symbols, stagingUrl)
        {
            _validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));
            _announcementsUrl = announcementsUrl ?? throw new ArgumentNullException(nameof(announcementsUrl));
            _twitterUrl = twitterUrl ?? throw new ArgumentNullException(nameof(twitterUrl));
        }

        public override IEmailRecipients GetRecipients()
        {
            if (Owner.IsDeleted)
            {
                return new EmailRecipients(Array.Empty<MailAddress>());
            }

            return new EmailRecipients(new[] { Owner.ToMailAddress() });
        }

        public override string GetSubject()
        {
            return $"[{Configuration.GalleryOwner.DisplayName}] Staged {ArtifactName.ToLowerInvariant()} validation failed - {Package.Id} {Package.Version}";
        }

        protected override string GetMarkdownBody()
        {
            var issues = _validationSet.GetValidationIssues().ToList();
            var body = new StringBuilder($@"The staged {ArtifactName.ToLowerInvariant()} [{EscapeMarkdown(Package.Id)} {EscapeMarkdown(Package.Version)}]({StagingUrl}) failed validation:
");
            foreach (var issue in issues)
            {
                body.Append($@"
- {issue.ToMarkdownString(_announcementsUrl, _twitterUrl)}");
            }

            body.Append($@"

This staged content remains private and cannot be promoted until validation succeeds. Sign in with access to **{EscapeMarkdown(Owner.Username)}** to review the details.");
            if (issues.Any(issue => issue.IssueCode == ValidationIssueCode.Unknown))
            {
                body.Append($@" [Contact support](mailto:{Configuration.GalleryOwner.Address}) for help.");
            }
            else
            {
                body.Append(" Fix the reported issues, then replace the staged content.");
            }

            return body.ToString();
        }
    }
}
