// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [UIAuthorize]
    public class StagingController : AppController
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageStagingManagementService _packageStagingManagementService;

        public StagingController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadPackage(string id, string version)
        {
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagedPackage == null || !_packageStagingAuthorizationService.CanManage(GetCurrentUser(), stagedPackage))
            {
                return HttpNotFound();
            }

            var content = await _packageStagingManagementService.OpenPackageContentAsync(stagedPackage);
            if (content == null)
            {
                return HttpNotFound();
            }

            return File(content, CoreConstants.PackageContentType, $"{id}.{version}{CoreConstants.NuGetPackageFileExtension}");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UpdateListed(string id, string version, bool listed)
        {
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagedPackage == null || !_packageStagingAuthorizationService.CanManage(GetCurrentUser(), stagedPackage))
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.UpdateListedAsync(stagedPackage, listed);

            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }
    }
}
