// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using NuGetGallery.Infrastructure.Mail.Requests;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactOwnersMessage : EmailBuilder
    {
        private readonly ICoreMessageServiceConfiguration _configuration;
        private readonly ContactOwnersRequest _request;

        public ContactOwnersMessage(
            ICoreMessageServiceConfiguration configuration, 
            ContactOwnersRequest request)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            var to = AddOwnersToRecipients();

            return new EmailRecipients(
                to,
                replyTo: new[] { _request.FromAddress });
        }

        private IReadOnlyList<MailAddress> AddOwnersToRecipients()
        {
            var recipients = new List<MailAddress>();
            foreach (var owner in _request.Package.PackageRegistration.Owners.Where(o => o.EmailAllowed))
            {
                recipients.Add(owner.ToMailAddress());
            }
            return recipients;
        }

        public override string GetSubject() 
            => $"[{_configuration.GalleryOwner.DisplayName}] Message for owners of the package '{_request.Package.PackageRegistration.Id}'";

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
                _request.FromAddress.DisplayName,
                _request.FromAddress.Address,
                _request.Package.PackageRegistration.Id,
                _request.Package.Version,
                _request.PackageUrl,
                _request.HtmlEncodedMessage,
                _configuration.GalleryOwner.DisplayName,
                _request.EmailSettingsUrl);
        }

        protected override string GetPlainTextBody()
        {
            var bodyTemplate = @"User {0} &lt;{1}&gt; sends the following message to the owners of Package '{2} {3}' ({4}).

{5}

-----------------------------------------------
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    change your email notification settings: {7}";

            return string.Format(
                CultureInfo.CurrentCulture,
                bodyTemplate,
                _request.FromAddress.DisplayName,
                _request.FromAddress.Address,
                _request.Package.PackageRegistration.Id,
                _request.Package.Version,
                _request.PackageUrl,
                _request.HtmlEncodedMessage,
                _configuration.GalleryOwner.DisplayName,
                _request.EmailSettingsUrl);
        }
    }
}
