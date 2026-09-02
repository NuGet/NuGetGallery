// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class SymbolPackageFileService : CorePackageFileService, ISymbolPackageFileService
    {
        private readonly ICoreFileStorageService _fileStorageService;

        public SymbolPackageFileService(ICoreFileStorageService fileStorageService)
            : base(fileStorageService, new SymbolPackageFileMetadataService())
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, SymbolPackage symbolPackage)
        {
            var fileName = FileNameHelper.BuildFileName(symbolPackage.Package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);

            var packageVersion = NuGetVersionFormatter.GetNormalizedPackageVersion(symbolPackage.Package);

            return _fileStorageService
                .CreateDownloadFileResultAsync(requestUrl, CoreConstants.Folders.SymbolPackagesFolderName, fileName, packageVersion)
                .ToActionResultAsync();
        }

        public Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = FileNameHelper.BuildFileName(id, version, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);

            // version cannot be null here as BuildFileName will throw if it is
            return _fileStorageService
                .CreateDownloadFileResultAsync(requestUrl, CoreConstants.Folders.SymbolPackagesFolderName, fileName, NuGetVersionFormatter.Normalize(version))
                .ToActionResultAsync();
        }
    }
}