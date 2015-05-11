// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public class NuGetExeDownloaderService : INuGetExeDownloaderService
    {
        private const int MaxNuGetExeFileSize = 10 * 1024 * 1024;
        private readonly IFileStorageService _fileStorageService;

        public NuGetExeDownloaderService(
            IFileStorageService fileStorageService)
        {
            _fileStorageService = fileStorageService;
        }

        public Task<ActionResult> CreateNuGetExeDownloadActionResultAsync(Uri requestUrl)
        {
            return _fileStorageService.CreateDownloadFileActionResultAsync(requestUrl, Constants.DownloadsFolderName, "nuget.exe");
        }
    }
}
