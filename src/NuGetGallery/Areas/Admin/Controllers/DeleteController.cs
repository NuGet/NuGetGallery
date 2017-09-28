// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public partial class DeleteController : AdminControllerBase
    {
        private readonly IPackageService _packageService;

        protected DeleteController() { }

        public DeleteController(IPackageService packageService)
        {
            _packageService = packageService;
        }

        [HttpGet]
        public virtual ActionResult Index()
        {
            var model = new DeletePackagesRequest
            {
                ReasonChoices = ReportMyPackageReasons
            };
            return View(model);
        }

        private static readonly ReportPackageReason[] ReportMyPackageReasons = {
            ReportPackageReason.ContainsPrivateAndConfidentialData,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        [HttpGet]
        public virtual ActionResult Search(string query)
        {
            // Search suports several options:
            //   1) Full package id (e.g. jQuery)
            //   2) Full package id + version (e.g. jQuery 1.9.0)
            //   3) Any of the above separated by comma
            // We are not using Lucene index here as we want to have the database values.

            var queryParts = query.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var packages = new List<Package>();
            var completedQueryParts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var queryPart in queryParts)
            {
                // Don't make the same query twice.
                if (!completedQueryParts.Add(queryPart.Trim()))
                {
                    continue;
                }

                var splitQueryPart = queryPart.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (splitQueryPart.Length == 1)
                {
                    var resultingRegistration = _packageService.FindPackageRegistrationById(splitQueryPart[0].Trim());
                    if (resultingRegistration != null)
                    {
                        packages.AddRange(resultingRegistration
                            .Packages
                            .OrderBy(p => NuGetVersion.Parse(p.NormalizedVersion)));
                    }
                }
                else if (splitQueryPart.Length == 2)
                {
                    var resultingPackage = _packageService.FindPackageByIdAndVersionStrict(splitQueryPart[0].Trim(), splitQueryPart[1].Trim());
                    if (resultingPackage != null)
                    {
                        packages.Add(resultingPackage);
                    }
                }
            }

            // Filter out duplicate packages and create the view model.
            var uniquePackagesKeys = new HashSet<int>();
            var results = new List<DeleteSearchResult>();
            foreach (var package in packages)
            {
                if (!uniquePackagesKeys.Add(package.Key))
                {
                    continue;
                }

                results.Add(CreateDeleteSearchResult(package));
            }
            
            return Json(results, JsonRequestBehavior.AllowGet);
        }

        private DeleteSearchResult CreateDeleteSearchResult(Package package)
        {
            return new DeleteSearchResult
            {
                PackageId = package.PackageRegistration.Id,
                PackageVersionNormalized = !string.IsNullOrEmpty(package.NormalizedVersion)
                    ? package.NormalizedVersion 
                    : NuGetVersion.Parse(package.Version).ToNormalizedString(),
                DownloadCount = package.DownloadCount,
                Created = package.Created.ToNuGetShortDateString(),
                Listed = package.Listed,
                PackageStatus = package.PackageStatusKey.ToString(),
                Owners = package
                    .PackageRegistration
                    .Owners
                    .Select(u => u.Username)
                    .OrderBy(u => u)
                    .Select(username => new UserViewModel
                    {
                        Username = username,
                        ProfileUrl = Url.User(username, area: string.Empty),
                    })
                    .ToList()
            };
        }
    }
}