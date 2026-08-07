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
        private readonly ICoreFileStorageService _fileStorageService;

        public StagingValidationInputProvider(
            IEntitiesContext entitiesContext,
            IStagingBlobService stagingBlobService,
            ICloudBlobClient validationStorageClient,
            IFileMetadataService fileMetadataService,
            ICoreFileStorageService fileStorageService)
        {
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _stagingBlobService = stagingBlobService ?? throw new ArgumentNullException(nameof(stagingBlobService));
            _validationStorageClient = validationStorageClient ?? throw new ArgumentNullException(nameof(validationStorageClient));
            _fileMetadataService = fileMetadataService ?? throw new ArgumentNullException(nameof(fileMetadataService));
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
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

            if (validationSet.ValidatingType == ValidatingType.SymbolPackage)
            {
                await CopyParentPackageForValidationSetAsync(validationSet);
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

        private async Task CopyParentPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var parent = _entitiesContext.StagedSymbolArtifacts
                .Where(a => a.SymbolPackageKey == validationSet.PackageKey.Value
                    && a.ValidationTrackingId == validationSet.ValidationTrackingId)
                .Select(a => new
                {
                    a.ParentContentHash,
                    ParentPackageKey = a.StagingEntry.PackageKey,
                    CurrentParentContentHash = a.StagingEntry.Package.Hash,
                    ParentFileSize = a.StagingEntry.Package.PackageFileSize,
                    ParentStatus = (PackageStatus)a.StagingEntry.Package.PackageStatusKey,
                })
                .SingleOrDefault();

            if (parent == null)
            {
                throw new InvalidOperationException($"No staged symbol artifact matches validation set {validationSet.ValidationTrackingId}.");
            }

            if (string.IsNullOrEmpty(parent.ParentContentHash)
                || !string.Equals(
                    parent.ParentContentHash,
                    parent.CurrentParentContentHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The parent package changed before staged symbol validation set {validationSet.ValidationTrackingId} was created.");
            }

            var destinationFileName = ValidationFileService.BuildStagedSymbolParentFileName(validationSet);
            if (parent.ParentStatus == PackageStatus.Staged)
            {
                var parentArtifact = _entitiesContext.StagedPackageArtifacts
                    .Where(a => a.StagingEntry.PackageKey == parent.ParentPackageKey && a.ContentHash == parent.ParentContentHash)
                    .Select(a => new
                    {
                        a.BlobPath,
                        a.BlobETag,
                        a.ContentHash,
                    })
                    .SingleOrDefault();
                if (parentArtifact == null)
                {
                    throw new InvalidOperationException($"No staged parent package artifact matches validation set {validationSet.ValidationTrackingId}.");
                }

                await _stagingBlobService.CopyAsync(
                    new StagingBlobReference(
                        parentArtifact.BlobPath,
                        parentArtifact.BlobETag,
                        parentArtifact.ContentHash,
                        parent.ParentFileSize,
                        StagingBlobType.Nupkg),
                    _validationStorageClient,
                    _fileMetadataService.ValidationFolderName,
                    destinationFileName,
                    AccessConditionWrapper.GenerateIfNotExistsCondition());
                return;
            }

            string sourceFolder;
            switch (parent.ParentStatus)
            {
                case PackageStatus.Available:
                    sourceFolder = CoreConstants.Folders.PackagesFolderName;
                    break;
                case PackageStatus.Validating:
                case PackageStatus.FailedValidation:
                    sourceFolder = CoreConstants.Folders.ValidationFolderName;
                    break;
                default:
                    throw new InvalidOperationException($"A parent package in the {parent.ParentStatus} state cannot be used for staged symbol validation.");
            }

            var sourceFileName = FileNameHelper.BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            await _fileStorageService.CopyFileAsync(
                sourceFolder,
                sourceFileName,
                _fileMetadataService.ValidationFolderName,
                destinationFileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());

            bool snapshotMatches;
            using (var parentStream = await _fileStorageService.GetFileAsync(_fileMetadataService.ValidationFolderName, destinationFileName))
            {
                var copiedHash = CryptographyService.GenerateHash(
                    parentStream,
                    CoreConstants.Sha512HashAlgorithmId);
                snapshotMatches = parentStream.Length == parent.ParentFileSize
                    && string.Equals(copiedHash, parent.ParentContentHash, StringComparison.Ordinal);
            }

            if (!snapshotMatches)
            {
                await _fileStorageService.DeleteFileAsync(
                    _fileMetadataService.ValidationFolderName,
                    destinationFileName);
                throw new StagingBlobIntegrityException(
                    $"The parent snapshot for staged symbol validation set {validationSet.ValidationTrackingId} " +
                    "does not match its recorded content hash and length.");
            }
        }
    }
}
