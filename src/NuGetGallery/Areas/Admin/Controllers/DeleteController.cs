// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using NuGet;
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
            ReportPackageReason.PublishedWithWrongVersion,
            ReportPackageReason.ReleasedInPublicByAccident,
            ReportPackageReason.ContainsMaliciousCode,
            ReportPackageReason.Other
        };

        public virtual ActionResult Search(string query)
        {
            // Search suports several options:
            //   1) Full package id (e.g. jQuery)
            //   2) Full package id + version (e.g. jQuery 1.9.0)
            //   3) Any of the above separated by comma
            // We are not using Lucene index here as we want to have the database values.

            var queryParts = query.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var results = new List<DeleteSearchResult>();
            foreach (var queryPart in queryParts)
            {
                var splitQueryPart = queryPart.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                if (splitQueryPart.Length == 1)
                {
                    var resultingRegistration = _packageService.FindPackageRegistrationById(splitQueryPart[0].Trim());
                    if (resultingRegistration != null)
                    {
                        results.AddRange(resultingRegistration.Packages.Select(CreateDeleteSearchResult));
                    }
                }
                else if (splitQueryPart.Length == 2)
                {
                    var resultingPackage = _packageService.FindPackageByIdAndVersion(splitQueryPart[0].Trim(), splitQueryPart[1].Trim(), true);
                    if (resultingPackage != null)
                    {
                        results.Add(CreateDeleteSearchResult(resultingPackage));
                    }
                }
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
                    : SemanticVersion.Parse(package.Version).ToNormalizedString(),
                DownloadCount = package.DownloadCount,
                Listed = package.Listed,
                Deleted = package.Deleted
            };
        }
    }
}