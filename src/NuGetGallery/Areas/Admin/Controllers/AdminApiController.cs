// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // No need to validate Antiforgery Token with API request
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
        private const int MaxUserCount = 10;

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

            var callerAzp = HttpContext.Items[AdminApiAuthAttribute.AzpItemKey] as string;

            _telemetryService.TrackAdminApiReflowPackage(
                request.Packages.Count,
                acceptedPackages.Count,
                request.Reason,
                callerAzp);

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

        [HttpPost]
        [ActionName("LockPackage")]
        public virtual async Task<ActionResult> LockPackageAsync()
        {
            AdminLockPackageRequest request;
            try
            {
                Request.InputStream.Position = 0;
                using var reader = new StreamReader(Request.InputStream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var body = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<AdminLockPackageRequest>(body);
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
            var results = new List<AdminLockPackageResult>();
            var acceptedPackages = new List<(string Id, string Version)>();

            foreach (var entry in request.Packages)
            {
                if (string.IsNullOrWhiteSpace(entry?.Id) || string.IsNullOrWhiteSpace(entry?.Version))
                {
                    results.Add(new AdminLockPackageResult
                    {
                        Id = entry?.Id ?? string.Empty,
                        Version = entry?.Version ?? string.Empty,
                        Status = AdminLockPackageStatus.Invalid
                    });

                    continue;
                }

                if (!NuGetVersion.TryParse(entry.Version, out var nugetVersion))
                {
                    results.Add(new AdminLockPackageResult
                    {
                        Id = entry.Id,
                        Version = entry.Version,
                        Status = AdminLockPackageStatus.Invalid
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
                    results.Add(new AdminLockPackageResult
                    {
                        Id = entry.Id,
                        Version = normalizedVersion,
                        Status = AdminLockPackageStatus.NotFound
                    });

                    continue;
                }

                results.Add(new AdminLockPackageResult
                {
                    Id = entry.Id,
                    Version = normalizedVersion,
                    Status = AdminLockPackageStatus.Accepted
                });

                acceptedPackages.Add((entry.Id, normalizedVersion));
            }

            if (acceptedPackages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminLockPackageResponse { Results = results });
            }

            var callerAzp = HttpContext.Items[AdminApiAuthAttribute.AzpItemKey] as string;

            _telemetryService.TrackAdminApiLockPackage(
                request.Packages.Count,
                acceptedPackages.Count,
                request.Reason,
                callerAzp);

            //var reflowService = new ReflowPackageService(
            //    _entitiesContext,
            //    _packageService,
            //    _packageFileService,
            //    _telemetryService);

            foreach (var (id, version) in acceptedPackages)
            {
                try
                {
                    // await reflowService.ReflowAsync(id, version);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminLockPackageResponse { Results = results });
        }

        [HttpPost]
        [ActionName("LockUser")]
        public virtual async Task<ActionResult> LockUserAsync()
        {
            AdminLockUserRequest request;
            try
            {
                Request.InputStream.Position = 0;
                using var reader = new StreamReader(Request.InputStream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var body = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<AdminLockUserRequest>(body);
            }
            catch (JsonException)
            {
                return Json(HttpStatusCode.BadRequest, new { error = "Invalid JSON in request body." });
            }

            if (request?.Users == null || request.Users.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new { error = "The 'users' field is required and must contain at least one entry." });
            }

            if (request.Users.Count > MaxUserCount)
            {
                return Json(HttpStatusCode.BadRequest, new { error = $"The 'users' field must contain at most {MaxUserCount} entries." });
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminLockUserResult>();
            var acceptedUsers = new List<string>();

            foreach (var entry in request.Users)
            {
                if (string.IsNullOrWhiteSpace(entry?.Username))
                {
                    results.Add(new AdminLockUserResult
                    {
                        Username = entry?.Username ?? string.Empty,
                        Status = AdminLockUserStatus.Invalid
                    });

                    continue;
                }

                if (!seen.Add(entry.Username))
                {
                    continue;
                }

                results.Add(new AdminLockUserResult
                {
                    Username = entry.Username,
                    Status = AdminLockUserStatus.Accepted
                });

                acceptedUsers.Add(entry.Username);
            }

            if (acceptedUsers.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminLockUserResponse { Results = results });
            }

            var callerAzp = HttpContext.Items[AdminApiAuthAttribute.AzpItemKey] as string;

            _telemetryService.TrackAdminApiLockUser(
                request.Users.Count,
                acceptedUsers.Count,
                request.Reason,
                callerAzp);

            //var reflowService = new ReflowPackageService(
            //    _entitiesContext,
            //    _packageService,
            //    _packageFileService,
            //    _telemetryService);

            foreach (var username in acceptedUsers)
            {
                try
                {
                    // await reflowService.ReflowAsync(id, version);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminLockUserResponse { Results = results });
        }

        [HttpPost]
        [ActionName("SoftDeletePackage")]
        public virtual async Task<ActionResult> SoftDeletePackageAsync()
        {
            AdminSoftDeletePackageRequest request;
            try
            {
                Request.InputStream.Position = 0;
                using var reader = new StreamReader(Request.InputStream, Encoding.UTF8, true, 4096, leaveOpen: true);
                var body = reader.ReadToEnd();
                request = JsonConvert.DeserializeObject<AdminSoftDeletePackageRequest>(body);
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
            var results = new List<AdminSoftDeletePackageResult>();
            var acceptedPackages = new List<(string Id, string Version)>();

            foreach (var entry in request.Packages)
            {
                if (string.IsNullOrWhiteSpace(entry?.Id) || string.IsNullOrWhiteSpace(entry?.Version))
                {
                    results.Add(new AdminSoftDeletePackageResult
                    {
                        Id = entry?.Id ?? string.Empty,
                        Version = entry?.Version ?? string.Empty,
                        Status = AdminSoftDeletePackageStatus.Invalid
                    });

                    continue;
                }

                if (!NuGetVersion.TryParse(entry.Version, out var nugetVersion))
                {
                    results.Add(new AdminSoftDeletePackageResult
                    {
                        Id = entry.Id,
                        Version = entry.Version,
                        Status = AdminSoftDeletePackageStatus.Invalid
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
                    results.Add(new AdminSoftDeletePackageResult
                    {
                        Id = entry.Id,
                        Version = normalizedVersion,
                        Status = AdminSoftDeletePackageStatus.NotFound
                    });

                    continue;
                }

                results.Add(new AdminSoftDeletePackageResult
                {
                    Id = entry.Id,
                    Version = normalizedVersion,
                    Status = AdminSoftDeletePackageStatus.Accepted
                });

                acceptedPackages.Add((entry.Id, normalizedVersion));
            }

            if (acceptedPackages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminSoftDeletePackageResponse { Results = results });
            }

            var callerAzp = HttpContext.Items[AdminApiAuthAttribute.AzpItemKey] as string;

            _telemetryService.TrackAdminApiSoftDeletePackage(
                request.Packages.Count,
                acceptedPackages.Count,
                request.Reason,
                callerAzp);

            //var reflowService = new ReflowPackageService(
            //    _entitiesContext,
            //    _packageService,
            //    _packageFileService,
            //    _telemetryService);

            foreach (var (id, version) in acceptedPackages)
            {
                try
                {
                    // await reflowService.ReflowAsync(id, version);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminSoftDeletePackageResponse { Results = results });
        }
    }
}
