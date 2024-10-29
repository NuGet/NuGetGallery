// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class EmailChangeNoticeToPreviousEmailAddressMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;
        private readonly User _user;
        private readonly string _previousEmailAddress;
        private readonly bool _isOrganization;

        public EmailChangeNoticeToPreviousEmailAddressMessage(
            IMessageServiceConfiguration configuration,
            User user,
            string previousEmailAddress)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _user = user ?? throw new ArgumentNullException(nameof(user));
            _previousEmailAddress = previousEmailAddress ?? throw new ArgumentNullException(nameof(previousEmailAddress));
            _isOrganization = user is Organization;
        }

        public override MailAddress Sender => _configuration.GalleryNoReplyAddress;

        public override IEmailRecipients GetRecipients()
        {
            return new EmailRecipients(
                to: new[] { new MailAddress(_previousEmailAddress, _user.Username) });
        }

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                "[{0}] Recent changes to your {1}'s email",
                _configuration.GalleryOwner.DisplayName,
                _isOrganization ? "organization" : "account");

        protected override string GetMarkdownBody()
        {
            var template = @"The email address associated with your {0} {1} was recently changed from _{2}_ to _{3}_.

Thanks,
The {0} Team";

            return string.Format(
                CultureInfo.CurrentCulture,
                template,
                _configuration.GalleryOwner.DisplayName,
                _isOrganization ? "organization" : "account",
                _previousEmailAddress,
                _user.EmailAddress);
        }
    }
}
