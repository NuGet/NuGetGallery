// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class StagingValidationInputProvider : IStagingValidationInputProvider
    {
        private readonly IEntitiesContext _entitiesContext;
        private readonly IStagingBlobService _stagingBlobService;
        private readonly ICloudBlobClient _validationStorageClient;
        private readonly IFileMetadataService _fileMetadataService;

        public StagingValidationInputProvider(
            IEntitiesContext entitiesContext,
            IStagingBlobService stagingBlobService,
            ICloudBlobClient validationStorageClient,
            IFileMetadataService fileMetadataService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _validationStorageClient = validationStorageClient ?? throw new ArgumentNullException(nameof(validationStorageClient));
            _fileMetadataService = fileMetadataService ?? throw new ArgumentNullException(nameof(fileMetadataService));
        }

        public async Task CopyStagedPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (!validationSet.PackageKey.HasValue)
            {
                throw new InvalidOperationException("A staged validation set must have a package key.");
            }

            StagingBlobReference source;
            switch (validationSet.ValidatingType)
            {
                case ValidatingType.Package:
                    var packageSource = _entitiesContext.StagedPackageArtifacts
                        .Where(a => a.StagingEntry.PackageKey == validationSet.PackageKey.Value
                            && a.ValidationTrackingId == validationSet.ValidationTrackingId)
                        .Select(a => new
                        {
                            a.BlobPath,
                            a.BlobETag,
                            a.ContentHash,
                            ContentLength = a.StagingEntry.Package.PackageFileSize,
                        })
                        .SingleOrDefault();
                    if (packageSource == null)
                    {
                        throw new InvalidOperationException(
                            $"No staged package artifact matches validation set {validationSet.ValidationTrackingId}.");
                    }

                    source = new StagingBlobReference(
                        packageSource.BlobPath,
                        packageSource.BlobETag,
                        packageSource.ContentHash,
                        packageSource.ContentLength,
                        StagingBlobType.Nupkg);
                    break;
                case ValidatingType.SymbolPackage:
                    var symbolSource = _entitiesContext.StagedSymbolArtifacts
                        .Where(a => a.SymbolPackageKey == validationSet.PackageKey.Value
                            && a.ValidationTrackingId == validationSet.ValidationTrackingId)
                        .Select(a => new
                        {
                            a.BlobPath,
                            a.BlobETag,
                            a.ContentHash,
                            ContentLength = a.SymbolPackage.FileSize,
                        })
                        .SingleOrDefault();
                    if (symbolSource == null)
                    {
                        throw new InvalidOperationException(
                            $"No staged symbol artifact matches validation set {validationSet.ValidationTrackingId}.");
                    }

                    source = new StagingBlobReference(
                        symbolSource.BlobPath,
                        symbolSource.BlobETag,
                        symbolSource.ContentHash,
                        symbolSource.ContentLength,
                        StagingBlobType.Snupkg);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"The validating type {validationSet.ValidatingType} does not support staged input.");
            }

            await _stagingBlobService.CopyAsync(
                source,
                _validationStorageClient,
                _fileMetadataService.ValidationFolderName,
                ValidationFileService.BuildValidationSetPackageFileName(
                    validationSet,
                    _fileMetadataService.FileExtension),
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }
    }
}
