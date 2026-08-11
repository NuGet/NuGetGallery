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
            var currentUser = GetCurrentUser();
            var credential = currentUser.GetCurrentApiKeyCredential(User.Identity);
            var owner = credential?.Scopes.GetOwnerScope();
            if (owner == null)
            {
                return Error(HttpStatusCode.Forbidden, StagingApiErrorCodes.StagingScopeRequired, "An owner-scoped package:stage credential is required.");
            }

            if (!_featureFlagService.IsStagingEnabled(owner))
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            try
            {
                var request = ReadUploadRequest();
                var result = await _stagingService.UploadAsync(currentUser, owner, credential, request);
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
        public ActionResult ListStagedPackages(string groupId, bool ungrouped = false, int take = 100, string continuationToken = null) => Unavailable();

        [HttpGet]
        public ActionResult GetStagedPackage(string id, string version) => Unavailable();

        [HttpPatch]
        public ActionResult SetStagedPackageListed(string id, string version, StagingListedRequest request) => Unavailable();

        [HttpGet]
        public ActionResult DownloadStagedPackage(string id, string version) => Unavailable();

        [HttpGet]
        public ActionResult DownloadStagedSymbolsPackage(string id, string version) => Unavailable();

        [HttpDelete]
        public ActionResult DeleteStagedPackage(string id, string version) => Unavailable();

        [HttpDelete]
        public ActionResult DeleteStagedSymbolsPackage(string id, string version) => Unavailable();

        [HttpPost]
        public ActionResult CreateStagingGroup(StagingCreateGroupRequest request) => Unavailable();

        [HttpGet]
        public ActionResult ListStagingGroups() => Unavailable();

        [HttpGet]
        public ActionResult GetStagingGroup(string groupId, int take = 100, string continuationToken = null) => Unavailable();

        [HttpPatch]
        public ActionResult RenameStagingGroup(string groupId, StagingRenameGroupRequest request) => Unavailable();

        [HttpDelete]
        public ActionResult DeleteStagingGroup(string groupId) => Unavailable();

        [HttpPut]
        public ActionResult AddPackageToStagingGroup(string groupId, string id, string version) => Unavailable();

        [HttpDelete]
        public ActionResult RemovePackageFromStagingGroup(string groupId, string id, string version) => Unavailable();

        private ActionResult Unavailable()
        {
            var currentUser = GetCurrentUser();
            var credential = currentUser.GetCurrentApiKeyCredential(User.Identity);
            var owner = credential?.Scopes.GetOwnerScope();
            if (owner == null)
            {
                return Error(HttpStatusCode.Forbidden, StagingApiErrorCodes.StagingScopeRequired, "An owner-scoped package:stage credential is required.");
            }

            if (!_featureFlagService.IsStagingEnabled(owner))
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            return Error(HttpStatusCode.ServiceUnavailable, StagingApiErrorCodes.StagingUnavailable, "Package staging is temporarily unavailable.");
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
