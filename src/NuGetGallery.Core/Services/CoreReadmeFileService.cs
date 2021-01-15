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
    public class CoreReadmeFileService : ICoreReadmeFileService
    {
        private const string ReadmeFileName = "readme";

        private readonly ICoreFileStorageService _fileStorageService;
        private readonly IContentFileMetadataService _metadata;

        public CoreReadmeFileService(ICoreFileStorageService fileStorageService, IContentFileMetadataService metadata)
        {
            _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
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

            var fileName = BuildReadmeFileName(package);

            return _fileStorageService.SaveFileAsync(_metadata.PackageContentFolderName, fileName, readmeFile, overwrite: true);
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

        public async Task<string> DownloadReadmeFileAsync(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var fileName = BuildReadmeFileName(package);

            using (var readmeFileStream = await _fileStorageService.GetFileAsync(_metadata.PackageContentFolderName, fileName))
            {
                if (readmeFileStream != null)
                {
                    using (var readMeMdReader = new StreamReader(readmeFileStream))
                    {
                        return await readMeMdReader.ReadToEndAsync();
                    }
                }
            }
            return null;
        }

        public Task DeleteReadmeFileAsync(string id, string version)
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
            var fileName = BuildReadmeFileName(id, normalizedVersion);

            return _fileStorageService.DeleteFileAsync(_metadata.PackageContentFolderName, fileName);
        }

        private string ReadmePathTemplate => $"{_metadata.PackageContentPathTemplate}/{ReadmeFileName}";

        private string BuildReadmeFileName(Package package)
            => FileNameHelper.BuildFileName(package, ReadmePathTemplate, string.Empty);

        private string BuildReadmeFileName(string id, string version)
            => FileNameHelper.BuildFileName(id, version, ReadmePathTemplate, string.Empty);
    }
}
