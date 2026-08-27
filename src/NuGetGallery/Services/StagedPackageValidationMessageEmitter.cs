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
    public class StagedPackageValidationMessageEmitter : IStagedPackageValidationMessageEmitter
    {
        private readonly IPackageValidationEnqueuer _validationEnqueuer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IDiagnosticsSource _diagnosticsSource;

        public StagedPackageValidationMessageEmitter(
            IPackageValidationEnqueuer validationEnqueuer,
            IAppConfiguration appConfiguration,
            IDiagnosticsService diagnosticsService)
        {
            _validationEnqueuer = validationEnqueuer ?? throw new ArgumentNullException(nameof(validationEnqueuer));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            diagnosticsService = diagnosticsService ?? throw new ArgumentNullException(nameof(diagnosticsService));
            _diagnosticsSource = diagnosticsService.SafeGetSource(nameof(StagedPackageValidationMessageEmitter));
        }

        public async Task StartValidationAsync(StagedPackage stagedPackage)
        {
            stagedPackage = stagedPackage ?? throw new ArgumentNullException(nameof(stagedPackage));

            if (_appConfiguration.ReadOnlyMode)
            {
                throw new ReadOnlyModeException(Strings.CannotEnqueueDueToReadOnly);
            }

            var entityKey = stagedPackage.PackageKey == default ? (int?)null : stagedPackage.PackageKey;
            var data = PackageValidationMessageData.NewProcessValidationSet(
                stagedPackage.Package.Id,
                stagedPackage.Package.Version,
                Guid.NewGuid(),
                ValidatingType.StagedPackage,
                entityKey);

            var activityName = "Enqueuing asynchronous staged package validation: " +
                $"{data.ProcessValidationSet.PackageId} {data.ProcessValidationSet.PackageVersion} " +
                $"({data.ProcessValidationSet.ValidationTrackingId})";
            using (_diagnosticsSource.Activity(activityName))
            {
                var postponeProcessingTill = DateTimeOffset.UtcNow + _appConfiguration.AsynchronousPackageValidationDelay;
                await _validationEnqueuer.SendMessageAsync(data, postponeProcessingTill);
            }
        }
    }
}
