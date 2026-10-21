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
    /// Sends private staged symbol validation notifications to the staging owner.
    /// </summary>
    public class StagedSymbolPackageMessageService : IMessageService<StagedSymbolPackage>
    {
        private readonly IMessageService _messageService;
        private readonly MessageServiceConfiguration _configuration;

        public StagedSymbolPackageMessageService(IMessageService messageService, IOptionsSnapshot<EmailConfiguration> emailConfigurationAccessor)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _configuration = new MessageServiceConfiguration(emailConfigurationAccessor);
        }

        public Task SendPublishedMessageAsync(StagedSymbolPackage entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var identity = entity.StagedPackageIdentity;
            var stagingUrl = _configuration.StagedSymbolPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationSucceededMessage(
                _configuration, identity.Owner, identity.Package, symbols: true, stagingUrl, _configuration.EmailConfiguration.EmailSettingsUrl));
        }

        public Task SendValidationFailedMessageAsync(StagedSymbolPackage entity, PackageValidationSet validationSet)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var identity = entity.StagedPackageIdentity;
            var email = _configuration.EmailConfiguration;
            var stagingUrl = _configuration.StagedSymbolPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationFailedMessage(
                _configuration, identity.Owner, identity.Package, symbols: true, stagingUrl, validationSet, email.AnnouncementsUrl, email.TwitterUrl));
        }

        public Task SendValidationTakingTooLongMessageAsync(StagedSymbolPackage entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var identity = entity.StagedPackageIdentity;
            var email = _configuration.EmailConfiguration;
            var stagingUrl = _configuration.StagedSymbolPackageUrl(identity.Package.Id, identity.Package.NormalizedVersion);
            return _messageService.SendMessageAsync(new StagedPackageValidationTakingTooLongMessage(
                _configuration, identity.Owner, identity.Package, symbols: true, stagingUrl, email.EmailSettingsUrl));
        }
    }
}
