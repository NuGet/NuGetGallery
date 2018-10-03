// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ReportMyPackageMessage : EmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ReportMyPackageMessage(
            IMessageServiceConfiguration configuration,
            ReportPackageRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public ReportPackageRequest Request { get; }

        public override IEmailRecipients GetRecipients()
        {
            var cc = new List<MailAddress>();
            if (Request.CopySender)
            {
                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                cc.Add(Request.FromAddress);
            }

            return new EmailRecipients(
                to: new[] { Sender },
                cc: cc,
                replyTo: new[] { Request.FromAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Owner Support Request for '{Request.Package.PackageRegistration.Id}' version {Request.Package.Version} (Reason: {Request.Reason})";

        protected override string GetMarkdownBody()
        {
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

**Package**: {Request.Package.PackageRegistration.Id}
{Request.PackageUrl}

**Version**: {Request.Package.Version}
{Request.PackageVersionUrl}
{userString}

**Reason**:
{Request.Reason}

**Message**:
{Request.Message}


Message sent from {_configuration.GalleryOwner.DisplayName}";
        }

        protected override string GetPlainTextBody()
        {
            var userString = string.Empty;
            if (Request.RequestingUser != null && Request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}User: {0} ({1}){2}{3}",
                    Request.RequestingUser.Username,
                    Request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    Request.RequestingUserUrl);
            }

            return $@"Email: {Request.FromAddress.DisplayName} ({Request.FromAddress.Address})

Package: {Request.Package.PackageRegistration.Id}
{Request.PackageUrl}

Version: {Request.Package.Version}
{Request.PackageVersionUrl}
{userString}

Reason:
{Request.Reason}

Message:
{Request.Message}


Message sent from {_configuration.GalleryOwner.DisplayName}";
        }
    }
}
