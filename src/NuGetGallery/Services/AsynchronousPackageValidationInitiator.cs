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

        public PackageStatus GetPackageStatus(TPackageEntity package)
        {
            // Give an opportunity to the caller to fail early if StartValidationAsync is guaranteed to fail
            ValidateAndGetType(package);

            return TargetPackageStatus;
        }

        public async Task<PackageStatus> StartValidationAsync(TPackageEntity package)
        {
            var validatingType = ValidateAndGetType(package);

            var entityKey = package.Key == default(int) ? (int?)null : package.Key;
            var data = PackageValidationMessageData.NewProcessValidationSet(
                package.Id,
                package.Version,
                Guid.NewGuid(),
                validatingType,
                entityKey: entityKey);

            var activityName = "Enqueuing asynchronous package validation: " +
                $"{data.ProcessValidationSet.PackageId} {data.ProcessValidationSet.PackageVersion} " +
                $"{data.ProcessValidationSet.ValidatingType} ({data.ProcessValidationSet.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;

                await _validationEnqueuer.SendMessageAsync(data, postponeProcessingTill);
            }

            return TargetPackageStatus;
        }

        private PackageStatus TargetPackageStatus => _appConfiguration.BlockingAsynchronousPackageValidationEnabled
                           ? PackageStatus.Validating
                           : PackageStatus.Available;

        private ValidatingType ValidateAndGetType(TPackageEntity package)
        {
            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            if (package is Package)
            {
                return ValidatingType.Package;
            }
            else if (package is SymbolPackage)
            {
                return ValidatingType.SymbolPackage;
            }

            throw new ArgumentException($"Unknown IPackageEntity type: {nameof(package)}");
        }
    }
}