// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // API-key-authenticated requests do not use antiforgery tokens.
using System.Net;
using System.Web.Mvc;
using NuGetGallery.Authentication;
using NuGetGallery.Filters;

namespace NuGetGallery
{
    [ApiAuthorize]
    [ApiScopeRequired(NuGetScopes.PackageStage)]
    public class StagingController : AppController
    {
        [HttpPut]
        public ActionResult PushStagingPackage() => Unavailable();

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
            return Error(
                HttpStatusCode.ServiceUnavailable,
                StagingApiErrorCodes.StagingUnavailable,
                "Package staging is unavailable.");
        }

        private static StagingJsonResult Error(HttpStatusCode statusCode, string code, string message, string target = null)
        {
            return new StagingJsonResult(statusCode, new StagingApiErrorResponse(new StagingApiError(code, message, target)));
        }
    }
}
