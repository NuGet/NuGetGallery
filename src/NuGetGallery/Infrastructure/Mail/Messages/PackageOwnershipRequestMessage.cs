// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly bool _isToUserOrganization;
        private readonly string _rawConfirmationUrl;
        private readonly string _confirmationUrl;
        private readonly string _rawRejectionUrl;
        private readonly string _rejectionUrl;

        public PackageOwnershipRequestMessage(
            IMessageServiceConfiguration configuration,
            PackageOwnershipRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));

            _isToUserOrganization = request.ToUser is Organization;

            _rawConfirmationUrl = request.ConfirmationUrl ?? throw new ArgumentNullException(nameof(request.ConfirmationUrl));
            _confirmationUrl = EscapeLinkForMarkdown(request.ConfirmationUrl);
            _rawRejectionUrl = request.RejectionUrl ?? throw new ArgumentNullException(nameof(request.RejectionUrl));
            _rejectionUrl = EscapeLinkForMarkdown(request.RejectionUrl);
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public PackageOwnershipRequest Request { get; }

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                Request.ToUser,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { Request.FromUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{Request.PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            var policyMessage = GetPolicyMessage();

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{Request.FromUser.Username}' would like to add {(_isToUserOrganization ? "your organization" : "you")} as an owner of the package ['{Request.PackageRegistration.Id}']({Request.PackageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(Request.HtmlEncodedMessage))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{Request.FromUser.Username}' added the following message for you:

'{Request.HtmlEncodedMessage}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(_isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:

[{_confirmationUrl}]({_rawConfirmationUrl})

To decline:

[{_rejectionUrl}]({_rawRejectionUrl})");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";

            return body;
        }

        private string GetPolicyMessage()
        {
            var policyMessage = string.Empty;
            if (!string.IsNullOrEmpty(Request.PolicyMessage))
            {
                policyMessage = Environment.NewLine + Request.PolicyMessage + Environment.NewLine;
            }

            return policyMessage;
        }
    }
}
