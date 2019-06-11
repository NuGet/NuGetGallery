// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Jobs;
using NuGet.Jobs.Configuration;

namespace NuGet.Services.AzureSearch
{
    public class DatabaseOwnerFetcher : IDatabaseOwnerFetcher
    {
        private static readonly string[] EmptyStringArray = new string[0];

        private readonly ISqlConnectionFactory<GalleryDbConfiguration> _connectionFactory;
        private readonly IEntitiesContextFactory _entitiesContextFactory;
        private readonly ILogger<DatabaseOwnerFetcher> _logger;

        private const string Sql = @"
SELECT
    pr.Id,
    u.Username
FROM PackageRegistrations pr (NOLOCK)
INNER JOIN PackageRegistrationOwners pro (NOLOCK) ON pro.PackageRegistrationKey = pr.[Key]
INNER JOIN Users u (NOLOCK) ON pro.UserKey = u.[Key]
";

        public DatabaseOwnerFetcher(
            ISqlConnectionFactory<GalleryDbConfiguration> connectionFactory,
            IEntitiesContextFactory entitiesContextFactory,
            ILogger<DatabaseOwnerFetcher> logger)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _entitiesContextFactory = entitiesContextFactory ?? throw new ArgumentNullException(nameof(entitiesContextFactory));
            _logger = logger;
        }

        public async Task<string[]> GetOwnersOrEmptyAsync(string id)
        {
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
                _logger.LogInformation("The package registration with ID {PackageId} has {Count} owners.", id, sortedOwners.Length);
                return sortedOwners;
            }
        }

        public async Task<SortedDictionary<string, SortedSet<string>>> GetPackageIdToOwnersAsync()
        {
            using (var connection = await _connectionFactory.OpenAsync())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = Sql;

                using (var reader = await command.ExecuteReaderAsync())
                {
                    var builder = new PackageIdToOwnersBuilder(_logger);
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetString(0);
                        var username = reader.GetString(1);

                        builder.Add(id, username);
                    }

                    return builder.GetResult();
                }
            }
        }
    }
}

