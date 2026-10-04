// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Infrastructure.Mail.Messages;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Sends private staged package validation notifications to the staging owner.
    /// </summary>
    public class StagedPackageMessageService : IMessageService<StagedPackage>
    {
        private readonly IMessageService _messageService;
        private readonly MessageServiceConfiguration _configuration;

        public StagedPackageMessageService(IMessageService messageService, IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _configuration = new MessageServiceConfiguration(emailConfigurationAccessor);
        }

        public Task SendPublishedMessageAsync(StagedPackage stagedPackage)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            var identity = stagedPackage.StagedPackageIdentity;
            var stagingUrl = _configuration.StagedPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationSucceededMessage(
                _configuration, identity.Owner, identity.Package, symbols: false, stagingUrl, _configuration.EmailConfiguration.EmailSettingsUrl));
        }

        public Task SendValidationFailedMessageAsync(StagedPackage stagedPackage, PackageValidationSet validationSet)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            var identity = stagedPackage.StagedPackageIdentity;
            var email = _configuration.EmailConfiguration;
            var stagingUrl = _configuration.StagedPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationFailedMessage(
                _configuration, identity.Owner, identity.Package, symbols: false, stagingUrl, validationSet, email.AnnouncementsUrl, email.TwitterUrl));
        }

        public Task SendValidationTakingTooLongMessageAsync(StagedPackage stagedPackage)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            var identity = stagedPackage.StagedPackageIdentity;
            var email = _configuration.EmailConfiguration;
            var stagingUrl = _configuration.StagedPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationTakingTooLongMessage(
                _configuration, identity.Owner, identity.Package, symbols: false, stagingUrl, email.EmailSettingsUrl));
        }
    }
}
