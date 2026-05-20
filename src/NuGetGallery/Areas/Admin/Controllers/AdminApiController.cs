// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // No need to validate Antiforgery Token with API request
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Filters;

namespace NuGetGallery.Areas.Admin.Controllers
{
    [AdminApiAuth]
    public class AdminApiController : AppController
    {
        private readonly IPackageService _packageService;
        private readonly IReflowPackageService _reflowPackageService;
        private readonly ILockPackageService _lockPackageService;
        private readonly ILockUserService _lockUserService;
        private readonly IPackageDeleteService _packageDeleteService;

        public AdminApiController(
            IPackageService packageService,
            IReflowPackageService reflowPackageService,
            ILockPackageService lockPackageService,
            ILockUserService lockUserService,
            IPackageDeleteService packageDeleteService)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reflowPackageService = reflowPackageService ?? throw new ArgumentNullException(nameof(reflowPackageService));
            _lockPackageService = lockPackageService ?? throw new ArgumentNullException(nameof(lockPackageService));
            _lockUserService = lockUserService ?? throw new ArgumentNullException(nameof(lockUserService));
            _packageDeleteService = packageDeleteService ?? throw new ArgumentNullException(nameof(packageDeleteService));
        }

        [HttpPost]
        [ActionName("ReflowPackage")]
        public virtual async Task<ActionResult> ReflowPackageAsync(AdminReflowPackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var seen = new HashSet<PackageIdentity>();
            var results = new List<AdminReflowPackageResult>();
            var acceptedPackages = new List<PackageIdentity>();

            foreach (var entry in request.Packages)
            {
                var nugetVersion = NuGetVersion.Parse(entry.Version.Trim());
                var identity = new PackageIdentity(entry.Id.Trim(), nugetVersion);

                if (!seen.Add(identity))
                {
                    continue;
                }

                var normalizedVersion = identity.Version.ToNormalizedString();
                var package = _packageService.FindPackageByIdAndVersionStrict(identity.Id, normalizedVersion);
                if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                {
                    results.Add(new AdminReflowPackageResult
                    {
                        Id = identity.Id,
                        Version = normalizedVersion,
                        Status = AdminReflowPackageStatus.NotFound
                    });

                    continue;
                }

                results.Add(new AdminReflowPackageResult
                {
                    Id = identity.Id,
                    Version = normalizedVersion,
                    Status = AdminReflowPackageStatus.Accepted
                });

                acceptedPackages.Add(identity);
            }

            if (acceptedPackages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminReflowPackageResponse
                {
                    Results = results
                });
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;

            foreach (var packageIdentity in acceptedPackages)
            {
                try
                {
                    await _reflowPackageService.ReflowAsync(
                        packageIdentity.Id,
                        packageIdentity.Version.ToNormalizedString(),
                        request.Reason,
                        callerIdentity);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminReflowPackageResponse
            {
                Results = results
            });
        }

        [HttpPost]
        [ActionName("LockPackage")]
        public virtual async Task<ActionResult> LockPackageAsync(AdminLockPackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminLockPackageResult>();
            var acceptedPackageIds = new List<string>();

            foreach (var entry in request.Packages)
            {
                var packageId = entry.Id.Trim();

                if (!seen.Add(packageId))
                {
                    continue;
                }

                results.Add(new AdminLockPackageResult
                {
                    Id = packageId,
                    Status = AdminLockPackageStatus.Accepted
                });

                acceptedPackageIds.Add(packageId);
            }

            if (acceptedPackageIds.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminLockPackageResponse
                {
                    Results = results
                });
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;

            foreach (var packageId in acceptedPackageIds)
            {
                try
                {
                    await _lockPackageService.SetLockStateAsync(
                        packageId,
                        isLocked: request.Locked.Value,
                        request.Reason,
                        callerIdentity);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminLockPackageResponse
            {
                Results = results
            });
        }

        [HttpPost]
        [ActionName("LockUser")]
        public virtual async Task<ActionResult> LockUserAsync(AdminLockUserRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminLockUserResult>();
            var acceptedUsers = new List<string>();

            foreach (var entry in request.Users)
            {
                var username = entry.Username.Trim();

                if (!seen.Add(username))
                {
                    continue;
                }

                results.Add(new AdminLockUserResult
                {
                    Username = username,
                    Status = AdminLockUserStatus.Accepted
                });

                acceptedUsers.Add(username);
            }

            if (acceptedUsers.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminLockUserResponse
                {
                    Results = results
                });
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;

            foreach (var username in acceptedUsers)
            {
                try
                {
                    await _lockUserService.SetLockStateAsync(
                        username,
                        isLocked: request.Locked.Value,
                        request.Reason,
                        callerIdentity);
                }
                catch (Exception ex)
                {
                    QuietLog.LogHandledException(ex);
                }
            }

            return Json(HttpStatusCode.Accepted, new AdminLockUserResponse
            {
                Results = results
            });
        }

        [HttpPost]
        [ActionName("SoftDeletePackage")]
        public virtual async Task<ActionResult> SoftDeletePackageAsync(AdminSoftDeletePackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var seen = new HashSet<PackageIdentity>();
            var results = new List<AdminSoftDeletePackageResult>();
            var acceptedPackages = new List<Package>();

            foreach (var entry in request.Packages)
            {
                var packageId = entry.Id.Trim();
                var nugetVersion = NuGetVersion.Parse(entry.Version.Trim());
                var identity = new PackageIdentity(packageId, nugetVersion);

                if (!seen.Add(identity))
                {
                    continue;
                }

                var normalizedVersion = identity.Version.ToNormalizedString();
                var package = _packageService.FindPackageByIdAndVersionStrict(packageId, normalizedVersion);
                if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                {
                    results.Add(new AdminSoftDeletePackageResult
                    {
                        Id = packageId,
                        Version = normalizedVersion,
                        Status = AdminSoftDeletePackageStatus.NotFound
                    });

                    continue;
                }

                results.Add(new AdminSoftDeletePackageResult
                {
                    Id = packageId,
                    Version = normalizedVersion,
                    Status = AdminSoftDeletePackageStatus.Accepted
                });

                acceptedPackages.Add(package);
            }

            if (acceptedPackages.Count == 0)
            {
                return Json(HttpStatusCode.BadRequest, new AdminSoftDeletePackageResponse
                {
                    Results = results
                });
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;

            try
            {
                await _packageDeleteService.SoftDeletePackagesAsync(
                    acceptedPackages,
                    deletedBy: null,
                    reason: request.Reason,
                    signature: callerIdentity);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
            }

            return Json(HttpStatusCode.Accepted, new AdminSoftDeletePackageResponse
            {
                Results = results
            });
        }

        private JsonResult BadRequestFromModelState()
        {
            var errors = new Dictionary<string, string[]>();

            foreach (var entry in ModelState)
            {
                var modelState = entry.Value;
                if (modelState.Errors.Count == 0)
                {
                    continue;
                }

                errors[entry.Key] = [.. modelState.Errors.Select(GetErrorMessage)];
            }

            return Json(HttpStatusCode.BadRequest, new
            {
                message = "The request is invalid.",
                errors
            });
        }

        private static string GetErrorMessage(ModelError error)
        {
            if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
            {
                return error.ErrorMessage;
            }

            if (error.Exception != null)
            {
                return error.Exception.Message;
            }

            return "The value is invalid.";
        }
    }
}
