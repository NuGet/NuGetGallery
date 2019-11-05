// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using System.Globalization;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class ApiKeyCredentialRevokedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly Credential _credential;
        private readonly string _leakedUrl;
        private readonly string _revokedBy;

        public ApiKeyCredentialRevokedMessage(
            IMessageServiceConfiguration configuration,
            Credential credential,
            string leakedUrl,
            string revokedBy)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _leakedUrl = leakedUrl ?? throw new ArgumentNullException(nameof(leakedUrl));
            _revokedBy = revokedBy ?? throw new ArgumentNullException(nameof(revokedBy));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(to: new[] { _credential.User.ToMailAddress() });

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] API Key '{1}' has been revoked due to a potential leaking from {2}.",
                _configuration.GalleryOwner.DisplayName,
                _credential.Description,
                _revokedBy);

        protected override string GetMarkdownBody()
        {
            var body = @"Hello, 
{4}We find a potential leaking of the API key '{0}', which has the access to manage your packages. The API key '{0}' has been revoked in order to protect your packages.
{4}The leaking Url from {1} is: {2}
{4}Please regenerate or create a new API key by visiting the API keys profile page, and we recommend the following ways to avoid exposing credentials in the future: 
- Use NuGet CLI 'setapikey' command to save the API key for a given server URL;
- ...

Thanks,
The NuGet Team";
            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _credential.Description,
                _revokedBy,
                _leakedUrl,
                Environment.NewLine);
        }
    }
}