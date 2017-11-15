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
        private readonly IPackageValidationEnqueuer _enqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public AsynchronousPackageValidationInitiator(
            IPackageValidationEnqueuer enqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _enqueuer = enqueuer ?? throw new ArgumentNullException(nameof(enqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));

            if (diagnosticsService == null)
            {
                throw new ArgumentNullException(nameof(IDiagnosticsService));
            }

            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(AsynchronousPackageValidationInitiator));
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
                await _enqueuer.StartValidationAsync(data);
            }

            if (_appConfiguration.BlockingAsynchronousPackageValidationEnabled)
            {
                return PackageStatus.Validating;
            }

            return PackageStatus.Available;
        }
    }
}