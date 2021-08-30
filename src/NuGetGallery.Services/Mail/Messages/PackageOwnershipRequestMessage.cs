// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly bool _isToUserOrganization;

        public PackageOwnershipRequestMessage(
            IMessageServiceConfiguration configuration,
            User fromUser,
            User toUser,
            PackageRegistration packageRegistration,
            string packageUrl,
            string confirmationUrl,
            string rejectionUrl,
            string htmlEncodedMessage,
            string policyMessage)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FromUser = fromUser ?? throw new ArgumentNullException(nameof(fromUser));
            ToUser = toUser ?? throw new ArgumentNullException(nameof(toUser));
            PackageRegistration = packageRegistration ?? throw new ArgumentNullException(nameof(packageRegistration));
            PackageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            RawConfirmationUrl = confirmationUrl ?? throw new ArgumentNullException(nameof(confirmationUrl));
            RawRejectionUrl = rejectionUrl ?? throw new ArgumentNullException(nameof(rejectionUrl));
            HtmlEncodedMessage = htmlEncodedMessage;
            PolicyMessage = policyMessage ?? throw new ArgumentNullException(nameof(policyMessage));

            _isToUserOrganization = ToUser is Organization;
            ConfirmationUrl = EscapeLinkForMarkdown(confirmationUrl);
            RejectionUrl = EscapeLinkForMarkdown(rejectionUrl);
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public User FromUser { get; }
        public User ToUser { get; }
        public PackageRegistration PackageRegistration { get; }
        public string PackageUrl { get; }
        public string RawConfirmationUrl { get; }
        public string ConfirmationUrl { get; }
        public string RawRejectionUrl { get; }
        public string RejectionUrl { get; }
        public string HtmlEncodedMessage { get; }
        public string PolicyMessage { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: GalleryEmailRecipientsUtility.GetAddressesWithPermission(
                    ToUser,
                    ActionsRequiringPermissions.HandlePackageOwnershipRequest),
                replyTo: new[] { FromUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            var policyMessage = GetPolicyMessage();

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{FromUser.Username}' would like to add {(_isToUserOrganization ? "your organization" : "you")} as an owner of the package ['{PackageRegistration.Id}']({PackageUrl}).
{policyMessage}");

            if (!string.IsNullOrWhiteSpace(HtmlEncodedMessage))
            {
                body += Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{FromUser.Username}' added the following message for you:

'{HtmlEncodedMessage}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(_isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:

[{ConfirmationUrl}]({RawConfirmationUrl})

To decline:

[{RejectionUrl}]({RawRejectionUrl})");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";

            return body;
        }

        private string GetPolicyMessage()
        {
            var policyMessage = string.Empty;
            if (!string.IsNullOrEmpty(PolicyMessage))
            {
                policyMessage = Environment.NewLine + PolicyMessage + Environment.NewLine;
            }

            return policyMessage;
        }
    }
}
