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
    public class AsynchronousPackageValidationInitiator<TPackageEntity> : IPackageValidationInitiator<TPackageEntity> 
        where TPackageEntity: IPackageEntity
    {
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public AsynchronousPackageValidationInitiator(
            IPackageValidationEnqueuer enqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _validationEnqueuer = enqueuer ?? throw new ArgumentNullException(nameof(enqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(IDiagnosticsService));
            }

            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(AsynchronousPackageValidationInitiator<TPackageEntity>));
        }

        public async Task<PackageStatus> StartValidationAsync(TPackageEntity package)
        {
            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            ValidatingType validatingType;
            if (package is Package)
            {
                validatingType = ValidatingType.Package;
            }
            else if (package is SymbolPackage)
            {
                validatingType = ValidatingType.SymbolPackage;
            }
            else
            {
                throw new ArgumentException($"Unknown IPackageEntity type: {nameof(package)}");
            }

            var data = new PackageValidationMessageData(
                package.Id,
                package.Version,
                Guid.NewGuid(),
                validatingType);

            var activityName = $"Enqueuing asynchronous package validation: " +
                $"{data.PackageId} {data.PackageVersion} {data.ValidatingType} ({data.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;

                await _validationEnqueuer.StartValidationAsync(data, postponeProcessingTill);
            }

            if (_appConfiguration.BlockingAsynchronousPackageValidationEnabled)
            {
                return PackageStatus.Validating;
            }

            return PackageStatus.Available;
        }
    }
}