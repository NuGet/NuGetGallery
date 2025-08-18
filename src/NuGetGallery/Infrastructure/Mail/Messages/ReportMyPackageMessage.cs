// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ReportMyPackageMessage : ReportPackageMessageBase
    {
        public ReportMyPackageMessage(
            IMessageServiceConfiguration configuration,
            ReportPackageRequest request)
            : base(configuration, request)
        {
        }

        public override string GetSubject()
            => $"[{Configuration.GalleryOwner.DisplayName}] Owner Support Request for '{Request.Package.PackageRegistration.Id}' version {Request.Package.Version} (Reason: {Request.Reason})";

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

            var packageId = EscapeMarkdown(Request.Package.PackageRegistration.Id);

            var userLine = string.Empty;
            if (Request.RequestingUser != null && Request.RequestingUserUrl != null)
            {
                var username = EscapeMarkdown(Request.RequestingUser.Username);
                var email = EscapeMarkdown(Request.RequestingUser.EmailAddress);
                var url = EscapeMarkdown(Request.RequestingUserUrl);
                userLine = $"**User:** [{username} ({email})]({url})";
            }

            var message = EscapeMarkdown(Request.Message);
            var galleryOwnerDisplayName = EscapeMarkdown(Configuration.GalleryOwner.DisplayName);

            return $@"{emailLine}

**Package:** [{packageId}]({Request.PackageUrl})

**Version:** [{Request.Package.Version}]({Request.PackageVersionUrl})

{userLine}

**Reason:** {Request.Reason}

**Message:** {message}

_Message sent from {galleryOwnerDisplayName}_";
        }
    }
}
