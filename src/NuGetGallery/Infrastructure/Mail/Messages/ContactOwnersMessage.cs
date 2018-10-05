// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactOwnersMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ContactOwnersMessage(
            IMessageServiceConfiguration configuration,
            ContactOwnersRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public ContactOwnersRequest Request { get; }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            var to = EmailRecipients.GetAllOwners(
                Request.Package.PackageRegistration,
                requireEmailAllowed: true);

            return new EmailRecipients(
                to,
                replyTo: new[] { Request.FromAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Message for owners of the package '{Request.Package.PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            var bodyTemplate = @"_User {0} &lt;{1}&gt; sends the following message to the owners of Package '[{2} {3}]({4})'._

{5}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    [change your email notification settings]({7}).
</em>";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                Request.FromAddress.DisplayName,
                Request.FromAddress.Address,
                Request.Package.PackageRegistration.Id,
                Request.Package.Version,
                Request.PackageUrl,
                Request.HtmlEncodedMessage,
                _configuration.GalleryOwner.DisplayName,
                Request.EmailSettingsUrl);
        }
    }
}
