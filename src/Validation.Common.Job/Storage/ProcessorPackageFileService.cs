// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation;
using NuGetGallery;

namespace NuGet.Jobs.Validation.Storage
{
    public class ProcessorPackageFileService : IProcessorPackageFileService
    {
        /// <summary>
        /// This meant to be significantly longer than the maximum validation set duration (currently 1 day).
        /// </summary>
        private static readonly TimeSpan AccessDuration = TimeSpan.FromDays(7);

        private ICoreFileStorageService _fileStorageService;
        private readonly ISharedAccessSignatureService _sharedAccessSignatureService;
        private ILogger<ProcessorPackageFileService> _logger;
        private readonly string _processorName;

        public ProcessorPackageFileService(
            ICoreFileStorageService fileStorageService,
            Type processorType,
            ISharedAccessSignatureService sharedAccessSignatureService,
            ILogger<ProcessorPackageFileService> logger)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _sharedAccessSignatureService = sharedAccessSignatureService ?? throw new ArgumentNullException(nameof(sharedAccessSignatureService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (processorType == null)
            {
                throw new ArgumentNullException(nameof(processorType));
            }

            if (!typeof(INuGetProcessor).IsAssignableFrom(processorType))
            {
                throw new ArgumentException($"The validator type {processorType} must extend {nameof(INuGetProcessor)}", nameof(processorType));
            }

            _processorName = processorType.Name;
        }

        public async Task<Uri> GetReadAndDeleteUriAsync(
            string packageId,
            string packageNormalizedVersion,
            Guid validationId,
            string sasDefinition)
        {
            var fileName = BuildFileName(packageId, packageNormalizedVersion, validationId);

            if(string.IsNullOrEmpty(sasDefinition))
            {
                return await _fileStorageService.GetPriviledgedFileUriAsync(
                    CoreConstants.Folders.ValidationFolderName,
                    fileName,
                    FileUriPermissions.Read | FileUriPermissions.Delete,
                    DateTimeOffset.UtcNow + AccessDuration);
            }

            var fileUri = await _fileStorageService.GetFileUriAsync(CoreConstants.Folders.ValidationFolderName, fileName);
            var sasToken = await _sharedAccessSignatureService.GetFromManagedStorageAccountAsync(sasDefinition);
            
            return new Uri(fileUri, sasToken);
        }

        public Task SaveAsync(
            string packageId,
            string packageNormalizedVersion,
            Guid validationId,
            Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(packageId, packageNormalizedVersion, validationId);

            _logger.LogInformation(
                "Saving package file {PackageId} {PackageNormalizedVersion} for processor {ProcessorName} and " +
                "validation ID {ValidationId}.",
                packageId,
                packageNormalizedVersion,
                _processorName,
                validationId);

            packageFile.Position = 0;

            return _fileStorageService.SaveFileAsync(
                CoreConstants.Folders.ValidationFolderName,
                fileName,
                packageFile,
                overwrite: true);
        }

        private string BuildFileName(string packageId, string packageNormalizedVersion, Guid validationId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (packageNormalizedVersion == null)
            {
                throw new ArgumentNullException(nameof(packageNormalizedVersion));
            }

            return $"{_processorName}/{validationId}/{packageId.ToLowerInvariant()}." +
                packageNormalizedVersion.ToLowerInvariant() +
                CoreConstants.NuGetPackageFileExtension;
        }
    }
}
