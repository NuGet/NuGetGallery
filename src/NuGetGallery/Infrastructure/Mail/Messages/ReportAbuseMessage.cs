// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
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
            var alreadyContactedOwnersString = AlreadyContactedOwners ? "Yes" : "No";
            var userString = string.Empty;
            if (Request.RequestingUser != null && Request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}**User:** {0} ({1}){2}{3}",
                    Request.RequestingUser.Username,
                    Request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    Request.RequestingUserUrl);
            }

            return $@"**Email**: {Request.FromAddress.DisplayName} ({Request.FromAddress.Address})

**Signature**: {Request.Signature}

**Package**: {Request.Package.PackageRegistration.Id}
{Request.PackageUrl}

**Version**: {Request.Package.Version}
{Request.PackageVersionUrl}
{userString}

**Reason**:
{Request.Reason}

**Has the package owner been contacted?**
{alreadyContactedOwnersString}

**Message:**
{Request.Message}


Message sent from {Configuration.GalleryOwner.DisplayName}";
        }
    }
}