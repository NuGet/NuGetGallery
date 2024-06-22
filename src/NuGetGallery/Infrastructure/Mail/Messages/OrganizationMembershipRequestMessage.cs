// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class OrganizationMembershipRequestMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public OrganizationMembershipRequestMessage(
            IMessageServiceConfiguration configuration,
            Organization organization,
            User newUser,
            User adminUser,
            bool isAdmin,
            string profileUrl,
            string confirmationUrl,
            string rejectionUrl)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Organization = organization ?? throw new ArgumentNullException(nameof(organization));
            NewUser = newUser ?? throw new ArgumentNullException(nameof(newUser));
            AdminUser = adminUser ?? throw new ArgumentNullException(nameof(adminUser));
            ProfileUrl = profileUrl ?? throw new ArgumentNullException(nameof(profileUrl));
            RawConfirmationUrl = confirmationUrl ?? throw new ArgumentNullException(nameof(confirmationUrl));
            RawRejectionUrl = rejectionUrl ?? throw new ArgumentNullException(nameof(rejectionUrl));

            ConfirmationUrl = EscapeLinkForMarkdown(confirmationUrl);
            RejectionUrl = EscapeLinkForMarkdown(rejectionUrl);
            IsAdmin = isAdmin;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public Organization Organization { get; }
        public User NewUser { get; }
        public User AdminUser { get; }
        public bool IsAdmin { get; }
        public string ProfileUrl { get; }
        public string RawConfirmationUrl { get; }
        public string RawRejectionUrl { get; }
        public string ConfirmationUrl { get; }
        public string RejectionUrl { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: NewUser.EmailAllowed
                    ? new[] { NewUser.ToMailAddress() }
                    : Array.Empty<MailAddress>(),
                replyTo: new[]
                {
                    Organization.ToMailAddress(),
                    AdminUser.ToMailAddress()
                });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Membership request for organization '{Organization.Username}'";

        protected override string GetMarkdownBody()
        {
            var membershipLevel = IsAdmin ? "an administrator" : "a collaborator";
            return string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{AdminUser.Username}' would like you to become {membershipLevel} of their organization, ['{Organization.Username}']({ProfileUrl}).

To learn more about organization roles, [refer to the documentation.](https://go.microsoft.com/fwlink/?linkid=870439)

To accept the request and become {membershipLevel} of '{Organization.Username}':

[{ConfirmationUrl}]({RawConfirmationUrl})

To decline the request:

[{RejectionUrl}]({RawRejectionUrl})

Thanks,
The {_configuration.GalleryOwner.DisplayName} Team");
        }
    }
}