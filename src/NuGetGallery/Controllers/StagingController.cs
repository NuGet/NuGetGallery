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
        private readonly IPackageStagingPromotionService _packageStagingPromotionService;
        private readonly IPackageStagingUploadService _packageStagingUploadService;

        public StagingController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService,
            IPackageStagingPromotionService packageStagingPromotionService,
            IPackageStagingUploadService packageStagingUploadService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
            _packageStagingPromotionService = packageStagingPromotionService ?? throw new ArgumentNullException(nameof(packageStagingPromotionService));
            _packageStagingUploadService = packageStagingUploadService ?? throw new ArgumentNullException(nameof(packageStagingUploadService));
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadPackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

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
            ValidatePackageIdentity(id, version);

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
            ValidatePackageIdentity(id, version);

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
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            await _packageStagingManagementService.DeletePackageAsync(stagedPackage);
            return Redirect(Url.ManageMyPackages());
        }

        /// <summary>
        /// Begins asynchronous publication of a ready staged package.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public virtual async Task<ActionResult> PromotePackage(string id, string version)
        {
            ValidatePackageIdentity(id, version);

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return HttpNotFound();
            }

            var result = await _packageStagingPromotionService.PromotePackageAsync(GetCurrentUser(), stagedPackage);
            switch (result)
            {
                case PackageStagingPromotionResult.Accepted:
                    return Redirect(Url.ManageMyPackages());
                case PackageStagingPromotionResult.Unauthorized:
                    return HttpNotFound();
                case PackageStagingPromotionResult.NotReady:
                    TempData["ErrorMessage"] = "The staged package is not ready for promotion.";
                    return Redirect(Url.ManageMyPackages());
                default:
                    throw new InvalidOperationException($"Unknown package promotion result '{result}'.");
            }
        }

        private static void ValidatePackageIdentity(string id, string version)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException(nameof(version));
            }
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
