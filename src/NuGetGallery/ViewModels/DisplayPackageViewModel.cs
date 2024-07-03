// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Services.Entities;
using NuGet.Services.Licenses;
using NuGet.Services.Validation.Issues;
using NuGet.Versioning;
using NuGetGallery.Frameworks;

namespace NuGetGallery
{
    public class DisplayPackageViewModel : ListPackageItemViewModel
    {
        public NuGetVersion NuGetVersion { get; set; }
        public bool ValidatingTooLong { get; set; }
        public IReadOnlyCollection<ValidationIssue> PackageValidationIssues { get; set; }
        public IReadOnlyCollection<ValidationIssue> SymbolsPackageValidationIssues { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IReadOnlyCollection<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }
        public string ReadMeHtml { get; set; }
        public bool ReadMeImagesRewritten { get; set; }
        public bool ReadmeImageSourceDisallowed { get; set; }
        public DateTime? LastEdited { get; set; }
        public long DownloadsPerDay { get; set; }
        public int TotalDaysSinceCreated { get; set; }
        public long PackageFileSize { get; set; }
        public SymbolPackage LatestSymbolsPackage { get; set; }
        public SymbolPackage LatestAvailableSymbolsPackage { get; set; }

        public bool IsDotnetToolPackageType { get; set; }
        public bool IsDotnetNewTemplatePackageType { get; set; }
        public bool IsMSBuildSdkPackageType { get; set; }
        public bool IsAtomFeedEnabled { get; set; }
        public bool IsPackageDeprecationEnabled { get; set; }
        public bool IsPackageVulnerabilitiesEnabled { get; set; }
        public bool IsFuGetLinksEnabled { get; set; }
        public bool IsDNDocsLinksEnabled { get; set; }
        public bool IsNuGetTrendsLinksEnabled { get; set; }
        public bool IsNuGetPackageExplorerLinkEnabled { get; set; }
        public bool IsPackageRenamesEnabled { get; set; }
        public bool IsGitHubUsageEnabled { get; set; }
        public bool IsPackageDependentsEnabled { get; set; }
        public bool IsRecentPackagesNoIndexEnabled { get; set; }
        public bool IsMarkdigMdSyntaxHighlightEnabled { get; set; }
        public bool CanDisplayReadmeWarning { get; set; }
        public NuGetPackageGitHubInformation GitHubDependenciesInformation { get; set; }
        public bool HasEmbeddedIcon { get; set; }
        public bool HasEmbeddedReadmeFile { get; set; }
        public PackageDependents PackageDependents { get; set; }

        public const int NumberOfDaysToBlockIndexing = 90;

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

        public string DownloadsPerDayLabel { get; set; }

        public string PushedBy { get; set; }

        public bool IsCertificatesUIEnabled { get; set; }
        public string RepositoryUrl { get; private set; }
        public RepositoryKind RepositoryType { get; private set; }
        public string ProjectUrl { get; set; }
        public string LicenseUrl { get; set; }
        public string FuGetUrl { get; set; }
        public string DNDocsUrl { get; internal set; }
        public string NuGetTrendsUrl { get; set; }
        public string NuGetPackageExplorerUrl { get; set; }
        public IReadOnlyCollection<string> LicenseNames { get; set; }
        public string LicenseExpression { get; set; }
        public IReadOnlyCollection<CompositeLicenseExpressionSegment> LicenseExpressionSegments { get; set; }
        public EmbeddedLicenseFileType EmbeddedLicenseType { get; set; }

        public PackageDeprecationStatus DeprecationStatus { get; set; }
        public IReadOnlyCollection<PackageVulnerability> Vulnerabilities { get; set; }
        public PackageVulnerabilitySeverity MaxVulnerabilitySeverity { get; set; }
        public string PackageWarningIconTitle { get; set; }
        public string AlternatePackageId { get; set; }
        public string AlternatePackageVersion { get; set; }
        public string CustomMessage { get; set; }

        public IReadOnlyCollection<PackageRename> PackageRenames { get; set; }
        public string RenamedMessage { get; set; }
        public bool IsDisplayTargetFrameworkEnabled { get; set; }
        public bool IsComputeTargetFrameworkEnabled { get; set; }
        public PackageFrameworkCompatibility PackageFrameworkCompatibility { get; set; }

        public void InitializeRepositoryMetadata(string repositoryUrl, string repositoryType)
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

        public bool CanDisplayNuGetPackageExplorerLink()
        {
            return IsNuGetPackageExplorerLinkEnabled && !string.IsNullOrEmpty(NuGetPackageExplorerUrl) && Available;
        }

        public bool CanDisplayFuGetLink()
        {
            return IsFuGetLinksEnabled && !string.IsNullOrEmpty(FuGetUrl) && Available;
        }

        public bool CanDisplayDNDocsLink()
        {
            return IsDNDocsLinksEnabled && !string.IsNullOrEmpty(DNDocsUrl) && Available;
        }

        public bool CanDisplayNuGetTrendsLink()
        {
            return IsNuGetTrendsLinksEnabled && !string.IsNullOrEmpty(NuGetTrendsUrl) && Available;
        }

        public bool CanDisplayTargetFrameworks()
        {
            return IsDisplayTargetFrameworkEnabled && !Deleted && !IsDotnetNewTemplatePackageType;
        }

        public bool BlockSearchEngineIndexing
        {
            get
            {
                return !Listed || !Available || (IsRecentPackagesNoIndexEnabled && TotalDaysSinceCreated < NumberOfDaysToBlockIndexing);
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
