// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;

namespace NuGetGallery
{
    public class DisplayPackageViewModel : ListPackageItemViewModel
    {
        public DisplayPackageViewModel(Package package, User currentUser)
            : this(package, currentUser, (string)null)
        {
            HasSemVer2Version = NuGetVersion.IsSemVer2;
            HasSemVer2Dependency = package.Dependencies.ToList()
                .Where(pd => !string.IsNullOrEmpty(pd.VersionSpec))
                .Select(pd => VersionRange.Parse(pd.VersionSpec))
                .Any(p => (p.HasUpperBound && p.MaxVersion.IsSemVer2) || (p.HasLowerBound && p.MinVersion.IsSemVer2));

            Dependencies = new DependencySetsViewModel(package.Dependencies);

            var packageHistory = package
                .PackageRegistration
                .Packages
                .OrderByDescending(p => new NuGetVersion(p.Version))
                .ToList();
            PackageVersions = packageHistory.Select(p => new DisplayPackageViewModel(p, currentUser, GetPushedBy(p, currentUser))).ToList();

            PushedBy = GetPushedBy(package, currentUser);
            PackageFileSize = package.PackageFileSize;

            LatestSymbolsPackage = package.LatestSymbolPackage();

            if (packageHistory.Any())
            {
                // calculate the number of days since the package registration was created
                // round to the nearest integer, with a min value of 1
                // divide the total download count by this number
                TotalDaysSinceCreated = Convert.ToInt32(Math.Max(1, Math.Round((DateTime.UtcNow - packageHistory.Min(p => p.Created)).TotalDays)));
                DownloadsPerDay = TotalDownloadCount / TotalDaysSinceCreated; // for the package
                DownloadsPerDayLabel = DownloadsPerDay < 1 ? "<1" : DownloadsPerDay.ToNuGetNumberString();
                IsDotnetToolPackageType = package.PackageTypes.Any(e => e.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
            }
        }

        private DisplayPackageViewModel(Package package, User currentUser, string pushedBy)
            : base(package, currentUser)
        {
            Copyright = package.Copyright;

            DownloadCount = package.DownloadCount;
            LastEdited = package.LastEdited;

            TotalDaysSinceCreated = 0;
            DownloadsPerDay = 0;

            PushedBy = pushedBy;

            InitializeRepositoryMetadata(package.RepositoryUrl, package.RepositoryType);

            if (PackageHelper.TryPrepareUrlForRendering(package.ProjectUrl, out string projectUrl))
            {
                ProjectUrl = projectUrl;
            }

            EmbeddedLicenseType = package.EmbeddedLicenseType;
            LicenseExpression = package.LicenseExpression;

            if (PackageHelper.TryPrepareUrlForRendering(package.LicenseUrl, out string licenseUrl))
            {
                LicenseUrl = licenseUrl;

                var licenseNames = package.LicenseNames;
                if (!string.IsNullOrEmpty(licenseNames))
                {
                    LicenseNames = licenseNames.Split(',').Select(l => l.Trim());
                }
            }
        }

        public bool ValidatingTooLong { get; set; }
        public IReadOnlyList<ValidationIssue> PackageValidationIssues { get; set; }
        public IReadOnlyList<ValidationIssue> SymbolsPackageValidationIssues { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IReadOnlyList<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }
        public string ReadMeHtml { get; set; }
        public DateTime? LastEdited { get; set; }
        public int DownloadsPerDay { get; private set; }
        public int TotalDaysSinceCreated { get; private set; }
        public long PackageFileSize { get; private set; }
        public SymbolPackage LatestSymbolsPackage { get; private set; }

        public bool HasSemVer2Version { get; }
        public bool HasSemVer2Dependency { get; }
        public bool IsDotnetToolPackageType { get; set; }

        public bool HasNewerPrerelease
        {
            get
            {
                var latestPrereleaseVersion = PackageVersions
                    .Where(pv => pv.Prerelease && pv.Available && pv.Listed)
                    .Max(pv => pv.NuGetVersion);

                return latestPrereleaseVersion > NuGetVersion;
            }
        }

        public bool HasNewerRelease
        {
            get
            {
                var latestReleaseVersion = PackageVersions
                    .Where(pv => !pv.Prerelease && pv.Available && pv.Listed)
                    .Max(pv => pv.NuGetVersion);

                return latestReleaseVersion > NuGetVersion;
            }
        }

        public bool? IsIndexed { get; set; }

        public string DownloadsPerDayLabel { get; private set; }

        public string PushedBy { get; private set; }

        public bool IsCertificatesUIEnabled { get; set; }
        public string RepositoryUrl { get; private set; }
        public RepositoryKind RepositoryType { get; private set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public IEnumerable<string> LicenseNames { get; set; }
        public string LicenseExpression { get; set; }
        public IReadOnlyCollection<CompositeLicenseExpressionSegment> LicenseExpressionSegments { get; set; }
        public EmbeddedLicenseFileType EmbeddedLicenseType { get; set; }

        private IDictionary<User, string> _pushedByCache = new Dictionary<User, string>();

        private string GetPushedBy(Package package, User currentUser)
        {
            var userPushedBy = package.User;

            if (userPushedBy == null)
            {
                return null;
            }

            if (!_pushedByCache.ContainsKey(userPushedBy))
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
                        _pushedByCache[userPushedBy] =
                            organizationsThatUserPushedByBelongsTo.Any(organization => ActionsRequiringPermissions.ViewAccount.CheckPermissions(currentUser, organization) == PermissionsCheckResult.Allowed) ?
                                userPushedBy?.Username :
                                organizationsThatUserPushedByBelongsTo.First().Username;
                    }
                    else
                    {
                        // Otherwise, show the user
                        _pushedByCache[userPushedBy] = userPushedBy?.Username;
                    }
                }
                else
                {
                    _pushedByCache[userPushedBy] = null;
                }
            }

            return _pushedByCache[userPushedBy];
        }

        private void InitializeRepositoryMetadata(string repositoryUrl, string repositoryType)
        {
            RepositoryType = RepositoryKind.Unknown;

            if (Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var repoUri))
            {
                if (repoUri.IsHttpsProtocol())
                {
                    RepositoryUrl = repositoryUrl;
                }

                if (repoUri.IsGitHubUri())
                {
                    RepositoryType = RepositoryKind.GitHub;

                    // Fix-up git:// to https:// for GitHub URLs (we should add this fix-up to other repos in the future)
                    if (repoUri.IsGitProtocol())
                    {
                        RepositoryUrl = repoUri.ToHttps().ToString();
                    }
                }
                else if (PackageHelper.IsGitRepositoryType(repositoryType))
                {
                    RepositoryType = RepositoryKind.Git;
                }
            }
        }

        public enum RepositoryKind
        {
            Unknown,
            Git,
            GitHub,
        }
    }
}