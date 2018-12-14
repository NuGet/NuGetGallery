// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class CoreLicenseFileService : ICoreLicenseFileService
    {
        private const string LicenseFileName = "license";

        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IContentFileMetadataService _metadata;

        public CoreLicenseFileService(ICoreFileStorageService fileStorageService, IContentFileMetadataService metadata)
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

            return _fileStorageService.SaveFileAsync(_metadata.PackageContentFolderName, fileName, CoreConstants.TextContentType, licenseFile, overwrite: true);
        }

        public async Task ExtractAndSaveLicenseFileAsync(Package package, Stream packageStream)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (packageStream == null)
            {
                throw new ArgumentNullException(nameof(packageStream));
            }

            packageStream.Seek(0, SeekOrigin.Begin);
            using (var packageArchiveReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                var packageMetadata = PackageMetadata.FromNuspecReader(packageArchiveReader.GetNuspecReader(), strict: true);
                if (packageMetadata.LicenseMetadata == null || packageMetadata.LicenseMetadata.Type != LicenseType.File || string.IsNullOrWhiteSpace(packageMetadata.LicenseMetadata.License))
                {
                    throw new InvalidOperationException("No license file specified in the nuspec");
                }

                var filename = FileNameHelper.GetZipEntryPath(packageMetadata.LicenseMetadata.License);
                var licenseFileEntry = packageArchiveReader.GetEntry(filename); // throws on non-existent file
                using (var licenseFileStream = licenseFileEntry.Open())
                {
                    await SaveLicenseFileAsync(package, licenseFileStream);
                }
            }
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
