// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class SigninAssistanceMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly MailAddress _toAddress;
        private readonly IEnumerable<Credential> _credentials;

        public SigninAssistanceMessage(
            IMessageServiceConfiguration configuration,
            MailAddress toAddress,
            IEnumerable<Credential> credentials)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _toAddress = toAddress ?? throw new ArgumentNullException(nameof(toAddress));
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(to: new[] { _toAddress });
        }

        public override string GetSubject() => $"[{_configuration.GalleryOwner.DisplayName}] Sign-In Assistance.";

        protected override string GetMarkdownBody()
        {
            string body = @"Hi there,

We heard you were looking for Microsoft logins associated with your account on {0}.

{1}

Thanks,

The {0} Team";

            string msaIdentity;
            if (_credentials.Any())
            {
                var identities = string.Join("; ", _credentials.Select(cred => cred.Identity).ToArray());
                msaIdentity = string.Format(@"Our records indicate the associated Microsoft login(s): {0}.", identities);
            }
            else
            {
                msaIdentity = "No associated Microsoft logins were found.";
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                body,
                _configuration.GalleryOwner.DisplayName,
                msaIdentity);
        }
    }
}
