// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    public class StagingValidationMessageEmitter : IStagingValidationMessageEmitter
    {
        private readonly IPackageValidationEnqueuer _packageEnqueuer;
        private readonly IPackageValidationEnqueuer _symbolPackageEnqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public StagingValidationMessageEmitter(
            IPackageValidationEnqueuer packageEnqueuer,
            IPackageValidationEnqueuer symbolPackageEnqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _packageEnqueuer = packageEnqueuer ?? throw new ArgumentNullException(nameof(packageEnqueuer));
            _symbolPackageEnqueuer = symbolPackageEnqueuer ?? throw new ArgumentNullException(nameof(symbolPackageEnqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsService));
            }

            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(StagingValidationMessageEmitter));
        }

        public Task StartValidationAsync(Package package, Guid validationTrackingId)
        {
            return StartValidationAsync(package, package?.PackageStatusKey, validationTrackingId, ValidatingType.Package, _packageEnqueuer);
        }

        public Task StartValidationAsync(SymbolPackage symbolPackage, Guid validationTrackingId)
        {
            return StartValidationAsync(symbolPackage, symbolPackage?.StatusKey, validationTrackingId, ValidatingType.SymbolPackage, _symbolPackageEnqueuer);
        }

        private async Task StartValidationAsync(IPackageEntity entity, PackageStatus? status, Guid validationTrackingId, ValidatingType validatingType, IPackageValidationEnqueuer enqueuer)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.Key <= 0)
            {
                throw new InvalidOperationException("A staged entity must be persisted before validation is enqueued.");
            }

            if (status != PackageStatus.Staged)
            {
                throw new InvalidOperationException($"A staged validation requires the entity to have {nameof(PackageStatus.Staged)} status.");
            }

            if (validationTrackingId == Guid.Empty)
            {
                throw new ArgumentOutOfRangeException(nameof(validationTrackingId));
            }

            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            var data = PackageValidationMessageData.NewProcessValidationSet(entity.Id, entity.Version, validationTrackingId, validatingType, entity.Key);
            var activityName = "Enqueuing staged package validation: " +
                $"{data.ProcessValidationSet.PackageId} {data.ProcessValidationSet.PackageVersion} " +
                $"{data.ProcessValidationSet.ValidatingType} ({data.ProcessValidationSet.ValidationTrackingId})";

            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;
                await enqueuer.SendMessageAsync(data, postponeProcessingTill);
            }
        }
    }
}
