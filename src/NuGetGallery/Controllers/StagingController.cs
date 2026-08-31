// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    /// <summary>
    /// Handles signed-in owner management of private staged packages.
    /// </summary>
    [UIAuthorize]
    public class StagingController : AppController
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageStagingManagementService _packageStagingManagementService;
        private readonly IPackageStagingUploadService _packageStagingUploadService;

        public StagingController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService,
            IPackageStagingUploadService packageStagingUploadService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
            _packageStagingUploadService = packageStagingUploadService ?? throw new ArgumentNullException(nameof(packageStagingUploadService));
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadPackage(string id, string version)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
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
        public virtual async Task<ActionResult> ReplacePackage(string id, string version, HttpPostedFileBase packageFile)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            if (packageFile == null || packageFile.ContentLength == 0)
            {
                TempData["ErrorMessage"] = "Select a package file.";
                return Redirect(Url.ManageMyPackages());
            }

            var result = await _packageStagingUploadService.ReplacePackageAsync(
                GetCurrentUser(),
                HttpContext,
                stagedPackage,
                packageFile.InputStream);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage;
            }

            return Redirect(Url.ManageMyPackages());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> UpdateListed(string id, string version, bool listed)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.UpdateListedAsync(stagedPackage, listed);
            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> DeletePackage(string id, string version)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.DeletePackageAsync(stagedPackage);
            return Redirect(Url.ManageMyPackages());
        }

        private StagedPackage FindAuthorizedStagedPackage(string id, string version)
        {
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return null;
            }

            var currentUser = GetCurrentUser();
            if (!_packageStagingAuthorizationService.CanManage(currentUser, stagedPackage))
            {
                return null;
            }

            return stagedPackage;
        }
    }
}
