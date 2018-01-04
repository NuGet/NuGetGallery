// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class MessageService : IMessageService
    {
        private readonly ICoreMessageService _coreMessageService;
        private readonly EmailConfiguration _emailConfiguration;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            ICoreMessageService coreMessageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<MessageService> logger)
        {
            _coreMessageService = coreMessageService ?? throw new ArgumentNullException(nameof(coreMessageService));
            if (emailConfigurationAccessor == null)
            {
                throw new ArgumentNullException(nameof(emailConfigurationAccessor));
            }
            _emailConfiguration = emailConfigurationAccessor.Value ?? throw new ArgumentException("Value cannot be null", nameof(emailConfigurationAccessor));
            if (string.IsNullOrWhiteSpace(_emailConfiguration.PackageUrlTemplate))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(_emailConfiguration.PackageUrlTemplate)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (string.IsNullOrWhiteSpace(_emailConfiguration.PackageSupportTemplate))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(_emailConfiguration.PackageSupportTemplate)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (string.IsNullOrWhiteSpace(_emailConfiguration.EmailSettingsUrl))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(_emailConfiguration.EmailSettingsUrl)} cannot be empty", nameof(emailConfigurationAccessor));
            }
            if (!Uri.TryCreate(_emailConfiguration.EmailSettingsUrl, UriKind.Absolute, out Uri result))
            {
                throw new ArgumentException($"{nameof(emailConfigurationAccessor.Value)}.{nameof(_emailConfiguration.EmailSettingsUrl)} must be an absolute Url", nameof(emailConfigurationAccessor));
            }
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void SendPackagePublishedMessage(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = string.Format(_emailConfiguration.PackageUrlTemplate, package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = string.Format(_emailConfiguration.PackageSupportTemplate, package.PackageRegistration.Id, package.NormalizedVersion);
            _coreMessageService.SendPackageAddedNotice(package, galleryPackageUrl, packageSupportUrl, _emailConfiguration.EmailSettingsUrl);
        }

        public void SendPackageValidationFailedMessage(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = string.Format(_emailConfiguration.PackageUrlTemplate, package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = string.Format(_emailConfiguration.PackageSupportTemplate, package.PackageRegistration.Id, package.NormalizedVersion);
            _coreMessageService.SendPackageValidationFailedNotice(package, galleryPackageUrl, packageSupportUrl);
        }

        public void SendPackageSignedValidationFailedMessage(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = string.Format(_emailConfiguration.PackageUrlTemplate, package.PackageRegistration.Id, package.NormalizedVersion);
            _coreMessageService.SendSignedPackageNotAllowedNotice(package, galleryPackageUrl, _emailConfiguration.AnnouncementsUrl, _emailConfiguration.TwitterUrl);
        }
    }
}
