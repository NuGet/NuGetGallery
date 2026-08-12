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
    [ApiScopeRequired(NuGetScopes.PackageStage)]
    public class StagingController : AppController
    {
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IStagingService _stagingService;

        public StagingController(IFeatureFlagService featureFlagService, IStagingService stagingService)
        {
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _stagingService = stagingService ?? throw new ArgumentNullException(nameof(stagingService));
        }

        [HttpPut]
        public async Task<ActionResult> PushStagingPackage()
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                var request = ReadUploadRequest();
                var result = await _stagingService.UploadAsync(context.CurrentUser, context.Owner, context.Credential, request);
                var location = Url?.RouteUrl(RouteName.GetStagedPackage, new { id = result.Package.Id, version = result.Package.Version });
                if (location != null)
                {
                    Response.Headers["Location"] = location;
                }

                return new StagingJsonResult(result.Created ? HttpStatusCode.Created : HttpStatusCode.OK, result.Package);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
            catch (HttpException ex) when (ex.IsMaxRequestLengthExceeded())
            {
                return Error(HttpStatusCode.RequestEntityTooLarge, StagingApiErrorCodes.RequestTooLarge, "The complete staging request exceeds the maximum allowed size.");
            }
        }

        [HttpGet]
        public ActionResult ListStagedPackages(string groupId, bool ungrouped = false, int take = 100, string continuationToken = null)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                return new StagingJsonResult(HttpStatusCode.OK, _stagingService.ListPackages(context, groupId, ungrouped, take, continuationToken));
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpGet]
        public ActionResult GetStagedPackage(string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                return new StagingJsonResult(HttpStatusCode.OK, _stagingService.GetPackage(context, id, version));
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpPatch]
        public async Task<ActionResult> SetStagedPackageListed(string id, string version, StagingListedRequest request)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                if (request?.Listed == null)
                {
                    throw new StagingApiException(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidRequestBody, "A JSON body with an explicit listed value is required.", "listed");
                }

                var stagedPackage = await _stagingService.SetListedAsync(context, id, version, request.Listed.Value);
                return new StagingJsonResult(HttpStatusCode.OK, stagedPackage);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpGet]
        public async Task<ActionResult> DownloadStagedPackage(string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                var download = await _stagingService.DownloadPackageAsync(context, id, version);
                return File(download.Content, download.ContentType, download.FileName);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpGet]
        public async Task<ActionResult> DownloadStagedSymbolsPackage(string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                var download = await _stagingService.DownloadSymbolsAsync(context, id, version);
                return File(download.Content, download.ContentType, download.FileName);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteStagedPackage(string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                await _stagingService.DeletePackageAsync(context, id, version);
                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteStagedSymbolsPackage(string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                await _stagingService.DeleteSymbolsAsync(context, id, version);
                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpPost]
        public async Task<ActionResult> CreateStagingGroup(StagingCreateGroupRequest request)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                request = request ?? new StagingCreateGroupRequest();
                var result = await _stagingService.CreateGroupAsync(context, request);
                return new StagingJsonResult(result.Created ? HttpStatusCode.Created : HttpStatusCode.OK, result.Group);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpGet]
        public ActionResult ListStagingGroups()
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                return new StagingJsonResult(HttpStatusCode.OK, _stagingService.ListGroups(context));
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpGet]
        public ActionResult GetStagingGroup(string groupId, int take = 100, string continuationToken = null)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                return new StagingJsonResult(HttpStatusCode.OK, _stagingService.GetGroup(context, groupId, take, continuationToken));
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpPatch]
        public async Task<ActionResult> RenameStagingGroup(string groupId, StagingRenameGroupRequest request)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                request = request ?? new StagingRenameGroupRequest();
                var group = await _stagingService.RenameGroupAsync(context, groupId, request);
                return new StagingJsonResult(HttpStatusCode.OK, group);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpDelete]
        public async Task<ActionResult> DeleteStagingGroup(string groupId)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                await _stagingService.DeleteGroupAsync(context, groupId);
                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpPut]
        public async Task<ActionResult> AddPackageToStagingGroup(string groupId, string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                var stagedPackage = await _stagingService.AddPackageToGroupAsync(context, groupId, id, version);
                return new StagingJsonResult(HttpStatusCode.OK, stagedPackage);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        [HttpDelete]
        public async Task<ActionResult> RemovePackageFromStagingGroup(string groupId, string id, string version)
        {
            var context = ResolveContext(out var guard);
            if (guard != null)
            {
                return guard;
            }

            try
            {
                var stagedPackage = await _stagingService.RemovePackageFromGroupAsync(context, groupId, id, version);
                return new StagingJsonResult(HttpStatusCode.OK, stagedPackage);
            }
            catch (StagingApiException ex)
            {
                return Error(ex.StatusCode, ex.Code, ex.Message, ex.Target);
            }
        }

        private StagingAuthorizationContext ResolveContext(out ActionResult guard)
        {
            var currentUser = GetCurrentUser();
            var credential = currentUser.GetCurrentApiKeyCredential(User.Identity);
            var owner = credential?.Scopes.GetOwnerScope();
            if (owner == null)
            {
                guard = Error(HttpStatusCode.Forbidden, StagingApiErrorCodes.StagingScopeRequired, "An owner-scoped package:stage credential is required.");
                return null;
            }

            if (!_featureFlagService.IsStagingEnabled(owner))
            {
                guard = new HttpStatusCodeResult(HttpStatusCode.NotFound);
                return null;
            }

            guard = null;
            return new StagingAuthorizationContext(currentUser, owner, credential);
        }

        private StagingUploadRequest ReadUploadRequest()
        {
            if (Request?.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) != true)
            {
                throw new StagingApiException(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidMultipart, "The request must use multipart/form-data.");
            }

            HttpPostedFileBase package = null;
            HttpPostedFileBase symbols = null;
            for (var i = 0; i < Request.Files.Count; i++)
            {
                var name = Request.Files.AllKeys[i];
                var file = Request.Files[i];
                if (string.Equals(name, "package", StringComparison.OrdinalIgnoreCase) && package == null)
                {
                    package = file;
                }
                else if (string.Equals(name, "symbols", StringComparison.OrdinalIgnoreCase) && symbols == null)
                {
                    symbols = file;
                }
                else
                {
                    throw new StagingApiException(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidMultipart, "The multipart request contains an unexpected or duplicate file part.");
                }
            }

            bool? listed = null;
            var listedValue = Request.Form["listed"];
            if (listedValue != null)
            {
                if (!bool.TryParse(listedValue, out var parsed))
                {
                    throw new StagingApiException(HttpStatusCode.BadRequest, StagingApiErrorCodes.InvalidMultipart, "The listed field must be true or false.", "listed");
                }
                listed = parsed;
            }

            return new StagingUploadRequest
            {
                Package = package?.InputStream,
                Symbols = symbols?.InputStream,
                GroupId = Request.Form["groupId"],
                Listed = listed,
            };
        }

        private static ActionResult Error(HttpStatusCode statusCode, string code, string message, string target = null)
        {
            return new StagingJsonResult(statusCode, new StagingApiErrorResponse(new StagingApiError(code, message, target)));
        }
    }
}
