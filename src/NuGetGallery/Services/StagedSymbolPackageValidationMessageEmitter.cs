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
    /// <summary>
    /// Enqueues asynchronous validation for staged symbol packages.
    /// </summary>
    public class StagedSymbolPackageValidationMessageEmitter : IStagedSymbolPackageValidationMessageEmitter
    {
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public StagedSymbolPackageValidationMessageEmitter(
            IPackageValidationEnqueuer validationEnqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(StagedSymbolPackageValidationMessageEmitter));
        }

        public async Task<StagedPackageStatus> StartValidationAsync(StagedSymbolPackage stagedSymbolPackage)
        {
            stagedSymbolPackage = stagedSymbolPackage ?? throw new ArgumentNullException(nameof(stagedSymbolPackage));

            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            var package = stagedSymbolPackage.StagedPackageIdentity.Package;
            var data = PackageValidationMessageData.NewProcessValidationSet(
                package.Id,
                package.Version,
                Guid.NewGuid(),
                ValidatingType.StagedSymbolPackage,
                entityKey: stagedSymbolPackage.Key);

            var activityName = "Enqueuing asynchronous staged symbol package validation: " +
                $"{data.ProcessValidationSet.PackageId} {data.ProcessValidationSet.PackageVersion} " +
                $"({data.ProcessValidationSet.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;
                await _validationEnqueuer.SendMessageAsync(data, postponeProcessingTill);
            }

            return StagedPackageStatus.Validating;
        }
    }
}
