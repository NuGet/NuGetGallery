// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DisplayPackageViewModelFactory
    {
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelHelper;

        public DisplayPackageViewModelFactory()
        {
            _listPackageItemViewModelHelper = new ListPackageItemViewModelFactory();
        }

        public DisplayPackageViewModel Create(
            Package package,
            User currentUser,
            PackageDeprecation deprecation,
            string readmeHtml)
        {
            var viewModel = new DisplayPackageViewModel();
            return Setup(
                viewModel,
                package,
                currentUser,
                deprecation,
                readmeHtml);
        }

        public DisplayPackageViewModel Setup(
            DisplayPackageViewModel viewModel,
            Package package,
            User currentUser,
            PackageDeprecation deprecation,
            string readMeHtml)
        {
            _listPackageItemViewModelHelper.Setup(viewModel, package, currentUser);
            SetupCommon(viewModel, package, pushedBy: null);
            return SetupInternal(viewModel, package, currentUser, deprecation, readMeHtml);
        }

        private DisplayPackageViewModel SetupInternal(
            DisplayPackageViewModel viewModel,
            Package package,
            User currentUser,
            PackageDeprecation deprecation,
            string readMeHtml)
        {
            viewModel.HasSemVer2Version = viewModel.NuGetVersion.IsSemVer2;
            viewModel.HasSemVer2Dependency = package.Dependencies.ToList()
                .Where(pd => !string.IsNullOrEmpty(pd.VersionSpec))
                .Select(pd => VersionRange.Parse(pd.VersionSpec))
                .Any(p => (p.HasUpperBound && p.MaxVersion.IsSemVer2) || (p.HasLowerBound && p.MinVersion.IsSemVer2));

            viewModel.Dependencies = new DependencySetsViewModel(package.Dependencies);

            var packageHistory = package
                .PackageRegistration
                .Packages
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();
            var pushedByCache = new Dictionary<User, string>();
            viewModel.PackageVersions = packageHistory
                .Select(
                    p => 
                    {
                        var vm = new DisplayPackageViewModel();
                        _listPackageItemViewModelHelper.Setup(vm, p, currentUser);
                        return SetupCommon(vm, p, GetPushedBy(p, currentUser, pushedByCache));
                    })
                .ToList();

            viewModel.PushedBy = GetPushedBy(package, currentUser, pushedByCache);
            viewModel.PackageFileSize = package.PackageFileSize;

            viewModel.LatestSymbolsPackage = package.LatestSymbolPackage();
            viewModel.LatestAvailableSymbolsPackage = viewModel.LatestSymbolsPackage != null && viewModel.LatestSymbolsPackage.StatusKey == PackageStatus.Available
                ? viewModel.LatestSymbolsPackage
                : package.LatestAvailableSymbolPackage();

            if (packageHistory.Any())
            {
                // calculate the number of days since the package registration was created
                // round to the nearest integer, with a min value of 1
                // divide the total download count by this number
                viewModel.TotalDaysSinceCreated = Convert.ToInt32(Math.Max(1, Math.Round((DateTime.UtcNow - packageHistory.Min(p => p.Created)).TotalDays)));
                viewModel.DownloadsPerDay = viewModel.TotalDownloadCount / viewModel.TotalDaysSinceCreated; // for the package
                viewModel.DownloadsPerDayLabel = viewModel.DownloadsPerDay < 1 ? "<1" : viewModel.DownloadsPerDay.ToNuGetNumberString();
                viewModel.IsDotnetToolPackageType = package.PackageTypes.Any(e => e.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
            }

            if (deprecation != null)
            {
                viewModel.AlternatePackageId = deprecation.AlternatePackageRegistration?.Id;

                var alternatePackage = deprecation.AlternatePackage;
                if (alternatePackage != null)
                {
                    // A deprecation should not have both an alternate package registration and an alternate package.
                    // In case a deprecation does have both, we will hide the alternate package registration's ID in this model.
                    viewModel.AlternatePackageId = alternatePackage?.Id;
                    viewModel.AlternatePackageVersion = alternatePackage?.Version;
                }

                viewModel.CustomMessage = deprecation.CustomMessage;
            }

            viewModel.ReadMeHtml = readMeHtml;

            return viewModel;
        }

        private DisplayPackageViewModel SetupCommon(
            DisplayPackageViewModel viewModel,
            Package package,
            string pushedBy)
        {
            viewModel.NuGetVersion = NuGetVersion.Parse(NuGetVersionFormatter.ToFullString(package.Version));
            viewModel.Copyright = package.Copyright;

            viewModel.DownloadCount = package.DownloadCount;
            viewModel.LastEdited = package.LastEdited;

            viewModel.TotalDaysSinceCreated = 0;
            viewModel.DownloadsPerDay = 0;

            viewModel.PushedBy = pushedBy;

            viewModel.InitializeRepositoryMetadata(package.RepositoryUrl, package.RepositoryType);

            if (PackageHelper.TryPrepareUrlForRendering(package.ProjectUrl, out string projectUrl))
            {
                viewModel.ProjectUrl = projectUrl;
            }

            viewModel.EmbeddedLicenseType = package.EmbeddedLicenseType;
            viewModel.LicenseExpression = package.LicenseExpression;

            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                viewModel.LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    viewModel.LicenseNames = licenseNames.Split(',').Select(l => l.Trim()).ToList();
                }
            }

            viewModel.DeprecationStatus = package.Deprecations.SingleOrDefault()?.Status
                ?? PackageDeprecationStatus.NotDeprecated;

            return viewModel;
        }

        private static string GetPushedBy(Package package, User currentUser, Dictionary<User, string> pushedByCache)
        {
            var userPushedBy = package.User;

            if (userPushedBy == null)
            {
                return null;
            }

            if (!pushedByCache.ContainsKey(userPushedBy))
            {
                // Only show who pushed the package version to users that can see private package metadata
                if (ActionsRequiringPermissions.DisplayPrivatePackageMetadata.CheckPermissionsOnBehalfOfAnyAccount(currentUser, package) == PermissionsCheckResult.Allowed)
                {
                    var organizationsThatUserPushedByBelongsTo =
                        (package.PackageRegistration?.Owners ?? Enumerable.Empty<User>())
                            .OfType<Organization>()
                            .Where(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(userPushedBy, organization) == PermissionsCheckResult.Allowed);
                    if (organizationsThatUserPushedByBelongsTo.Any())
                    {
                        // If the user is a member of any organizations that are package owners, only show the user if the current user is a member of the same organization
                        pushedByCache[userPushedBy] =
                            organizationsThatUserPushedByBelongsTo.Any(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, organization) == PermissionsCheckResult.Allowed) ?
                                userPushedBy?.Username :
                                organizationsThatUserPushedByBelongsTo.First().Username;
                    }
                    else
                    {
                        // Otherwise, show the user
                        pushedByCache[userPushedBy] = userPushedBy?.Username;
                    }
                }
                else
                {
                    pushedByCache[userPushedBy] = null;
                }
            }

            return pushedByCache[userPushedBy];
        }
    }
}