// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.AzureSearch
{
    public class DatabaseAuxiliaryDataFetcher : IDatabaseAuxiliaryDataFetcher
    {
        private static readonly string[] EmptyStringArray = new string[0];

        private readonly ISqlConnectionFactory<GalleryDbConfiguration> _connectionFactory;
        private readonly IEntitiesContextFactory _entitiesContextFactory;
        private readonly IAzureSearchTelemetryService _telemetryService;
        private readonly ILogger<DatabaseAuxiliaryDataFetcher> _logger;

        private const string GetVerifiedPackagesSql = @"
SELECT pr.Id
FROM PackageRegistrations pr (NOLOCK)
WHERE pr.IsVerified = 1
";

        private const string GetPackageIdToOwnersSql = @"
SELECT
    pr.Id,
    u.Username
FROM PackageRegistrations pr (NOLOCK)
INNER JOIN PackageRegistrationOwners pro (NOLOCK) ON pro.PackageRegistrationKey = pr.[Key]
INNER JOIN Users u (NOLOCK) ON pro.UserKey = u.[Key]
";

        private const int GetPopularityTransfersPageSize = 1000;
        private const string GetPopularityTransfersSkipParameter = "@skip";
        private const string GetPopularityTransfersTakeParameter = "@take";
        private const string GetPopularityTransfersSql = @"
SELECT TOP (@take)
    fpr.Id AS FromPackageId,
    tpr.Id AS ToPackageId
FROM PackageRenames r (NOLOCK)
INNER JOIN PackageRegistrations fpr (NOLOCK) ON fpr.[Key] = r.[FromPackageRegistrationKey]
INNER JOIN PackageRegistrations tpr (NOLOCK) ON tpr.[Key] = r.[ToPackageRegistrationKey]
WHERE r.TransferPopularity != 0 AND r.[Key] >= @skip
ORDER BY r.[Key] ASC
";

        public DatabaseAuxiliaryDataFetcher(
            ISqlConnectionFactory<GalleryDbConfiguration> connectionFactory,
            IEntitiesContextFactory entitiesContextFactory,
            IAzureSearchTelemetryService telemetryService,
            ILogger<DatabaseAuxiliaryDataFetcher> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _entitiesContextFactory = entitiesContextFactory ?? throw new ArgumentNullException(nameof(entitiesContextFactory));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger;
        }

        public async Task<string[]> GetOwnersOrEmptyAsync(string id)
        {
            var stopwatch = Stopwatch.StartNew();
            using (var entitiesContext = await _entitiesContextFactory.CreateAsync(readOnly: true))
            {
                _logger.LogInformation("Fetching owners for package registration with ID {PackageId}.", id);
                var owners = await entitiesContext
                    .PackageRegistrations
                    .Where(pr => pr.Id == id)
                    .Select(pr => pr.Owners.Select(u => u.Username).ToList())
                    .FirstOrDefaultAsync();

                if (owners == null)
                {
                    _logger.LogWarning("No package registration with ID {PackageId} was found. Assuming no owners.", id);
                    return EmptyStringArray;
                }

                if (owners.Count == 0)
                {
                    _logger.LogInformation("The package registration with ID {PackageId} has no owners.", id);
                    return EmptyStringArray;
                }

                // Sort the usernames in a consistent manner.
                var sortedOwners = owners
                    .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                stopwatch.Stop();
                _telemetryService.TrackGetOwnersForPackageId(sortedOwners.Length, stopwatch.Elapsed);
                _logger.LogInformation("The package registration with ID {PackageId} has {Count} owners.", id, sortedOwners.Length);
                return sortedOwners;
            }
        }

        public async Task<HashSet<string>> GetVerifiedPackagesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetVerifiedPackagesSql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var output = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        output.Add(id);
                    }

                    stopwatch.Stop();
                    _telemetryService.TrackReadLatestVerifiedPackagesFromDatabase(output.Count, stopwatch.Elapsed);

                    return output;
                }
            }
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetPackageIdToOwnersSql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var builder = new PackageIdToOwnersBuilder(_logger);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        var username = reader.GetString(1);

                        builder.Add(id, username);
                    }

                    var output = builder.GetResult();
                    stopwatch.Stop();
                    _telemetryService.TrackReadLatestOwnersFromDatabase(output.Count, stopwatch.Elapsed);

                    return output;
                }
            }
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToPopularityTransfersAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var builder = new PackageIdToPopularityTransfersBuilder(_logger);
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = GetPopularityTransfersSql;
                command.Parameters.Add(GetPopularityTransfersSkipParameter, SqlDbType.Int);
                command.Parameters.AddWithValue(GetPopularityTransfersTakeParameter, GetPopularityTransfersPageSize);

                // Load popularity transfers by paging through the database.
                // We continue paging until we receive fewer results than the page size.
                int currentPageResults;
                int totalResults = 0;
                do
                {
                    command.Parameters[GetPopularityTransfersSkipParameter].Value = totalResults;

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        currentPageResults = 0;

                        while (await reader.ReadAsync())
                        {
                            currentPageResults++;

                            var fromId = reader.GetString(0);
                            var toId = reader.GetString(1);

                            builder.Add(fromId, toId);
                        }
                    }

                    totalResults += currentPageResults;
                }
                while (currentPageResults == GetPopularityTransfersPageSize);

                var output = builder.GetResult();
                stopwatch.Stop();
                _telemetryService.TrackReadLatestPopularityTransfersFromDatabase(output.Count, stopwatch.Elapsed);

                return output;
            }
        }
    }
}

