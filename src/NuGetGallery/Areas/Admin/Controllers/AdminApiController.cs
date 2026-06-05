// Copyright(c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma warning disable CA3147 // No need to validate Antiforgery Token with API request
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;
using NuGet.Packaging.Core;
using NuGet.Services.Entities;
using NuGet.Services.Validation;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.Filters;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.Services;

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
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IUpdateListedService _updateListedService;
        private readonly ValidationAdminService _validationAdminService;
        private readonly IValidationService _validationService;
        private readonly ISymbolPackageService _symbolPackageService;

        public AdminApiController(
            IPackageService packageService,
            IReflowPackageService reflowPackageService,
            ILockPackageService lockPackageService,
            ILockUserService lockUserService,
            IPackageDeleteService packageDeleteService,
            IFeatureFlagService featureFlagService,
            IUpdateListedService updateListedService,
            ValidationAdminService validationAdminService = null,
            IValidationService validationService = null,
            ISymbolPackageService symbolPackageService = null)
        {
            _packageService = packageService ?? throw new ArgumentNullException(nameof(packageService));
            _reflowPackageService = reflowPackageService ?? throw new ArgumentNullException(nameof(reflowPackageService));
            _lockPackageService = lockPackageService ?? throw new ArgumentNullException(nameof(lockPackageService));
            _lockUserService = lockUserService ?? throw new ArgumentNullException(nameof(lockUserService));
            _packageDeleteService = packageDeleteService ?? throw new ArgumentNullException(nameof(packageDeleteService));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _updateListedService = updateListedService ?? throw new ArgumentNullException(nameof(updateListedService));
            _validationAdminService = validationAdminService;
            _validationService = validationService;
            _symbolPackageService = symbolPackageService;
        }

        [HttpPost]
        [ActionName("ReflowPackage")]
        public virtual async Task<ActionResult> ReflowPackageAsync(AdminReflowPackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;
            var seen = new HashSet<PackageIdentity>();
            var results = new List<AdminReflowPackageResult>();
            var hasAccepted = false;

            foreach (var entry in request.Packages)
            {
                var nugetVersion = NuGetVersion.Parse(entry.Version.Trim());
                var identity = new PackageIdentity(entry.Id.Trim(), nugetVersion);

                if (!seen.Add(identity))
                {
                    continue;
                }

                var normalizedVersion = identity.Version.ToNormalizedString();
                var status = AdminReflowPackageStatus.Accepted;

                try
                {
                    var package = await _reflowPackageService.ReflowAsync(
                        identity.Id,
                        normalizedVersion,
                        request.Reason,
                        callerIdentity);

                    if (package != null)
                    {
                        hasAccepted = true;
                    }
                    else
                    {
                        status = AdminReflowPackageStatus.NotFound;
                    }
                }
                catch (Exception ex)
                {
                    status = AdminReflowPackageStatus.Failed;
                    QuietLog.LogHandledException(ex);
                }

                results.Add(new AdminReflowPackageResult
                {
                    Id = identity.Id,
                    Version = normalizedVersion,
                    Status = status
                });
            }

            var statusCode = hasAccepted ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest;
            return Json(statusCode, new AdminReflowPackageResponse
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

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminLockPackageResult>();
            var hasAccepted = false;

            foreach (var entry in request.Packages)
            {
                var packageId = entry.Id.Trim();

                if (!seen.Add(packageId))
                {
                    continue;
                }

                var status = AdminLockPackageStatus.Accepted;
                try
                {
                    var serviceResult = await _lockPackageService.SetLockStateAsync(
                        packageId,
                        isLocked: request.Locked.Value,
                        request.Reason,
                        callerIdentity);

                    if (serviceResult == LockPackageServiceResult.PackageNotFound)
                    {
                        status = AdminLockPackageStatus.NotFound;
                    }
                    else
                    {
                        hasAccepted = true;
                    }
                }
                catch (Exception ex)
                {
                    status = AdminLockPackageStatus.Failed;
                    QuietLog.LogHandledException(ex);
                }

                results.Add(new AdminLockPackageResult
                {
                    Id = packageId,
                    Status = status
                });
            }

            var statusCode = hasAccepted ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest;
            return Json(statusCode, new AdminLockPackageResponse
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

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminLockUserResult>();
            var hasAccepted = false;

            foreach (var entry in request.Users)
            {
                var username = entry.Username.Trim();

                if (!seen.Add(username))
                {
                    continue;
                }

                var status = AdminLockUserStatus.Accepted;
                try
                {
                    var serviceResult = await _lockUserService.SetLockStateAsync(
                        username,
                        isLocked: request.Locked.Value,
                        request.Reason,
                        callerIdentity);

                    if (serviceResult == LockUserServiceResult.UserNotFound)
                    {
                        status = AdminLockUserStatus.NotFound;
                    }
                    else
                    {
                        hasAccepted = true;
                    }
                }
                catch (Exception ex)
                {
                    status = AdminLockUserStatus.Failed;
                    QuietLog.LogHandledException(ex);
                }

                results.Add(new AdminLockUserResult
                {
                    Username = username,
                    Status = status
                });
            }

            var statusCode = hasAccepted ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest;
            return Json(statusCode, new AdminLockUserResponse
            {
                Results = results
            });
        }

        [HttpPost]
        [ActionName("SoftDeletePackage")]
        public virtual async Task<ActionResult> SoftDeletePackageAsync(AdminSoftDeletePackageRequest request)
        {
            if (_featureFlagService == null || !_featureFlagService.IsAdminApiSoftDeleteEnabled())
            {
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);
            }

            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;
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
                foreach (var result in results)
                {
                    if (result.Status == AdminSoftDeletePackageStatus.Accepted)
                    {
                        result.Status = AdminSoftDeletePackageStatus.Failed;
                    }
                }

                QuietLog.LogHandledException(ex);

                return Json(HttpStatusCode.BadRequest, new AdminSoftDeletePackageResponse
                {
                    Results = results
                });
            }

            return Json(HttpStatusCode.Accepted, new AdminSoftDeletePackageResponse
            {
                Results = results
            });
        }

        [HttpPost]
        [ActionName("ListPackage")]
        public virtual async Task<ActionResult> UpdateListedPackageAsync(AdminUpdateListedPackageRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var callerIdentity = HttpContext.Items[AdminApiAuthAttribute.CallerIdentityItemKey] as string;
            var seen = new HashSet<PackageIdentity>();
            var results = new List<AdminUpdateListedPackageResult>();
            var hasAccepted = false;

            var deduped = new List<UpdateListedPackageIdentity>();
            foreach (var entry in request.Packages)
            {
                var nugetVersion = NuGetVersion.Parse(entry.Version.Trim());
                var identity = new PackageIdentity(entry.Id.Trim(), nugetVersion);

                if (!seen.Add(identity))
                {
                    continue;
                }

                deduped.Add(new UpdateListedPackageIdentity
                {
                    Id = identity.Id,
                    Version = identity.Version.ToNormalizedString()
                });
            }

            try
            {
                var serviceResults = await _updateListedService.UpdateListedAsync(
                    deduped,
                    request.Listed.Value,
                    request.Reason,
                    callerIdentity);

                foreach (var serviceResult in serviceResults)
                {
                    var status = serviceResult.Result == UpdateListedServiceResult.Success
                        ? AdminUpdateListedPackageStatus.Accepted
                        : AdminUpdateListedPackageStatus.NotFound;

                    if (serviceResult.Result == UpdateListedServiceResult.Success)
                    {
                        hasAccepted = true;
                    }

                    results.Add(new AdminUpdateListedPackageResult
                    {
                        Id = serviceResult.Id,
                        Version = serviceResult.Version,
                        Status = status
                    });
                }
            }
            catch (Exception ex)
            {
                foreach (var entry in deduped)
                {
                    results.Add(new AdminUpdateListedPackageResult
                    {
                        Id = entry.Id,
                        Version = entry.Version,
                        Status = AdminUpdateListedPackageStatus.Failed
                    });
                }

                QuietLog.LogHandledException(ex);

                return Json(HttpStatusCode.BadRequest, new AdminUpdateListedPackageResponse
                {
                    Results = results
                });
            }

            var statusCode = hasAccepted ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest;
            return Json(statusCode, new AdminUpdateListedPackageResponse
            {
                Results = results
            });
        }

        [HttpGet]
        [ActionName("PendingValidations")]
        public virtual ActionResult GetPendingValidations()
        {
            if (_validationAdminService == null)
            {
                return Json(HttpStatusCode.ServiceUnavailable, new { error = "Validation is not configured." }, JsonRequestBehavior.AllowGet);
            }

            var validationSets = _validationAdminService.GetPending();

            var groups = validationSets
                .GroupBy(s => new { s.PackageKey, s.ValidatingType })
                .OrderBy(g => g.First().PackageId, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(g => g.First().PackageNormalizedVersion);

            var results = new List<AdminPendingValidationResult>();
            foreach (var group in groups)
            {
                var first = group.First();
                results.Add(new AdminPendingValidationResult
                {
                    PackageKey = first.PackageKey.Value,
                    PackageId = first.PackageId,
                    PackageVersion = first.PackageNormalizedVersion,
                    ValidatingType = group.Key.ValidatingType.ToString(),
                    ValidationSets = group
                        .OrderByDescending(s => s.Created)
                        .Select(s => new AdminPendingValidationSetResult
                        {
                            Key = s.Key,
                            ValidationTrackingId = s.ValidationTrackingId,
                            ValidationSetStatus = s.ValidationSetStatus.ToString(),
                            Created = s.Created,
                            Updated = s.Updated,
                            Validations = s.PackageValidations
                                ?.OrderBy(v => v.Started)
                                .Select(v => new AdminPendingValidationStepResult
                                {
                                    Key = v.Key,
                                    Type = v.Type,
                                    Status = v.ValidationStatus.ToString(),
                                    Started = v.Started,
                                    ValidationStatusTimestamp = v.ValidationStatusTimestamp
                                })
                                .ToList() ?? []
                        })
                        .ToList()
                });
            }

            return Json(HttpStatusCode.OK, new AdminPendingValidationsResponse { Results = results }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ActionName("RevalidatePackage")]
        public virtual async Task<ActionResult> RevalidatePackageAsync(AdminRevalidatePackageRequest request)
        {
            if (_validationService == null)
            {
                return Json(HttpStatusCode.ServiceUnavailable, new { error = "Validation is not configured." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequestFromModelState();
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<AdminRevalidatePackageResult>();
            var hasAccepted = false;

            foreach (var entry in request.Packages)
            {
                var nugetVersion = NuGetVersion.Parse(entry.Version.Trim());
                var normalizedVersion = nugetVersion.ToNormalizedString();
                var packageId = entry.Id.Trim();
                var validatingType = entry.ValidatingType.Trim();
                var dedupeKey = $"{packageId}/{normalizedVersion}/{validatingType}";

                if (!seen.Add(dedupeKey))
                {
                    continue;
                }

                var status = AdminRevalidatePackageStatus.Accepted;

                try
                {
                    if (validatingType == nameof(ValidatingType.Package))
                    {
                        var package = _packageService.FindPackageByIdAndVersionStrict(packageId, normalizedVersion);

                        if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                        {
                            status = AdminRevalidatePackageStatus.NotFound;
                        }
                        else
                        {
                            await _validationService.RevalidateAsync(package);
                            hasAccepted = true;
                        }
                    }
                    else if (validatingType == nameof(ValidatingType.SymbolPackage))
                    {
                        var package = _packageService.FindPackageByIdAndVersionStrict(packageId, normalizedVersion);

                        if (package == null || package.PackageStatusKey == PackageStatus.Deleted)
                        {
                            status = AdminRevalidatePackageStatus.NotFound;
                        }
                        else
                        {
                            var latestSymbolPackage = package.LatestSymbolPackage();

                            if (latestSymbolPackage == null || latestSymbolPackage.StatusKey == PackageStatus.Deleted)
                            {
                                status = AdminRevalidatePackageStatus.NotFound;
                            }
                            else
                            {
                                await _validationService.RevalidateAsync(latestSymbolPackage);
                                hasAccepted = true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    status = AdminRevalidatePackageStatus.Failed;
                    QuietLog.LogHandledException(ex);
                }

                results.Add(new AdminRevalidatePackageResult
                {
                    Id = packageId,
                    Version = normalizedVersion,
                    ValidatingType = validatingType,
                    Status = status
                });
            }

            var statusCode = hasAccepted ? HttpStatusCode.Accepted : HttpStatusCode.BadRequest;
            return Json(statusCode, new AdminRevalidatePackageResponse
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

        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.Exception.StackTrace?.Contains("JsonValueProviderFactory") == true)
            {
                filterContext.ExceptionHandled = true;
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new JsonResult
                {
                    Data = new { message = "The request body contains invalid JSON." },
                    ContentType = "application/json",
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            filterContext.ExceptionHandled = true;
            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            Response.TrySkipIisCustomErrors = true;
            filterContext.Result = new JsonResult
            {
                Data = new { message = "An unexpected error occurred." },
                ContentType = "application/json",
                JsonRequestBehavior = JsonRequestBehavior.AllowGet
            };
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
