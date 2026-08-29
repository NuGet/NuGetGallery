// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // API-key-authenticated requests do not use antiforgery tokens.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [ApiAuthorize]
    [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
    public class StagingApiController : AppController
    {
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageStagingManagementService _packageStagingManagementService;
        private readonly IPackageStagingUploadService _packageStagingUploadService;

        public StagingApiController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService,
            IPackageStagingUploadService packageStagingUploadService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
            _packageStagingUploadService = packageStagingUploadService ?? throw new ArgumentNullException(nameof(packageStagingUploadService));
        }

        [HttpPut]
        public virtual async Task<ActionResult> StagePackage()
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            try
            {
                var result = await _packageStagingUploadService.StagePackageAsync(currentUser, scopes, HttpContext, Request.InputStream);
                if (!result.Success)
                {
                    return new HttpStatusCodeWithBodyResult(result.StatusCode, result.ErrorMessage);
                }

                return new HttpStatusCodeWithServerWarningResult(result.StatusCode, result.Warnings);
            }
            catch (HttpException exception) when (exception.IsMaxRequestLengthExceeded())
            {
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.RequestEntityTooLarge, Strings.PackageFileTooLarge);
            }
            catch (HttpException exception) when (!Response.IsClientConnected)
            {
                QuietLog.LogHandledException(exception);
                return new HttpStatusCodeWithBodyResult(HttpStatusCode.BadRequest, Strings.PackageUploadCancelled);
            }
        }

        [HttpGet]
        public virtual ActionResult GetStagedPackages()
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            var packages = _packageStagingManagementService.GetPackages(currentUser, scopes);
            return Json(packages, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public virtual async Task<ActionResult> DownloadStagedPackage(string id, string version)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            var content = await _packageStagingManagementService.OpenPackageContentAsync(stagedPackage);
            return File(content, CoreConstants.PackageContentType, $"{id}.{version}{CoreConstants.NuGetPackageFileExtension}");
        }

        [HttpGet]
        public virtual ActionResult GetStagedPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            var package = _packageStagingManagementService.GetPackage(currentUser, scopes, id, version);
            if (package == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            return Json(package, JsonRequestBehavior.AllowGet);
        }

        [AcceptVerbs(HttpVerbs.Patch)]
        public virtual async Task<ActionResult> UpdateStagedPackage(string id, string version, UpdateStagedPackageRequest request)
        {
            if (request == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            await _packageStagingManagementService.UpdateListedAsync(stagedPackage, request.Listed);

            return Json(_packageStagingManagementService.GetStatus(stagedPackage));
        }

        [HttpDelete]
        public virtual async Task<ActionResult> DeleteStagedPackage(string id, string version)
        {
            var stagedPackage = FindAuthorizedStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            await _packageStagingManagementService.DeletePackageAsync(stagedPackage);
            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }

        private StagedPackage FindAuthorizedStagedPackage(string id, string version)
        {
            var stagedPackage = _packageStagingManagementService.FindCurrentStagedPackage(id, version);
            if (stagedPackage == null)
            {
                return null;
            }

            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            if (!_packageStagingAuthorizationService.CanManageWithApiKey(currentUser, scopes, stagedPackage))
            {
                return null;
            }

            return stagedPackage;
        }
    }
}
