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
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly ReportPackageRequest _request;

        public ReportMyPackageMessage(
            ICoreMessageServiceConfiguration configuration,
            ReportPackageRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            var cc = new List<MailAddress>();
            if (_request.CopySender)
            {
                // Normally we use a second email to copy the sender to avoid disclosing the receiver's address
                // but here, the receiver is the gallery operators who already disclose their address
                // CCing helps to create a thread of email that can be augmented by the sending user
                cc.Add(_request.FromAddress);
            }

            return new EmailRecipients(
                to: new[] { Sender },
                cc: cc,
                replyTo: new[] { _request.FromAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Owner Support Request for '{_request.Package.PackageRegistration.Id}' version {_request.Package.Version} (Reason: {_request.Reason})";

        protected override string GetMarkdownBody()
        {
            var userString = string.Empty;
            if (_request.RequestingUser != null && _request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}**User:** {0} ({1}){2}{3}",
                    _request.RequestingUser.Username,
                    _request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    _request.RequestingUserUrl);
            }

            return $@"**Email**: {_request.FromAddress.DisplayName} ({_request.FromAddress.Address})

**Package**: {_request.Package.PackageRegistration.Id}
{_request.PackageUrl}

**Version**: {_request.Package.Version}
{_request.PackageVersionUrl}
{userString}

**Reason**:
{_request.Reason}

**Message**:
{_request.Message}


Message sent from {_configuration.GalleryOwner.DisplayName}";
        }

        protected override string GetPlainTextBody()
        {
            var userString = string.Empty;
            if (_request.RequestingUser != null && _request.RequestingUserUrl != null)
            {
                userString = string.Format(
                    CultureInfo.CurrentCulture,
                    "{2}User: {0} ({1}){2}{3}",
                    _request.RequestingUser.Username,
                    _request.RequestingUser.EmailAddress,
                    Environment.NewLine,
                    _request.RequestingUserUrl);
            }

            return $@"Email: {_request.FromAddress.DisplayName} ({_request.FromAddress.Address})

Package: {_request.Package.PackageRegistration.Id}
{_request.PackageUrl}

Version: {_request.Package.Version}
{_request.PackageVersionUrl}
{userString}

Reason:
{_request.Reason}

Message:
{_request.Message}


Message sent from {_configuration.GalleryOwner.DisplayName}";
        }
    }
}
