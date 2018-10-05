// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ContactOwnersMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public ContactOwnersMessage(
            IMessageServiceConfiguration configuration,
            MailAddress fromAddress,
            Package package,
            string packageUrl,
            string htmlEncodedMessage,
            string emailSettingsUrl,
            bool copySender)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            FromAddress = fromAddress ?? throw new ArgumentNullException(nameof(fromAddress));
            Package = package ?? throw new ArgumentNullException(nameof(package));
            PackageUrl = packageUrl ?? throw new ArgumentNullException(nameof(packageUrl));
            HtmlEncodedMessage = htmlEncodedMessage ?? throw new ArgumentNullException(nameof(htmlEncodedMessage));
            EmailSettingsUrl = emailSettingsUrl ?? throw new ArgumentNullException(nameof(emailSettingsUrl));
            CopySender = copySender;
        }

        public MailAddress FromAddress { get; }
        public Package Package { get; }
        public string PackageUrl { get; }
        public string HtmlEncodedMessage { get; }
        public string EmailSettingsUrl { get; }
        public bool CopySender { get; }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
        {
            var to = EmailRecipients.GetAllOwners(
                Package.PackageRegistration,
                requireEmailAllowed: true);

            return new EmailRecipients(
                to,
                cc: CopySender ? new[] { FromAddress } : null,
                replyTo: new[] { FromAddress });
        }

        public override string GetSubject()
            => $"[{_configuration.GalleryOwner.DisplayName}] Message for owners of the package '{Package.PackageRegistration.Id}'";

        protected override string GetMarkdownBody()
        {
            var bodyTemplate = @"_User {0} &lt;{1}&gt; sends the following message to the owners of Package '[{2} {3}]({4})'._

{5}

-----------------------------------------------
<em style=""font-size: 0.8em;"">
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    [change your email notification settings]({7}).
</em>";

            return GetBodyInternal(bodyTemplate);
        }

        protected override string GetPlainTextBody()
        {
            // The HTML emphasis tag is not supported by the Plain Text renderer in Markdig.
            // Manually overriding this one.
            var bodyTemplate = @"User {0} &lt;{1}&gt; sends the following message to the owners of Package '{2} {3} ({4})'.

{5}

-----------------------------------------------
    To stop receiving contact emails as an owner of this package, sign in to the {6} and
    change your email notification settings ({7}).";

            return GetBodyInternal(bodyTemplate);
        }

        private string GetBodyInternal(string template)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                FromAddress.DisplayName,
                FromAddress.Address,
                Package.PackageRegistration.Id,
                Package.Version,
                PackageUrl,
                HtmlEncodedMessage,
                _configuration.GalleryOwner.DisplayName,
                EmailSettingsUrl);
        }
    }
}
