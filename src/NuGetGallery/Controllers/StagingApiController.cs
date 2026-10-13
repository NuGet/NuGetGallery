// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // API-key-authenticated requests do not use antiforgery tokens.

using System;
using System.Collections.Generic;
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
    [ApiScopeRequired(NuGetScopes.PackageStage)]
    public class StagingApiController : AppController
    {
        private const string JsonContentType = "application/json";
        private const string MultipartContentType = "multipart/form-data";
        private const int DefaultPageSize = 100;
        private const int MaximumPageSize = 500;
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
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var result = await _packageStagingManagementService.CreateStagingGroupAsync(stagingOwner, request.Id, request.Name);
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
                case CreateStagingGroupResultType.GroupAlreadyExists:
                    return Error(HttpStatusCode.Conflict, "GroupAlreadyExists", $"A staging group with the ID '{request.Id}' already exists.", "id");
                default:
                    throw new NotImplementedException($"Unexpected staging group creation result: {result.Type}");
            }
        }

        [HttpPut]
        public virtual async Task<ActionResult> StagePackage(StagePackageRequest request)
        {
            try
            {
                if (!MediaTypeWithQualityHeaderValue.TryParse(Request.ContentType, out var contentType) || !string.Equals(contentType.MediaType, MultipartContentType, StringComparison.OrdinalIgnoreCase))
                {
                    return Error(HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType", $"The request must have a Content-Type of '{MultipartContentType}'.");
                }

                if (request == null || request.Package == null || Request.Files.Count != 1 || !string.Equals(Request.Files.GetKey(0), "package", StringComparison.OrdinalIgnoreCase))
                {
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The request must contain one package file.", "package");
                }

                if (Request.Form.AllKeys.Any(key => string.Equals(key, "groupId", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(Request.Form["groupId"]))
                {
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The group ID must not be empty.", "groupid");
                }

                if (!ModelState.IsValid)
                {
                    var target = ModelState.First(entry => entry.Value.Errors.Count > 0).Key;
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The request is invalid.", target.ToLowerInvariant());
                }

                var currentUser = GetCurrentUser();
                var scopes = User.Identity.GetScopesFromClaim();
                var result = await _packageStagingUploadService.StagePackageAsync(currentUser, scopes, HttpContext, request.Package.InputStream, request.GroupId, request.Listed);
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
        public virtual ActionResult GetStagingGroups(int page = 1, int pageSize = DefaultPageSize)
        {
            var pagingError = ValidatePaging(page, pageSize);
            if (pagingError != null)
            {
                return pagingError;
            }

            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var summaryPage = _packageStagingManagementService.GetStagingGroupSummaryPage(stagingOwner, page, pageSize);
            var responses = summaryPage.Items
                .Select(summary => StagingGroupResponse.FromGroup(
                    summary.Group,
                    summary.Packages,
                    summary.Group.CreatedDate.Add(InitialGroupExpiration),
                    Url.ManageStagingGroup(summary.Group.Owner.Username, summary.Group.Id, relativeUrl: false)))
                .ToList();

            return JsonContent(new StagingPagedResponse<StagingGroupResponse>(
                responses,
                page,
                pageSize,
                summaryPage.TotalCount));
        }

        [HttpGet]
        public virtual ActionResult GetStagingGroup(string groupId, int page = 1, int pageSize = DefaultPageSize)
        {
            var pagingError = ValidatePaging(page, pageSize);
            if (pagingError != null)
            {
                return pagingError;
            }

            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var packagePage = _packageStagingManagementService.GetStagingGroupPackagePage(stagingOwner, groupId, page, pageSize);
            if (packagePage == null)
            {
                return Error(HttpStatusCode.NotFound, "GroupNotFound", "The staging group was not found.");
            }

            var group = packagePage.Group;
            var managementUrl = Url.ManageStagingGroup(group.Owner.Username, group.Id, relativeUrl: false);
            var expirationDate = group.CreatedDate.Add(InitialGroupExpiration);
            var artifacts = packagePage.Items
                .Select(package => StagingArtifactResponse.FromPackage(package, expirationDate, managementUrl))
                .ToList();

            var stagingGroupResponse = StagingGroupResponse.FromGroup(group, packagePage.TotalCount, packagePage.AllPackagesReady, expirationDate, managementUrl);
            var response = new StagingGroupDetailResponse(stagingGroupResponse, artifacts, page, pageSize, packagePage.TotalCount);

            return JsonContent(response);
        }

        [HttpDelete]
        public virtual async Task<ActionResult> DeleteStagingGroup(string groupId)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var group = _packageStagingManagementService.FindStagingGroup(stagingOwner, groupId);
            if (group == null)
            {
                return Error(HttpStatusCode.NotFound, "GroupNotFound", "The staging group was not found.");
            }

            var result = await _packageStagingManagementService.DeleteStagingGroupAsync(stagingOwner, group);
            switch (result.Type)
            {
                case StagingGroupDeletionResultType.Deleted:
                    return new HttpStatusCodeResult(HttpStatusCode.NoContent);
                case StagingGroupDeletionResultType.Conflict:
                    return Error(HttpStatusCode.Conflict, "GroupPromotionInProgress", "The staging group cannot be deleted while package promotion is active.");
                default:
                    throw new InvalidOperationException($"Unexpected staging group deletion result: {result.Type}");
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

            if (!await _packageStagingManagementService.UpdateListedAsync(stagedPackage, request.Listed))
            {
                return new HttpStatusCodeResult(HttpStatusCode.Conflict);
            }

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

            var deleted = await _packageStagingManagementService.DeletePackageAsync(stagedPackage);
            return new HttpStatusCodeResult(deleted ? HttpStatusCode.NoContent : HttpStatusCode.Conflict);
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
            return Json(statusCode, new { error }, JsonRequestBehavior.AllowGet);
        }

        private ActionResult ValidatePaging(int page, int pageSize)
        {
            var invalidParameter = ModelState
                .Where(entry => entry.Value.Errors.Count > 0)
                .Select(entry => entry.Key)
                .FirstOrDefault(key =>
                    string.Equals(key, "page", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "pageSize", StringComparison.OrdinalIgnoreCase));
            if (invalidParameter != null)
            {
                return Error(HttpStatusCode.BadRequest, "InvalidPaging", "The paging parameter must be a positive integer.", invalidParameter);
            }

            if (page < 1)
            {
                return Error(HttpStatusCode.BadRequest, "InvalidPaging", "The page parameter must be a positive integer.", "page");
            }

            if (pageSize < 1 || pageSize > MaximumPageSize)
            {
                return Error(HttpStatusCode.BadRequest, "InvalidPaging", $"The pageSize parameter must be between 1 and {MaximumPageSize}.", "pageSize");
            }

            return null;
        }

        private ContentResult JsonContent(object response)
        {
            return Content(JsonConvert.SerializeObject(response), JsonContentType);
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
