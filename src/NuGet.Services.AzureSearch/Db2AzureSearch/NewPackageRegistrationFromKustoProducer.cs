// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Kusto.Data.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using NuGet.Services.AzureSearch.AuxiliaryFiles;
using NuGet.Services.Entities;
using NuGet.Versioning;
using NuGetGallery;

namespace NuGet.Services.AzureSearch.Db2AzureSearch
{
    public class NewPackageRegistrationFromKustoProducer : INewPackageRegistrationProducer
    {
        private const int KustoPageSize = 500_000;

        private readonly ICslQueryProvider _kustoQueryProvider;
        private readonly IDownloadTransferrer _downloadTransferrer;
        private readonly IFeatureFlagService _featureFlags;
        private readonly IOptionsSnapshot<Db2AzureSearchConfiguration> _options;
        private readonly IOptionsSnapshot<Db2AzureSearchDevelopmentConfiguration> _developmentOptions;
        private readonly ILogger<NewPackageRegistrationFromKustoProducer> _logger;

        public NewPackageRegistrationFromKustoProducer(
            ICslQueryProvider kustoQueryProvider,
            IDownloadTransferrer downloadTransferrer,
            IFeatureFlagService featureFlags,
            IOptionsSnapshot<Db2AzureSearchConfiguration> options,
            IOptionsSnapshot<Db2AzureSearchDevelopmentConfiguration> developmentOptions,
            ILogger<NewPackageRegistrationFromKustoProducer> logger)
        {
            _kustoQueryProvider = kustoQueryProvider ?? throw new ArgumentNullException(nameof(kustoQueryProvider));
            _downloadTransferrer = downloadTransferrer ?? throw new ArgumentNullException(nameof(downloadTransferrer));
            _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _developmentOptions = developmentOptions ?? throw new ArgumentNullException(nameof(developmentOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<DateTimeOffset> GetInitialCursorValueAsync(CancellationToken token)
        {
            var tableName = string.Format(_developmentOptions.Value.KustoTableNameFormat, "CatalogLeafItems");
            var query = $@"
{tableName}
| summarize CommitTimestamp = max(CommitTimestamp)";

            _logger.LogInformation(
                "Fetching initial cursor value from Kusto database {Database}, table {Table}.",
                _developmentOptions.Value.KustoDatabaseName,
                tableName);
            using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                _developmentOptions.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties(),
                token))
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (dataSet.Read())
                {
                    return dataSet.GetDateTime(0);
                }
            }

            return DateTimeOffset.MinValue;
        }

        public async Task<InitialAuxiliaryData> ProduceWorkAsync(
            ConcurrentBag<NewPackageRegistration> allWork,
            CancellationToken cancellationToken)
        {
            var excludedPackages = await LoadExcludedPackagesAsync(cancellationToken);

            var downloads = await LoadDownloadsAsync(cancellationToken);

            var popularityTransfers = await LoadPopularityTransfers(cancellationToken);

            // Apply changes from popularity transfers.
            var transferredDownloads = GetTransferredDownloads(downloads, popularityTransfers);

            var allOwners = await LoadPackageOwners(cancellationToken);

            var verifiedPackages = await LoadVerifiedPackages(cancellationToken);

            var packages = new List<Package>();
            var lastIdentity = string.Empty;
            string currentId = null;
            List<Package> currentPackages = null;

            while (true)
            {
                if (ShouldWait(allWork, log: true))
                {
                    while (ShouldWait(allWork, log: false))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }

                    _logger.LogInformation("Resuming fetching packages from Kusto.");
                }

                var pageSize = 20_000; // smaller than the max to avoid the max response size of 64 MB.
                while (true)
                {
                    try
                    {
                        packages.Clear();
                        lastIdentity = await PopulatePackageDataAsync(packages, pageSize, lastIdentity, cancellationToken);
                        break;
                    }
                    catch (KustoException ex) when (
                        pageSize > 100
                            && (ex.Message.Contains("E_QUERY_RESULT_SET_TOO_LARGE")
                                || ex.Message.Contains("https://aka.ms/kustoquerylimits")))
                    {
                        pageSize = pageSize / 2;
                        _logger.LogWarning("Kusto query failed due to large result set. Reducing page size to {PageSize}.", pageSize);
                    }
                }

                foreach (var package in packages)
                {
                    if (!StringComparer.OrdinalIgnoreCase.Equals(currentId, package.Id))
                    {
                        EmitNewPackageRegistration(
                            allWork,
                            excludedPackages,
                            transferredDownloads,
                            allOwners,
                            currentId,
                            currentPackages);

                        currentId = package.Id;
                        currentPackages = new List<Package>();
                    }

                    currentPackages.Add(package);
                }

                if (packages.Count < pageSize)
                {
                    break;
                }
            }

            EmitNewPackageRegistration(
                allWork,
                excludedPackages,
                transferredDownloads,
                allOwners,
                currentId,
                currentPackages);

            return new InitialAuxiliaryData(
                allOwners,
                downloads,
                excludedPackages,
                verifiedPackages,
                popularityTransfers);
        }

        private void EmitNewPackageRegistration(ConcurrentBag<NewPackageRegistration> allWork, HashSet<string> excludedPackages, Dictionary<string, long> transferredDownloads, SortedDictionary<string, SortedSet<string>> allOwners, string currentId, List<Package> currentPackages)
        {
            if (currentId is null || ShouldSkipPackageRegistration(currentId))
            {
                return;
            }

            string[] owners;
            if (allOwners.TryGetValue(currentId, out var ownersArray))
            {
                owners = ownersArray.ToArray();
            }
            else
            {
                owners = Array.Empty<string>();
            }

            if (!transferredDownloads.TryGetValue(currentId, out var packageDownloads))
            {
                packageDownloads = 0;
            }

            var uniquePackages = currentPackages
                .GroupBy(x => x.NormalizedVersion, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            allWork.Add(new NewPackageRegistration(
                currentId,
                packageDownloads,
                owners,
                uniquePackages,
                excludedPackages.Contains(currentId)));
        }

        private string GetQueryPrefix(string firstTableName)
        {
            if (_developmentOptions.Value.KustoTopPackageCount <= 0)
            {
                return firstTableName;
            }

            var versionsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageVersions");
            var downloadsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageDownloads");

            return $@"
{versionsTable}
| where ResultType == ""Available""
| join kind=inner {downloadsTable} on Identity
| summarize TotalDownloads = max(TotalDownloads) by LowerId
| order by TotalDownloads desc
| take {_developmentOptions.Value.KustoTopPackageCount}
| project LowerId
| join kind=inner {firstTableName} on LowerId";
        }

        private string GetLatestFilter()
        {
            var onlyLatest = "";
            if (_developmentOptions.Value.KustoOnlyLatestPackages)
            {
                onlyLatest = " and (IsLatest or IsLatestStable or IsLatestSemVer2 or IsLatestStableSemVer2)";
            }

            return onlyLatest;
        }

        private async Task<HashSet<string>> LoadExcludedPackagesAsync(CancellationToken token)
        {
            var tableName = string.Format(_developmentOptions.Value.KustoTableNameFormat, "ExcludedPackages");
            var query = $@"{GetQueryPrefix(tableName)}
| where IsExcluded == true
| summarize Id = take_any(Id) by LowerId
| project Id
";

            _logger.LogInformation(
                "Fetching excluded packages from Kusto database {Database}, table {Table}.",
                _developmentOptions.Value.KustoDatabaseName,
                tableName);
            using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                _developmentOptions.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties(),
                token))
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (dataSet.Read())
                {
                    var id = dataSet.GetString(0);
                    result.Add(id);
                }

                _logger.LogInformation("Fetched {Count} excluded packages from Kusto", result.Count);

                return result;
            }
        }

        private async Task<DownloadData> LoadDownloadsAsync(CancellationToken token)
        {
            var versionsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageVersions");
            var downloadsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageDownloads");
            var created = new DateTime(2000, 1, 1);
            var result = new DownloadData();

            _logger.LogInformation(
                "Fetching package downloads from Kusto database {Database}, table {VersionsTable} and {DownloadsTable}.",
                _developmentOptions.Value.KustoDatabaseName,
                versionsTable,
                downloadsTable);

            while (true)
            {
                var query = $@"{GetQueryPrefix(versionsTable)}
| where ResultType == ""Available""
| project Identity, Created
| join kind=inner {downloadsTable} on Identity
| order by Created asc
| project Id, Version, Downloads, Created
| where Created >= datetime({created:O})
| take {KustoPageSize}
";

                _logger.LogInformation("Fetching package downloads for packages created >= {Created:O}.", created);

                using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                    _developmentOptions.Value.KustoDatabaseName,
                    query,
                    new ClientRequestProperties(),
                    token))
                {
                    var recordCount = 0;
                    while (dataSet.Read())
                    {
                        var id = dataSet.GetString(0);
                        var version = dataSet.GetString(1);
                        var downloads = dataSet.GetInt64(2);
                        result.SetDownloadCount(id, version, downloads);
                        created = dataSet.GetDateTime(3);
                        recordCount++;
                    }

                    if (recordCount < KustoPageSize)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private async Task<PopularityTransferData> LoadPopularityTransfers(CancellationToken token)
        {
            if (!_options.Value.EnablePopularityTransfers)
            {
                _logger.LogWarning(
                    "Popularity transfers are disabled. Popularity transfers will be ignored.");
                return new PopularityTransferData();
            }

            if (!_featureFlags.IsPopularityTransferEnabled())
            {
                _logger.LogWarning(
                    "Popularity transfers feature flag is disabled. " +
                    "Popularity transfers will be ignored.");
                return new PopularityTransferData();
            }

            var tableName = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PopularityTransfers");
            var query = $@"{GetQueryPrefix(tableName)}
| where array_length(TransferLowerIds) > 0
| mv-expand ToId = TransferIds to typeof(string)
| project FromId = Id, ToId
";

            _logger.LogInformation(
                "Fetching popularity transfers from Kusto database {Database}, table {Table}.",
                _developmentOptions.Value.KustoDatabaseName,
                tableName);

            using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                _developmentOptions.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties(),
                token))
            {
                var result = new PopularityTransferData();
                while (dataSet.Read())
                {
                    var fromId = dataSet.GetString(0);
                    var toId = dataSet.GetString(1);
                    result.AddTransfer(fromId, toId);
                }

                _logger.LogInformation("Fetched popularity transfers from {Count} package IDs from Kusto", result.Count);

                return result;
            }
        }

        private async Task<HashSet<string>> LoadVerifiedPackages(CancellationToken token)
        {
            var tableName = string.Format(_developmentOptions.Value.KustoTableNameFormat, "VerifiedPackages");
            var query = $@"{GetQueryPrefix(tableName)}
| where IsVerified == true
| summarize Id = take_any(Id) by LowerId
| project Id
";

            _logger.LogInformation(
                "Fetching verified packages from Kusto database {Database}, table {Table}.",
                _developmentOptions.Value.KustoDatabaseName,
                tableName);
            using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                _developmentOptions.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties(),
                token))
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (dataSet.Read())
                {
                    var id = dataSet.GetString(0);
                    result.Add(id);
                }

                _logger.LogInformation("Fetched {Count} verified packages from Kusto", result.Count);

                return result;
            }
        }

        private async Task<SortedDictionary<string, SortedSet<string>>> LoadPackageOwners(CancellationToken token)
        {
            var versionsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageVersions");
            var ownersTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageOwners");
            var created = new DateTime(2000, 1, 1);
            var builder = new PackageIdToOwnersBuilder(_logger);

            _logger.LogInformation(
                "Fetching package owners from Kusto database {Database}, table {VersionsTable} and {OwnersTable}.",
                _developmentOptions.Value.KustoDatabaseName,
                versionsTable,
                ownersTable);

            while (true)
            {
                var query = $@"{GetQueryPrefix(versionsTable)}
| where ResultType == ""Available""
| summarize Created = min(Created) by LowerId
| join kind=inner {ownersTable} on LowerId
| where array_length(Owners) > 0
| mv-expand Owner = Owners to typeof(string)
| order by Created asc
| project Id, Owner, Created
| where Created >= datetime({created:O})
| take {KustoPageSize}
";

                _logger.LogInformation("Fetching package owners for packages created >= {Created:O}.", created);

                using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                    _developmentOptions.Value.KustoDatabaseName,
                    query,
                    new ClientRequestProperties(),
                    token))
                {
                    var recordCount = 0;
                    while (dataSet.Read())
                    {
                        var id = dataSet.GetString(0);
                        var username = dataSet.GetString(1);
                        builder.Add(id, username);
                        created = dataSet.GetDateTime(2);
                        recordCount++;
                    }

                    if (recordCount < KustoPageSize)
                    {
                        break;
                    }
                }
            }

            return builder.GetResult();
        }

        private async Task<string> PopulatePackageDataAsync(List<Package> packages, int pageSize, string lastIdentity, CancellationToken token)
        {
            var versionsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageVersions");
            var manifestsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageManifests");
            var compatibilitiesTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageCompatibilities");
            var vulnerabilitiesTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageVulnerabilities");
            var deprecationsTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageDeprecations");
            var archivesTable = string.Format(_developmentOptions.Value.KustoTableNameFormat, "PackageArchives");

            var query = $@"{GetQueryPrefix(versionsTable)}
| where ResultType == ""Available""{GetLatestFilter()}
| where strcmp(Identity, '{lastIdentity}') >= 0
| order by Identity asc
| take {pageSize}
| project
    Identity,
    Created,
    IsListed,
    IsSemVer2,
    Published,
    LastEdited
| join kind=inner (
    {manifestsTable}
    | project
        Identity,
        Authors,
        Copyright,
        DependencyGroups,
        Description,
        Icon,
        IconUrl,
        Id,
        Language,
        LicenseMetadata,
        LicenseUrl,
        MinClientVersion,
        OriginalVersion,
        PackageTypes,
        ProjectUrl,
        ReleaseNotes,
        RequireLicenseAcceptance,
        Summary,
        Tags,
        Title
) on Identity
| project-away Identity1
| join kind=inner (
    {compatibilitiesTable}
    | project 
        Identity,
        SupportedFrameworks = NuGetGallery
) on Identity
| project-away Identity1
| join kind=leftouter (
    {vulnerabilitiesTable}
    | where ResultType == ""Vulnerable""
    | extend Vulnerability = bag_pack(""AdvisoryUrl"", AdvisoryUrl, ""Severity"", Severity)
    | summarize Vulnerabilities = make_list(Vulnerability) by Identity
) on Identity
| project-away Identity1
| join kind=leftouter (
    {deprecationsTable}
    | where ResultType == ""Deprecated""
    | extend Deprecation = bag_pack(
        ""Reasons"", Reasons,
        ""Message"", Message,
        ""AlternatePackageId"",
        AlternatePackageId, ""AlternateVersionRange"", AlternateVersionRange)
    | summarize Deprecations = make_list(Deprecation) by Identity
) on Identity
| project-away Identity1
| join kind=inner (
    {archivesTable}
    | project
        Identity,
        Size,
        SHA512
) on Identity
| project-away Identity1
| order by Identity asc
";

            _logger.LogInformation("Fetching package metadata for packages with identity >= '{Identity}'.", lastIdentity);

            using (var dataSet = await _kustoQueryProvider.ExecuteQueryAsync(
                _developmentOptions.Value.KustoDatabaseName,
                query,
                new ClientRequestProperties(),
                token))
            {
                while (dataSet.Read())
                {
                    lastIdentity = dataSet.GetString(dataSet.GetOrdinal("Identity"));

                    try
                    {
                        var package = new Package();

                        package.Created = dataSet.GetDateTime(dataSet.GetOrdinal("Created"));
                        package.Listed = dataSet.GetBoolean(dataSet.GetOrdinal("IsListed"));
                        package.SemVerLevelKey = dataSet.GetBoolean(dataSet.GetOrdinal("IsSemVer2")) ? SemVerLevelKey.SemVer2 : SemVerLevelKey.Unknown;
                        package.Published = dataSet.GetDateTime(dataSet.GetOrdinal("Published"));
                        package.LastEdited = dataSet.IsDBNull(dataSet.GetOrdinal("LastEdited")) ? (DateTime?)null : dataSet.GetDateTime(dataSet.GetOrdinal("LastEdited"));
                        package.FlattenedAuthors = dataSet.GetString(dataSet.GetOrdinal("Authors"));
                        package.Copyright = dataSet.GetString(dataSet.GetOrdinal("Copyright"));
                        SetFlattenedDependencies(package, (JValue)dataSet[dataSet.GetOrdinal("DependencyGroups")]);
                        package.Description = dataSet.GetString(dataSet.GetOrdinal("Description"));
                        package.HasEmbeddedIcon = !string.IsNullOrEmpty(dataSet.GetString(dataSet.GetOrdinal("Icon")));
                        package.IconUrl = dataSet.GetString(dataSet.GetOrdinal("IconUrl"));
                        package.Id = dataSet.GetString(dataSet.GetOrdinal("Id"));
                        package.Language = dataSet.GetString(dataSet.GetOrdinal("Language"));
                        SetLicenseMetadata(package, (JValue)dataSet[dataSet.GetOrdinal("LicenseMetadata")]);
                        package.LicenseUrl = dataSet.GetString(dataSet.GetOrdinal("LicenseUrl"));
                        package.MinClientVersion = dataSet.GetString(dataSet.GetOrdinal("MinClientVersion"));
                        var version = NuGetVersion.Parse(dataSet.GetString(dataSet.GetOrdinal("OriginalVersion")));
                        package.Version = version.OriginalVersion;
                        package.NormalizedVersion = version.ToNormalizedString();
                        package.IsPrerelease = version.IsPrerelease;
                        SetPackageTypes(package, (JValue)dataSet[dataSet.GetOrdinal("PackageTypes")]);
                        package.ProjectUrl = dataSet.GetString(dataSet.GetOrdinal("ProjectUrl"));
                        package.ReleaseNotes = dataSet.GetString(dataSet.GetOrdinal("ReleaseNotes"));
                        package.RequiresLicenseAcceptance = dataSet.GetBoolean(dataSet.GetOrdinal("RequireLicenseAcceptance"));
                        package.Summary = dataSet.GetString(dataSet.GetOrdinal("Summary"));
                        package.Tags = dataSet.GetString(dataSet.GetOrdinal("Tags"));
                        package.Title = dataSet.GetString(dataSet.GetOrdinal("Title"));
                        SetSupportedFrameworks(package, (JValue)dataSet[dataSet.GetOrdinal("SupportedFrameworks")]);
                        SetVulnerabilities(package, (JValue)dataSet[dataSet.GetOrdinal("Vulnerabilities")]);
                        SetDeprecations(package, (JValue)dataSet[dataSet.GetOrdinal("Deprecations")]);
                        package.PackageFileSize = dataSet.GetInt64(dataSet.GetOrdinal("Size"));
                        package.Hash = dataSet.GetString(dataSet.GetOrdinal("SHA512"));
                        package.HashAlgorithm = "SHA512";

                        packages.Add(package);
                    }
                    catch
                    {
                        _logger.LogError("Failed to map Kusto data for {Identity} to a package entity. Kusto query: {Query}", lastIdentity, query);
                        throw;
                    }
                }
            }

            return lastIdentity;
        }

        private static void SetFlattenedDependencies(Package package, JToken parsedDependencyGroups)
        {
            parsedDependencyGroups = ParseJTokenIfNeeded(parsedDependencyGroups);

            if (parsedDependencyGroups is null)
            {
                return;
            }

            var dependencyGroups = new List<Protocol.Catalog.PackageDependencyGroup>();
            foreach (var parsedGroup in parsedDependencyGroups)
            {
                var group = new Protocol.Catalog.PackageDependencyGroup();
                group.TargetFramework = parsedGroup["TargetFramework"].Value<string>();
                group.Dependencies = new List<Protocol.Catalog.PackageDependency>();
                foreach (var parsedPackage in parsedGroup["Packages"])
                {
                    group.Dependencies.Add(new Protocol.Catalog.PackageDependency
                    {
                        Id = parsedPackage["Id"].Value<string>(),
                        Range = parsedPackage["VersionRange"].Value<string>(),
                    });
                }

                dependencyGroups.Add(group);
            }

            package.FlattenedDependencies = BaseDocumentBuilder.GetFlattenedDependencies(dependencyGroups);
        }

        private static void SetLicenseMetadata(Package package, JToken parsedLicenseMetadata)
        {
            parsedLicenseMetadata = ParseJTokenIfNeeded(parsedLicenseMetadata);

            if (parsedLicenseMetadata is null)
            {
                return;
            }

            switch (parsedLicenseMetadata.Value<string>("Type"))
            {
                case "Expression":
                    package.EmbeddedLicenseType = EmbeddedLicenseFileType.Absent;
                    package.LicenseExpression = parsedLicenseMetadata.Value<string>("License");
                    break;
                case "File":
                    var isMarkdown = parsedLicenseMetadata.Value<string>("License").EndsWith(".md", StringComparison.OrdinalIgnoreCase);
                    package.EmbeddedLicenseType = isMarkdown ? EmbeddedLicenseFileType.Markdown : EmbeddedLicenseFileType.PlainText;
                    break;
            }
        }

        private static void SetPackageTypes(Package package, JToken parsedPackageTypes)
        {
            parsedPackageTypes = ParseJTokenIfNeeded(parsedPackageTypes);

            if (parsedPackageTypes is null)
            {
                return;
            }

            package.PackageTypes = new List<Entities.PackageType>();
            foreach (var parsedPackageType in parsedPackageTypes)
            {
                package.PackageTypes.Add(new Entities.PackageType
                {
                    Name = parsedPackageType.Value<string>("Name"),
                    Version = parsedPackageType.Value<string>("Version"),
                });
            }
        }

        private static void SetSupportedFrameworks(Package package, JToken parsedSupportedFrameworks)
        {
            parsedSupportedFrameworks = ParseJTokenIfNeeded(parsedSupportedFrameworks);

            if (parsedSupportedFrameworks is null)
            {
                return;
            }

            package.SupportedFrameworks = new List<PackageFramework>();
            foreach (var parsedSupportedFramework in parsedSupportedFrameworks)
            {
                package.SupportedFrameworks.Add(new PackageFramework
                {
                    TargetFramework = (string)parsedSupportedFramework,
                });
            }
        }

        private static void SetVulnerabilities(Package package, JToken parsedVulnerabilities)
        {
            parsedVulnerabilities = ParseJTokenIfNeeded(parsedVulnerabilities);

            if (parsedVulnerabilities is null)
            {
                return;
            }

            package.VulnerablePackageRanges = new List<VulnerablePackageVersionRange>();
            foreach (var parsedVulnerability in parsedVulnerabilities)
            {
                package.VulnerablePackageRanges.Add(new VulnerablePackageVersionRange
                {
                    Vulnerability = new Entities.PackageVulnerability
                    {
                        AdvisoryUrl = parsedVulnerability.Value<string>("AdvisoryUrl"),
                        Severity = (PackageVulnerabilitySeverity)parsedVulnerability.Value<int>("Severity"),
                    }
                });
            }
        }

        private static void SetDeprecations(Package package, JToken parsedDeprecations)
        {
            parsedDeprecations = ParseJTokenIfNeeded(parsedDeprecations);

            if (parsedDeprecations is null)
            {
                return;
            }

            package.Deprecations = new List<Entities.PackageDeprecation>();
            foreach (var parsedDeprecation in parsedDeprecations)
            {
                var reasons = parsedDeprecation
                    .Value<JArray>("Reasons")
                    .Select(r => (PackageDeprecationStatus)Enum.Parse(typeof(PackageDeprecationStatus), r.Value<string>()))
                    .Aggregate((a, b) => a | b);

                var deprecation = new Entities.PackageDeprecation
                {
                    Status = reasons,
                    CustomMessage = parsedDeprecation.Value<string>("Message"),
                };

                var alternateId = parsedDeprecation.Value<string>("AlternatePackageId");
                if (!string.IsNullOrEmpty(alternateId))
                {
                    deprecation.AlternatePackage = new Entities.Package
                    {
                        Id = alternateId,
                    };

                    var alternateVersionRange = parsedDeprecation.Value<string>("AlternateVersionRange");
                    if (alternateVersionRange != "*")
                    {
                        deprecation.AlternatePackage.Version = VersionRange.Parse(alternateVersionRange).MinVersion.ToNormalizedString();
                    }
                }
            }
        }

        /// <summary>
        /// The Kusto client library returns dynamic values in a strange way.
        /// </summary>
        private static JToken ParseJTokenIfNeeded(JToken token)
        {
            if (token is null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token.Type == JTokenType.String)
            {
                var json = token.Value<string>();
                if (string.IsNullOrEmpty(json))
                {
                    return null;
                }

                token = JToken.Parse(json);
            }

            return token;
        }

        private bool ShouldWait(ConcurrentBag<NewPackageRegistration> allWork, bool log)
        {
            var packageCount = allWork.Sum(x => x.Packages.Count);
            var max = 2 * _options.Value.DatabaseBatchSize;

            if (packageCount > max)
            {
                if (log)
                {
                    _logger.LogInformation(
                        "There are {PackageCount} packages in memory waiting to be pushed to Azure Search. " +
                        "Waiting until this number drops below {Max} before fetching more packages.",
                        packageCount,
                        max);
                }

                return true;
            }

            return false;
        }

        private Dictionary<string, long> GetTransferredDownloads(
            DownloadData downloads,
            PopularityTransferData popularityTransfers)
        {
            var transferChanges = _downloadTransferrer.InitializeDownloadTransfers(
                downloads,
                popularityTransfers);

            var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            foreach (var packageDownload in downloads)
            {
                result[packageDownload.Key] = packageDownload.Value.Total;
            }

            foreach (var transferChange in transferChanges)
            {
                result[transferChange.Key] = transferChange.Value;
            }

            return result;
        }

        private bool ShouldSkipPackageRegistration(string packageId)
        {
            // Capture the skip list to avoid reload issues.
            var skipPrefixes = _developmentOptions.Value.SkipPackagePrefixes;
            if (skipPrefixes == null)
            {
                return false;
            }

            foreach (var skipPrefix in skipPrefixes)
            {
                if (packageId.StartsWith(skipPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}