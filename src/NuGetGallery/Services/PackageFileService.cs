// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Packaging;
using NuGet.Services.Entities;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class PackageFileService : CorePackageFileService, IPackageFileService
    {
        /// <summary>
        /// Active readme markdown file, formatted as 'active/{packageId}/{version}.md'
        /// </summary>
        private const string ReadMeFilePathTemplateActive = "active/{0}/{1}{2}";

        private readonly IFileStorageService _fileStorageService;

        public PackageFileService(IFileStorageService fileStorageService)
            : base(fileStorageService, new PackageFileMetadataService())
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, Package package)
        {
            var fileName = FileNameHelper.BuildFileName(package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, CoreConstants.Folders.PackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = FileNameHelper.BuildFileName(id, version, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, CoreConstants.Folders.PackagesFolderName, fileName);
        }

        /// <summary>
        /// Deletes the package readme.md file from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        public Task DeleteReadMeMdFileAsync(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            
            var fileName = FileNameHelper.BuildFileName(package, ReadMeFilePathTemplateActive, ServicesConstants.MarkdownFileExtension);

            return _fileStorageService.DeleteFileAsync(CoreConstants.Folders.PackageReadMesFolderName, fileName);
        }

        /// <summary>
        /// Saves the package readme.md file to storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="readMeMd">Markdown content.</param>
        public async Task SaveReadMeMdFileAsync(Package package, string readMeMd)
        {
            if (string.IsNullOrWhiteSpace(readMeMd))
            {
                throw new ArgumentNullException(nameof(readMeMd));
            }

            var fileName = FileNameHelper.BuildFileName(package, ReadMeFilePathTemplateActive, ServicesConstants.MarkdownFileExtension);

            using (var readMeMdStream = new MemoryStream(Encoding.UTF8.GetBytes(readMeMd)))
            {
                await _fileStorageService.SaveFileAsync(CoreConstants.Folders.PackageReadMesFolderName, fileName, readMeMdStream, overwrite: true);
            }
        }

        /// <summary>
        /// Save the readme file from package stream. This method should throw if the package
        /// does not have an embedded readme file 
        /// </summary>
        /// <param name="package">Package information.</param>
        /// <param name="packageStream">Package stream with .nupkg contents.</param>
        public async Task ExtractAndSaveReadmeFileAsync(Package package, Stream packageStream)
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
                if (string.IsNullOrWhiteSpace(packageMetadata.ReadmeFile))
                {
                    throw new InvalidOperationException("No readme file specified in the nuspec");
                }

                var filename = FileNameHelper.GetZipEntryPath(packageMetadata.ReadmeFile);
                var ReadmeFileEntry = packageArchiveReader.GetEntry(filename); // throws on non-existent file
                using (var readmeFileStream = ReadmeFileEntry.Open())
                {
                    await SaveReadmeFileAsync(package, readmeFileStream);
                }
            }
        }

        /// <summary>
        /// Saves the package readme.md file to storage. This method should throw if the package
        /// does not have an embedded readme file 
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        /// <param name="readmeFile">The content of readme file.</param>
        public Task SaveReadmeFileAsync(Package package, Stream readmeFile)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (readmeFile == null)
            {
                throw new ArgumentNullException(nameof(readmeFile));
            }

            if (package.EmbeddedReadmeType == EmbeddedReadmeFileType.Absent)
            {
                throw new ArgumentException("Package must have an embedded readme", nameof(package));
            }

            var fileName = FileNameHelper.BuildFileName(package, ReadMeFilePathTemplateActive, ServicesConstants.MarkdownFileExtension);

            return _fileStorageService.SaveFileAsync(CoreConstants.Folders.PackageReadMesFolderName, fileName, readmeFile, overwrite: true);
        }

        /// <summary>
        /// Downloads the readme.md from storage.
        /// </summary>
        /// <param name="package">The package associated with the readme.</param>
        public async Task<string> DownloadReadMeMdFileAsync(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            
            var fileName = FileNameHelper.BuildFileName(package, ReadMeFilePathTemplateActive, ServicesConstants.MarkdownFileExtension);

            using (var readMeMdStream = await _fileStorageService.GetFileAsync(CoreConstants.Folders.PackageReadMesFolderName, fileName))
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
    }
}