// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGetGallery;
using NuGetGallery.Services;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageMessageService : IMessageService<Package>
    {
        private readonly ICoreMessageService _coreMessageService;
        private readonly ILogger<PackageMessageService> _logger;
        private readonly MessageServiceConfiguration _serviceConfiguration;

        public PackageMessageService(
            ICoreMessageService coreMessageService,
            IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor,
            ILogger<PackageMessageService> logger)
        {
            _serviceConfiguration = new MessageServiceConfiguration(emailConfigurationAccessor);
            _coreMessageService = coreMessageService ?? throw new ArgumentNullException(nameof(coreMessageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendPublishedMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);

            await _coreMessageService.SendPackageAddedNoticeAsync(package, galleryPackageUrl, packageSupportUrl, _serviceConfiguration.EmailConfiguration.EmailSettingsUrl);
        }

        public async Task SendValidationFailedMessageAsync(Package package, PackageValidationSet validationSet)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));
            validationSet = validationSet ?? throw new ArgumentNullException(nameof(validationSet));

            var galleryPackageUrl = _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion);
            var packageSupportUrl = _serviceConfiguration.PackageSupportUrl(package.PackageRegistration.Id, package.NormalizedVersion);

            await _coreMessageService.SendPackageValidationFailedNoticeAsync(package, validationSet, galleryPackageUrl, packageSupportUrl, _serviceConfiguration.EmailConfiguration.AnnouncementsUrl, _serviceConfiguration.EmailConfiguration.TwitterUrl);
        }

        public async Task SendValidationTakingTooLongMessageAsync(Package package)
        {
            package = package ?? throw new ArgumentNullException(nameof(package));

            await _coreMessageService.SendValidationTakingTooLongNoticeAsync(package, _serviceConfiguration.GalleryPackageUrl(package.PackageRegistration.Id, package.NormalizedVersion));
        }
    }
}
