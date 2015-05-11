// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet;
using NuGetGallery.Packaging;

namespace NuGetGallery
{
    public interface INuGetExeDownloaderService
    {
        Task<ActionResult> CreateNuGetExeDownloadActionResultAsync(Uri requestUrl);
    }
}