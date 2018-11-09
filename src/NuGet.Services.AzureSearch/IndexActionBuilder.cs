// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.Azure.Search.Models;
using NuGet.Services.AzureSearch.Db2AzureSearch;
using NuGet.Services.Entities;
using NuGet.Services.Metadata.Catalog;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.AzureSearch
{
    public class IndexActionBuilder : IIndexActionBuilder
    {
        private static readonly DateTimeOffset UnlistedPublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public IndexActions AddNewPackageRegistration(NewPackageRegistration packageRegistration)
        {
            var versionProperties = new Dictionary<string, VersionPropertiesData>();
            var versionListData = new VersionListData(versionProperties);
            var versionLists = new VersionLists(versionListData);

            var changes = packageRegistration
                .Packages
                .Select(GetVersionListChange)
                .ToList();
            var indexChanges = versionLists.ApplyChanges(changes);

            var versionToPackage = packageRegistration
                .Packages
                .ToDictionary(p => NuGetVersion.Parse(p.Version));

            var search = indexChanges
                .Search
                .Select(p => GetSearchIndexAction(
                    packageRegistration,
                    versionToPackage,
                    versionLists,
                    p.Key,
                    p.Value))
                .ToList();

            var hijack = indexChanges
                .Hijack
                .Select(p => GetHijackIndexAction(
                    packageRegistration.PackageId,
                    p.Key,
                    versionToPackage[p.Key],
                    p.Value))
                .ToList();

            return new IndexActions(
                search,
                hijack,
                new ResultAndAccessCondition<VersionListData>(
                    versionLists.GetVersionListData(),
                    AccessConditionWrapper.GenerateEmptyCondition()));
        }

        private static VersionListChange GetVersionListChange(Package x)
        {
            return VersionListChange.Upsert(
                fullOrOriginalVersion: x.Version,
                data: new VersionPropertiesData(
                    listed: x.Listed,
                    semVer2: x.SemVerLevelKey.HasValue && x.SemVerLevelKey.Value >= SemVerLevelKey.SemVer2));
        }

        private IndexAction<KeyedDocument> GetSearchIndexAction(
            NewPackageRegistration packageRegistration,
            IReadOnlyDictionary<NuGetVersion, Package> versionToPackage,
            VersionLists versionLists,
            SearchFilters searchFilters,
            SearchIndexChangeType changeType)
        {
            var key = GetSearchDocumentKey(packageRegistration.PackageId, searchFilters);

            if (changeType == SearchIndexChangeType.Delete)
            {
                return IndexAction.Delete(new KeyedDocument { Key = key });
            }

            if (changeType != SearchIndexChangeType.AddFirst)
            {
                // TODO: https://github.com/NuGet/NuGetGallery/issues/6444
                throw new NotImplementedException();
            }
            
            var latest = versionLists.GetLatestVersionInfoOrNull(searchFilters);
            var package = versionToPackage[latest.ParsedVersion];

            var metadata = new SearchDocument.Full();
            ApplyMetadata(metadata, packageRegistration.PackageId, package);

            metadata.Key = key;
            metadata.FullVersion = latest.FullVersion;
            metadata.TotalDownloadCount = packageRegistration.TotalDownloadCount;
            metadata.Owners = packageRegistration
                .Owners
                .OrderBy(u => u, StringComparer.InvariantCultureIgnoreCase)
                .ToArray();
            metadata.Versions = latest.ListedFullVersions;

            return IndexAction.Upload<KeyedDocument>(metadata);
        }

        private IndexAction<KeyedDocument> GetHijackIndexAction(
            string packageId,
            NuGetVersion version,
            Package package,
            HijackDocumentChanges changes)
        {
            var key = GetHijackDocumentKey(packageId, version);

            if (!changes.UpdateMetadata)
            {
                // TODO: https://github.com/NuGet/NuGetGallery/issues/6445
                throw new NotImplementedException();
            }

            var metadata = new HijackDocument.Full();
            ApplyMetadata(metadata, packageId, package);

            metadata.Key = key;
            metadata.SortableTitle = package.Title ?? packageId;
            metadata.IsLatestStableSemVer1 = changes.LatestStableSemVer1;
            metadata.IsLatestSemVer1 = changes.LatestSemVer1;
            metadata.IsLatestStableSemVer2 = changes.LatestStableSemVer2;
            metadata.IsLatestSemVer2 = changes.LatestSemVer2;

            return IndexAction.Upload<KeyedDocument>(metadata);
        }

        private static void ApplyMetadata(IBaseMetadataDocument metadata, string packageId, Package package)
        {
            var parsedVersion = NuGetVersion.Parse(package.Version);
            Guard.Assert(
                package.NormalizedVersion == null
                || package.NormalizedVersion == parsedVersion.ToNormalizedString(),
                $"The calculated {nameof(Package.NormalizedVersion)} does not match the DB value for {packageId} {package.Version}.");
            Guard.Assert(
                package.IsPrerelease == parsedVersion.IsPrerelease,
                $"The calculated {nameof(Package.IsPrerelease)} does not match the DB value for {packageId} {package.Version}.");

            metadata.Authors = package.FlattenedAuthors;
            metadata.Copyright = package.Copyright;
            metadata.Created = AssumeUtc(package.Created);
            metadata.Description = package.Description;
            metadata.FileSize = package.PackageFileSize;
            metadata.FlattenedDependencies = package.FlattenedDependencies;
            metadata.Hash = package.Hash;
            metadata.HashAlgorithm = package.HashAlgorithm;
            metadata.IconUrl = package.IconUrl;
            metadata.Language = package.Language;
            metadata.LastEdited = AssumeUtc(package.Created);
            metadata.LicenseUrl = package.LicenseUrl;
            metadata.MinClientVersion = package.MinClientVersion;
            metadata.NormalizedVersion = package.NormalizedVersion;
            metadata.OriginalVersion = package.Version;
            metadata.PackageId = packageId;
            metadata.Prerelease = package.IsPrerelease;
            metadata.ProjectUrl = package.ProjectUrl;
            metadata.Published = package.Listed ? AssumeUtc(package.Published) : UnlistedPublished;
            metadata.ReleaseNotes = package.ReleaseNotes;
            metadata.RequiresLicenseAcceptance = package.RequiresLicenseAcceptance;
            metadata.SemVerLevel = package.SemVerLevelKey;
            metadata.Summary = package.Summary;
            metadata.Tags = Utils.SplitTags(package.Tags ?? string.Empty);
            metadata.Title = package.Title;
        }

        private static DateTimeOffset AssumeUtc(DateTime dateTime)
        {
            return new DateTimeOffset(
                DateTime.SpecifyKind(dateTime, DateTimeKind.Utc),
                TimeSpan.Zero);
        }

        private static string GetSearchDocumentKey(string packageId, SearchFilters searchFilters)
        {
            var lowerId = packageId.ToLowerInvariant();
            var encodedId = EncodeKey(lowerId);
            return $"{encodedId}-{searchFilters}";
        }

        private static string GetHijackDocumentKey(string packageId, NuGetVersion version)
        {
            var lowerId = packageId.ToLowerInvariant();
            var lowerVersion = version.ToNormalizedString().ToLowerInvariant();
            return EncodeKey($"{lowerId}/{lowerVersion}");
        }

        private static string EncodeKey(string rawKey)
        {
            // First, encode the raw value for uniqueness.
            var bytes = Encoding.UTF8.GetBytes(rawKey);
            var unique = HttpServerUtility.UrlTokenEncode(bytes);

            // Then, prepend a string as close to the raw key as possible, for readibility.
            var readable = ReplaceUnsafeKeyCharacters(rawKey).TrimStart('_');

            return readable.Length > 0 ? $"{readable}-{unique}" : unique;
        }

        private static string ReplaceUnsafeKeyCharacters(string input)
        {
            return Regex.Replace(
                input,
                "[^A-Za-z0-9-_]", // Remove equal sign as well, since it's ugly.
                "_",
                RegexOptions.None,
                TimeSpan.FromSeconds(10));
        }
    }
}
