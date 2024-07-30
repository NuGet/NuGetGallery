// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using NuGet.Services.Messaging.Email;

namespace NuGet.Services.Validation.Orchestrator
{
    public class MessageServiceConfiguration : IMessageServiceConfiguration
    {
        public EmailConfiguration EmailConfiguration { get; }

        public MessageServiceConfiguration(IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor)
        {
            if (emailConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(emailConfigurationAccessor));
            }
            EmailConfiguration = emailConfigurationAccessor.Value ?? throw new ArgumentException("Value cannot be null", nameof(emailConfigurationAccessor));
            
            if (string.IsNullOrWhiteSpace(EmailConfiguration.PackageUrlTemplate))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(EmailConfiguration.PackageUrlTemplate)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (string.IsNullOrWhiteSpace(EmailConfiguration.PackageSupportTemplate))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(EmailConfiguration.PackageSupportTemplate)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (string.IsNullOrWhiteSpace(EmailConfiguration.EmailSettingsUrl))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(EmailConfiguration.EmailSettingsUrl)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (!Uri.TryCreate(EmailConfiguration.EmailSettingsUrl, UriKind.Absolute, out Uri result))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(EmailConfiguration.EmailSettingsUrl)} must be an absolute Url", nameof(emailConfigurationAccessor));
            }

            GalleryOwner = new MailAddress(EmailConfiguration.GalleryOwner);
            GalleryNoReplyAddress = new MailAddress(EmailConfiguration.GalleryNoReplyAddress);
        }

        public string GalleryPackageUrl(string packageId, string packageNormalizedVersion) => string.Format(EmailConfiguration.PackageUrlTemplate, packageId, packageNormalizedVersion);
        public string PackageSupportUrl(string packageId, string packageNormalizedVersion) => string.Format(EmailConfiguration.PackageSupportTemplate, packageId, packageNormalizedVersion);

        public MailAddress GalleryOwner { get; set; }

        /// <summary>
        /// Gets the gallery e-mail from name and email address
        /// </summary>
        public MailAddress GalleryNoReplyAddress { get; set; }
    }
}
