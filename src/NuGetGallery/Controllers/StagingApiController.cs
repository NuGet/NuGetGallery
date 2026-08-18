// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // API-key-authenticated requests do not use antiforgery tokens.

using System;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [ApiAuthorize]
    [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
    public class StagingApiController : AppController
    {
        private readonly IPackageStagingService _packageStagingService;

        public StagingApiController(IPackageStagingService packageStagingService)
        {
            _packageStagingService = packageStagingService ?? throw new ArgumentNullException(nameof(packageStagingService));
        }

        [HttpPut]
        public virtual async Task<ActionResult> StagePackage()
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            try
            {
                var result = await _packageStagingService.StagePackageAsync(currentUser, scopes, HttpContext, Request.InputStream);
                if (!result.Success)
                {
                    return new HttpStatusCodeWithBodyResult(result.StatusCode, result.ErrorMessage);
                }

                return new HttpStatusCodeWithServerWarningResult(HttpStatusCode.Created, result.Warnings);
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
        public virtual ActionResult GetStagedPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            var package = _packageStagingService.GetPackage(currentUser, scopes, id, version);
            if (package == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            return Json(package, JsonRequestBehavior.AllowGet);
        }
    }
}
