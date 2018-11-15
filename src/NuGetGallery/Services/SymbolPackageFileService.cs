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
        private readonly IFileStorageService _fileStorageService;

        public SymbolPackageFileService(IFileStorageService fileStorageService)
            : base(fileStorageService, new SymbolPackageFileMetadataService())
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, SymbolPackage symbolPackage)
        {
            var fileName = FileNameHelper.BuildFileName(symbolPackage.Package, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, CoreConstants.Folders.SymbolPackagesFolderName, fileName);
        }

        public Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, string id, string version)
        {
            var fileName = FileNameHelper.BuildFileName(id, version, CoreConstants.PackageFileSavePathTemplate, CoreConstants.NuGetSymbolPackageFileExtension);
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, CoreConstants.Folders.SymbolPackagesFolderName, fileName);
        }
    }
}