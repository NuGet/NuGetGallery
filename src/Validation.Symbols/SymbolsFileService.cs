// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using NuGetGallery;

namespace Validation.Symbols
{
    public class SymbolsFileService : ISymbolsFileService
    {
        private CorePackageFileService _packageFileService;
        private CorePackageFileService _packageValidationFileService;
        private CorePackageFileService _symbolValidationFileService;

        public SymbolsFileService(
            ICoreFileStorageService packageStorageService,
            ICoreFileStorageService packageValidationStorageService,
            ICoreFileStorageService symbolValidationStorageService)
        {
            if (packageStorageService == null)
            {
                throw new ArgumentNullException(nameof(packageStorageService));
            }
            if(packageValidationStorageService == null)
            {
                throw new ArgumentNullException(nameof(packageValidationStorageService));
            }
            if (symbolValidationStorageService == null)
            {
                throw new ArgumentNullException(nameof(symbolValidationStorageService));
            }
            _packageFileService = new CorePackageFileService(packageStorageService, new PackageFileMetadataService());
            _packageValidationFileService = new CorePackageFileService(packageValidationStorageService, new PackageFileMetadataService());
            _symbolValidationFileService = new CorePackageFileService(symbolValidationStorageService, new SymbolPackageFileMetadataService());
        }

        public async Task<Stream> DownloadSnupkgFileAsync(string packageId, string packageNormalizedVersion, CancellationToken cancellationToken)
        {
            var package = new Package()
            {
                NormalizedVersion = packageNormalizedVersion,
                PackageRegistration = new PackageRegistration()
                {
                    Id = packageId
                }
            };

            if (await _symbolValidationFileService.DoesValidationPackageFileExistAsync(package))
            {
                return await _symbolValidationFileService.DownloadValidationPackageFileAsync(package);
            }

            throw new FileNotFoundException(string.Format("Symbols package {0} {1} not found in the validation container.", packageId, packageNormalizedVersion));
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
