// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NuGet.Services.Messaging.Email;

namespace NuGetGallery.AccountDeleter
{
    public class CoreMessageServiceConfiguration : IMessageServiceConfiguration
    {
        public CoreMessageServiceConfiguration(IOptionsSnapshot<AccountDeleteConfiguration> emailConfigurationAccessor)
        {
            if (emailConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(emailConfigurationAccessor));
            }

            var accountDeleteConfiguration = emailConfigurationAccessor.Value ?? throw new ArgumentException("Value property cannot be null", nameof(emailConfigurationAccessor));
            var emailConfiguration = accountDeleteConfiguration.EmailConfiguration;

            if (string.IsNullOrWhiteSpace(emailConfiguration.GalleryOwner))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(emailConfiguration.GalleryOwner)} property cannot be empty", nameof(emailConfigurationAccessor));
            }

            if (string.IsNullOrWhiteSpace(emailConfiguration.GalleryNoReplyAddress))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(emailConfiguration.GalleryNoReplyAddress)} property cannot be empty", nameof(emailConfigurationAccessor));
            }

            GalleryOwner = new MailAddress(emailConfiguration.GalleryOwner);
            GalleryNoReplyAddress = new MailAddress(emailConfiguration.GalleryNoReplyAddress);
        }

        public MailAddress GalleryOwner { get; set; }
        public MailAddress GalleryNoReplyAddress { get; set; }
    }
}
