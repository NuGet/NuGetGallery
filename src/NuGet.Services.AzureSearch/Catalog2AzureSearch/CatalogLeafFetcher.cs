// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol.Catalog;
using NuGet.Protocol.Registration;
using NuGet.Versioning;

namespace NuGet.Services.AzureSearch.Catalog2AzureSearch
{
    public class CatalogLeafFetcher : ICatalogLeafFetcher
    {
        private readonly IRegistrationClient _registrationClient;
        private readonly ICatalogClient _catalogClient;
        private readonly IOptionsSnapshot<Catalog2AzureSearchConfiguration> _options;
        private readonly ILogger<CatalogLeafFetcher> _logger;

        public CatalogLeafFetcher(
            IRegistrationClient registrationClient,
            ICatalogClient catalogClient,
            IOptionsSnapshot<Catalog2AzureSearchConfiguration> options,
            ILogger<CatalogLeafFetcher> logger)
        {
            _registrationClient = registrationClient ?? throw new ArgumentNullException(nameof(registrationClient));
            _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (_options.Value.MaxConcurrentBatches <= 0)
            {
                throw new ArgumentException(
                    $"The {nameof(AzureSearchJobConfiguration.MaxConcurrentBatches)} must be greater than zero.",
                    nameof(options));
            }

            if (_options.Value.RegistrationsBaseUrl == null)
            {
                throw new ArgumentException(
                    $"The {nameof(Catalog2AzureSearchConfiguration.RegistrationsBaseUrl)} must be set.",
                    nameof(options));
            }
        }

        public async Task<LatestCatalogLeaves> GetLatestLeavesAsync(
            string packageId,
            IReadOnlyList<IReadOnlyList<NuGetVersion>> versions)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            if (!versions.Any())
            {
                throw new ArgumentException("At least one version list must be provided.", nameof(versions));
            }

            var unavailable = new HashSet<NuGetVersion>();
            var fetched = new Dictionary<NuGetVersion, PackageDetailsCatalogLeaf>();
            var unlisted = new Dictionary<NuGetVersion, string>();

            var registrationIndexUrl = RegistrationUrlBuilder.GetIndexUrl(_options.Value.RegistrationsBaseUrl, packageId);
            var registrationIndex = await _registrationClient.GetIndexOrNullAsync(registrationIndexUrl);
            if (registrationIndex == null)
            {
                _logger.LogWarning(
                    "No registation index was found. ID: {PackageId}, registration index URL: {RegistrationIndexUrl}",
                    packageId,
                    registrationIndexUrl);

                foreach (var version in versions.SelectMany(x => x))
                {
                    unavailable.Add(version);
                }

                return new LatestCatalogLeaves(unavailable, fetched);
            }

            var pageUrlToInfo = registrationIndex
                .Items
                .ToDictionary(x => x.Url, x => new RegistrationPageInfo(x));

            // Make a list of ranges for logging purposes.
            var ranges = pageUrlToInfo
                .OrderBy(x => x.Value.Range.MinVersion)
                .Select(x => x.Value.RangeString)
                .ToList();

            foreach (var versionList in versions)
            {
                await AddLatestLeafAsync(
                    packageId,
                    versionList,
                    pageUrlToInfo,
                    ranges,
                    unavailable,
                    fetched,
                    unlisted);
            }

            // Fetch the unlisted version's metadata in parallel. This can be many versions in the case of a bulk
            // unlist.
            var allWork = new ConcurrentBag<KeyValuePair<NuGetVersion, string>>(unlisted);
            var allResults = new ConcurrentBag<KeyValuePair<NuGetVersion, PackageDetailsCatalogLeaf>>();
            var tasks = Enumerable
                .Range(0, _options.Value.MaxConcurrentBatches)
                .Select(async x =>
                {
                    await Task.Yield();
                    while (allWork.TryTake(out var work))
                    {
                        var leaf = await _catalogClient.GetPackageDetailsLeafAsync(work.Value);
                        allResults.Add(KeyValuePair.Create(work.Key, leaf));
                    }
                })
                .ToList();
            await Task.WhenAll(tasks);
            foreach (var pair in allResults)
            {
                fetched.Add(pair.Key, pair.Value);
            }

            return new LatestCatalogLeaves(unavailable, fetched);
        }

        private async Task AddLatestLeafAsync(
            string packageId,
            IReadOnlyList<NuGetVersion> versionList,
            Dictionary<string, RegistrationPageInfo> pageUrlToInfo,
            List<string> ranges,
            HashSet<NuGetVersion> unavailable,
            Dictionary<NuGetVersion, PackageDetailsCatalogLeaf> fetched,
            Dictionary<NuGetVersion, string> unlisted)
        {
            var descendingVersions = versionList
                .OrderByDescending(x => x)
                .ToList();

            foreach (var version in descendingVersions)
            {
                if (unavailable.Contains(version))
                {
                    _logger.LogDebug(
                        "For {PackageId}, version {Version} was already discovered to be unavailable.",
                        packageId,
                        version);
                    continue;
                }

                if (unlisted.ContainsKey(version))
                {
                    _logger.LogDebug(
                        "For {PackageId}, version {Version} was already discovered to be unlisted.",
                        packageId,
                        version);
                    continue;
                }

                if (fetched.TryGetValue(version, out var leaf))
                {
                    _logger.LogDebug(
                        "For {PackageId}, version {Version} was already fetched.",
                        packageId,
                        version);
                }
                else
                {
                    _logger.LogInformation(
                        "Looking for the catalog leaf for {PackageId} {Version}.",
                        packageId,
                        version);

                    var info = GetPageInfo(pageUrlToInfo, version);
                    if (info == null)
                    {
                        _logger.LogWarning(
                            "No page was found for {PackageId} {Version}. Page ranges were: {Ranges}",
                            packageId,
                            version,
                            ranges);
                        unavailable.Add(version);
                        continue;
                    }

                    // When the items are not inlined, we need to make a network request to get the metadata.
                    if (info.VersionToItem == null)
                    {
                        _logger.LogInformation(
                            "Fetching the items for page {PageUrl}. Range: {Range}",
                            info.Page.Url,
                            info.RangeString);

                        var page = await _registrationClient.GetPageAsync(info.Page.Url);
                        info.SetVersionToItem(page.Items);
                    }

                    if (!info.VersionToItem.TryGetValue(version, out var item))
                    {
                        _logger.LogWarning(
                            "No registration leaf item found for {PackageId} {Version} on {PageUrl}",
                            packageId,
                            version,
                            info.Page.Url);
                        unavailable.Add(version);
                        continue;
                    }

                    if (!item.CatalogEntry.Listed)
                    {
                        _logger.LogInformation(
                            "{PackageId} {Version} was found to be unlisted from page {Url}. This will not be used as a latest version.",
                            packageId,
                            version,
                            info.Page.Url);
                        unlisted.Add(version, item.CatalogEntry.Url);
                        continue;
                    }

                    _logger.LogInformation(
                        "Fetching the catalog leaf for {PackageId} {Version} from {LeafUrl}",
                        packageId,
                        version,
                        item.CatalogEntry.Url);

                    leaf = await _catalogClient.GetPackageDetailsLeafAsync(item.CatalogEntry.Url);
                    fetched.Add(version, leaf);
                }

                if (leaf.IsListed())
                {
                    _logger.LogInformation(
                        "{PackageId} {Version} was found to be listed. Metadata from {Url} will be used.",
                        packageId,
                        version,
                        leaf.Url);
                    return;
                }
                else
                {
                    // We'll only hit this case if the catalog index/page told us that this version was listed but the
                    // leaf says that it is unlisted.
                    _logger.LogInformation(
                        "{PackageId} {Version} was found to be unlisted from leaf {Url}. This will not be used as a latest version.",
                        packageId,
                        version,
                        leaf.Url);
                }
            }

            _logger.LogWarning(
                "No catalog leaves matchings leaves found for {PackageId}. Versions tried: {Versions}",
                packageId,
                descendingVersions);
        }

        private RegistrationPageInfo GetPageInfo(
            IReadOnlyDictionary<string, RegistrationPageInfo> pageUrlToInfo,
            NuGetVersion version)
        {
            foreach (var info in pageUrlToInfo.Values)
            {
                if (info.Range.Satisfies(version))
                {
                    return info;
                }
            }

            return null;
        }

        private class RegistrationPageInfo
        {
            public RegistrationPageInfo(RegistrationPage page)
            {
                Page = page;
                Range = new VersionRange(
                    minVersion: NuGetVersion.Parse(page.Lower),
                    includeMinVersion: true,
                    maxVersion: NuGetVersion.Parse(page.Upper),
                    includeMaxVersion: true);
                RangeString = Range.ToNormalizedString();

                // When the items are inlined, we don't need to make a network request to get the metadata.
                if (page.Items != null)
                {
                    SetVersionToItem(page.Items);
                }
            }

            public void SetVersionToItem(IEnumerable<RegistrationLeafItem> items)
            {
                VersionToItem = items.ToDictionary(x => NuGetVersion.Parse(x.CatalogEntry.Version));
            }

            public RegistrationPage Page { get; }
            public VersionRange Range { get; }
            public string RangeString { get; }
            public Dictionary<NuGetVersion, RegistrationLeafItem> VersionToItem { get; private set; }
        }
    }
}
