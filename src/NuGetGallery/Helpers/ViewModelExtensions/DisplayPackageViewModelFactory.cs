// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery.Frameworks;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public class DisplayPackageViewModelFactory
    {
        private readonly ListPackageItemViewModelFactory _listPackageItemViewModelFactory;

        public DisplayPackageViewModelFactory(IIconUrlProvider iconUrlProvider, IPackageFrameworkCompatibilityFactory frameworkCompatibilityFactory, IFeatureFlagService featureFlagService)
        {
            _listPackageItemViewModelFactory = new ListPackageItemViewModelFactory(iconUrlProvider, frameworkCompatibilityFactory, featureFlagService);
        }

        public DisplayPackageViewModel Create(
            Package package,
            IReadOnlyCollection<Package> allVersions,
            User currentUser,
            IReadOnlyDictionary<int, PackageDeprecation> packageKeyToDeprecation,
            IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> packageKeyToVulnerabilities,
            IReadOnlyList<PackageRename> packageRenames,
            RenderedMarkdownResult readmeResult)
        {
            var viewModel = new DisplayPackageViewModel();
            return Setup(
                viewModel,
                package,
                allVersions,
                currentUser,
                packageKeyToDeprecation,
                packageKeyToVulnerabilities,
                packageRenames,
                readmeResult);
        }

        public DisplayPackageViewModel Setup(
            DisplayPackageViewModel viewModel,
            Package package,
            IReadOnlyCollection<Package> allVersions,
            User currentUser,
            IReadOnlyDictionary<int, PackageDeprecation> packageKeyToDeprecation,
            IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> packageKeyToVulnerabilities,
            IReadOnlyList<PackageRename> packageRenames,
            RenderedMarkdownResult readmeResult)
        {
            _listPackageItemViewModelFactory.Setup(viewModel, package, currentUser);
            SetupCommon(viewModel, package, pushedBy: null,
                packageKeyToDeprecation: packageKeyToDeprecation, packageKeyToVulnerabilities: packageKeyToVulnerabilities);
            return SetupInternal(viewModel, package, allVersions, currentUser,
                packageKeyToDeprecation, packageKeyToVulnerabilities, packageRenames, readmeResult);
        }

        private DisplayPackageViewModel SetupInternal(
            DisplayPackageViewModel viewModel,
            Package package,
            IReadOnlyCollection<Package> allVersions,
            User currentUser,
            IReadOnlyDictionary<int, PackageDeprecation> packageKeyToDeprecation,
            IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> packageKeyToVulnerabilities,
            IReadOnlyList<PackageRename> packageRenames,
            RenderedMarkdownResult readmeResult)
        {
            var dependencies = package.Dependencies.ToList();

            viewModel.Dependencies = new DependencySetsViewModel(dependencies);

            var packageHistory = allVersions
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();
            var pushedByCache = new Dictionary<User, string>();
            viewModel.PackageVersions = packageHistory
                .Select(
                    p =>
                    {
                        var vm = new DisplayPackageViewModel();
                        _listPackageItemViewModelFactory.Setup(vm, p, currentUser);
                        return SetupCommon(vm, p, GetPushedBy(p, currentUser, pushedByCache), packageKeyToDeprecation, packageKeyToVulnerabilities);
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

                // Lazily load the package types from the database.
                viewModel.IsDotnetToolPackageType = package.PackageTypes.Any(e => e.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
                viewModel.IsDotnetNewTemplatePackageType = package.PackageTypes.Any(e => e.Name.Equals("Template", StringComparison.OrdinalIgnoreCase));
                viewModel.IsMSBuildSdkPackageType = package.PackageTypes.Any(e => e.Name.Equals("MSBuildSdk", StringComparison.OrdinalIgnoreCase));
            }

            if (packageKeyToDeprecation != null && packageKeyToDeprecation.TryGetValue(package.Key, out var deprecation))
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

            if (packageRenames != null && packageRenames.Count != 0)
            {
                viewModel.PackageRenames = packageRenames;
                if (package.PackageRegistration?.RenamedMessage != null)
                {
                    viewModel.RenamedMessage = package.PackageRegistration.RenamedMessage;
                }
            }

            viewModel.ReadMeHtml = readmeResult?.Content;
            viewModel.ReadMeImagesRewritten = readmeResult != null ? readmeResult.ImagesRewritten : false;
            viewModel.ReadmeImageSourceDisallowed = readmeResult != null ? readmeResult.ImageSourceDisallowed : false;
            viewModel.HasEmbeddedIcon = package.HasEmbeddedIcon;
            viewModel.HasEmbeddedReadmeFile = package.HasEmbeddedReadme;

            return viewModel;
        }

        private DisplayPackageViewModel SetupCommon(
            DisplayPackageViewModel viewModel,
            Package package,
            string pushedBy,
            IReadOnlyDictionary<int, PackageDeprecation> packageKeyToDeprecation,
            IReadOnlyDictionary<int, IReadOnlyList<PackageVulnerability>> packageKeyToVulnerabilities)
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

            var fugetUrl = $"https://www.fuget.org/packages/{package.Id}/{package.NormalizedVersion}";
            if (PackageHelper.TryPrepareUrlForRendering(fugetUrl, out string fugetReadyUrl))
            {
                viewModel.FuGetUrl = fugetReadyUrl;
            }

            var dndocsUrl = $"https://dndocs.com/i/nuget/{package.Id}/{package.NormalizedVersion}";
            if (PackageHelper.TryPrepareUrlForRendering(dndocsUrl, out string dndocsReadyUrl))
            {
                viewModel.DNDocsUrl = dndocsReadyUrl;
            }

            var nugetPackageExplorerUrl = $"https://nuget.info/packages/{package.Id}/{package.NormalizedVersion}";
            if (PackageHelper.TryPrepareUrlForRendering(nugetPackageExplorerUrl, out string nugetPackageExplorerReadyUrl))
            {
                viewModel.NuGetPackageExplorerUrl = nugetPackageExplorerReadyUrl;
            }

            var nugetTrendsUrl = $"https://nugettrends.com/packages?ids={package.Id}";
            if (PackageHelper.TryPrepareUrlForRendering(nugetTrendsUrl, out string nugetTrendsReadyUrl))
            {
                viewModel.NuGetTrendsUrl = nugetTrendsReadyUrl;
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

            PackageDeprecation deprecation = null;
            if (packageKeyToDeprecation != null && packageKeyToDeprecation.TryGetValue(package.Key, out deprecation))
            {
                viewModel.DeprecationStatus = deprecation.Status;
            }
            else
            {
                viewModel.DeprecationStatus = PackageDeprecationStatus.NotDeprecated;
            }

            PackageVulnerabilitySeverity? maxVulnerabilitySeverity = null;
            if (packageKeyToVulnerabilities != null
                && packageKeyToVulnerabilities.TryGetValue(package.Key, out var vulnerabilities)
                && vulnerabilities != null && vulnerabilities.Any())
            {
                viewModel.Vulnerabilities = vulnerabilities.OrderByDescending(vul => vul.Severity).ToList().AsReadOnly();
                maxVulnerabilitySeverity = viewModel.Vulnerabilities.Max(v => v.Severity); // cache for messaging
                viewModel.MaxVulnerabilitySeverity = maxVulnerabilitySeverity.Value;
            }
            else
            {
                viewModel.Vulnerabilities = null;
                viewModel.MaxVulnerabilitySeverity = default;
            }

            viewModel.PackageWarningIconTitle = WarningTitleHelper.GetWarningIconTitle(viewModel.Version, deprecation, maxVulnerabilitySeverity);

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
