// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs.Validation;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidationPackageFileService : CorePackageFileService, IValidationPackageFileService
    {
        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IPackageDownloader _packageDownloader;
        private readonly ILogger<ValidationPackageFileService> _logger;

        public ValidationPackageFileService(
            ICoreFileStorageService fileStorageService,
            IPackageDownloader packageDownloader,
            ILogger<ValidationPackageFileService> logger) : base(fileStorageService)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _packageDownloader = packageDownloader ?? throw new ArgumentNullException(nameof(packageDownloader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Stream> DownloadPackageFileToDiskAsync(Package package)
        {
            var packageUri = await GetPackageReadUriAsync(package);

            return await _packageDownloader.DownloadAsync(packageUri, CancellationToken.None);
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
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        public Task<string> CopyPackageFileForValidationSetAsync(PackageValidationSet validationSet)
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
                BuildValidationSetPackageFileName(validationSet),
                AccessConditionWrapper.GenerateEmptyCondition());
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
                fileName,
                AccessConditionWrapper.GenerateIfNotExistsCondition());
        }

        public Task CopyValidationSetPackageToPackageFileAsync(
            PackageValidationSet validationSet,
            IAccessCondition destAccessCondition)
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
                destFileName,
                destAccessCondition);
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

        public Task CopyPackageUrlForValidationSetAsync(PackageValidationSet validationSet, string srcPackageUrl)
        {
            var destFileName = BuildValidationSetPackageFileName(validationSet);

            _logger.LogInformation(
                "Copying URL {SrcPackageUrl} to {DestFolderName}/{DestFileName}.",
                srcPackageUrl,
                CoreConstants.ValidationFolderName,
                srcPackageUrl);

            return _fileStorageService.CopyFileAsync(
                new Uri(srcPackageUrl),
                CoreConstants.ValidationFolderName,
                destFileName,
                AccessConditionWrapper.GenerateEmptyCondition());
        }

        private Task<string> CopyFileAsync(
            string srcFolderName,
            string srcFileName,
            string destFolderName,
            string destFileName,
            IAccessCondition destAccessCondition)
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
                destFileName,
                destAccessCondition);
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
