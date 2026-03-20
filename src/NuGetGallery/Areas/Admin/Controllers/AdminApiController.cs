// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Filters;

namespace NuGetGallery.Controllers
{
    [AdminApiAuth]
    public class AdminApiController : AppController
    {
        private const int MaxPackageCount = 100;

        private readonly IPackageService _packageService;
        private readonly IEntitiesContext _entitiesContext;
        private readonly IPackageFileService _packageFileService;
        private readonly ITelemetryService _telemetryService;

        public AdminApiController(
            IPackageService packageService,
            IEntitiesContext entitiesContext,
            IPackageFileService packageFileService,
            ITelemetryService telemetryService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _entitiesContext = entitiesContext ?? throw new ArgumentNullException(nameof(entitiesContext));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        }

        [HttpPost]
        [ActionName("ReflowPackage")]
        public virtual async Task<ActionResult> ReflowPackageAsync()
        {
            AdminReflowPackageRequest request;
            try
            {
                Request.InputStream.Position = 0;
                using var reader = new StreamReader(Request.InputStream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var body = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<AdminReflowPackageRequest>(body);
            }
            catch (JsonException)
            {
                return Json(HttpStatusCode.BadRequest, new { error = "Invalid JSON in request body." });
            }

            if (request?.Packages == null || request.Packages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new { error = "The 'packages' field is required and must contain at least one entry." });
            }

            if (request.Packages.Count > MaxPackageCount)
            {
                return Json(HttpStatusCode.BadRequest, new { error = $"The 'packages' field must contain at most {MaxPackageCount} entries." });
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminReflowPackageResult>();
            var acceptedPackages = new List<(string Id, string Version)>();

            foreach (var entry in request.Packages)
            {
                if (string.IsNullOrWhiteSpace(entry?.Id) || string.IsNullOrWhiteSpace(entry?.Version))
                {
                    results.Add(new AdminReflowPackageResult
                    {
                        Id = entry?.Id ?? string.Empty,
                        Version = entry?.Version ?? string.Empty,
                        Status = AdminReflowPackageStatus.Invalid
                    });

                    continue;
                }

                if (!NuGetVersion.TryParse(entry.Version, out var nugetVersion))
                {
                    results.Add(new AdminReflowPackageResult
                    {
                        Id = entry.Id,
                        Version = entry.Version,
                        Status = AdminReflowPackageStatus.Invalid
                    });

                    continue;
                }

                var normalizedVersion = nugetVersion.ToNormalizedString();
                var dedupeKey = $"{entry.Id}/{normalizedVersion}";
                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                var package = _packageService.FindPackageByIdAndVersionStrict(entry.Id, normalizedVersion);
                if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                {
                    results.Add(new AdminReflowPackageResult
                    {
                        Id = entry.Id,
                        Version = normalizedVersion,
                        Status = AdminReflowPackageStatus.NotFound
                    });

                    continue;
                }

                results.Add(new AdminReflowPackageResult
                {
                    Id = entry.Id,
                    Version = normalizedVersion,
                    Status = AdminReflowPackageStatus.Accepted
                });

                acceptedPackages.Add((entry.Id, normalizedVersion));
            }

            if (acceptedPackages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminReflowPackageResponse { Results = results });
            }

            var callerAppId = HttpContext.Items["GenevaAdminApi.CallerAppId"] as string;

            _telemetryService.TrackAdminApiReflow(
                request.Packages.Count,
                acceptedPackages.Count,
                request.Reason,
                callerAppId);

            var reflowService = new ReflowPackageService(
                _entitiesContext,
                _packageService,
                _packageFileService,
                _telemetryService);

            foreach (var (id, version) in acceptedPackages)
            {
                try
                {
                    await reflowService.ReflowAsync(id, version);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminReflowPackageResponse { Results = results });
        }
    }
}
