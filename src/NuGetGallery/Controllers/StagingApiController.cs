// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // API-key-authenticated requests do not use antiforgery tokens.

using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [ApiAuthorize]
    [ApiScopeRequired(NuGetScopes.PackagePush, NuGetScopes.PackagePushVersion)]
    public class StagingApiController : AppController
    {
        private const string JsonContentType = "application/json";
        private static readonly TimeSpan InitialGroupExpiration = TimeSpan.FromDays(30);

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

        [HttpPost]
        public virtual async Task<ActionResult> CreateStagingGroup(CreateStagingGroupRequest request)
        {
            if (!MediaTypeWithQualityHeaderValue.TryParse(Request.ContentType, out var contentType)
                || !string.Equals(contentType.MediaType, JsonContentType, StringComparison.OrdinalIgnoreCase))
            {
                return Error(HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType", $"The request must have a Content-Type of '{JsonContentType}'.");
            }

            if (request == null)
            {
                return Error(HttpStatusCode.BadRequest, "InvalidJson", "The request body must be a valid JSON object.");
            }

            if (!ModelState.IsValid)
            {
                var target = ModelState.First(entry => entry.Value.Errors.Count > 0).Key;
                return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The request is invalid.", target.ToLowerInvariant());
            }

            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            var result = await _packageStagingManagementService.CreateStagingGroupWithApiKeyAsync(currentUser, scopes, request.Id, request.Name);
            switch (result.Type)
            {
                case CreateStagingGroupResultType.Created:
                    var group = result.Group;
                    var response = StagingGroupResponse.FromNewGroup(
                        group,
                        group.CreatedDate.Add(InitialGroupExpiration),
                        Url.ManageStagingGroup(group.Owner.Username, group.Id, relativeUrl: false));
                    Response.StatusCode = (int)HttpStatusCode.Created;
                    return Content(JsonConvert.SerializeObject(response), JsonContentType);
                case CreateStagingGroupResultType.OwnerNotFound:
                    return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
                case CreateStagingGroupResultType.GroupAlreadyExists:
                    return Error(HttpStatusCode.Conflict, "GroupAlreadyExists", $"A staging group with the ID '{request.Id}' already exists.", "id");
                default:
                    throw new NotImplementedException($"Unexpected staging group creation result: {result.Type}");
            }
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
        public virtual ActionResult GetStagedPackageStatus(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();

            var package = _packageStagingManagementService.GetPackageStatus(currentUser, scopes, id, version);
            if (package == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            return Json(package, JsonRequestBehavior.AllowGet);
        }

        [AcceptVerbs(HttpVerbs.Patch)]
        public virtual async Task<ActionResult> UpdateStagedPackageListed(string id, string version, UpdateStagedPackageRequest request)
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

        private JsonResult Error(HttpStatusCode statusCode, string code, string message, string target = null)
        {
            var error = target == null ? (object)new { code, message } : new { code, message, target };
            return Json(statusCode, new { error });
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.Exception.StackTrace?.Contains("JsonValueProviderFactory") == true)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = Error(HttpStatusCode.BadRequest, "InvalidJson", "The request body must be valid JSON.");
                return;
            }

            base.OnException(filterContext);
        }
    }
}
