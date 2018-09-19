// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly OrganizationMembershipRequest _request;

        public OrganizationMembershipRequestMessage(
            ICoreMessageServiceConfiguration configuration,
            OrganizationMembershipRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[]
                {
                    _request.NewUser.ToMailAddress()
                },
                replyTo: new[]
                {
                    _request.Organization.ToMailAddress(),
                    _request.AdminUser.ToMailAddress()
                });

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{_request.Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            var membershipLevel = _request.IsAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_request.AdminUser.Username}' would like you to become {membershipLevel} of their organization, ['{_request.Organization.Username}']({_request.ProfileUrl}).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become {membershipLevel} of '{_request.Organization.Username}':

[{_request.ConfirmationUrl}]({_request.RawConfirmationUrl})

To decline the request:

[{_request.RejectionUrl}]({_request.RawRejectionUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }

        protected override string GetPlainTextBody()
        {
            var membershipLevel = _request.IsAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_request.AdminUser.Username}' would like you to become {membershipLevel} of their organization, '{_request.Organization.Username}' ({_request.ProfileUrl}).

To learn more about organization roles, refer to the documentation (https://go.microsoft.com/fwlink/?linkid=870439).

To accept the request and become {membershipLevel} of '{_request.Organization.Username}':

{_request.RawConfirmationUrl}

To decline the request:

{_request.RawRejectionUrl}

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}