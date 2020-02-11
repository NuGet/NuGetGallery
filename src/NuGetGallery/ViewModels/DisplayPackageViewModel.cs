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
        public NuGetVersion NuGetVersion { get; set; }
        public bool ValidatingTooLong { get; set; }
        public IReadOnlyCollection<ValidationIssue> PackageValidationIssues { get; set; }
        public IReadOnlyCollection<ValidationIssue> SymbolsPackageValidationIssues { get; set; }
        public DependencySetsViewModel Dependencies { get; set; }
        public IReadOnlyCollection<DisplayPackageViewModel> PackageVersions { get; set; }
        public string Copyright { get; set; }
        public string ReadMeHtml { get; set; }
        public bool ReadMeImagesRewritten { get; set; }
        public DateTime? LastEdited { get; set; }
        public int DownloadsPerDay { get; set; }
        public int TotalDaysSinceCreated { get; set; }
        public long PackageFileSize { get; set; }
        public SymbolPackage LatestSymbolsPackage { get; set; }
        public SymbolPackage LatestAvailableSymbolsPackage { get; set; }

        public bool IsDotnetToolPackageType { get; set; }
        public bool IsDotnetNewTemplatePackageType { get; set; }
        public bool IsAtomFeedEnabled { get; set; }
        public bool IsPackageDeprecationEnabled { get; set; }
        public bool IsGitHubUsageEnabled { get; set; }
        public NuGetPackageGitHubInformation GitHubDependenciesInformation { get; set; }
        public bool HasEmbeddedIcon { get; set; }

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
        public IReadOnlyCollection<string> LicenseNames { get; set; }
        public string LicenseExpression { get; set; }
        public IReadOnlyCollection<CompositeLicenseExpressionSegment> LicenseExpressionSegments { get; set; }
        public EmbeddedLicenseFileType EmbeddedLicenseType { get; set; }

        public PackageDeprecationStatus DeprecationStatus { get; set; }
        public string AlternatePackageId { get; set; }
        public string AlternatePackageVersion { get; set; }
        public string CustomMessage { get; set; }

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

        public enum RepositoryKind
        {
            Unknown,
            Git,
            GitHub,
        }
    }
}