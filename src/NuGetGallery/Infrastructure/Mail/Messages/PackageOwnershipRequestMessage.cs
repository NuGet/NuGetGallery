// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using System.Web;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class PackageOwnershipRequestMessage : EmailBuilder
    {
        private readonly PackageOwnershipRequest _request;
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly bool _isToUserOrganization;
        private readonly string _rawConfirmationUrl;
        private readonly string _confirmationUrl;
        private readonly string _rawRejectionUrl;
        private readonly string _rejectionUrl;

        public PackageOwnershipRequestMessage(
            ICoreMessageServiceConfiguration configuration,
            PackageOwnershipRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _request = request ?? throw new ArgumentNullException(nameof(request));

            _isToUserOrganization = request.ToUser is Organization;

            _rawConfirmationUrl = request.ConfirmationUrl ?? throw new ArgumentNullException(nameof(request.ConfirmationUrl));
            _confirmationUrl = HttpUtility.UrlDecode(request.ConfirmationUrl).Replace("_", "\\_");
            _rawRejectionUrl = request.RejectionUrl ?? throw new ArgumentNullException(nameof(request.RejectionUrl));
            _rejectionUrl = HttpUtility.UrlDecode(request.RejectionUrl).Replace("_", "\\_");
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipientsWithPermission(
                _request.ToUser,
                ActionsRequiringPermissions.HandlePackageOwnershipRequest,
                replyTo: new[] { _request.FromUser.ToMailAddress() });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Package ownership request for '{_request.PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            var policyMessage = GetPolicyMessage();

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_request.FromUser.Username}' would like to add {(_isToUserOrganization ? "your organization" : "you")} as an owner of the package ['{_request.PackageRegistration.Id}']({_request.PackageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(_request.HtmlEncodedMessage))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{_request.FromUser.Username}' added the following message for you:

'{_request.HtmlEncodedMessage}'");
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
            if (!string.IsNullOrEmpty(_request.PolicyMessage))
            {
                policyMessage = Environment.NewLine + _request.PolicyMessage + Environment.NewLine;
            }

            return policyMessage;
        }

        protected override string GetPlainTextBody()
        {
            var policyMessage = GetPolicyMessage();

            string body = string.Format(
                CultureInfo.CurrentCulture,
                $@"The user '{_request.FromUser.Username}' would like to add {(_isToUserOrganization ? "your organization" : "you")} as an owner of the package '{_request.PackageRegistration.Id}' ({_request.PackageUrl}).

{policyMessage}");

            if (!string.IsNullOrWhiteSpace(_request.HtmlEncodedMessage))
            {
                body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"The user '{_request.FromUser.Username}' added the following message for you:

'{_request.HtmlEncodedMessage}'");
            }

            body += Environment.NewLine + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, $@"To accept this request and {(_isToUserOrganization ? "make your organization" : "become")} a listed owner of the package:
{_rawConfirmationUrl}

To decline:
{_rawRejectionUrl}");

            body += Environment.NewLine + Environment.NewLine + $@"Thanks,
The {_configuration.GalleryOwner.DisplayName} Team";

            return body;
        }
    }
}
