// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [UIAuthorize]
    public class StagingController : AppController
    {
        private readonly IPackageStagingManagementService _packageStagingManagementService;

        public StagingController(IPackageStagingManagementService packageStagingManagementService)
        {
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadPackage(string id, string version)
        {
            var content = await _packageStagingManagementService.OpenPackageContentAsync(GetCurrentUser(), id, version);
            if (content == null)
            {
                return HttpNotFound();
            }

            return File(content, CoreConstants.PackageContentType, $"{id}.{version}{CoreConstants.NuGetPackageFileExtension}");
        }
    }
}
