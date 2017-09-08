// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery;
using NuGet.Versioning;
using System.Text;

namespace NuGetGallery
{
    public class PackageFileService : IPackageFileService
    {
        private const string ReadMeFilePathTemplateActive = "active/{0}/{1}{2}";
        private const string ReadMeFilePathTemplatePending = "pending/{0}/{1}{2}";

        private readonly IFileStorageService _fileStorageService;

        public PackageFileService(IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package)
        {
            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = BuildFileName(id, version, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.PackagesFolderName, fileName);
        }

        public Task DeletePackageFileAsync(string id, string version)
        {
            if (String.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (String.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }

            var fileName = BuildFileName(id, version, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.DeleteFileAsync(Constants.PackagesFolderName, fileName);
        }

        public Task SavePackageFileAsync(Package package, Stream packageFile)
        {
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.SaveFileAsync(Constants.PackagesFolderName, fileName, packageFile, overwrite: false);
        }

        public Task StorePackageFileInBackupLocationAsync(Package package, Stream packageFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            
            if (packageFile == null)
            {
                throw new ArgumentNullException(nameof(packageFile));
            }

            if (package.PackageRegistration == null ||
                string.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (string.IsNullOrWhiteSpace(package.NormalizedVersion) && string.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(Strings.PackageIsMissingRequiredData, nameof(package));
            }

            var fileName = BuildBackupFileName(package.PackageRegistration.Id, string.IsNullOrEmpty(package.NormalizedVersion) 
                ? NuGetVersion.Parse(package.Version).ToNormalizedString() : package.NormalizedVersion, package.Hash);
            return _fileStorageService.SaveFileAsync(Constants.PackageBackupsFolderName, fileName, packageFile);
        }

        public Task<Stream> DownloadPackageFileAsync(Package package)
        {
            var fileName = BuildFileName(package, Constants.PackageFileSavePathTemplate, Constants.NuGetPackageFileExtension);
            return _fileStorageService.GetFileAsync(Constants.PackagesFolderName, fileName);
        }

        /// <summary>
        /// Deletes the package readme.md file from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="isPending">True to delete pending blob, false for active.</param>
        public Task DeleteReadMeMdFileAsync(Package package, bool isPending = false)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var format = isPending ?
                ReadMeFilePathTemplatePending :
                ReadMeFilePathTemplateActive;
            var fileName = BuildFileName(package, format, Constants.MarkdownFileExtension);

            return _fileStorageService.DeleteFileAsync(Constants.PackageReadMesFolderName, fileName);
        }

        /// <summary>
        /// Saves the (pending) package readme.md file to storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="readMeMd">Markdown content.</param>
        public Task SavePendingReadMeMdFileAsync(Package package, string readMeMd)
        {
            if (string.IsNullOrWhiteSpace(readMeMd))
            {
                throw new ArgumentNullException(nameof(readMeMd));
            }

            var fileName = BuildFileName(package, ReadMeFilePathTemplatePending, Constants.MarkdownFileExtension);
            using (var readMeMdStream = new MemoryStream(Encoding.UTF8.GetBytes(readMeMd)))
            {
                return _fileStorageService.SaveFileAsync(Constants.PackageReadMesFolderName, fileName, readMeMdStream, overwrite: true);
            }
        }

        /// <summary>
        /// Downloads the readme.md from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="isPending">True to download the pending blob, false for active.</param>
        public async Task<string> DownloadReadMeMdFileAsync(Package package, bool isPending = false)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var format = isPending ?
                ReadMeFilePathTemplatePending :
                ReadMeFilePathTemplateActive;
            var fileName = BuildFileName(package, format, Constants.MarkdownFileExtension);

            using (var readMeMdStream = await _fileStorageService.GetFileAsync(Constants.PackageReadMesFolderName, fileName))
            {
                // Note that fileStorageService implementations return null if not found.
                if (readMeMdStream != null)
                {
                    using (var readMeMdReader = new StreamReader(readMeMdStream))
                    {
                        return await readMeMdReader.ReadToEndAsync();
                    }
                }
            }

            return null;
        }

        private static string BuildFileName(string id, string version, string pathTemplate, string extension)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            // Note: packages should be saved and retrieved in blob storage using the lower case version of their filename because
            // a) package IDs can and did change case over time
            // b) blob storage is case sensitive
            // c) we don't want to hit the database just to look up the right case
            // and remember - version can contain letters too.
            return String.Format(
                CultureInfo.InvariantCulture,
                pathTemplate,
                id.ToLowerInvariant(),
                version.ToLowerInvariant(),
                extension);
        }

        private static string BuildFileName(Package package, string format, string extension)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (package.PackageRegistration == null ||
                String.IsNullOrWhiteSpace(package.PackageRegistration.Id) ||
                (String.IsNullOrWhiteSpace(package.NormalizedVersion) && String.IsNullOrWhiteSpace(package.Version)))
            {
                throw new ArgumentException(Strings.PackageIsMissingRequiredData, nameof(package));
            }

            return BuildFileName(
                package.PackageRegistration.Id,
                string.IsNullOrEmpty(package.NormalizedVersion) ?
                    NuGetVersionFormatter.Normalize(package.Version) :
                    package.NormalizedVersion, format, extension);
        }

        private static string BuildBackupFileName(string id, string version, string hash)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version == null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (hash == null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            var hashBytes = Convert.FromBase64String(hash);

            return string.Format(
                CultureInfo.InvariantCulture,
                Constants.PackageFileBackupSavePathTemplate,
                id,
                version,
                HttpServerUtility.UrlTokenEncode(hashBytes),
                Constants.NuGetPackageFileExtension);
        }
    }
}