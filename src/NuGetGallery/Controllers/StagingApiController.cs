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
using NuGet.Services.Validation.Issues;
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
        private readonly IPackageStagingAuthorizationService _packageStagingAuthorizationService;
        private readonly IPackageStagingManagementService _packageStagingManagementService;
        private readonly IPackageStagingUploadService _packageStagingUploadService;
        private readonly ISymbolPackageStagingUploadService _symbolPackageStagingUploadService;
        private readonly ISymbolPackageStagingManagementService _symbolPackageStagingManagementService;
        private readonly IStagingQuotaService _stagingQuotaService;
        private readonly IValidationService _validationService;

        public StagingApiController(
            IPackageStagingAuthorizationService packageStagingAuthorizationService,
            IPackageStagingManagementService packageStagingManagementService,
            IPackageStagingUploadService packageStagingUploadService,
            ISymbolPackageStagingUploadService symbolPackageStagingUploadService,
            ISymbolPackageStagingManagementService symbolPackageStagingManagementService,
            IStagingQuotaService stagingQuotaService,
            IValidationService validationService)
        {
            _packageStagingAuthorizationService = packageStagingAuthorizationService ?? throw new ArgumentNullException(nameof(packageStagingAuthorizationService));
            _packageStagingManagementService = packageStagingManagementService ?? throw new ArgumentNullException(nameof(packageStagingManagementService));
            _packageStagingUploadService = packageStagingUploadService ?? throw new ArgumentNullException(nameof(packageStagingUploadService));
            _symbolPackageStagingUploadService = symbolPackageStagingUploadService ?? throw new ArgumentNullException(nameof(symbolPackageStagingUploadService));
            _symbolPackageStagingManagementService = symbolPackageStagingManagementService ?? throw new ArgumentNullException(nameof(symbolPackageStagingManagementService));
            _stagingQuotaService = stagingQuotaService ?? throw new ArgumentNullException(nameof(stagingQuotaService));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
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
                        group.ExpirationDate,
                        Url.ManageStagingGroup(group.Owner.Username, group.Id, relativeUrl: false));
                    Response.StatusCode = (int)HttpStatusCode.Created;
                    return Content(JsonConvert.SerializeObject(response), JsonContentType);
                case CreateStagingGroupResultType.GroupAlreadyExists:
                    return Error(HttpStatusCode.Conflict, "GroupAlreadyExists", $"A staging group with the ID '{request.Id}' already exists.", "id");
                default:
                    throw new NotImplementedException($"Unexpected staging group creation result: {result.Type}");
            }
        }

        /// <summary>
        /// Creates or replaces a staged package and returns its artifact resource.
        /// </summary>
        /// <param name="request">The package file, optional group, and listed intent.</param>
        /// <returns>The accepted artifact with upload warnings and a status Location on creation, or an upload error.</returns>
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

                var fieldError = ValidateUploadFields(symbols: false);
                if (fieldError != null)
                {
                    return fieldError;
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
                    return Error(result.StatusCode, "PackageUploadFailed", result.ErrorMessage);
                }

                var package = result.StagedPackage;
                var response = GetPackageResponses(new[] { package })[package.Key];
                return UploadResponse(result, response, RouteName.GetStagedPackageStatus);
            }
            catch (HttpException exception) when (exception.IsMaxRequestLengthExceeded())
            {
                return Error(HttpStatusCode.RequestEntityTooLarge, "PackageFileTooLarge", Strings.PackageFileTooLarge);
            }
            catch (HttpException exception) when (!Response.IsClientConnected)
            {
                QuietLog.LogHandledException(exception);
                return Error(HttpStatusCode.BadRequest, "PackageUploadCancelled", Strings.PackageUploadCancelled);
            }
        }

        /// <summary>
        /// Creates or replaces staged symbols and returns their artifact resource.
        /// </summary>
        /// <param name="request">The symbol package file and optional group.</param>
        /// <returns>The accepted artifact with upload warnings and a status Location on creation, or an upload error.</returns>
        [HttpPut]
        public virtual async Task<ActionResult> StageSymbolPackage(StageSymbolPackageRequest request)
        {
            try
            {
                if (!MediaTypeWithQualityHeaderValue.TryParse(Request.ContentType, out var contentType) || !string.Equals(contentType.MediaType, MultipartContentType, StringComparison.OrdinalIgnoreCase))
                {
                    return Error(HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType", $"The request must have a Content-Type of '{MultipartContentType}'.");
                }

                if (request == null || request.Symbols == null || Request.Files.Count != 1 || !string.Equals(Request.Files.GetKey(0), "symbols", StringComparison.OrdinalIgnoreCase))
                {
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The request must contain one symbol package file in the symbols field.", "symbols");
                }

                var fieldError = ValidateUploadFields(symbols: true);
                if (fieldError != null)
                {
                    return fieldError;
                }

                if (!ModelState.IsValid)
                {
                    var target = ModelState.First(entry => entry.Value.Errors.Count > 0).Key;
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The request is invalid.", target.ToLowerInvariant());
                }

                var currentUser = GetCurrentUser();
                var scopes = User.Identity.GetScopesFromClaim();
                var result = await _symbolPackageStagingUploadService.StageSymbolPackageAsync(currentUser, scopes, HttpContext, request.Symbols.InputStream, request.GroupId);
                if (!result.Success)
                {
                    return Error(result.StatusCode, "SymbolPackageUploadFailed", result.ErrorMessage);
                }

                var symbolPackage = result.StagedSymbolPackage;
                var response = GetSymbolResponses(new[] { symbolPackage })[symbolPackage.Key];
                return UploadResponse(result, response, RouteName.GetStagedSymbolPackageStatus);
            }
            catch (HttpException exception) when (exception.IsMaxRequestLengthExceeded())
            {
                return Error(HttpStatusCode.RequestEntityTooLarge, "PackageFileTooLarge", Strings.PackageFileTooLarge);
            }
            catch (HttpException exception) when (!Response.IsClientConnected)
            {
                QuietLog.LogHandledException(exception);
                return Error(HttpStatusCode.BadRequest, "PackageUploadCancelled", Strings.PackageUploadCancelled);
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

            var summaryPage = _packageStagingManagementService.GetStagingGroupSummaryPage(stagingOwner, scopes, page, pageSize);
            var responses = summaryPage.Items
                .Select(summary => StagingGroupResponse.FromGroup(
                    summary.Group,
                    summary.Packages,
                    summary.Group.ExpirationDate,
                    Url.ManageStagingGroup(summary.Group.Owner.Username, summary.Group.Id, relativeUrl: false),
                    summary.Symbols))
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

            var packagePage = _packageStagingManagementService.GetStagingGroupPackagePage(stagingOwner, scopes, groupId, page, pageSize);
            if (packagePage == null)
            {
                return Error(HttpStatusCode.NotFound, "GroupNotFound", "The staging group was not found.");
            }

            var group = packagePage.Group;
            var managementUrl = Url.ManageStagingGroup(group.Owner.Username, group.Id, relativeUrl: false);
            var expirationDate = group.ExpirationDate;
            var packageResponses = GetPackageResponses(packagePage.Items);
            var symbolResponses = GetSymbolResponses(packagePage.Symbols);
            var artifacts = packagePage.Items
                .Select(package => new { package.Key, package.UploadedDate, IsSymbol = false, Artifact = packageResponses[package.Key] })
                .Concat(packagePage.Symbols.Select(symbol => new { symbol.Key, symbol.UploadedDate, IsSymbol = true, Artifact = symbolResponses[symbol.Key] }))
                .OrderByDescending(item => item.UploadedDate)
                .ThenByDescending(item => item.Key)
                .ThenBy(item => item.IsSymbol)
                .Select(item => item.Artifact)
                .ToList();

            var stagingGroupResponse = StagingGroupResponse.FromGroup(
                group, packagePage.TotalCount, packagePage.AllPackagesReady, expirationDate, managementUrl,
                packagePage.SymbolCount, packagePage.HasRegistrationOwnershipLoss, packagePage.HasLockedRegistration);
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

            var result = await _packageStagingManagementService.DeleteStagingGroupAsync(stagingOwner, scopes, group);
            switch (result.Type)
            {
                case StagingGroupDeletionResultType.Deleted:
                    return new HttpStatusCodeResult(HttpStatusCode.NoContent);
                case StagingGroupDeletionResultType.Conflict:
                    return Error(HttpStatusCode.Conflict, "GroupPromotionInProgress", "The staging group cannot be deleted while package promotion is active.");
                case StagingGroupDeletionResultType.NotFound:
                    return Error(HttpStatusCode.NotFound, "GroupNotFound", "The staging group was not found.");
                default:
                    throw new InvalidOperationException($"Unexpected staging group deletion result: {result.Type}");
            }
        }

        /// <summary>
        /// Lists one page of current staged packages visible to the API-key owner.
        /// </summary>
        /// <param name="page">The one-based page number.</param>
        /// <param name="pageSize">The number of packages per page.</param>
        /// <returns>The artifact page and owner-wide quota metadata.</returns>
        [HttpGet]
        public virtual ActionResult GetStagedPackages(int page = 1, int pageSize = DefaultPageSize)
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

            var packagePage = _packageStagingManagementService.GetStagedPackagePage(stagingOwner, scopes, page, pageSize);
            var responses = GetPackageResponses(packagePage.Items);
            var artifacts = packagePage.Items.Select(package => responses[package.Key]).ToList();
            var quota = _stagingQuotaService.GetUsage(stagingOwner);
            return JsonContent(new StagingArtifactPagedResponse(artifacts, page, pageSize, packagePage.TotalCount, quota));
        }

        /// <summary>
        /// Lists one page of current staged symbols visible to the API-key owner.
        /// </summary>
        /// <param name="page">The one-based page number.</param>
        /// <param name="pageSize">The number of symbol packages per page.</param>
        /// <returns>The artifact page and owner-wide quota metadata.</returns>
        [HttpGet]
        public virtual ActionResult GetStagedSymbolPackages(int page = 1, int pageSize = DefaultPageSize)
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

            var symbolPage = _packageStagingManagementService.GetStagedSymbolPackagePage(stagingOwner, scopes, page, pageSize);
            var responses = GetSymbolResponses(symbolPage.Items);
            var artifacts = symbolPage.Items.Select(symbol => responses[symbol.Key]).ToList();
            var quota = _stagingQuotaService.GetUsage(stagingOwner);
            return JsonContent(new StagingArtifactPagedResponse(artifacts, page, pageSize, symbolPage.TotalCount, quota));
        }

        private IReadOnlyDictionary<int, StagingArtifactResponse> GetPackageResponses(IReadOnlyCollection<StagedPackage> packages)
        {
            var failedKeys = packages.Where(package => package.Status == StagedPackageStatus.FailedValidation).Select(package => package.Key).ToList();
            IReadOnlyDictionary<int, IReadOnlyList<ValidationIssue>> issues = new Dictionary<int, IReadOnlyList<ValidationIssue>>();
            if (failedKeys.Count > 0)
            {
                issues = _validationService.GetStagedPackageValidationIssues(failedKeys);
            }

            return packages.ToDictionary(package => package.Key, package =>
            {
                issues.TryGetValue(package.Key, out var validationIssues);
                return StagingArtifactResponse.FromPackage(
                    package, StagingExpirationPolicy.GetDeadline(package), GetManagementUrl(package.StagedPackageIdentity), validationIssues);
            });
        }

        private IReadOnlyDictionary<int, StagingArtifactResponse> GetSymbolResponses(IReadOnlyCollection<StagedSymbolPackage> symbols)
        {
            var failedKeys = symbols.Where(symbol => symbol.Status == StagedPackageStatus.FailedValidation).Select(symbol => symbol.Key).ToList();
            IReadOnlyDictionary<int, IReadOnlyList<ValidationIssue>> issues = new Dictionary<int, IReadOnlyList<ValidationIssue>>();
            if (failedKeys.Count > 0)
            {
                issues = _validationService.GetStagedSymbolPackageValidationIssues(failedKeys);
            }

            return symbols.ToDictionary(symbol => symbol.Key, symbol =>
            {
                issues.TryGetValue(symbol.Key, out var validationIssues);
                return StagingArtifactResponse.FromSymbolPackage(
                    symbol, StagingExpirationPolicy.GetDeadline(symbol), GetManagementUrl(symbol.StagedPackageIdentity), validationIssues);
            });
        }

        private ActionResult ValidateUploadFields(bool symbols)
        {
            foreach (var field in Request.Form.AllKeys)
            {
                if (string.Equals(field, "groupId", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(Request.Form[field]))
                    {
                        return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The group ID must not be empty.", "groupid");
                    }
                }
                else if (!symbols && string.Equals(field, "listed", StringComparison.OrdinalIgnoreCase))
                {
                    var value = Request.Form[field];
                    if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The listed value must be true or false.", "listed");
                    }
                }
                else
                {
                    return Error(HttpStatusCode.BadRequest, "InvalidRequest", "The multipart field is not supported.", field?.ToLowerInvariant());
                }
            }

            return null;
        }

        private ActionResult UploadResponse(PackageStagingResult result, StagingArtifactResponse response, string statusRoute)
        {
            Response.StatusCode = (int)result.StatusCode;
            if (result.StatusCode == HttpStatusCode.Created)
            {
                Response.AppendHeader("Location", Url.RouteUrl(statusRoute, new { id = response.Id, version = response.Version }, Request.Url.Scheme));
            }

            if (!Response.HeadersWritten)
            {
                foreach (var warning in result.Warnings)
                {
                    if (!string.IsNullOrWhiteSpace(warning.PlainTextMessage))
                    {
                        Response.AppendHeader(GalleryConstants.WarningHeaderName, warning.PlainTextMessage);
                    }
                }
            }

            return JsonContent(response);
        }

        private string GetManagementUrl(StagedPackageIdentity identity)
        {
            if (identity.StagingGroupKey.HasValue)
            {
                return Url.ManageStagingGroup(identity.Owner.Username, identity.StagingGroup.Id, relativeUrl: false);
            }

            return Url.ManageUngroupedStaging(identity.Owner.Username, relativeUrl: false);
        }

        /// <summary>
        /// Downloads the selected staged package content visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The package attachment, or an owner-availability or private-resource error.</returns>
        [HttpGet]
        public virtual async Task<ActionResult> DownloadStagedPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var stagedPackage = _packageStagingManagementService.GetStagedPackage(stagingOwner, scopes, id, version);
            if (stagedPackage == null)
            {
                return Error(HttpStatusCode.NotFound, "PackageNotFound", "The staged package was not found.");
            }

            var content = await _packageStagingManagementService.OpenPackageContentAsync(stagedPackage);
            var package = stagedPackage.StagedPackageIdentity.Package;
            var fileName = $"{package.PackageRegistration.Id}.{package.NormalizedVersion}{CoreConstants.NuGetPackageFileExtension}";
            return File(content, CoreConstants.OctetStreamContentType, fileName);
        }

        /// <summary>
        /// Gets the current package artifact visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The artifact resource, or an owner-availability or private-resource error.</returns>
        [HttpGet]
        public virtual ActionResult GetStagedPackageStatus(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var package = _packageStagingManagementService.GetStagedPackage(stagingOwner, scopes, id, version);
            if (package == null)
            {
                return Error(HttpStatusCode.NotFound, "PackageNotFound", "The staged package was not found.");
            }

            return JsonContent(GetPackageResponses(new[] { package })[package.Key]);
        }

        /// <summary>
        /// Gets the current symbol artifact visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The artifact resource, or an owner-availability or private-resource error.</returns>
        [HttpGet]
        public virtual ActionResult GetStagedSymbolPackageStatus(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var symbolPackage = _packageStagingManagementService.GetStagedSymbolPackage(stagingOwner, scopes, id, version);
            if (symbolPackage == null)
            {
                return Error(HttpStatusCode.NotFound, "SymbolPackageNotFound", "The staged symbol package was not found.");
            }

            return JsonContent(GetSymbolResponses(new[] { symbolPackage })[symbolPackage.Key]);
        }

        /// <summary>
        /// Downloads the immutable uploaded symbols visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>The symbol package attachment, or an owner-availability or private-resource error.</returns>
        [HttpGet]
        public virtual async Task<ActionResult> DownloadStagedSymbolPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var symbolPackage = _packageStagingManagementService.GetStagedSymbolPackage(stagingOwner, scopes, id, version);
            if (symbolPackage == null)
            {
                return Error(HttpStatusCode.NotFound, "SymbolPackageNotFound", "The staged symbol package was not found.");
            }

            var content = await _symbolPackageStagingManagementService.OpenPackageContentAsync(symbolPackage);
            if (content == null)
            {
                return Error(HttpStatusCode.NotFound, "SymbolPackageNotFound", "The staged symbol package content was not found.");
            }

            var package = symbolPackage.StagedPackageIdentity.Package;
            var fileName = $"{package.PackageRegistration.Id}.{package.NormalizedVersion}{CoreConstants.NuGetSymbolPackageFileExtension}";
            return File(content, CoreConstants.OctetStreamContentType, fileName);
        }

        /// <summary>
        /// Deletes the current private symbols visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>No content on success, or an owner-availability, private-resource, or state-conflict error.</returns>
        [HttpDelete]
        public virtual async Task<ActionResult> DeleteStagedSymbolPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var symbolPackage = _packageStagingManagementService.GetStagedSymbolPackage(stagingOwner, scopes, id, version);
            if (symbolPackage == null)
            {
                return Error(HttpStatusCode.NotFound, "SymbolPackageNotFound", "The staged symbol package was not found.");
            }

            if (!await _symbolPackageStagingManagementService.DeletePackageAsync(symbolPackage))
            {
                return Error(HttpStatusCode.Conflict, "SymbolPackageDeletionConflict", "The staged symbols could not be deleted because promotion is active or their staging state changed. Refresh and try again.");
            }

            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Deletes the current private package visible to the API-key owner.
        /// </summary>
        /// <param name="id">The package ID.</param>
        /// <param name="version">The package version.</param>
        /// <returns>No content on success, or an owner-availability, private-resource, or state-conflict error.</returns>
        [HttpDelete]
        public virtual async Task<ActionResult> DeleteStagedPackage(string id, string version)
        {
            var currentUser = GetCurrentUser();
            var scopes = User.Identity.GetScopesFromClaim();
            var stagingOwner = _packageStagingAuthorizationService.GetEnabledApiKeyOwner(currentUser, scopes);
            if (stagingOwner == null)
            {
                return Error(HttpStatusCode.Forbidden, "StagingOwnerUnavailable", "Staging is not available for the API key owner.");
            }

            var stagedPackage = _packageStagingManagementService.GetStagedPackage(stagingOwner, scopes, id, version);
            if (stagedPackage == null || stagedPackage.StagedPackageIdentity.Package.PackageStatusKey != PackageStatus.Staged)
            {
                return Error(HttpStatusCode.NotFound, "PackageNotFound", "The staged package was not found.");
            }

            if (!await _packageStagingManagementService.DeletePackageAsync(stagedPackage))
            {
                return Error(HttpStatusCode.Conflict, "PackageDeletionConflict", "The staged package could not be deleted because promotion is active or its staging state changed. Refresh and try again.");
            }

            return new HttpStatusCodeResult(HttpStatusCode.NoContent);
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
