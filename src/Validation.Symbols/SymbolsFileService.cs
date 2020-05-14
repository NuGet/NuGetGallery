// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using NuGetGallery;
using NuGet.Jobs.Validation;
using NuGet.Services.Entities;

namespace Validation.Symbols
{
    public class SymbolsFileService : ISymbolsFileService
    {
        private CorePackageFileService _packageFileService;
        private CorePackageFileService _packageValidationFileService;
        private IFileDownloader _fileDownloader;

        public SymbolsFileService(
            ICoreFileStorageService packageStorageService,
            ICoreFileStorageService packageValidationStorageService,
            IFileDownloader fileDownloader)
        {
            if (packageStorageService == null)
            {
                throw new ArgumentNullException(nameof(packageStorageService));
            }
            if(packageValidationStorageService == null)
            {
                throw new ArgumentNullException(nameof(packageValidationStorageService));
            }
            _packageFileService = new CorePackageFileService(packageStorageService, new PackageFileMetadataService());
            _packageValidationFileService = new CorePackageFileService(packageValidationStorageService, new PackageFileMetadataService());
            _fileDownloader = fileDownloader ?? throw new ArgumentNullException(nameof(fileDownloader));
        }

        public async Task<Stream> DownloadSnupkgFileAsync(string snupkgUri, CancellationToken cancellationToken)
        {
            var result = await _fileDownloader.DownloadAsync(new Uri(snupkgUri), cancellationToken);
            return result.GetStreamOrThrow();
        }

        public async Task<Stream> DownloadNupkgFileAsync(string packageId, string packageNormalizedVersion, CancellationToken cancellationToken)
        {
            var package = new Package()
            {
                NormalizedVersion = packageNormalizedVersion,
                PackageRegistration = new PackageRegistration()
                {
                    Id = packageId
                }
            };
            if (await _packageFileService.DoesPackageFileExistAsync(package))
            {
                return await _packageFileService.DownloadPackageFileAsync(package);
            }

            if (await _packageValidationFileService.DoesValidationPackageFileExistAsync(package))
            {
                return await _packageValidationFileService.DownloadValidationPackageFileAsync(package);
            }

            throw new FileNotFoundException(string.Format("Package {0} {1} not found in the packages or validation container.", packageId, packageNormalizedVersion));
        }
    }
}
