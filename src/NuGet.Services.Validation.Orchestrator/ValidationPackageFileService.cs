// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationPackageFileService : CorePackageFileService, IValidationPackageFileService
    {
        private readonly ICoreFileStorageService _fileStorageService;
        private readonly ILogger<ValidationPackageFileService> _logger;

        public ValidationPackageFileService(
            ICoreFileStorageService fileStorageService,
            ILogger<ValidationPackageFileService> logger) : base(fileStorageService)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task CopyValidationPackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet));
        }

        public Task CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.PackagesFolderName,
                srcFileName,
                CoreConstants.ValidationFolderName,
                BuildValidationSetPackageFileName(validationSet));
        }

        public Task CopyValidationPackageToPackageFileAsync(string id, string normalizedVersion)
        {
            var fileName = BuildFileName(
                id,
                normalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                fileName,
                CoreConstants.PackagesFolderName,
                fileName);
        }

        public Task CopyValidationSetPackageToPackageFileAsync(PackageValidationSet validationSet)
        {
            var srcFileName = BuildValidationSetPackageFileName(validationSet);

            var destFileName = BuildFileName(
                validationSet.PackageId,
                validationSet.PackageNormalizedVersion,
                CoreConstants.PackageFileSavePathTemplate,
                CoreConstants.NuGetPackageFileExtension);

            return CopyFileAsync(
                CoreConstants.ValidationFolderName,
                srcFileName,
                CoreConstants.PackagesFolderName,
                destFileName);
        }

        public Task<bool> DoesValidationSetPackageExistAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);
            
            return _fileStorageService.FileExistsAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task DeletePackageForValidationSetAsync(PackageValidationSet validationSet)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Deleting package for validation set {ValidationTrackingId} from {FolderName}/{FileName}.",
                validationSet.ValidationTrackingId,
                CoreConstants.ValidationFolderName,
                fileName);

            return _fileStorageService.DeleteFileAsync(CoreConstants.ValidationFolderName, fileName);
        }

        public Task<Uri> GetPackageForValidationSetReadUriAsync(PackageValidationSet validationSet, DateTimeOffset endOfAccess)
        {
            var fileName = BuildValidationSetPackageFileName(validationSet);

            return _fileStorageService.GetFileReadUriAsync(CoreConstants.ValidationFolderName, fileName, endOfAccess);
        }

        private Task CopyFileAsync(string srcFolderName, string srcFileName, string destFolderName, string destFileName)
        {
            _logger.LogInformation(
                "Copying file {SrcFolderName}/{SrcFileName} to {DestFolderName}/{DestFileName}.",
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName);

            return _fileStorageService.CopyFileAsync(
                srcFolderName,
                srcFileName,
                destFolderName,
                destFileName);
        }

        private static string BuildValidationSetPackageFileName(PackageValidationSet validationSet)
        {
            return $"validation-sets/{validationSet.ValidationTrackingId}/" +
                $"{validationSet.PackageId.ToLowerInvariant()}." +
                $"{validationSet.PackageNormalizedVersion.ToLowerInvariant()}" +
                CoreConstants.NuGetPackageFileExtension;
        }
    }
}
