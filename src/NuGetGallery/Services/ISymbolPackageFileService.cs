// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface ISymbolPackageFileService : ICorePackageFileService
    {
        /// <summary>
        /// Creates an ActionResult that allows a third-party client to download the snupkg for the package.
        /// </summary>
        Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, SymbolPackage package);

        /// <summary>
        /// Creates an ActionResult that allows a third-party client to download the snupkg for the package.
        /// </summary>
        Task<ActionResult> CreateDownloadSymbolPackageActionResultAsync(Uri requestUrl, string unsafeId, string unsafeVersion);
    }
}