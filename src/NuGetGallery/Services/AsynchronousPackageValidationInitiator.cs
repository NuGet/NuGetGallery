// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Services.Validation;
using NuGetGallery.Configuration;
using NuGetGallery.Diagnostics;

namespace NuGetGallery
{
    /// <summary>
    /// Initiates asynchronous validation on a package by enqueuing a message containing the package identity and a new
    /// <see cref="Guid"/>. The <see cref="Guid"/> represents a unique validation request.
    /// </summary>
    public class AsynchronousPackageValidationInitiator : IPackageValidationInitiator
    {
        private readonly IPackageValidationEnqueuer _packageValidationEnqueuer;
        private readonly IPackageValidationEnqueuer _symbolPackageValidationEnqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public AsynchronousPackageValidationInitiator(
            IPackageValidationEnqueuer packageValidationEnqueuer,
            IPackageValidationEnqueuer symbolPackageValidationEnqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _packageValidationEnqueuer = packageValidationEnqueuer ?? throw new ArgumentNullException(nameof(packageValidationEnqueuer));
            _symbolPackageValidationEnqueuer = symbolPackageValidationEnqueuer ?? throw new ArgumentNullException(nameof(symbolPackageValidationEnqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(IDiagnosticsService));
            }

            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(AsynchronousPackageValidationInitiator));
        }

        public async Task<PackageStatus> StartSymbolsPackageValidationAsync(SymbolPackage symbolPackage)
        {
            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            if (symbolPackage == null)
            {
                throw new ArgumentNullException(nameof(symbolPackage));
            }

            var data = new PackageValidationMessageData(
                symbolPackage.Package.PackageRegistration.Id,
                symbolPackage.Package.Version,
                Guid.NewGuid(),
                ValidatingType.SymbolPackage);

            var activityName = $"Enqueuing asynchronous symbol package validation: " +
                $"{data.PackageId} {data.PackageVersion} ({data.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;

                await _symbolPackageValidationEnqueuer.StartValidationAsync(data, postponeProcessingTill);
            }

            if (_appConfiguration.BlockingAsynchronousPackageValidationEnabled)
            {
                return PackageStatus.Validating;
            }

            return PackageStatus.Available;
        }

        public async Task<PackageStatus> StartValidationAsync(Package package)
        {
            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            var data = new PackageValidationMessageData(
                package.PackageRegistration.Id,
                package.Version,
                Guid.NewGuid());

            var activityName = $"Enqueuing asynchronous package validation: " +
                $"{data.PackageId} {data.PackageVersion} ({data.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;

                await _packageValidationEnqueuer.StartValidationAsync(data, postponeProcessingTill);
            }

            if (_appConfiguration.BlockingAsynchronousPackageValidationEnabled)
            {
                return PackageStatus.Validating;
            }

            return PackageStatus.Available;
        }
    }
}