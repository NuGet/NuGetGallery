// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class CoreLicenseFileService : ICoreLicenseFileService
    {
        private const string LicenseFileName = "license";

        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IFileMetadataService _metadata;

        public CoreLicenseFileService(ICoreFileStorageService fileStorageService, IFileMetadataService metadata)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public Task SaveLicenseFileAsync(Package package, Stream licenseFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (licenseFile == null)
            {
                throw new ArgumentNullException(nameof(licenseFile));
            }

            if (package.EmbeddedLicenseType == EmbeddedLicenseFileType.Absent)
            {
                throw new ArgumentException("Package must have an embedded license", nameof(package));
            }

            var fileName = BuildLicenseFileName(package);

            // Gallery will generally ignore the content type on license files and will use the value from the DB,
            // but we'll be nice and try to specify correct content type for them.
            var contentType = package.EmbeddedLicenseType == EmbeddedLicenseFileType.Markdown
                ? CoreConstants.MarkdownContentType
                : CoreConstants.TextContentType;

            return _fileStorageService.SaveFileAsync(_metadata.PackageContentFolderName, fileName, contentType, licenseFile, overwrite: true);
        }

        public Task<Stream> DownloadLicenseFileAsync(Package package)
        {
            var fileName = BuildLicenseFileName(package);
            return _fileStorageService.GetFileAsync(_metadata.PackageContentFolderName, fileName);
        }

        public Task DeleteLicenseFileAsync(string id, string version)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException($"{nameof(id)} cannot be empty", nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException($"{nameof(version)} cannot be empty", nameof(version));
            }

            var normalizedVersion = NuGetVersionFormatter.Normalize(version);
            var fileName = BuildLicenseFileName(id, normalizedVersion);

            return _fileStorageService.DeleteFileAsync(_metadata.PackageContentFolderName, fileName);
        }

        private string LicensePathTemplate => $"{_metadata.PackageContentPathTemplate}/{LicenseFileName}";

        private string BuildLicenseFileName(Package package)
            => FileNameHelper.BuildFileName(package, LicensePathTemplate, string.Empty);

        private string BuildLicenseFileName(string id, string version)
            => FileNameHelper.BuildFileName(id, version, LicensePathTemplate, string.Empty);

    }
}
