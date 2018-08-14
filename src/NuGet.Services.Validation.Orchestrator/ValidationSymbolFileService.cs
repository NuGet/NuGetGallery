// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The save operations for symbols need to allow overwrite.
    /// Extend the ValidationFileService and overwrite the Copy methods.
    /// </summary>
    public class ValidationSymbolFileService : ValidationFileService
    {
        public ValidationSymbolFileService(
            ICoreFileStorageService fileStorageService,
            IFileDownloader fileDownloader,
            ITelemetryService telemetryService,
            ILogger<ValidationFileService> logger,
            IFileMetadataService fileMetadataService) : base(fileStorageService, fileDownloader, telemetryService, logger, fileMetadataService)
        {
        }

        public override async Task CopyValidationPackageToPackageFileAsync(PackageValidationSet validationSet)
        {
            var packageUri = await GetPackageForValidationSetReadUriAsync(
                validationSet,
                DateTimeOffset.UtcNow.Add(AccessDuration));

            Package packageFromValidationSet = new Package()
            {
                PackageRegistration = new PackageRegistration() { Id = validationSet.PackageId },
                NormalizedVersion = validationSet.PackageNormalizedVersion,
                Key = validationSet.PackageKey
            };

            using (var packageStream = await _fileDownloader.DownloadAsync(packageUri, CancellationToken.None))
            {
                await SavePackageFileAsync(packageFromValidationSet, packageStream, overwrite: true);
            }
        }

        public override async Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
        {
            await CopyValidationPackageToPackageFileAsync(validationSet);
        }
    }
}