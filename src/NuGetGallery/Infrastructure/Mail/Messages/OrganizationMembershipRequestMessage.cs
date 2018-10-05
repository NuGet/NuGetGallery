// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMembershipRequestMessage(
            IMessageServiceConfiguration configuration,
            OrganizationMembershipRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));

            ConfirmationUrl = EscapeLinkForMarkdown(request.RawConfirmationUrl);
            RejectionUrl = EscapeLinkForMarkdown(request.RawRejectionUrl);
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public OrganizationMembershipRequest Request { get; }
        public string ConfirmationUrl { get; }
        public string RejectionUrl { get; }

        public override IEmailRecipients GetRecipients()
        {
            if (!Request.NewUser.EmailAllowed)
            {
                return EmailRecipients.None;
            }

            return new EmailRecipients(
                to: new[] { Request.NewUser.ToMailAddress() },
                replyTo: new[]
                {
                    Request.Organization.ToMailAddress(),
                    Request.AdminUser.ToMailAddress()
                });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{Request.Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            var membershipLevel = Request.IsAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{Request.AdminUser.Username}' would like you to become {membershipLevel} of their organization, ['{Request.Organization.Username}']({Request.ProfileUrl}).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become {membershipLevel} of '{Request.Organization.Username}':

[{ConfirmationUrl}]({Request.RawConfirmationUrl})

To decline the request:

[{RejectionUrl}]({Request.RawRejectionUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}