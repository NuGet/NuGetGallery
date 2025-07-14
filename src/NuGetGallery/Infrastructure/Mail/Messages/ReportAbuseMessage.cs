// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ReportAbuseMessage : ReportPackageMessageBase
    {
        public ReportAbuseMessage(
            IMessageServiceConfiguration configuration,
            ReportPackageRequest request,
            bool alreadyContactedOwners)
            : base(configuration, request)
        {
            AlreadyContactedOwners = alreadyContactedOwners;
        }

        public bool AlreadyContactedOwners { get; }

        public override string GetSubject()
            => $"[{Configuration.GalleryOwner.DisplayName}] Support Request for '{Request.Package.PackageRegistration.Id}' version {Request.Package.Version} (Reason: {Request.Reason})";

        protected override string GetMarkdownBody()
        {
            string emailLine;
            var fromAddress = EscapeMarkdown(Request.FromAddress.Address);
            var fromAddressDisplayName = EscapeMarkdown(Request.FromAddress.DisplayName);
            if (!string.IsNullOrEmpty(fromAddressDisplayName))
            {
                emailLine = $"**Email:** {fromAddressDisplayName} ({fromAddress})";
            }
            else
            {
                emailLine = $"**Email:** {fromAddress}";
            }

            var signatureLine = string.Empty;
            if (!string.IsNullOrEmpty(Request.Signature))
            {
                signatureLine = $"**Signature:** {EscapeMarkdown(Request.Signature)}";
            }

            var packageId = EscapeMarkdown(Request.Package.PackageRegistration.Id);

            var userLine = string.Empty;
            if (Request.RequestingUser != null && Request.RequestingUserUrl != null)
            {
                var username = EscapeMarkdown(Request.RequestingUser.Username);
                var email = EscapeMarkdown(Request.RequestingUser.EmailAddress);
                var url = EscapeMarkdown(Request.RequestingUserUrl);
                userLine = $"**User:** [{username} ({email})]({url})";
            }

            var alreadyContactedOwners = AlreadyContactedOwners ? "Yes" : "No";
            var message = EscapeMarkdown(Request.Message);
            var galleryOwnerDisplayName = EscapeMarkdown(Configuration.GalleryOwner.DisplayName);

            return $@"{emailLine}

{signatureLine}

**Package:** [{packageId}]({Request.PackageUrl})

**Version:** [{Request.Package.Version}]({Request.PackageVersionUrl})

{userLine}

**Reason:** {Request.Reason}

**Has the package owner been contacted?** {alreadyContactedOwners}

**Message:** {message}

_Message sent from {galleryOwnerDisplayName}_";
        }
    }
}
