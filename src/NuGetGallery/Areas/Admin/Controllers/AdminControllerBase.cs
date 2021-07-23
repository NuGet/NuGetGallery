// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Filters;

namespace NuGetGallery.Areas.Admin.Controllers
{
    [UIAuthorize(Roles="Admins")]
    public class AdminControllerBase : AppController
    {
        internal List<Package> SearchForPackages(IPackageService packageService, string query)
        {
            // Search supports several options:
            //   1) Full package id (e.g. jQuery)
            //   2) Full package id + version (e.g. jQuery 1.9.0, jQuery/1.9.0)
            //   3) Any of the above separated by comma
            // We are not using Lucene index here as we want to have the database values.

            var queryParts = query.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var packages = new List<Package>();
            var completedQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var queryPart in queryParts)
            {
                var splitQuery = queryPart.Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (spitQuery.Length == 1)
                {
                    // Don't make the same query twice.
                    var id = spitQuery[0].Trim();
                    if (!completedQueries.Add(id))
                    {
                        continue;
                    }

                    var resultingRegistration = packageService.FindPackageRegistrationById(id);
                    if (resultingRegistration != null)
                    {
                        packages.AddRange(resultingRegistration
                            .Packages
                            .OrderBy(p => NuGetVersion.Parse(p.NormalizedVersion)));
                    }
                }
                else if (spitQuery.Length == 2)
                {
                    // Don't make the same query twice.
                    var id = spitQuery[0].Trim();
                    var version = spitQuery[1].Trim();
                    if (!completedQueries.Add(id + "/" + version))
                    {
                        continue;
                    }

                    var resultingPackage = packageService.FindPackageByIdAndVersionStrict(id, version);
                    if (resultingPackage != null)
                    {
                        packages.Add(resultingPackage);
                    }
                }
            }

            // Ensure only unique package instances are returned.
            var uniquePackagesKeys = new HashSet<int>();
            var uniquePackages = new List<Package>();
            foreach (var package in packages)
            {
                if (!uniquePackagesKeys.Add(package.Key))
                {
                    continue;
                }

                uniquePackages.Add(package);
            }

            return uniquePackages;
        }

        internal PackageSearchResult CreatePackageSearchResult(Package package)
        {
            return new PackageSearchResult
            {
                PackageId = package.Id,
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
                    .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
                    .Select(username => new UserViewModel
                    {
                        Username = username,
                        ProfileUrl = Url.User(username),
                    })
                    .ToList()
            };
        }
    }
}
