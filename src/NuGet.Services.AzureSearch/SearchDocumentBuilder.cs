// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Protocol.Catalog;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Frameworks;

namespace NuGet.Services.AzureSearch
{
    public class SearchDocumentBuilder : ISearchDocumentBuilder
    {
        private readonly string[] ImpliedDependency = new string[] { "Dependency" };
        private readonly string[] FilterableImpliedDependency = new string[] { "dependency" };

        private readonly IBaseDocumentBuilder _baseDocumentBuilder;

        public SearchDocumentBuilder(IBaseDocumentBuilder baseDocumentBuilder)
        {
            _baseDocumentBuilder = baseDocumentBuilder ?? throw new ArgumentNullException(nameof(baseDocumentBuilder));
        }

        public SearchDocument.LatestFlags LatestFlagsOrNull(VersionLists versionLists, SearchFilters searchFilters)
        {
            var latest = versionLists.GetLatestVersionInfoOrNull(searchFilters);
            if (latest == null)
            {
                return null;
            }

            // The latest version, given the "include prerelease" bit of the search filter, may or may not be the
            // absolute latest version when considering both prerelease and stable versions. Consider the following
            // cases:
            //
            // Case #1:
            //   SearchFilters.Default:
            //     All versions: 1.0.0, 2.0.0-alpha
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = false
            //
            // Case #2:
            //   SearchFilters.Default:
            //     All versions: 1.0.0
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = true
            //
            // Case #3:
            //   SearchFilters.IncludePrerelease:
            //     All versions: 1.0.0, 2.0.0-alpha
            //     Latest version given filters: 2.0.0-alpha
            //     V2 search document flags:
            //       IsLatestStable = false
            //       IsLatest       = true
            //
            // Case #4:
            //   SearchFilters.IncludePrerelease:
            //     All versions: 1.0.0
            //     Latest version given filters: 1.0.0
            //     V2 search document flags:
            //       IsLatestStable = true
            //       IsLatest       = true
            //
            // In cases #1 and #2, we know the value of IsLatestStable will always be true. We cannot know whether
            // IsLatest is true or false without looking at the version list that includes prerelease versions. For
            // cases #3 and #4, we know IsLatest will always be true and we can determine IsLatestStable by looking
            // at whether the latest version is prerelease or not.
            bool isLatestStable;
            bool isLatest;
            if ((searchFilters & SearchFilters.IncludePrerelease) == 0)
            {
                // This is the case where prerelease versions are excluded.
                var latestIncludePrerelease = versionLists
                    .GetLatestVersionInfoOrNull(searchFilters | SearchFilters.IncludePrerelease);
                Guard.Assert(
                    latestIncludePrerelease != null,
                    "If a search filter excludes prerelease and has a latest version, then there is a latest version including prerelease.");
                isLatestStable = true;
                isLatest = latestIncludePrerelease.ParsedVersion == latest.ParsedVersion;
            }
            else
            {
                // This is the case where prerelease versions are included.
                isLatestStable = !latest.ParsedVersion.IsPrerelease;
                isLatest = true;
            }

            return new SearchDocument.LatestFlags(latest, isLatestStable, isLatest);
        }

        public KeyedDocument Keyed(
            string packageId,
            SearchFilters searchFilters)
        {
            var document = new KeyedDocument();

            PopulateKey(document, packageId, searchFilters);

            return document;
        }

        public SearchDocument.UpdateVersionList UpdateVersionListFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest)
        {
            var document = new SearchDocument.UpdateVersionList();

            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: lastCommitTimestamp,
                lastCommitId: lastCommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest);

            return document;
        }

        public SearchDocument.UpdateVersionListAndOwners UpdateVersionListAndOwnersFromCatalog(
            string packageId,
            SearchFilters searchFilters,
            DateTimeOffset lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string[] owners)
        {
            var document = new SearchDocument.UpdateVersionListAndOwners();

            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: lastCommitTimestamp,
                lastCommitId: lastCommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest);
            PopulateOwners(
                document,
                owners);

            return document;
        }

        public SearchDocument.UpdateLatest UpdateLatestFromCatalog(
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string normalizedVersion,
            string fullVersion,
            PackageDetailsCatalogLeaf leaf,
            string[] owners)
        {
            var document = new SearchDocument.UpdateLatest();

            // Determine if we have packageTypes to forward.
            // Otherwise, we need to let the system know that there were no explicit package types
            string[] packageTypes = leaf.PackageTypes != null && leaf.PackageTypes.Count > 0
                                                ? leaf.PackageTypes.Select(pt => pt.Name).ToArray()
                                                : null;

            string[] frameworks = GetFrameworksFromCatalogLeaf(leaf);
            string[] tfms = GetTfmsFromCatalogLeaf(leaf);

            string[] computedFrameworks = GetComputedFrameworksFromCatalogLeaf(leaf);
            string[] computedTfms = GetComputedTfmsFromCatalogLeaf(leaf);

            PopulateUpdateLatest(
                document,
                leaf.PackageId,
                searchFilters,
                lastUpdatedFromCatalog: true,
                lastCommitTimestamp: leaf.CommitTimestamp,
                lastCommitId: leaf.CommitId,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest,
                fullVersion: fullVersion,
                owners: owners,
                packageTypes: packageTypes,
                frameworks: frameworks,
                tfms: tfms,
                computedFrameworks: computedFrameworks,
                computedTfms: computedTfms);
            _baseDocumentBuilder.PopulateMetadata(document, normalizedVersion, leaf);
            PopulateDeprecationFromCatalog(document, leaf);
            PopulateVulnerabilitiesFromCatalog(document, leaf);

            return document;
        }

        public SearchDocument.Full FullFromDb(
            string packageId,
            SearchFilters searchFilters,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            Package package,
            string[] owners,
            long totalDownloadCount,
            bool isExcludedByDefault)
        {
            var document = new SearchDocument.Full();

            // Determine if we have packageTypes to forward.
            // Otherwise, we need to let the system know that there were no explicit package types
            string[] packageTypes = package.PackageTypes != null && package.PackageTypes.Count > 0
                                                ? package.PackageTypes.Select(pt => pt.Name).ToArray()
                                                : null;

            string[] frameworks = package.SupportedFrameworks == null
                                                ? Array.Empty<string>()
                                                : GetFrameworksFromPackage(package.SupportedFrameworks);
            string[] tfms = package.SupportedFrameworks == null
                                                ? Array.Empty<string>()
                                                : GetTfmsFromPackage(package.SupportedFrameworks);

            string[] computedFrameworks = package.SupportedFrameworks == null
                                                ? Array.Empty<string>()
                                                : GetComputedFrameworksFromPackage(package.SupportedFrameworks);
            string[] computedTfms = package.SupportedFrameworks == null
                                                ? Array.Empty<string>()
                                                : GetComputedTfmsFromPackage(package.SupportedFrameworks);

            PopulateUpdateLatest(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog: false,
                lastCommitTimestamp: null,
                lastCommitId: null,
                versions: versions,
                isLatestStable: isLatestStable,
                isLatest: isLatest,
                fullVersion: fullVersion,
                owners: owners,
                packageTypes: packageTypes,
                frameworks: frameworks,
                tfms: tfms,
                computedFrameworks: computedFrameworks,
                computedTfms: computedTfms);
            _baseDocumentBuilder.PopulateMetadata(document, packageId, package);
            PopulateDownloadCount(document, totalDownloadCount);
            PopulateIsExcludedByDefault(document, isExcludedByDefault);
            PopulateDeprecationFromDb(document, package);
            PopulateVulnerabilitiesFromDb(document, package);

            return document;
        }

        private void PopulateVersions<T>(
            T document,
            string packageId,
            SearchFilters searchFilters,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest) where T : KeyedDocument, SearchDocument.IVersions
        {
            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateCommitted(
                document,
                lastUpdatedFromCatalog,
                lastCommitTimestamp,
                lastCommitId);
            document.Versions = versions;
            document.IsLatestStable = isLatestStable;
            document.IsLatest = isLatest;
        }

        private static void PopulateKey(KeyedDocument document, string packageId, SearchFilters searchFilters)
        {
            document.Key = DocumentUtilities.GetSearchDocumentKey(packageId, searchFilters);
        }

        private void PopulateUpdateLatest(
            SearchDocument.UpdateLatest document,
            string packageId,
            SearchFilters searchFilters,
            bool lastUpdatedFromCatalog,
            DateTimeOffset? lastCommitTimestamp,
            string lastCommitId,
            string[] versions,
            bool isLatestStable,
            bool isLatest,
            string fullVersion,
            string[] owners,
            string[] packageTypes,
            string[] frameworks,
            string[] tfms,
            string[] computedFrameworks,
            string[] computedTfms)
        {
            PopulateVersions(
                document,
                packageId,
                searchFilters,
                lastUpdatedFromCatalog,
                lastCommitTimestamp,
                lastCommitId,
                versions,
                isLatestStable,
                isLatest);
            document.SearchFilters = DocumentUtilities.GetSearchFilterString(searchFilters);
            document.FullVersion = fullVersion;
            document.Frameworks = frameworks;
            document.Tfms = tfms;
            document.ComputedFrameworks = computedFrameworks;
            document.ComputedTfms = computedTfms;

            // If the package has explicit types, we will set them here.
            // Otherwise, we will treat the package as a "Depedency" type and fill in the explicit type.
            if (packageTypes != null && packageTypes.Length > 0)
            {
                document.PackageTypes = packageTypes;
                document.FilterablePackageTypes = packageTypes.Select(pt => pt.ToLowerInvariant()).ToArray();
            }
            else
            {
                document.PackageTypes = ImpliedDependency;
                document.FilterablePackageTypes = FilterableImpliedDependency;
            }

            PopulateOwners(
                document,
                owners);
        }

        private static void PopulateOwners<T>(
            T document,
            string[] owners) where T : KeyedDocument, SearchDocument.IOwners
        {
            document.Owners = owners;
        }

        public SearchDocument.UpdateOwners UpdateOwners(
            string packageId,
            SearchFilters searchFilters,
            string[] owners)
        {
            var document = new SearchDocument.UpdateOwners();

            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateUpdated(
                document,
                lastUpdatedFromCatalog: false);
            PopulateOwners(document, owners);

            return document;
        }

        public SearchDocument.UpdateDownloadCount UpdateDownloadCount(
            string packageId,
            SearchFilters searchFilters,
            long totalDownloadCount)
        {
            var document = new SearchDocument.UpdateDownloadCount();

            PopulateKey(document, packageId, searchFilters);
            _baseDocumentBuilder.PopulateUpdated(
                document,
                lastUpdatedFromCatalog: false);
            PopulateDownloadCount(document, totalDownloadCount);

            return document;
        }

        private static void PopulateDownloadCount<T>(
            T document,
            long totalDownloadCount) where T : KeyedDocument, SearchDocument.IDownloadCount
        {
            document.TotalDownloadCount = totalDownloadCount;
            document.DownloadScore = DocumentUtilities.GetDownloadScore(totalDownloadCount);
        }

        private static void PopulateIsExcludedByDefault<T>(
            T document,
            bool isExcludedByDefault) where T : KeyedDocument, SearchDocument.IIsExcludedByDefault
        {
            document.IsExcludedByDefault = isExcludedByDefault;
        }

        private static string[] GetFrameworksFromPackage(ICollection<PackageFramework> supportedFrameworks)
        {
            NuGetFramework[] tfms = supportedFrameworks
                                            .Select(f => f.FrameworkName)
                                            .Where(f => f.IsSpecificFramework && !f.IsPCL)
                                            .ToArray();

            return ParseFrameworkGenerations(tfms);
        }

        private static string[] GetTfmsFromPackage(ICollection<PackageFramework> supportedFrameworks)
        {
            return supportedFrameworks
                            .Select(f => f.FrameworkName)
                            .Where(f => f.IsSpecificFramework && !f.IsPCL)
                            .Select(f => NormalizePlatformVersion(f))
                            .Select(f => f.GetShortFolderName())
                            .ToArray();
        }

        private static string[] GetComputedFrameworksFromPackage(IEnumerable<PackageFramework> supportedFrameworks)
        {
            IEnumerable<NuGetFramework> assetTfms = supportedFrameworks
                                                            .Select(f => f.FrameworkName);

            NuGetFramework[] computedTfms = assetTfms
                                                .Union(FrameworkCompatibilityService.GetCompatibleFrameworks(assetTfms)) // add computed TFMs
                                                .Where(f => f.IsSpecificFramework && !f.IsPCL)
                                                .ToArray();

            return ParseFrameworkGenerations(computedTfms);
        }

        private static string[] GetComputedTfmsFromPackage(IEnumerable<PackageFramework> supportedFrameworks)
        {
            IEnumerable<NuGetFramework> assetTfms = supportedFrameworks
                                                        .Select(f => f.FrameworkName)
                                                        .Select(f => NormalizePlatformVersion(f));

            return assetTfms
                        .Union(FrameworkCompatibilityService.GetCompatibleFrameworks(assetTfms)) // add computed TFMs
                        .Where(f => f.IsSpecificFramework && !f.IsPCL)
                        .Select(f => f.GetShortFolderName())
                        .ToArray();
        }

        private static string[] GetFrameworksFromCatalogLeaf(PackageDetailsCatalogLeaf leaf)
        {
            NuGetFramework[] tfms = GetSupportedFrameworks(leaf)
                                                        .ToArray();

            return ParseFrameworkGenerations(tfms);
        }

        private static string[] GetTfmsFromCatalogLeaf(PackageDetailsCatalogLeaf leaf)
        {
            return GetSupportedFrameworks(leaf)
                            .Select(f => NormalizePlatformVersion(f))
                            .Select(f => f.GetShortFolderName())
                            .Where(f => f != null)
                            .ToArray();
        }

        private static string[] GetComputedFrameworksFromCatalogLeaf(PackageDetailsCatalogLeaf leaf)
        {
            IEnumerable<NuGetFramework> assetTfms = GetSupportedFrameworks(leaf);

            NuGetFramework[] computedTfms = assetTfms
                                                .Union(FrameworkCompatibilityService.GetCompatibleFrameworks(assetTfms)) // add computed TFMs
                                                .Where(f => f.IsSpecificFramework && !f.IsPCL)
                                                .ToArray();

            return ParseFrameworkGenerations(computedTfms);
        }

        private static string[] GetComputedTfmsFromCatalogLeaf(PackageDetailsCatalogLeaf leaf)
        {
            IEnumerable<NuGetFramework> assetTfms = GetSupportedFrameworks(leaf)
                                                            .Select(f => NormalizePlatformVersion(f));

            return assetTfms
                        .Union(FrameworkCompatibilityService.GetCompatibleFrameworks(assetTfms)) // add computed TFMs
                        .Where(f => f.IsSpecificFramework && !f.IsPCL)
                        .Select(f => f.GetShortFolderName())
                        .Where(f => f != null)
                        .ToArray();
        }

        private static string[] ParseFrameworkGenerations(ICollection<NuGetFramework> tfms)
        {
            var frameworks = new HashSet<string>();
            foreach (var framework in tfms)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.Net, framework.Framework))
                {
                    frameworks.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetFramework);
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, framework.Framework))
                {
                    frameworks.Add(framework.Version.Major >= 5
                        ? AssetFrameworkHelper.FrameworkGenerationIdentifiers.Net
                        : AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetCoreApp);
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetStandard, framework.Framework))
                {
                    frameworks.Add(AssetFrameworkHelper.FrameworkGenerationIdentifiers.NetStandard);
                }
            }

            return frameworks.ToArray();
        }

        private static IEnumerable<NuGetFramework> GetSupportedFrameworks(PackageDetailsCatalogLeaf leaf)
        {
            string[] files = leaf.PackageEntries == null || leaf.PackageEntries.Count == 0
                                                                    ? Array.Empty<string>()
                                                                    : leaf.PackageEntries.Select(pe => pe.FullName).ToArray();

            List<Packaging.Core.PackageType> packageTypes = leaf.PackageTypes == null || leaf.PackageTypes.Count == 0
                                                                    ? new List<Packaging.Core.PackageType>()
                                                                    : GetPackageTypes(leaf);

            return AssetFrameworkHelper.GetAssetFrameworks(leaf.PackageId, packageTypes, files)
                                                                    .Where(f => f.IsSpecificFramework && !f.IsPCL);
        }

        // Any TFM with a target platform version, like 'net6.0-android31.0', will be normalized to 'net6.0-android' in the search index to provide greater coverage with filters.
        private static NuGetFramework NormalizePlatformVersion(NuGetFramework framework)
        {
            if (!string.IsNullOrEmpty(framework.Platform) && (framework.PlatformVersion != FrameworkConstants.EmptyVersion))
            {
                framework = new NuGetFramework(framework.Framework, framework.Version, framework.Platform, FrameworkConstants.EmptyVersion);
            }

            return framework;
        }

        private static List<Packaging.Core.PackageType> GetPackageTypes(PackageDetailsCatalogLeaf leaf)
        {
            return leaf.PackageTypes
                            .Select(pt => new Packaging.Core.PackageType(
                                                    pt.Name,
                                                    pt.Version == null
                                                        ? Packaging.Core.PackageType.EmptyVersion
                                                        : new Version(pt.Version)))
                            .ToList();
        }

        private static void PopulateDeprecationFromDb(
            SearchDocument.Full document,
            Package package)
        {
            if (package.Deprecations?.Count != 1)
            {
                return;
            }

            var packageDeprecation = package.Deprecations?.ElementAt(0) as Entities.PackageDeprecation;
            if (packageDeprecation == null || packageDeprecation.Status == PackageDeprecationStatus.NotDeprecated)
            {
                return;
            }

            document.Deprecation = new Deprecation()
            {
                Message = packageDeprecation.CustomMessage,
                Reasons = packageDeprecation.Status.ToString().Replace(" ", "").Split(','),
                AlternatePackage = packageDeprecation.AlternatePackage == null ? null : new AlternatePackage()
                {
                    Id = packageDeprecation.AlternatePackage.Id,
                    Range = string.IsNullOrWhiteSpace(packageDeprecation.AlternatePackage.Version) ? "*" : $"[{packageDeprecation.AlternatePackage.Version}, )"
                }
            };
        }

        private static void PopulateDeprecationFromCatalog(
            SearchDocument.UpdateLatest document,
            PackageDetailsCatalogLeaf leaf)
        {
            if (leaf.Deprecation?.Reasons == null || !leaf.Deprecation.Reasons.Any())
            {
                return;
            }

            document.Deprecation = new Deprecation()
            {
                Reasons = leaf.Deprecation.Reasons.ToArray<string>(),
                Message = leaf.Deprecation.Message,
                AlternatePackage = leaf.Deprecation.AlternatePackage == null ? null : new AlternatePackage()
                {
                    Id = leaf.Deprecation.AlternatePackage.Id,
                    Range = leaf.Deprecation.AlternatePackage.Range
                }
            };
        }

        private static void PopulateVulnerabilitiesFromDb(
            SearchDocument.Full document,
            Package package)
        {
            document.Vulnerabilities = new List<Vulnerability>();
            if (package.VulnerablePackageRanges == null)
            {
                return;
            }

            foreach (var range in package.VulnerablePackageRanges.Where( x => x?.Vulnerability != null ))
            {

                document.Vulnerabilities.Add(new Vulnerability() 
                {  
                    AdvisoryURL = range.Vulnerability.AdvisoryUrl,
                    Severity = (int)range.Vulnerability.Severity
                });
            }
        }

        private static void PopulateVulnerabilitiesFromCatalog(
            SearchDocument.UpdateLatest document,
            PackageDetailsCatalogLeaf leaf)
        {
            document.Vulnerabilities = new List<Vulnerability>();
            if (leaf.Vulnerabilities == null)
            {
                return;
            }

            foreach (var leafVulnerability in leaf.Vulnerabilities.Where( x => x != null ))
            {
                document.Vulnerabilities.Add(new Vulnerability()
                {
                    AdvisoryURL = leafVulnerability.AdvisoryUrl,
                    Severity = (int)Enum.Parse(typeof(PackageVulnerabilitySeverity), leafVulnerability.Severity)
                });
            }
        }
    }
}
